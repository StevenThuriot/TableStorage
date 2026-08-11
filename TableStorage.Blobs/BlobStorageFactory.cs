namespace TableStorage;

internal sealed class BlobStorageFactory(BlobServiceClient client, CreateIfNotExistsMode mode)
{
    private readonly BlobServiceClient _client = client;
    private readonly CreateIfNotExistsMode _creationMode = mode;

    private static readonly HashSet<string> s_createdContainers = new(StringComparer.OrdinalIgnoreCase);
    public Task<BlobContainerClient> GetClient(string container)
    {
        BlobContainerClient client = _client.GetBlobContainerClient(container ?? throw new ArgumentNullException(nameof(container)));

        return _creationMode switch
        {
            CreateIfNotExistsMode.Always => Init(client),
            CreateIfNotExistsMode.Once when ShouldInit(container) => Init(client),
            _ => Task.FromResult(client),
        };
    }

    private static bool ShouldInit(string container)
    {
        lock (s_createdContainers)
        {
            return s_createdContainers.Add(container);
        }
    }

    private static async Task<BlobContainerClient> Init(BlobContainerClient client)
    {
        _ = await client.CreateIfNotExistsAsync();
        return client;
    }
}