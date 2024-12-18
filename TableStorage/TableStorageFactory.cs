namespace TableStorage;

internal sealed class TableStorageFactory(string connectionString, bool createIfNotExists)
{
    private readonly TableServiceClient _client = new(connectionString ?? throw new ArgumentNullException(nameof(connectionString)));
    private readonly bool _createIfNotExists = createIfNotExists;

    public async Task<TableClient> GetClient(string tableName)
    {
        TableClient client = _client.GetTableClient(tableName);

        if (_createIfNotExists)
        {
            _ = await client.CreateIfNotExistsAsync();
        }

        return client;
    }
}
