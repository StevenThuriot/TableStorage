# TableStorage
Streamlined way of working with Azure Data Tables

# Usage

Create your own TableContext

```csharp
public class MyTableContext : TableContext { }
```

Create your models, these must be classes, inherit `ITableEntity` and have a parameterless constructor.

```csharp
public class Model : ITableEntity
{
    public string PartitionKey { get; set; }
    public string RowKey { get; set; }
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
    
    //Add your own custom properties, e.g:
    public string Data { get; set; }
    public bool Enabled { get; set; }
}
```

Place your tables on your TableContext. The sample below will create 2 tables in table storage, named Models1 and Models2.

```csharp
public class MyTableContext : TableContext
{
    public TableSet<Model> Models1 { get; set; }    
    public TableSet<Model> Models2 { get; set; }
}
```

Optionally, override the `Configure` method to adjust some configuration options

```csharp
protected override void Configure(TableOptions options)
{
    options.AutoTimestamps = true;
    options.TableMode = TableUpdateMode.Merge;
}
```

Register your TableContext in your services

```csharp
builder.Services.AddTableContext<MyTableContext>(builder.Configuration.GetConnectionString("MyConnectionString"));
```

Inject `MyTableContext` into your class and use as needed

```csharp
public class MyService
{
    private readonly MyTableContext _context;

    public MyService(MyTableContext context)
    {
        _context = context;
    }

    public async Task DoSomething(CancellationToken token)
    {
        var entity = await _context.Models1.GetEntityOrDefaultAsync("partitionKey", "rowKey", token);
        if (entity is not null)
        {
            //Do more
        }
    }
}
```