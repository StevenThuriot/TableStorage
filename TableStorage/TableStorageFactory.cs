namespace TableStorage;

internal sealed class TableStorageFactory(string connectionString, CreateIfNotExistsMode mode)
{
    private readonly TableServiceClient _client = new(connectionString ?? throw new ArgumentNullException(nameof(connectionString)));
    private readonly CreateIfNotExistsMode _creationMode = mode;

    private static readonly HashSet<string> s_createdTables = new(StringComparer.OrdinalIgnoreCase);
    public Task<TableClient> GetClient(string tableName)
    {
        TableClient client = _client.GetTableClient(tableName ?? throw new ArgumentNullException(nameof(tableName)));

        return _creationMode switch
        {
            CreateIfNotExistsMode.Always => Init(client),
            CreateIfNotExistsMode.Once when s_createdTables.Add(tableName) => Init(client),
            _ => Task.FromResult(client),
        };
    }

    private static async Task<TableClient> Init(TableClient client)
    {
        _ = await client.CreateIfNotExistsAsync();
        return client;
    }
}