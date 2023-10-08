using System.Collections.Concurrent;

namespace TableStorage;

internal sealed class TableStorageFactory
{
    private readonly string _connectionString;
    private readonly ConcurrentDictionary<string, Task<TableClient>> _tableClients = new(StringComparer.OrdinalIgnoreCase);

    public TableStorageFactory(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    public Task<TableClient> GetClient(string tableName)
    {
        return _tableClients.GetOrAdd(tableName, async name =>
        {
            TableClient client = new(_connectionString, name);
            _ = await client.CreateIfNotExistsAsync();
            return client;
        });
    }
}
