# ASP.NET Integration Guide

If you like to integrate your table stores with ASP.NET Dependency Injection, this can be done by registering table store as singletons.

## Basic Integration

In your `Program.cs` where you register services with the `ServiceCollection`, add the below code.

```csharp
builder.Services.AddSingleton((provider) =>
     new TypedTableStore<Car>("Cars",
                              "connectionString", 
                              partitionKeySelector: c => c.City, 
                              rowKeySelector: c => c.PlateNo));
// you can add more instances, as long as the type is different
// i.e 
// builder.Serices.AddSingleton (() => {
//   ...
//  return new TypedTableStore<User>(...);
// })
```

### **Why Singleton?**

Library checks if the table exists and creates it otherwise, at the initialization time. You do not want this for a few reasons.

- Checking the table's existence and creation is an HTTP call to Table Storage. You may not want this overhead on every request.
- If you get a few requests in quick succession, and your table did not exists, the library will check and attempt to create the table on each of these requests. Multiple checks will return as table not existing, then they will all try to create the table which can take a few seconds. During this time, all other creation requests will fail with a `Conflict` error.

You can use it as a Scoped dependency, but you will have to use the `createTableIfNotExists:false` parameter, and then make sure `CreateTableIfNotExistsAsync()` is called before any other call.

## Using Configuration

if you want to get your connection string and/or the table name from configuration, this can be done as well, though you have to build your configuration before registration.

>There's a **[bare sample available](../samples/QuickAzTables.AspNetIntegrationBasic/)** with the below code as well.

```csharp
builder.Services.AddSingleton((services) =>
{
    // this connection string must be specified in appsettings.json
    var connectionString = builder
                            .Configuration
                            .GetConnectionString("TableStorage");
     
    if (string.IsNullOrWhiteSpace(connectionString))
    {
       throw new Exception($"Could not find connection string. " +
                        $"This could be because the connection string was not specified in config.");
    }

    return new TypedTableStore<Car>(tableName: "Cars",
                                    connectionString: connectionString,
                                    partitionKeySelector: c => c.City,
                                    rowKeySelector: c => c.PlateNo);
});

// you can add more instances, as long as the type is different
// i.e 
// builder.Serices.AddSingleton (() => {
//   ...
//   return new TypedTableStore<User>(...);
// })

```
Then in a service or a Controller, you can inject the store.

```csharp
public class CarController {

    private readonly TypedTableStore<Car> _store;

    public CarController(TypedTableStore<Car> store) {
        _store = store;
    }

    [HttpGet]
    public async Task<IActionResult> Get(string plateNo, string city) {
        var car = await store.QuerySingleAsync(city, plateNo);
        if (car is null) return NotFound();
        
        return car;
    }
}
```