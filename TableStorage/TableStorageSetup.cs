using Microsoft.Extensions.DependencyInjection;

namespace TableStorage;

public static class TableStorageSetup
{
    public static ICreator BuildCreator(string connectionString, Action<TableOptions>? configure = null)
    {
        TableServiceClient client = new(connectionString ?? throw new ArgumentNullException(nameof(connectionString)));
        return BuildCreator(client, configure);
    }

    public static ICreator BuildCreator(IServiceProvider services, Action<TableOptions>? configure = null)
    {
        TableServiceClient client = services.GetRequiredService<TableServiceClient>();
        return BuildCreator(client, configure);
    }

    private static TableSetCreator BuildCreator(TableServiceClient client, Action<TableOptions>? configure)
    {
        TableOptions options = new();

        if (configure is not null)
        {
            configure(options);
        }

        TableStorageFactory factory = new(client, options.CreateTableIfNotExists);
        return new(factory, options);
    }

    private sealed class TableSetCreator(TableStorageFactory factory, TableOptions options) : ICreator
    {
        private readonly TableStorageFactory _factory = factory;
        private readonly TableOptions _options = options;

        TableSet<T> ICreator.CreateSet<T>(string tableName, Func<Type, ModelInfo> infoProvider) => new DefaultTableSet<T>(_factory, tableName, _options, infoProvider);
        TableSet<T> ICreator.CreateSetWithChangeTracking<T>(string tableName, Func<Type, ModelInfo> infoProvider) => new ChangeTrackingTableSet<T>(_factory, tableName, _options, infoProvider);
    }
}
