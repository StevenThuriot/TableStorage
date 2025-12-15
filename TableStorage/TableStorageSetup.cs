namespace TableStorage;

public static class TableStorageSetup
{
    public static ICreator BuildCreator(string connectionString, Action<TableOptions>? configure = null)
    {
        TableOptions options = new();

        if (configure is not null)
        {
            configure(options);
        }

        TableStorageFactory factory = new(connectionString, options.CreateTableIfNotExists);
        return new TableSetCreator(factory, options);
    }

    private sealed class TableSetCreator(TableStorageFactory factory, TableOptions options) : ICreator
    {
        private readonly TableStorageFactory _factory = factory;
        private readonly TableOptions _options = options;

        TableSet<T> ICreator.CreateSet<T>(string tableName, Func<Type, ModelInfo> infoProvider) => new DefaultTableSet<T>(_factory, tableName, _options, infoProvider);
        TableSet<T> ICreator.CreateSetWithChangeTracking<T>(string tableName, Func<Type, ModelInfo> infoProvider) => new ChangeTrackingTableSet<T>(_factory, tableName, _options, infoProvider);
    }
}