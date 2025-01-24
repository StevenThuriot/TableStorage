using Azure.Storage.Blobs.Models;
using System.Runtime.CompilerServices;

namespace TableStorage;

public sealed class BlobSet<T> : IAsyncEnumerable<T>
    where T : IBlobEntity
{
    public string Name { get; }
    public Type Type => typeof(T);
    public string EntityType => Type.Name;

    private readonly BlobOptions _options;
    private readonly BlobStorageFactory _factory;

    internal BlobSet(BlobStorageFactory factory, string tableName, BlobOptions options)
    {
        Name = tableName;
        _options = options;
        _factory = factory;
    }

    public Task<T?> GetEntityOrDefaultAsync(string partitionKey, string rowKey, CancellationToken cancellationToken = default)
    {
        var blob = _factory.GetClient(Name, partitionKey, rowKey);
        return Download(blob, cancellationToken);
    }

    public async Task AddEntityAsync(T entity, CancellationToken cancellationToken = default)
    {
        var blob = _factory.GetClient(Name, entity);

        if (await blob.ExistsAsync(cancellationToken))
        {
            throw new InvalidOperationException("Entity already exists.");
        }

        await Upload(blob, entity, cancellationToken);
    }

    public async Task UpdateEntityAsync(T entity, CancellationToken cancellationToken = default)
    {
        var blob = _factory.GetClient(Name, entity);

        if (!await blob.ExistsAsync(cancellationToken))
        {
            throw new InvalidOperationException("Entity doesn't exist.");
        }

        await Upload(blob, entity, cancellationToken);
    }

    public Task UpsertEntityAsync(T entity, CancellationToken cancellationToken = default)
    {
        var blob = _factory.GetClient(Name, entity);
        return Upload(blob, entity, cancellationToken);
    }

    public Task DeleteEntityAsync(T entity, CancellationToken cancellationToken = default) => DeleteEntityAsync(entity.PartitionKey, entity.RowKey, cancellationToken);
    public async Task DeleteEntityAsync(string partitionKey, string rowKey, CancellationToken cancellationToken = default)
    {
        var blob = _factory.GetClient(Name, partitionKey, rowKey);
        await blob.DeleteIfExistsAsync(cancellationToken: cancellationToken);
    }
    public async Task DeleteAllEntitiesAsync(string partitionKey, CancellationToken cancellationToken = default)
    {
        var container = _factory.GetClient(Name, partitionKey);
        await container.DeleteIfExistsAsync(cancellationToken: cancellationToken);
    }

    private async Task Upload(BlobClient blob, T entity, CancellationToken cancellationToken)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(entity, _options.SerializerOptions);
        BinaryData data = new(bytes);
        await blob.UploadAsync(data, true, cancellationToken);
    }

    private async Task<T?> Download(BlobClient blob, CancellationToken cancellationToken)
    {
        using var stream = await blob.OpenReadAsync(cancellationToken: cancellationToken);
        return JsonSerializer.Deserialize<T>(stream, _options.SerializerOptions);
    }

    public async IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        var container = _factory.GetClient(Name);

        await foreach (var blob in IterateBlobs(container, cancellationToken))
        {
            var blobClient = container.GetBlobClient(blob.Name);
            var result = await Download(blobClient, cancellationToken);

            if (result is not null)
            {
                yield return result;
            }
        }
    }

    private async IAsyncEnumerable<BlobItem> IterateBlobs(BlobContainerClient container, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (!await container.ExistsAsync(cancellationToken))
        {
            yield break;
        }

        await foreach (var blob in container.GetBlobsAsync(cancellationToken: cancellationToken))
        {
            if (blob.Deleted)
            {
                continue;
            }

            yield return blob;
        }
    }
}
