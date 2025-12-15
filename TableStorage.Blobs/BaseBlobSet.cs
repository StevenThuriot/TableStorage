using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace TableStorage;

internal readonly struct BlobId(string partitionKey, string rowKey)
{
    public string PartitionKey { get; } = partitionKey;
    public string RowKey { get; } = rowKey;

    internal static string Get(string partitionKey, string rowKey) =>
            $"{partitionKey ?? throw new ArgumentNullException(nameof(partitionKey))}/{rowKey ?? throw new ArgumentNullException(nameof(rowKey))}";

    internal static BlobId Parse(string id)
    {
        int lastSlash = id.LastIndexOf('/') + 1;
        string partitionKey = id.Substring(0, lastSlash - 1);
        string rowKey = id.Substring(lastSlash);
        return new(partitionKey, rowKey);
    }

    internal static BlobId Get(BlobBaseClient client) => Parse(client.Name);
}

public abstract class BaseBlobSet<T, TClient> : IStorageSet<T>
    where TClient : BlobBaseClient
    where T : IBlobEntity
{
    protected const string PartitionTagConstant = "partition";
    protected const string RowTagConstant = "row";

    private readonly Func<Type, ModelInfo> _infoProvider;

    public string Name { get; }
    public Type Type => ModelInfo.EntityType;
    public string EntityType => ModelInfo.EntityType.Name;

    protected internal BlobOptions Options { get; }
    protected internal IReadOnlyCollection<string> Tags { get; }
    public ModelInfo ModelInfo { get; }
    public ModelInfo GetModelInfo<TType>() => _infoProvider(typeof(TType));

    private readonly LazyAsync<BlobContainerClient> _containerClient;

    internal BaseBlobSet(BlobStorageFactory factory, string tableName, BlobOptions options, Func<Type, ModelInfo> infoProvider, IReadOnlyCollection<string> tags)
    {
        _infoProvider = infoProvider;
        ModelInfo = infoProvider(typeof(T));
        Name = tableName;
        Options = options;
        Tags = tags;
        _containerClient = new(() => factory.GetClient(tableName));
    }

    protected Task<TClient> GetClient(IBlobEntity entity) => GetClient(entity.PartitionKey, entity.RowKey);

    protected Task<TClient> GetClient(string partitionKey, string rowKey)
    {
        string id = BlobId.Get(partitionKey, rowKey);
        return GetClient(id);
    }

    protected internal async Task<TClient> GetClient(string id)
    {
        var container = await _containerClient;
        return GetClient(container, id);
    }

    protected internal abstract TClient GetClient(BlobContainerClient containerClient, string id);

    public async Task<T?> GetEntityAsync(string partitionKey, string rowKey, CancellationToken cancellationToken = default)
    {
        TClient blob = await GetClient(partitionKey, rowKey);

        if (!await blob.ExistsAsync(cancellationToken))
        {
            throw new KeyNotFoundException($"Entity with PartitionKey '{partitionKey}' and RowKey = '{rowKey}' was not found.");
        }

        return await DownloadAsync(blob, cancellationToken);
    }

    public async Task<T?> GetEntityOrDefaultAsync(string partitionKey, string rowKey, CancellationToken cancellationToken = default)
    {
        (bool _, T? result) = await TryGetEntityAsync(partitionKey, rowKey, cancellationToken);
        return result;
    }

    public async Task<(bool success, T? entity)> TryGetEntityAsync(string partitionKey, string rowKey, CancellationToken cancellationToken = default)
    {
        TClient blob = await GetClient(partitionKey, rowKey);

        if (!await blob.ExistsAsync(cancellationToken))
        {
            return (false, default);
        }

        T? result = await DownloadAsync(blob, cancellationToken);
        return (result is not null, result);
    }

    public async Task AddEntityAsync(T entity, CancellationToken cancellationToken = default)
    {
        TClient blob = await GetClient(entity);

        if (await blob.ExistsAsync(cancellationToken))
        {
            throw new InvalidOperationException("Entity already exists.");
        }

        await Upload(blob, entity, cancellationToken);
    }

    public async Task UpdateEntityAsync(T entity, CancellationToken cancellationToken = default)
    {
        TClient blob = await GetClient(entity);

        if (!await blob.ExistsAsync(cancellationToken))
        {
            throw new InvalidOperationException("Entity doesn't exist.");
        }

        await Upload(blob, entity, cancellationToken);
    }

    public async Task UpsertEntityAsync(T entity, CancellationToken cancellationToken = default)
    {
        TClient blob = await GetClient(entity);
        await Upload(blob, entity, cancellationToken);
    }

    public Task DeleteEntityAsync(T entity, CancellationToken cancellationToken = default) => DeleteEntityAsync(entity.PartitionKey, entity.RowKey, cancellationToken);

    public async Task DeleteEntityAsync(string partitionKey, string rowKey, CancellationToken cancellationToken = default)
    {
        TClient blob = await GetClient(partitionKey, rowKey);
        await blob.DeleteIfExistsAsync(cancellationToken: cancellationToken);
    }

    public async Task DeleteAllEntitiesAsync(string partitionKey, CancellationToken cancellationToken = default)
    {
        if (partitionKey is null)
        {
            throw new ArgumentNullException(nameof(partitionKey));
        }

        BlobContainerClient container = await _containerClient;

        string prefix = partitionKey + '/';

        await foreach (BlobHierarchyItem blob in container.GetBlobsByHierarchyAsync(BlobTraits.None, BlobStates.None, delimiter: "/", prefix: prefix, cancellationToken: cancellationToken))
        {
            TClient blobClient = GetClient(container, blob.Blob.Name);
            await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
        }
    }

    public Task<bool> ExistsAsync(T entity, CancellationToken cancellationToken = default) => ExistsAsync(entity.PartitionKey, entity.RowKey, cancellationToken);

    public async Task<bool> ExistsAsync(string partitionKey, string rowKey, CancellationToken cancellationToken = default)
    {
        TClient blobClient = await GetClient(partitionKey, rowKey);
        return await blobClient.ExistsAsync(cancellationToken);
    }

    protected abstract Task Upload(TClient blob, T entity, CancellationToken cancellationToken);

    protected Dictionary<string, string> CreateTags(T entity)
    {
        Dictionary<string, string> tags = new(2 + Tags.Count)
        {
            [PartitionTagConstant] = entity.PartitionKey,
            [RowTagConstant] = entity.RowKey
        };

        foreach (string tag in Tags)
        {
            object? tagValue = entity[tag];

            if (tagValue is not null)
            {
                tags[tag] = tagValue.ToString();
            }
        }

        return tags;
    }

    internal async Task<T?> DownloadAsync(TClient blob, CancellationToken cancellationToken)
    {
        using Stream stream = await GetStreamAsync(blob, cancellationToken);

        if (stream.Length is 0)
        {
            return default;
        }

        return await Options.Serializer.DeserializeAsync<T>(Name, stream, cancellationToken);
    }

    private static async Task<Stream> GetStreamAsync(TClient blob, CancellationToken cancellationToken)
    {
        MemoryStream stream = new();
        await blob.DownloadToAsync(stream, cancellationToken);
        stream.Position = 0;
        return stream;
    }

    public async Task<(bool success, Stream? stream)> TryGetStreamAsync(string partitionKey, string rowKey, CancellationToken cancellationToken = default)
    {
        TClient blob = await GetClient(partitionKey, rowKey);

        if (!await blob.ExistsAsync(cancellationToken))
        {
            return (false, null);
        }

        Stream stream = await GetStreamAsync(blob, cancellationToken);
        return (true, stream);
    }

    public async Task<Stream> GetStreamAsync(string partitionKey, string rowKey, CancellationToken cancellationToken = default)
    {
        var (success, stream) = await TryGetStreamAsync(partitionKey, rowKey, cancellationToken);

        if (!success)
        {
            throw new KeyNotFoundException($"Entity with PartitionKey '{partitionKey}' and RowKey = '{rowKey}' was not found.");
        }

        return stream!;
    }

    public async IAsyncEnumerable<string> FindRowsAsync(string partition, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (partition is null)
        {
            throw new ArgumentNullException(nameof(partition));
        }

        BlobContainerClient container = await _containerClient;

        if (Options.UseTags)
        {
            string filter = $"{PartitionTagConstant}='{partition}'";
            await foreach (TaggedBlobItem blob in container.FindBlobsByTagsAsync(filter, cancellationToken))
            {
                yield return BlobId.Parse(blob.BlobName).RowKey;
            }
        }
        else
        {
            await foreach (BlobItem blob in container.GetBlobsAsync(BlobTraits.None, BlobStates.None, prefix: partition + '/', cancellationToken: cancellationToken))
            {
                yield return BlobId.Parse(blob.Name).RowKey;
            }
        }
    }

    public async IAsyncEnumerable<string> FindPartitionsAsync(string row, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (row is null)
        {
            throw new ArgumentNullException(nameof(row));
        }

        BlobContainerClient container = await _containerClient;

        if (Options.UseTags)
        {
            string filter = $"{RowTagConstant}='{row}'";
            await foreach (TaggedBlobItem blob in container.FindBlobsByTagsAsync(filter, cancellationToken))
            {
                yield return BlobId.Parse(blob.BlobName).PartitionKey;
            }
        }
        else
        {
            await foreach (BlobItem blob in container.GetBlobsAsync(BlobTraits.None, BlobStates.None, cancellationToken: cancellationToken))
            {
                yield return BlobId.Parse(blob.Name).PartitionKey;
            }
        }
    }

    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default) => QueryAsync(cancellationToken).GetAsyncEnumerator(cancellationToken);

    public IAsyncEnumerable<T> QueryAsync(CancellationToken cancellationToken = default) => QueryAsync(null!, cancellationToken);

    public async IAsyncEnumerable<T> QueryAsync(Expression<Func<T, bool>> filter, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach ((TClient _, LazyAsync<T?> lazyEntity) in QueryInternalAsync(filter, cancellationToken))
        {
            T? entity = await lazyEntity;
            if (entity is not null)
            {
                yield return entity;
            }
        }
    }

    internal IAsyncEnumerable<(TClient client, LazyAsync<T?> entity)> QueryInternalAsync(Expression<Func<T, bool>>? filter, CancellationToken cancellationToken = default)
    {
        if (filter is null)
        {
            return IterateAllBlobs(cancellationToken);
        }

        return Options.QueryHandlerFactory.QueryAsync(this, filter, cancellationToken);
    }

    internal async IAsyncEnumerable<(TClient client, LazyAsync<T?> entity)> IterateAllBlobs([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        BlobContainerClient container = await _containerClient;

        await foreach (BlobItem blob in GetAllBlobItemsAsync(cancellationToken))
        {
            TClient client = GetClient(container, blob.Name);
            yield return (client, new(() => DownloadAsync(client, cancellationToken)));
        }
    }

    internal async IAsyncEnumerable<BlobItem> GetAllBlobItemsAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        BlobContainerClient container = await _containerClient;
        await foreach (BlobItem blob in GetAllBlobItemsAsync(container, cancellationToken))
        {
            yield return blob;
        }
    }

    private static async IAsyncEnumerable<BlobItem> GetAllBlobItemsAsync(BlobContainerClient container, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (BlobItem blob in container.GetBlobsAsync(BlobTraits.Tags, BlobStates.None, cancellationToken: cancellationToken))
        {
            yield return blob;
        }
    }

    internal async IAsyncEnumerable<(TClient client, LazyAsync<T?> entity)> IterateBlobsByTag(string filter, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (!Options.UseTags)
        {
            throw new InvalidOperationException("Tags is disabled yet we ended up in a tags call");
        }

        BlobContainerClient container = await _containerClient;

        await foreach (TaggedBlobItem blob in container.FindBlobsByTagsAsync(filter, cancellationToken))
        {
            TClient client = GetClient(container, blob.BlobName);
            yield return (client, new(() => DownloadAsync(client, cancellationToken)));
        }
    }
}