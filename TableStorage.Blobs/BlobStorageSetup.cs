namespace TableStorage;

public static class BlobStorageSetup
{
    public static IBlobCreator BuildCreator(string connectionString, Action<BlobOptions>? configure = null)
    {
        BlobOptions options = new();

        if (configure is not null)
        {
            configure(options);
        }

        BlobStorageFactory factory = new(connectionString, options.CreateTableIfNotExists);
        return new BlobSetCreator(factory, options);
    }

    private sealed class BlobSetCreator(BlobStorageFactory factory, BlobOptions options) : IBlobCreator
    {
        private readonly BlobStorageFactory _factory = factory;
        private readonly BlobOptions _options = options;

        BlobSet<T> IBlobCreator.CreateSet<T>(string tableName) => new(_factory, tableName, _options);

        BlobSet<T> IBlobCreator.CreateSet<T>(string tableName, string partitionKeyProxy, string rowKeyProxy)
        {
            // TODO: partitionKeyProxy;
            // TODO: rowKeyProxy;
            return new(_factory, tableName, _options);
        }
    }
}
