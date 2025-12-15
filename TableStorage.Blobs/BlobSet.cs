using Azure.Storage.Blobs.Models;

namespace TableStorage;

public sealed class BlobSet<T> : BaseBlobSet<T, BlobClient>
    where T : IBlobEntity
{
    internal BlobSet(BlobStorageFactory factory, string tableName, BlobOptions options, Func<Type, ModelInfo> infoProvider, IReadOnlyCollection<string> tags)
        : base(factory, tableName, options, infoProvider, tags)
    {
    }

    protected internal override BlobClient GetClient(BlobContainerClient containerClient, string id) => containerClient.GetBlobClient(id);

    protected override Task Upload(BlobClient blob, T entity, CancellationToken cancellationToken)
    {
        BinaryData data = Options.Serializer.Serialize(Name, entity);

        BlobUploadOptions? options = null;

        if (Options.UseTags)
        {
            Dictionary<string, string> tags = CreateTags(entity);

            options = new()
            {
                Tags = tags
            };
        }

        return blob.UploadAsync(data, options, cancellationToken);
    }
}