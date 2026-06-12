# TableStorage

[![NuGet](https://img.shields.io/nuget/v/TableStorage.svg)](https://www.nuget.org/packages/TableStorage)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

A streamlined, source-generated way of working with Azure Data Tables and Blob Storage. Built for performance, Native AOT compatibility, and developer ergonomics.

## Packages

| Package | Description |
|---------|-------------|
| [TableStorage.Core](https://www.nuget.org/packages/TableStorage.Core) | Core abstractions and LINQ helpers |
| [TableStorage](https://www.nuget.org/packages/TableStorage) | Table Storage context, sets, and source generators |
| [TableStorage.Blobs](https://www.nuget.org/packages/TableStorage.Blobs) | Blob Storage support (Block and Append blobs) |
| [TableStorage.Fluent](https://www.nuget.org/packages/TableStorage.Fluent) | Polymorphic fluent entities (2–4 types per table) |
| [TableStorage.Fluent.Extended](https://www.nuget.org/packages/TableStorage.Fluent.Extended) | Extended fluent entities (5–16 types per table) |
| [TableStorage.RuntimeCompilations](https://www.nuget.org/packages/TableStorage.RuntimeCompilations) | Runtime LINQ compilation for table queries |
| [TableStorage.Blobs.RuntimeCompilations](https://www.nuget.org/packages/TableStorage.Blobs.RuntimeCompilations) | Runtime LINQ compilation for blob queries |
| [TableStorage.Fluent.RuntimeCompilations](https://www.nuget.org/packages/TableStorage.Fluent.RuntimeCompilations) | Runtime LINQ compilation for fluent queries |

## Installation

```bash
dotnet add package TableStorage.Core
dotnet add package TableStorage
```

For Blob Storage support:

```bash
dotnet add package TableStorage.Blobs
```

For polymorphic fluent entity support:

```bash
dotnet add package TableStorage.Fluent
```

## Getting Started

### Define a Context

Create your own TableContext and mark it with the `[TableContext]` attribute. This class must be partial.

```csharp
[TableContext]
public partial class MyTableContext;
```

### Define Models

Create your models — these must be classes with a parameterless constructor. Mark them with the `[TableSet]` attribute. This class must be partial.

```csharp
[TableSet]
public partial class Model
{
    public string Data { get; set; }
    public bool Enabled { get; set; }
}
```

Properties can also be defined using the `[TableSetProperty]` attribute.
This is particularly useful if you are planning on using .NET 8+'s Native AOT, as the source generation will make sure any breaking reflection calls are avoided by the Azure.Core libraries.
Starting C# 13, you can also mark them as partial.

```csharp
[TableSet]
[TableSetProperty(typeof(string), "Data")]
[TableSetProperty(typeof(bool), "Enabled")]
public partial class Model;
```

### Key Aliases

Sometimes it's nice to have a pretty name for your `PartitionKey` and `RowKey` properties, as the original names might not always make much sense when reading your code.
You can use the `PartitionKey` and `RowKey` properties of `TableSet` to create a proxy for these two properties.

```csharp
[TableSet(PartitionKey = "MyPrettyPartitionKey", RowKey = "MyPrettyRowKey")]
public partial class Model;
```

### Base Class Inheritance

Models can inherit from base classes. The source generator correctly picks up properties defined in parent classes, including partition and row keys:

```csharp
public abstract class BaseEntity
{
    public string Category { get; set; }
    public string CommonProperty { get; set; }
}

[TableSet(PartitionKey = nameof(Category), RowKey = nameof(ProductId))]
public partial class Product : BaseEntity
{
    public partial string ProductId { get; set; }
    public partial decimal Price { get; set; }
}
```

You can also override base class properties using `virtual`/`override` or hide them with `new partial`:

```csharp
public abstract class BaseEntity
{
    public virtual string Category { get; set; }
    public string Name { get; set; }
}

// Override — the source generator uses the overridden property
[TableSet(PartitionKey = nameof(Category), RowKey = nameof(Id))]
public partial class Product : BaseEntity
{
    public override string Category { get; set; }
    public partial string Id { get; set; }
}

// Hide with 'new partial' — enables change tracking on the key
[TableSet(PartitionKey = nameof(Category), RowKey = nameof(Id))]
public partial class TrackedProduct : BaseEntity
{
    public new partial string Category { get; set; }
    public partial string Id { get; set; }
}
```

### Change Tracking

`TableSet` has a `TrackChanges` property (default `false`) that optimizes what is sent back to the server when making changes to an entity.
When tracking changes, it's important to either use the `TableSetProperty` attribute to define your properties, or mark them as partial starting C# 13, otherwise they will not be tracked.

```csharp
[TableSet(TrackChanges = true)]
[TableSetProperty(typeof(string), "Data")]
public partial class Model
{
    public partial bool Enabled { get; set; }
}
```

When `ChangesOnly` is enabled in `TableOptions`, update, upsert, and bulk operations on change-tracked entities automatically default to `Merge` mode — regardless of the configured `TableMode` or `BulkOperation` — to prevent unchanged properties from being overwritten with default values. Passing an explicit `TableUpdateMode` or `BulkOperation` argument to the method always takes precedence.

### Blob Support

Mark the model for Blob Storage support by setting `SupportBlobs` on the `TableSet` attribute to `true`.
When working with blobs, you can mark certain properties to be used as blob tags, either by decorating the property with `[Tag]` or by setting `Tag` to `true` on the `TableSetProperty` attribute.

```csharp
[TableSet(SupportBlobs = true)]
[TableSetProperty(typeof(string), "Data", Tag = true)]
public partial class Model
{
    [Tag]
    public partial bool Enabled { get; set; }
}
```

> **Important:** If you plan on using the default STJ serialization, or plan on using the source generated `JsonSerializerContext`, you need to make sure that the properties you want to serialize are defined on your partial class definition. This includes your partition and row key. If you do not do this, STJ will not serialize them.

### Register Your Context

Place your tables on your TableContext. The sample below will create 2 tables in table storage, named Models1 and Models2. It will also create a blob container named BlobModels1 which is a set for Block blobs. BlobModels2 is a set for Append blobs.

```csharp
[TableContext]
public partial class MyTableContext
{
    public TableSet<Model> Models1 { get; set; }
    public BlobSet<Model> BlobModels1 { get; set; }
    public AppendBlobSet<Model> BlobModels2 { get; set; }
    public TableSet<Model> Models2 { get; set; }
}
```

Register your TableContext in your services. An extension method will be available specifically for your context.

```csharp
builder.Services.AddMyTableContext(builder.Configuration.GetConnectionString("MyConnectionString"));
```

### Inject and Use

```csharp
public class MyService(MyTableContext context)
{
    private readonly MyTableContext _context = context;

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

## Configuration

### Table Options

Pass a configure delegate when registering your context:

```csharp
builder.Services.AddMyTableContext(builder.Configuration.GetConnectionString("MyConnectionString"), Configure);

static void Configure(TableOptions options)
{
    options.TableMode = TableUpdateMode.Merge;
}
```

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `TableMode` | `TableUpdateMode` | `Replace` | Update mode: `Merge` or `Replace` |
| `PageSize` | `int?` | `null` | Number of entities per page when querying |
| `CreateTableIfNotExists` | `CreateIfNotExistsMode` | `Always` | `Always`, `Once` (cached), or `Disabled` |
| `BulkOperation` | `BulkOperation` | `Replace` | Default bulk operation mode: `Merge` or `Replace` |
| `TransactionSafety` | `TransactionSafety` | `Enabled` | When `Enabled`, transactions are split by partition key and chunked |
| `TransactionChunkSize` | `int` | `100` | Max operations per transaction batch (must be > 0) |
| `ChangesOnly` | `bool` | `false` | When `true`, only changed properties are sent during updates. Also forces `Merge` mode by default for all update and bulk operations on change-tracked entities, to prevent unchanged properties from being overwritten with default values. Can still be overridden by passing an explicit mode to the method |
| `OptimizeQueries` | `bool` | `true` | When `true`, queries with multiple partition key comparisons are automatically split into per-partition sub-queries to avoid full table scans |

### Blob Options

If you have defined any `BlobSets`, a third parameter becomes available to configure the blob service.

```csharp
builder.Services.AddMyTableContext(builder.Configuration.GetConnectionString("MyConnectionString"), ConfigureTables, ConfigureBlobs);

static void ConfigureTables(TableOptions options)
{
    options.TableMode = TableUpdateMode.Merge;
}

static void ConfigureBlobs(BlobOptions options)
{
    options.UseTags = true;
}
```

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `CreateContainerIfNotExists` | `CreateIfNotExistsMode` | `Always` | `Always`, `Once` (cached), or `Disabled` |
| `Serializer` | `IBlobSerializer` | STJ-based | Custom blob serializer implementation |
| `UseTags` | `bool` | `true` | Store partition/row key and tagged properties as blob tags |

## LINQ

LINQ extension methods are provided in the `TableStorage.Linq` namespace that optimize queries specifically for Table Storage. These methods translate directly to server-side OData filters.

Since these return an instance that implements `IAsyncEnumerable`, `System.Linq.Async` is an excellent companion to these methods. Do keep in mind that as soon as you start using `IAsyncEnumerable`, any further operations will run client-side.

### Available Methods

| Method | Description |
|--------|-------------|
| `Where(predicate)` | Filter entities server-side using an expression |
| `SelectFields(selector)` | Retrieve only selected fields (returns original model type) |
| `Take(n)` | Limit the number of results |
| `FirstAsync()` / `FirstOrDefaultAsync()` | Get the first matching entity |
| `SingleAsync()` / `SingleOrDefaultAsync()` | Get the single matching entity |
| `ExistsIn(selector, elements)` | Filter to entities whose property value is in a collection |
| `NotExistsIn(selector, elements)` | Filter to entities whose property value is not in a collection |
| `FindAsync(partitionKey, rowKey)` | Find a single entity by partition/row key via LINQ |
| `FindAsync(keys)` | Find multiple entities by partition/row key pairs |
| `BatchDeleteAsync()` | Delete all matching entities |
| `BatchDeleteTransactionAsync()` | Delete all matching entities using transactions |

### Examples

```csharp
// Filter, select fields, take
var results = context.Models1
    .Where(x => x.Enabled)
    .SelectFields(x => new { x.Data })
    .Take(10);

await foreach (var entity in results)
{
    Console.WriteLine(entity.Data);
}

// Get single entity
var first = await context.Models1
    .Where(x => x.Data == "test")
    .FirstOrDefaultAsync();

// ExistsIn
var ids = new[] { "id1", "id2", "id3" };
var matches = context.Models1.ExistsIn(x => x.RowKey, ids);

// Batch delete
var deletedCount = await context.Models1
    .Where(x => !x.Enabled)
    .BatchDeleteAsync();

// Find by key
var entity = await context.Models1.FindAsync("partitionKey", "rowKey");
```

> **Note:** `Select` will include the actual transformation. If you want the original model, with only the selected fields retrieved, use `SelectFields` instead.
> If you are using Native AOT, you will need to use `SelectFields` as `Select` will not work.

## Bulk Operations

Bulk operations allow you to insert, update, upsert, or delete multiple entities efficiently using batched transactions:

```csharp
var entities = new List<Model> { /* ... */ };

await context.Models1.BulkInsertAsync(entities);
await context.Models1.BulkUpdateAsync(entities);
await context.Models1.BulkUpsertAsync(entities);
await context.Models1.BulkDeleteAsync(entities);
```

`BulkUpdateAsync` and `BulkUpsertAsync` accept an optional `BulkOperation` parameter to choose between `Replace` and `Merge` mode. The default is configured via `TableOptions.BulkOperation`. When using change-tracked entities with `ChangesOnly` enabled, the default is `Merge` regardless of `TableOptions.BulkOperation`.

## Transactions

Submit manual transactions with automatic partition key grouping and chunking:

```csharp
var actions = entities.Select(e => new TableTransactionAction(TableTransactionActionType.UpsertReplace, e));
await context.Models1.SubmitTransactionAsync(actions);
```

When `TransactionSafety` is `Enabled` (the default), transactions are automatically grouped by partition key and split into chunks of `TransactionChunkSize` (default 100). Set `TransactionSafety` to `Disabled` to submit a raw transaction without any safety processing.

## Blob Storage

Blob sets support `BlobSet<T>` (block blobs) and `AppendBlobSet<T>` (append blobs).

### Block Blob Operations

```csharp
await context.BlobModels1.AddEntityAsync(entity);
await context.BlobModels1.GetEntityAsync("partitionKey", "rowKey");
await context.BlobModels1.GetEntityOrDefaultAsync("partitionKey", "rowKey");
await context.BlobModels1.UpdateEntityAsync(entity);
await context.BlobModels1.UpsertEntityAsync(entity);
await context.BlobModels1.DeleteEntityAsync(entity);
await context.BlobModels1.DeleteAllEntitiesAsync("partitionKey");
bool exists = await context.BlobModels1.ExistsAsync("partitionKey", "rowKey");
```

### Append Blob Operations

Append blobs support the same CRUD operations plus streaming append:

```csharp
await context.BlobModels2.AppendAsync("partitionKey", "rowKey", stream);
```

### Custom Serialization

Blob storage allows for custom serialization and deserialization. By default, `System.Text.Json` will be used for serialization.
You can define your own by implementing `IBlobSerializer` and passing it to the `BlobOptions` object.

Here's an example for a model that uses ProtoBuf:

```csharp
builder.Services.AddMyTableContext(builder.Configuration.GetConnectionString("MyConnectionString"), ConfigureTables, ConfigureBlobs);

static void ConfigureTables(TableOptions options)
{
    options.TableMode = TableUpdateMode.Merge;
}

static void ConfigureBlobs(BlobOptions options)
{
    options.UseTags = true;
    options.Serializer = new ProtoBufSerializer();
}

[TableSet(PartitionKey = nameof(PrettyPartition), RowKey = nameof(PrettyRow), SupportBlobs = true)]
[ProtoContract(IgnoreListHandling = true)] // Important to ignore list handling because we are generating an IDictionary implementation that is not supported by protobuf
public partial class Model
{
    [ProtoMember(1)] public partial string PrettyPartition { get; set; } // We can partial the PK and RowKey to enable custom serialization attributes
    [ProtoMember(2)] public partial string PrettyRow { get; set; }
    [ProtoMember(3)] public partial int MyProperty1 { get; set; }
    [ProtoMember(4)] public partial string MyProperty2 { get; set; }
    [ProtoMember(5)] public partial string? MyNullableProperty2 { get; set; }
}

public sealed class ProtoBufSerializer : IBlobSerializer
{
    public ValueTask<T?> DeserializeAsync<T>(string table, Stream entity, CancellationToken cancellationToken) where T : IBlobEntity
    {
        return ValueTask.FromResult<T?>(Serializer.Deserialize<T>(entity));
    }

    public BinaryData Serialize<T>(string table, T entity) where T : IBlobEntity
    {
        using MemoryStream stream = new();
        Serializer.Serialize(stream, entity);
        return new(stream.ToArray());
    }
}
```

### Native AOT Serialization

For some specific cases, the source generator will have to generate a `.Deserialize` call using System.Text.Json.
Since this is not supported when publishing with Native AOT, you can use the `TableStorageSerializerContext` property in your csproj file to set the fullname of a class that implements `JsonSerializerContext` to support native deserialization.

```xml
<Project Sdk="Microsoft.NET.Sdk">
	<PropertyGroup>
		<OutputType>Exe</OutputType>
		<TargetFramework>net9.0</TargetFramework>
		<PublishAot>true</PublishAot>
		<TableStorageSerializerContext>TableStorage.Tests.Contexts.ModelSerializationContext</TableStorageSerializerContext>
	</PropertyGroup>
</Project>
```

When configuring your context, you can also pass a `JsonSerializerContext` to the `BlobOptions` object to support native deserialization. Otherwise the default serialization will be used that relies on reflection.

```csharp
static void ConfigureBlobs(BlobOptions options)
{
    options.Serializer = new AotJsonBlobSerializer(MyJsonSerializerContext.Default);
}
```

## Fluent Entities

Fluent entities allow you to store multiple entity types in a single Azure Table using a discriminator pattern. This enables polymorphic table storage with type-safe pattern matching.

```bash
dotnet add package TableStorage.Fluent           # 2–4 types per table
dotnet add package TableStorage.Fluent.Extended  # 5–16 types per table
```

### Quick Example

```csharp
[TableContext]
public partial class MyTableContext
{
    public TableSet<FluentTableEntity<Customer, Order>> MixedEntities { get; set; }
}

// Store using implicit conversion
FluentTableEntity<Customer, Order> fluent = new Customer { Name = "John" };
await context.MixedEntities.AddEntityAsync(fluent);

// Retrieve and pattern match
var entity = await context.MixedEntities.GetEntityOrDefaultAsync(pk, rk);
var result = entity.SwitchCase(
    case1: customer => $"Customer: {customer.Name}",
    case2: order => $"Order: {order.OrderNumber}"
);
```

Three discriminator strategies are available:
- `FluentTableEntity<T1, T2>` — uses a `$type` discriminator column
- `FluentPartitionTableEntity<T1, T2>` — uses the `PartitionKey` as discriminator
- `FluentRowTableEntity<T1, T2>` — uses the `RowKey` as discriminator

See the [TableStorage.Fluent README](TableStorage.Fluent/README.md) for full documentation.

## RuntimeCompilations

For scenarios where you need runtime-compiled LINQ expressions (e.g. dynamic query building), RuntimeCompilation packages are available:

```bash
dotnet add package TableStorage.RuntimeCompilations
dotnet add package TableStorage.Blobs.RuntimeCompilations
dotnet add package TableStorage.Fluent.RuntimeCompilations
```

Each package has a different registration requirement:

- **`TableStorage.RuntimeCompilations`** — no registration needed. Installing the package makes additional extension methods available directly on `TableSet<T>` (e.g. `Select`, `UpdateAsync`, `UpsertAsync`, `BatchUpdateAsync`, `BatchUpdateTransactionAsync`).
- **`TableStorage.Blobs.RuntimeCompilations`** — call `EnableCompilationAtRuntime()` on `BlobOptions`.
- **`TableStorage.Fluent.RuntimeCompilations`** — call `EnableFluentCompilationAtRuntime()` on `TableOptions`.

```csharp
services.AddMyTableContext(connectionString,
    configure: x =>
    {
        x.EnableFluentCompilationAtRuntime(); // TableStorage.Fluent.RuntimeCompilations
    },
    configureBlobs: x =>
    {
        x.EnableCompilationAtRuntime(); // TableStorage.Blobs.RuntimeCompilations
    });
```

## License

This project is licensed under the [MIT License](LICENSE).

## Contributing

Contributions are welcome! Please open an [issue](https://github.com/LieselThuriot/TableStorage/issues) or submit a [pull request](https://github.com/LieselThuriot/TableStorage/pulls).