# TableStorage.Blobs.RuntimeCompilations

Provides runtime LINQ expression compilation and advanced query helpers for working with Azure Blob Storage in the TableStorage ecosystem. This package extends TableStorage.Blobs with runtime-compiled queries for blob sets, enabling more dynamic and performant filtering and projection scenarios.

## Features
- Runtime compilation of LINQ queries for blob sets
- Helpers for advanced filtering and selection
- Integration with TableStorage.Blobs

## Installation

```bash
dotnet add package TableStorage.Core
dotnet add package TableStorage.Blobs
dotnet add package TableStorage.Blobs.RuntimeCompilations
```

To enable this package, you must call `EnableCompilationAtRuntime()` when adding your TableContext to the DI:

```csharp
services.AddMyTableContext(connectionString,
    configureBlobs: x =>
    {
        x.CreateContainerIfNotExists = true;
        x.EnableCompilationAtRuntime();
    });
```

If you create the Azure client elsewhere, register it with dependency injection and call the
parameterless context registration overload. The context will use the registered client rather
than creating a new one.

```csharp
services.AddSingleton(new BlobServiceClient(connectionString));
services.AddMyTableContext(
    configureBlobs: x =>
    {
        x.CreateContainerIfNotExists = true;
        x.EnableCompilationAtRuntime();
    });
```

The parameterless overload is generated for each context and is named after the context, such as
`AddMyTableContext`. It supports the same configuration callbacks as the connection-string
overload.

## See Also

- [TableStorage.Blobs](https://www.nuget.org/packages/TableStorage.Blobs)
