# QuickAzTables
Opinionated and minimal library to allow quick and easy use of Azure Table Storage with minimal code. This works best if you have a relatively small amount of data you'd like to store in Azure Tables. This library severely strips down the functionality from the official SDK, so the consumer has to deal with much less complexity, knowledge of the Table Storage requirements and its intricacies.


# Assumptions
Your consuming code must conform to or accept below assumptions.

- You have somewhat small amount of data to be stored.
- All your data can be mapped to tables with known and stable partition and row keys for each entity.
- If you have nested objects in your model, they can be JSON serialized with each JSON is 64KiB or smaller.
- You don't care about advanced usage of table storage like ETags, If-Match queries, .etc. (If you need this functionality, you're better off using the official SDK directly.)
- You are ok with either providing partition/row keys that are [valid table storage keys](https://learn.microsoft.com/en-us/rest/api/storageservices/understanding-the-table-service-data-model#characters-disallowed-in-key-fields) or ok with the library sanitizing them by removing invalid chars, truncating keys that are too long .etc.
  - There is a utility function that allows validation of a given key and describing the issue (`TableKey.Validate(key)`) however the checks done here are not meant to be exhaustive. (see [Table Storage docs](https://learn.microsoft.com/en-us/rest/api/storageservices/understanding-the-table-service-data-model#characters-disallowed-in-key-fields) for a definitive set of requirements.))


# Intro 

Imagine you want to store below in Azure Table Storage

```csharp
public class Car {
    public string PlateNo { get; set; }
    public string City { get; set; }
    public string Make { get; set; }
    public string Model { get; set; }
    public DateTimeOffset BuildDate { get; set; }
    public Owner Owner { get; set; } // JSON serialized
}

public class Owner {
    public string Name { get; set; }
    ...
}

```

## Initialization

Create an instance of `TypedTableStore` since the data is typed.

>You can also use the underlying `TableStore` class if you don't have a type. Though you will have to provide your data using `TableEntity` type from `Azure.Data.Tables` SDK.

```csharp
using QuickAzTables;

var store = new TypedTableStore<Car>(tableName: "Cars",
                               connectionString: "...", // can also use sasTokens
                               // optional selectors if your keys 
                               // can be derived from model, set to null otherwise
                               partitionKeySelector: (car) => car.City, 
                               rowKeySelector: (car) => car.PlateNo,
                               // createTableIfNotExists: true // default
                               
                               // invalidKeyCharReplacement: "" // default to omit 
                               // chars that are not valid in table keys.
                               );

// if you specified createTableIfNotExists: false
await store.CreateTableIfNotExists();

// Storing
await store.StoreSingleAsync(new Car {...});

// If you don't want to use the key selectors or your keys aren't derived from the model.

await store.StoreSingleAsync(new Car {...}, partitionKey, rowKey);
// this stores in the table. If you have a nested model like Car.Owner, this will be JSON serialized and stored in a property named __jsonFor_Owner

// Querying a single row (Point Query)
var car = await store.QuerySingleAsync(new Car {...});
// if (car is null) // handle missing row

// or
var car = await store.QuerySingleAsync(partitionKey, rowKey);
// if (car is null) // handle missing row


```


>Note that all the non standard properties will be saved under columns named "`__jsonFor_{propertyName}`". As long as the JSON is under the [table storage property limits](https://learn.microsoft.com/en-us/rest/api/storageservices/understanding-the-table-service-data-model#property-types) (about 64 KiB) this will work.

```csharp
// Docs WIP
```
