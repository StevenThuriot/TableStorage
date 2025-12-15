using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;

namespace TableStorage;

public sealed class AppendBlobSet<T> : BaseBlobSet<T, AppendBlobClient>
    where T : IBlobEntity
{
    internal AppendBlobSet(BlobStorageFactory factory, string tableName, BlobOptions options, Func<Type, ModelInfo> infoProvider, IReadOnlyCollection<string> tags)
        : base(factory, tableName, options, infoProvider, tags)
    {
    }

    protected internal override AppendBlobClient GetClient(BlobContainerClient containerClient, string id) => containerClient.GetAppendBlobClient(id);

    private static async Task AppendInternal(AppendBlobClient client, Stream stream, CancellationToken cancellationToken)
    {
        int maxBlockSize = client.AppendBlobMaxAppendBlockBytes;
        if (maxBlockSize > stream.Length)
        {
            await client.AppendBlockAsync(stream, cancellationToken: cancellationToken);
        }
        else
        {
            byte[] buffer = new byte[maxBlockSize];
            int bytesRead;
            while ((bytesRead = await stream.ReadAsync(buffer, 0, maxBlockSize, cancellationToken)) > 0)
            {
                using var data = BinaryData.FromBytes(buffer.AsMemory(0, bytesRead)).ToStream();
                await client.AppendBlockAsync(data, cancellationToken: cancellationToken);
            }
        }
    }

    public async Task AppendAsync(string partitionKey, string rowKey, Stream stream, CancellationToken cancellationToken = default)
    {
        AppendBlobClient client = await GetClient(partitionKey, rowKey);
        await AppendInternal(client, stream, cancellationToken);
    }

    protected override async Task Upload(AppendBlobClient blob, T entity, CancellationToken cancellationToken)
    {
        BinaryData data = Options.Serializer.Serialize(Name, entity);

        AppendBlobCreateOptions? options = null;

        if (Options.UseTags)
        {
            Dictionary<string, string> tags = CreateTags(entity);

            options = new()
            {
                Tags = tags
            };
        }

        await blob.CreateAsync(options, cancellationToken);
        await AppendInternal(blob, data.ToStream(), cancellationToken);
    }
}