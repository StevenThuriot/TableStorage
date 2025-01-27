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
    private readonly LazyAsync<BlobContainerClient> _containerClient;

    internal BlobSet(BlobStorageFactory factory, string tableName, BlobOptions options)
    {
        Name = tableName;
        _options = options;
        _containerClient = new(() => factory.GetClient(Name));
    }

    private async Task<BlobClient> GetClient(string partitionKey, string rowKey)
    {
        string id = $"{partitionKey ?? throw new ArgumentNullException(nameof(partitionKey))}/{rowKey ?? throw new ArgumentNullException(nameof(rowKey))}";
        var client = await _containerClient;
        return client.GetBlobClient(id);
    }

    private Task<BlobClient> GetClient(IBlobEntity entity)
    {
        return GetClient(entity.PartitionKey, entity.RowKey);
    }

    public async Task<T?> GetEntityAsync(string partitionKey, string rowKey, CancellationToken cancellationToken = default)
    {
        var blob = await GetClient(partitionKey, rowKey);

        if (!await blob.ExistsAsync(cancellationToken))
        {
            throw new KeyNotFoundException($"Entity with PartitionKey '{partitionKey}' and RowKey = '{rowKey}' was not found.");
        }

        return await Download(blob, cancellationToken);
    }

    public async Task<T?> GetEntityOrDefaultAsync(string partitionKey, string rowKey, CancellationToken cancellationToken = default)
    {
        var blob = await GetClient(partitionKey, rowKey);

        if (!await blob.ExistsAsync(cancellationToken))
        {
            return default;
        }

        return await Download(blob, cancellationToken);
    }

    public async Task AddEntityAsync(T entity, CancellationToken cancellationToken = default)
    {
        var blob = await GetClient(entity);

        if (await blob.ExistsAsync(cancellationToken))
        {
            throw new InvalidOperationException("Entity already exists.");
        }

        await Upload(blob, entity, cancellationToken);
    }

    public async Task UpdateEntityAsync(T entity, CancellationToken cancellationToken = default)
    {
        var blob = await GetClient(entity);

        if (!await blob.ExistsAsync(cancellationToken))
        {
            throw new InvalidOperationException("Entity doesn't exist.");
        }

        await Upload(blob, entity, cancellationToken);
    }

    public async Task UpsertEntityAsync(T entity, CancellationToken cancellationToken = default)
    {
        var blob = await GetClient(entity);
        await Upload(blob, entity, cancellationToken);
    }

    public Task DeleteEntityAsync(T entity, CancellationToken cancellationToken = default) => DeleteEntityAsync(entity.PartitionKey, entity.RowKey, cancellationToken);
    
    public async Task DeleteEntityAsync(string partitionKey, string rowKey, CancellationToken cancellationToken = default)
    {
        var blob = await GetClient(partitionKey, rowKey);
        await blob.DeleteIfExistsAsync(cancellationToken: cancellationToken);
    }

    public async Task DeleteAllEntitiesAsync(string partitionKey, CancellationToken cancellationToken = default)
    {
        partitionKey = partitionKey ?? throw new ArgumentNullException(nameof(partitionKey));

        var container = await _containerClient;

        await foreach (var blob in container.FindBlobsByTagsAsync($"partition='{partitionKey}'", cancellationToken))
        {
            var blobClient = container.GetBlobClient(blob.BlobName);
            await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
        }
    }

    private async Task Upload(BlobClient blob, T entity, CancellationToken cancellationToken)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(entity, _options.SerializerOptions);
        BinaryData data = new(bytes);

        await blob.UploadAsync(data, true, cancellationToken);

        await blob.SetTagsAsync(new Dictionary<string, string>
        {
            ["partition"] = entity.PartitionKey,
            ["row"] = entity.RowKey
        }, cancellationToken: cancellationToken);
    }

    private async Task<T?> Download(BlobClient blob, CancellationToken cancellationToken)
    {
        using var stream = await blob.OpenReadAsync(cancellationToken: cancellationToken);
        return JsonSerializer.Deserialize<T>(stream, _options.SerializerOptions);
    }

    public async IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        var container = await _containerClient;

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
