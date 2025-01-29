using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using TableStorage.Visitors;

namespace TableStorage;

public sealed class BlobSet<T> : IStorageSet<T>
    where T : IBlobEntity
{
    public string Name { get; }
    public Type Type => typeof(T);
    public string EntityType => Type.Name;

    private readonly BlobOptions _options;
    private readonly string? _partitionKeyProxy;
    private readonly string? _rowKeyProxy;
    private readonly IReadOnlyCollection<string> _tags;
    private readonly LazyAsync<BlobContainerClient> _containerClient;

    internal BlobSet(BlobStorageFactory factory, string tableName, BlobOptions options, string? partitionKeyProxy, string? rowKeyProxy, IReadOnlyCollection<string> tags)
    {
        Name = tableName;
        _options = options;
        _partitionKeyProxy = partitionKeyProxy;
        _rowKeyProxy = rowKeyProxy;
        _tags = tags;
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
        var (_, result) = await TryGetEntityAsync(partitionKey, rowKey, cancellationToken);
        return result;
    }

    public async Task<(bool success, T? entity)> TryGetEntityAsync(string partitionKey, string rowKey, CancellationToken cancellationToken = default)
    {
        var blob = await GetClient(partitionKey, rowKey);

        if (!await blob.ExistsAsync(cancellationToken))
        {
            return (false, default);
        }

        var result = await Download(blob, cancellationToken);
        return (result is not null, result);
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
        var bytes = _options.Serializer.Serialize(entity);
        BinaryData data = new(bytes);

        await blob.UploadAsync(data, true, cancellationToken);

        if (!_options.IsHierarchical)
        {
            Dictionary<string, string> tags = new ()
            {
                ["partition"] = entity.PartitionKey,
                ["row"] = entity.RowKey
            };

            foreach (var tag in _tags)
            {
                var tagValue = entity[tag];

                if (tagValue is not null)
                {
                    tags[tag] = tagValue.ToString();
                }
            }

            await blob.SetTagsAsync(tags, cancellationToken: cancellationToken);
        }
    }

    private async Task<T?> Download(BlobClient blob, CancellationToken cancellationToken)
    {
        using var stream = await blob.OpenReadAsync(cancellationToken: cancellationToken);
        return await _options.Serializer.DeserializeAsync<T>(stream);
    }

    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default) => QueryAsync(cancellationToken).GetAsyncEnumerator();

    public IAsyncEnumerable<T> QueryAsync(CancellationToken cancellationToken = default) => QueryAsync(null!, cancellationToken);

    public async IAsyncEnumerable<T> QueryAsync(Expression<Func<T, bool>> filter, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var (_, lazyEntity) in QueryInternalAsync(filter, cancellationToken))
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
        
        BlobQueryVisitor visitor = new(_partitionKeyProxy, _rowKeyProxy, _tags);
        var visitedFilter = visitor.VisitAndConvert(filter, nameof(QueryInternalAsync));
        LazyFilteringExpression<T> compiledFilter = filter;

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
            // Usecase: PartitionKey = 'a' and (RowKey = 'b' or RowKey = 'c')
            var lookup = visitor.Tags.ToLookup();

            var partitionKeys = lookup["partition"].ToList();
            var rowKeys = lookup["row"].ToList();

            return IterateHierarchicalFilteredOnPartitionAndRowKeys(partitionKeys, rowKeys, visitor.Tags.HasOthersThanDefaultKeys() ? compiledFilter : null, cancellationToken);
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

    private async IAsyncEnumerable<(BlobClient client, LazyAsync<T?> entity)> IterateHierarchicalFilteredOnPartitionAndRowKeys(IReadOnlyCollection<string> partitionKeys, IReadOnlyCollection<string> rowKeys, LazyFilteringExpression<T>? filterInstance, [EnumeratorCancellation] CancellationToken cancellationToken)
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
                if (entity is not null && (filterInstance?.Invoke(entity) != false))
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

                    if (entity is not null && (filterInstance?.Invoke(entity) != false))
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
