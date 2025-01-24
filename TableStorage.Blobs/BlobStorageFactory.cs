namespace TableStorage;

internal class BlobStorageFactory(string connectionString)
{
    private readonly BlobServiceClient _client = new(connectionString ?? throw new ArgumentNullException(nameof(connectionString)));

    public BlobContainerClient GetClient(string container)
    {
        return _client.GetBlobContainerClient(container ?? throw new ArgumentNullException(nameof(container)));
    }

    public BlobContainerClient GetClient(string container, string partitionKey)
    {
        return GetClient(container + "/" + (partitionKey ?? throw new ArgumentNullException(nameof(partitionKey))));
    }

    public BlobClient GetClient(string container, string partitionKey, string rowKey)
    {
        return GetClient(container, partitionKey).GetBlobClient(rowKey ?? throw new ArgumentNullException(nameof(rowKey)));
    }

    public BlobClient GetClient(string container, IBlobEntity entity)
    {
        return GetClient(container, entity.PartitionKey, entity.RowKey);
    }
}
