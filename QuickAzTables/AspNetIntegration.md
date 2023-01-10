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
```

## Using Configuration

if you want to get your connection string and/or the table name from configuration, this can be done as well, though you have to build your configuration before registration.

```csharp
// initialize your config, you may already have this in your project.
var configuration = new ConfigurationBuilder()
               .SetBasePath(...)
               .AddJsonFile(...)
               ...
               .Build();

// register your config class and map it to the corresponding section
builder.Services.Configure<MyConfig>(configuration.GetSection("..."));

builder.Services.AddSingleton((provider) =>
{
    // 
    var config = provider.GetService<IOptions<MyConfig>>();
    var connectionString = config.Value?./*connectionString*/ 
        ?? throw new Exception("configuration for typed tables not found");

    return new TypedTableStore<Car>("Cars",
                              connectionString, // provide the connectionstring
                              partitionKeySelector: c => c.City, 
                              rowKeySelector: c => c.PlateNo));
});
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
