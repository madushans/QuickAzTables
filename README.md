# QuickAzTables

Opinionated, minimal library to allow quick and easy use of Azure Table Storage with minimal code. This works best if you have a relatively small amount of data you'd like to store in Azure Tables. This library severely strips down the functionality from the official SDK, so the you deal with much less complexity, knowledge of the Table Storage requirements and its intricacies.

Prime use case is storing and retrieving strongly typed Dotnet objects in table storage.


## CI/CD Status
| | |
|-|-|
| **Build** | ![Build Status](https://dev.azure.com/madushan/QuickAzTables/_apis/build/status/madushans.QuickAzTables?branchName=main)|
|**Release** |![Publish Status](https://vsrm.dev.azure.com/madushan/_apis/public/Release/badge/8338fdff-7f34-4674-862f-772040e6a8da/1/1)|
| **Nuget** | [![Nuget Badge](https://buildstats.info/nuget/QuickAzTables)](https://www.nuget.org/packages/QuickAzTables)


## *Have Questions? Check the [FAQ](docs/FAQ.md) or open an issue.*
## *Found Bugs? Feature Requests? Open an issue. PRs also welcome.*

# Get Started 

Imagine you want to store below in Azure Table Storage.

```csharp
public class Car {
    public string PlateNo { get; set; } // Row Key
    public string City { get; set; } // Partition Key
    ... // other properties
    public Owner Owner { get; set; } // JSON serialized
}

public class Owner {
    public string Name { get; set; }
    ...
}

```

## API
 
 - **Store**
   - [`StoreSingleAsync`](#store-single-row)
   - [`StoreMultipleAsync`](#storing-multiple-rows) 
 - **Query**
   - [`QuerySingleAsync`](#retrieve-a-single-row-point-query)
   - [`QueryAsync`](#retrieve-multiple-rows)
   - [`RetrieveFullTable`](#want-the-whole-table)
 - **Delete**
   - [`DeleteSingleAsync`](#deleting-a-single-row)
   - [`DeleteMultipleAsync`](#deleting-multiple-rows)
   - [`DeleteMultipleInPartitionAsync`](#deleting-multiple-rows)
 - **[Other Misc APIs](#misc)**



## Initialization

First [install the latest package version](https://www.nuget.org/packages/QuickAzTables/) on your project.


Create an instance of `TypedTableStore`.

>You can also use the underlying `TableStore` class if you don't have a type. Though you will have to provide your data using `TableEntity` type from `Azure.Data.Tables` SDK.

```csharp
using QuickAzTables;

var store = new TypedTableStore<Car>(tableName: "Cars",
                               connectionString: "...", // can also use sasTokens
                               
                               // optional selectors if your keys can be derived 
                               // from model, set to null otherwise
                               partitionKeySelector: (car) => car.City, 
                               rowKeySelector: (car) => car.PlateNo,
                               
                               // createTableIfNotExists: true // default
                               
                               // invalidKeyCharReplacement: "" // default to omit 
                               // chars that are not valid in table keys.
                               );

// if you specified createTableIfNotExists: false you can create 
// the table at a later point. Other calls will fail until you do so.
await store.CreateTableIfNotExistsAsync();
```

### Note About Table Creation
You're expected to make sure that only one call is made "at a time" to `CreateTableIfNotExistsAsync` or to the constructor if `createTableIfNotExists: true` (default). If you call them concurrently and the table doesn't exist, Table Storage will respond with `Conflict` as it tries to create the table while its creating the table.

This only happens if the table was actually not present and we create the table. If you maintain a single `TypedTableStore` instance per table from your application, you do not have to worry about this issue.

---

## Using ASP.NET ?
See the **[ASP.NET Integration Guide](docs/AspNetIntegration.md)** if you'd like to inject table store into your services/controllers.

There is also a [bare bones sample](samples/QuickAzTables.AspNetIntegrationBasic/) available.

---

## Store Single Row

```csharp
var car = new Car { 
  ..., 
  Owner = new Owner {...}
}

await store.StoreSingleAsync(car);

// If your keys aren't derived from the model 
// (you set the key selectors to null in initialization)
await store.StoreSingleAsync(car, partitionKey, rowKey);
```

When calling this, the library does a few sensible (but slightly opinonated) things.
- the `partitionKeySelector` and `rowKeySelector` provided in the initialization is used to infer those keys. (unless you specify them like in the second call)
  - If keys returned from them were invalid, i.e contains invalid characters, they're replaced with `invalidKeyCharReplacement` provided in the initialization.
  - If the key is too long, it is truncated at 1024 chars. (Table Storage keys must be 1KiB or less. We assume your key only has ASCII characters.)
  - Values of your original properties (`PlateNo` and `City`) are not modified.
  - Note that you cannot return `null` or `""` as keys. This will throw an exception.

- Your item is sent to Table Storage, in an `Upsert` operation with `mode` set to `TableUpdateMode.Merge`.
  - Creates the entity if it doesn't exist,
  - Updates if it does,
    - overwrites any matching properties,
    - and keeps any non-matching ones as is.
 - If your model had a property Table Storage doesn't natively support, like the `Owner` property in the example, they get JSON serialized, into a column named `__jsonFor_{propertyName}`. For `Owner` this is `__jsonFor_Owner`. 
   - If you read back the rows using this library, these are populated in the result correctly as well.
     - Type of your property, must be Serializable (no arg constructor, public getters and setters).
     - Internally `Newtonsoft.Json` is used, so if you want to alter or ignore a property, `Newtonsoft.Json` attributes will work.
   - The resulting JSON must be within the 64KiB limit (about 32,000 chars). See [table storage property limits.](https://learn.microsoft.com/en-us/rest/api/storageservices/understanding-the-table-service-data-model#property-types) The library currently doesn't enforce this, and will fail if the JSON is too large.

All the other operations follow this pattern and decisions. If you're curious about the details, see the implementation in [`TableStore`](/QuickAzTables/TableStore.cs) class.


## Storing Multiple Rows

```csharp
var cars = new List<Car> {...}
await store.StoreMultipleAsync(cars);
```
When manipulating multiple rows at once, Table Storage requires them to be batched in groups where each group contains rows for a single partition. This and other calls that deal with multiple rows will automatically do this (using `partitionKeySelector`), and upload to table storage in batches of Entity Group Transactions.

Note that if you manipulate multiple rows that are in different partitions, they will be split into multiple calls in which 0 or more of them could fail (due to network, bad data, .etc.). If all rows are inthe same partition, they could also be split in to different batches if the total number of rows are larger than the default batch size (default size in the Table Storage SDK, I think is 100 rows). Due to this, failing such a call means some of the rows may have succeeded in the requested operation. The library sends batches synchronously and will stop at the first batch that fails.

I haven't tested the batch size. So it may fail if you provider items larger than the batch size.¯\\_(ツ)_/¯ 

> Currently there's no API to store multiple items without using key selectors.


## Retrieve a Single Row (Point Query)

```csharp
var match = new Car {
  // provide all information 
  // necessary for your partition and 
  // row key selectors
  PlateNo = "ABC123",
  City = "Westview"
}
var car = await store.QuerySingleAsync(match);
if (car is null) // handle missing row

// or
var car = await store.QuerySingleAsync(partitionKey, rowKey);
if (car is null) // handle missing row
```

## Retrieve Multiple Rows

```csharp
var match = new Car { 
  // specify enough props to infer either 
  // partition or row key
  City = "Westview"
 }
var cars = await store.QueryAsync(match);
// selectors will be used to infer the keys

// cars is an IAsyncEnumerable<Car>
await foreach(var car in rows) {
  ...
}
// or specify atleast one of the keys
var cars = await store.QueryAsync(partitionKey: partitionKey);
var cars = await store.QueryAsync(rowKey: rowKey);

```
If only the partition key was provided (or inferred using `partitionKeySelector(match)`) this will return all the rows in the specified partition.

If only the row key was provided (or inferred using `rowKeySelector(match)`) this will return all the matching rows in each partition.

Calls to Table Storage will be done in batches as the consuming code enumerates the resulting `IAsyncEnumerable`.

> If you want to manipulate `IAsyncEnumerable` you can use [System.Linq.Async](https://www.nuget.org/packages/System.Linq.Async/) package. 

## Want the whole table?

```csharp
var rows = await RetrieveFullTable();
```
> If you have a large amount of data in the table, consider partitioning it as this will download the whole table in batches as you enumerate.

## Deleting a Single Row

```csharp
await store.DeleteSingleAsync(car);
// both keys must be inferrable.

// or 
await store.DeleteSingleAsync(partitionKey, rowKey);
```

## Deleting Multiple Rows

```csharp
await DeleteMultipleAsync(new List<Car>{...});
// all items in the list must be able to infer keys,

// Items can be in multiple partitions and the calls
// will be batched per partition automatically.

// or 
await DeleteMultipleInPartitionAsync(partitionKey, 
  new List<string> {/*row keys*/} );
// Single partition at a time
```

## Misc

Some other functionality is also available as static functions. These may be useful in some related cases.

- `TableStore.ListTables(...)` lets you list the names of tables in the account.

Below are made public if you want to sanitize or validate keys yourself. You are not required to do so as all the APIs sanitize the keys before calling Table Storage. Keys are truncated at 1024 characters, which works for most cases, but this may or may not exceed the 1Kib limit.

- `TableKey.Sanitize(key)` returns a sanitized version of the given input that can be a partition or row key.
- `TableKey.Validate(key)` returns a string explaining why the given string would fail as a partition or row key (or `null` if it is a valid key). Note that this checks if the actual number of bytes for the input exceeds the 1KiB limit, so avoid passing large strings to this for perf reasons.
