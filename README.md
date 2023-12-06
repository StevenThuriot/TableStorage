# TableStorage
Streamlined way of working with Azure Data Tables

# Usage

Create your own TableContext and mark it with the `[TableContext]` attribute. This class must be partial.

```csharp
[TableContext]
public partial class MyTableContext { }
```

Create your models, these must be classes and have a parameterless constructor. Mark them with the `[TableSetModel]` attribute. This class must be partial.

```csharp
[TableSetModel]
public partial class Model
{
    public string Data { get; set; }
    public bool Enabled { get; set; }
}
```

Properties can also be defined using the `[TableSetModelProperty]` attribute. 
This is particularly useful if you are planning on using dotnet 8+'s Native AOT, as the source generation will make sure any breaking reflection calls are avoided by the Azure.Core libraries.

```csharp
[TableSetModel]
[TableSetModelProperty(typeof(string), "Data")]
[TableSetModelProperty(typeof(bool), "Enabled")]
public partial class Model { }
```

Place your tables on your TableContext. The sample below will create 2 tables in table storage, named Models1 and Models2.

```csharp
[TableContext]
public partial class MyTableContext
{
    public TableSet<Model> Models1 { get; set; }
    public TableSet<Model> Models2 { get; set; }
}
```

Register your TableContext in your services. An extension method will be available specifically for your context.

```csharp
builder.Services.AddMyTableContext(builder.Configuration.GetConnectionString("MyConnectionString"));
```

Optionally, pass along a `Configure` method to adjust some configuration options.

```csharp
builder.Services.AddMyTableContext(builder.Configuration.GetConnectionString("MyConnectionString"), Configure);

static void Configure(TableOptions options)
{
    options.AutoTimestamps = true;
    options.TableMode = TableUpdateMode.Merge;
}
```

Inject `MyTableContext` into your class and use as needed.

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

For some special cases, your table name might not be known at compile time. To handle those, an extension method has been added:

```csharp
var tableSet = context.GetTableSet<Model>("randomname");
```

# Linq

A few simple Linq extension methods have been provided in the `TableStorage.Linq` namespace.

Note: `Select` will include the actual transformation. If you want the original model, with only the selected fields retrieved, use `SelectFields` instead.
If you are using Native AOT, you will need to use `SelectFields` as `Select` will not work.