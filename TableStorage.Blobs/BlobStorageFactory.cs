namespace TableStorage;

internal sealed class BlobStorageFactory(string connectionString, bool autoCreate)
{
    private readonly BlobServiceClient _client = new(connectionString ?? throw new ArgumentNullException(nameof(connectionString)));
    private readonly bool _autoCreate = autoCreate;

    public async Task<BlobContainerClient> GetClient(string container)
    {
        BlobContainerClient client = _client.GetBlobContainerClient(container ?? throw new ArgumentNullException());

        if (_autoCreate && !await client.ExistsAsync())
        {
            await client.CreateAsync();
        }

        return client;
    }
}
