using Azure.Storage.Blobs.Models;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using TableStorage.Visitors;

namespace TableStorage;

public sealed class BlobSet<T> : IAsyncEnumerable<T>
    where T : IBlobEntity
{
    public string Name { get; }
    public Type Type => typeof(T);
    public string EntityType => Type.Name;

    private readonly BlobOptions _options;
    private readonly string? _partitionKeyProxy;
    private readonly string? _rowKeyProxy;
    private readonly LazyAsync<BlobContainerClient> _containerClient;

    internal BlobSet(BlobStorageFactory factory, string tableName, BlobOptions options, string? partitionKeyProxy, string? rowKeyProxy)
    {
        Name = tableName;
        _options = options;
        _partitionKeyProxy = partitionKeyProxy;
        _rowKeyProxy = rowKeyProxy;
        _containerClient = new(() => factory.GetClient(tableName));
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
        if (partitionKey is null)
        {
            throw new ArgumentNullException(nameof(partitionKey));
        }

        var container = await _containerClient;

        if (_options.IsHierarchical)
        {
            string prefix = partitionKey + '/';

            await foreach (var blob in container.GetBlobsByHierarchyAsync(prefix: prefix, delimiter: "/", cancellationToken: cancellationToken))
            {
                if (!blob.IsBlob || blob.Blob.Deleted)
                {
                    continue;
                }

                await Delete(container, blob.Blob.Name, cancellationToken);
            }
        }
        else
        {
            await foreach (var blob in container.FindBlobsByTagsAsync($"partition='{partitionKey}'", cancellationToken))
            {
                await Delete(container, blob.BlobName, cancellationToken);
            }
        }

        static async Task Delete(BlobContainerClient container, string name, CancellationToken cancellationToken)
        {
            var blobClient = container.GetBlobClient(name);
            await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
        }
    }

    private async Task Upload(BlobClient blob, T entity, CancellationToken cancellationToken)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(entity, _options.SerializerOptions);
        BinaryData data = new(bytes);

        await blob.UploadAsync(data, true, cancellationToken);

        if (!_options.IsHierarchical)
        {
            await blob.SetTagsAsync(new Dictionary<string, string>
            {
                ["partition"] = entity.PartitionKey,
                ["row"] = entity.RowKey
            }, cancellationToken: cancellationToken);
        }
    }

    private async Task<T?> Download(BlobClient blob, CancellationToken cancellationToken)
    {
        using var stream = await blob.OpenReadAsync(cancellationToken: cancellationToken);
        return JsonSerializer.Deserialize<T>(stream, _options.SerializerOptions);
    }

    public async IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        await foreach (var (_, lazyEntity) in QueryInternalAsync(null, cancellationToken))
        {
            var entity = await lazyEntity;

            if (entity is not null)
            {
                yield return entity;
            }
        }
    }

    internal IAsyncEnumerable<(BlobClient client, LazyAsync<T?> entity)> QueryInternalAsync(Expression<Func<T, bool>>? filter, CancellationToken cancellationToken = default)
    {
        if (filter is null)
        {
            return IterateAllBlobs(cancellationToken);
        }
        
        BlobQueryVisitor visitor = new(_partitionKeyProxy, _rowKeyProxy);
        LazyFilteringExpression<T> compiledFilter = visitor.VisitAndConvert(filter, nameof(QueryInternalAsync));

        if (!_options.IsHierarchical)
        {
            if (!visitor.Error)
            {
                if (visitor.SimpleFilter)
                {
                    return IterateBlobsByTag(visitor.Filter!, cancellationToken);
                }

                return IterateBlobsByTagAndComplexFilter(visitor.Filter!, compiledFilter, cancellationToken);
            }
        }

        if (!visitor.Error)
        {
            return IterateFilteredOnPartitionAndRowKeys(visitor.PartitionKeys, visitor.RowKeys, filter, cancellationToken);
        }

        return IterateFilteredAtRuntime(filter, cancellationToken);
    }

    private async IAsyncEnumerable<(BlobClient client, LazyAsync<T?> entity)> IterateAllBlobs([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var container = await _containerClient;

        await foreach (var blob in container.GetBlobsAsync(cancellationToken: cancellationToken))
        {
            if (!blob.Deleted)
            {
                var client = container.GetBlobClient(blob.Name);
                yield return (client, new(() => Download(client, cancellationToken)));
            }
        }
    }

    private async IAsyncEnumerable<(BlobClient client, LazyAsync<T?> entity)> IterateBlobsByTag(string filter, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var container = await _containerClient;

        if (_options.IsHierarchical)
        {
            throw new NotSupportedException("Hierarchical namespaces aren't supported in this method");
        }

        await foreach (var blob in container.FindBlobsByTagsAsync(filter, cancellationToken))
        {
            var client = container.GetBlobClient(blob.BlobName);
            yield return (client, new(() => Download(client, cancellationToken)));
        }
    }

    private async IAsyncEnumerable<(BlobClient client, LazyAsync<T?> entity)> IterateBlobsByTagAndComplexFilter(string filter, LazyFilteringExpression<T> compiledFilter, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var result in IterateBlobsByTag(filter, cancellationToken))
        {
            var entity = await result.entity;
            if (entity is not null && compiledFilter.Invoke(entity))
            {
                yield return result;
            }
        }
    }

    private async IAsyncEnumerable<(BlobClient client, LazyAsync<T?> entity)> IterateFilteredOnPartitionAndRowKeys(IReadOnlyCollection<string> partitionKeys, IReadOnlyCollection<string> rowKeys, LazyFilteringExpression<T> filterInstance, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var container = await _containerClient;

        if (partitionKeys.Count is 0)
        {
            await foreach (var result in IterateAllBlobs(cancellationToken))
            {
                if (!IsRowKeyMatch(result.client.Name))
                {
                    continue;
                }

                var entity = await result.entity;
                if (entity is not null && filterInstance.Invoke(entity))
                {
                    yield return result!;
                }
            }
        }
        else
        {
            foreach (var partitionKey in partitionKeys)
            {
                string prefix = partitionKey + '/';

                await foreach (var blob in container.GetBlobsByHierarchyAsync(prefix: prefix, delimiter: "/", cancellationToken: cancellationToken))
                {
                    if (!blob.IsBlob || blob.Blob.Deleted || !IsRowKeyMatch(blob.Blob.Name))
                    {
                        continue;
                    }

                    var client = container.GetBlobClient(blob.Blob.Name);
                    var entity = await Download(client, cancellationToken);

                    if (entity is not null && filterInstance.Invoke(entity))
                    {
                        yield return (client, new(() => Task.FromResult<T?>(entity)));
                    }
                }
            }
        }

        bool IsRowKeyMatch(string name)
        {
            if (rowKeys.Count is 0)
            {
                return true;
            }

            var lastSlash = name.LastIndexOf('/') + 1;
            var rowKey = name.Substring(lastSlash);
            return rowKeys.Contains(rowKey);
        }
    }

    private async IAsyncEnumerable<(BlobClient client, LazyAsync<T?> entity)> IterateFilteredAtRuntime(Expression<Func<T, bool>> filter, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        LazyFilteringExpression<T> compiledFilter = filter;

        await foreach (var result in IterateAllBlobs(cancellationToken))
        {
            var entity = await result.entity;
            if (entity is not null && compiledFilter.Invoke(entity))
            {
                yield return result;
            }
        }
    }
}
