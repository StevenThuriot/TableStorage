using System.Collections.Concurrent;

namespace TableStorage;

internal sealed class TableStorageFactory(string connectionString, bool createIfNotExists)
{
    private readonly string _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    private readonly bool _createIfNotExists = createIfNotExists;
    private readonly ConcurrentDictionary<string, Task<TableClient>> _tableClients = new(StringComparer.OrdinalIgnoreCase);

    public Task<TableClient> GetClient(string tableName)
    {
        return _tableClients.GetOrAdd(tableName, async name =>
        {
            TableClient client = new(_connectionString, name);

            if (_createIfNotExists)
            {
                _ = await client.CreateIfNotExistsAsync();
            }

            return client;
        });
    }
}
