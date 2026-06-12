using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using TableStorage.Visitors;

namespace TableStorage;

public abstract class TableSet<T> : IStorageSet<T>
    where T : class, ITableEntity, new()
{
    private readonly Func<Type, ModelInfo> _infoProvider;

    public string Name { get; }
    public Type Type => ModelInfo.EntityType;
    public string EntityType => ModelInfo.EntityType.Name;

    internal LazyAsync<TableClient> LazyClient { get; }
    internal TableOptions Options { get; }

    public ModelInfo ModelInfo { get; }
    public ModelInfo GetModelInfo<TType>() => GetModelInfo(typeof(TType));
    public ModelInfo GetModelInfo(Type type) => _infoProvider(type);

    internal TableSet(TableStorageFactory factory, string tableName, TableOptions options, Func<Type, ModelInfo> infoProvider)
    {
        _infoProvider = infoProvider;
        ModelInfo = infoProvider(typeof(T));
        Name = tableName;
        LazyClient = new(() => factory.GetClient(tableName));
        Options = options;
    }

    public abstract Task AddEntityAsync(T entity, CancellationToken cancellationToken = default);

    public Task DeleteEntityAsync(string partitionKey, string rowKey, CancellationToken cancellationToken = default) => DeleteEntityAsync(partitionKey, rowKey, ETag.All, cancellationToken);

    public async Task DeleteEntityAsync(string partitionKey, string rowKey, ETag ifMatch, CancellationToken cancellationToken = default)
    {
        TableClient client = await LazyClient;
        await client.DeleteEntityAsync(partitionKey, rowKey, ifMatch, cancellationToken);
    }

    public async Task DeleteEntityAsync(T entity, CancellationToken cancellationToken = default)
    {
        TableClient client = await LazyClient;
        await client.DeleteEntityAsync(entity.PartitionKey, entity.RowKey, entity.ETag, cancellationToken);
    }

    public Task SubmitTransactionAsync(IEnumerable<TableTransactionAction> transactionActions, CancellationToken cancellationToken = default)
    {
        return SubmitTransactionAsync(transactionActions, Options.TransactionSafety, cancellationToken);
    }

    public virtual Task SubmitTransactionAsync(IEnumerable<TableTransactionAction> transactionActions, TransactionSafety transactionSafety, CancellationToken cancellationToken = default)
    {
        return transactionSafety switch
        {
            TransactionSafety.Enabled => SafelySubmit(),
            TransactionSafety.Disabled => UnsafelySubmit(),
            _ => throw new NotSupportedException(),
        };

        async Task SafelySubmit()
        {
            TableClient client = await LazyClient;
            foreach (IGrouping<string, TableTransactionAction>? partition in transactionActions.GroupBy(x => x.Entity.PartitionKey))
            {
                foreach (IEnumerable<TableTransactionAction> chunk in partition.Chunk(Options.TransactionChunkSize))
                {
                    await client.SubmitTransactionAsync(chunk, cancellationToken);
                }
            }
        }

        async Task UnsafelySubmit()
        {
            TableClient client = await LazyClient;
            await client.SubmitTransactionAsync(transactionActions, cancellationToken);
        }
    }

    public Task UpdateEntityAsync(T entity, CancellationToken cancellationToken = default) => UpdateEntityAsync(entity, entity.ETag, null, cancellationToken);

    public abstract Task UpdateEntityAsync(T entity, ETag ifMatch, TableUpdateMode? mode, CancellationToken cancellationToken = default);

    public Task UpsertEntityAsync(T entity, CancellationToken cancellationToken = default) => UpsertEntityAsync(entity, null, cancellationToken);

    public abstract Task UpsertEntityAsync(T entity, TableUpdateMode? mode, CancellationToken cancellationToken = default);

    public Task<T?> GetEntityAsync(string partitionKey, string rowKey, CancellationToken cancellationToken = default) => GetEntityAsync(partitionKey, rowKey, null, cancellationToken);

    public virtual async Task<T?> GetEntityAsync(string partitionKey, string rowKey, IEnumerable<string>? select, CancellationToken cancellationToken = default)
    {
        TableClient client = await LazyClient;
        Response<T> result = await client.GetEntityAsync<T>(partitionKey, rowKey, select, cancellationToken);
        return result.Value;
    }

    public Task<T?> GetEntityOrDefaultAsync(string partitionKey, string rowKey, CancellationToken cancellationToken = default) => GetEntityOrDefaultAsync(partitionKey, rowKey, null, cancellationToken);

    public async Task<T?> GetEntityOrDefaultAsync(string partitionKey, string rowKey, IEnumerable<string>? select, CancellationToken cancellationToken = default)
    {
        (bool _, T? entity) = await TryGetEntityAsync(partitionKey, rowKey, select, cancellationToken);
        return entity;
    }

    public Task<(bool success, T? entity)> TryGetEntityAsync(string partitionKey, string rowKey, CancellationToken cancellationToken = default) => TryGetEntityAsync(partitionKey, rowKey, null, cancellationToken);

    public virtual async Task<(bool success, T? entity)> TryGetEntityAsync(string partitionKey, string rowKey, IEnumerable<string>? select, CancellationToken cancellationToken = default)
    {
        TableClient client = await LazyClient;
        NullableResponse<T> result = await client.GetEntityIfExistsAsync<T>(partitionKey, rowKey, select, cancellationToken);

        if (result.HasValue)
        {
            T entity = result.Value!;
            return (true, entity);
        }

        return (false, default);
    }

    public IAsyncEnumerable<T> QueryAsync(CancellationToken cancellationToken = default) => QueryAsync((string?)null, null, null, cancellationToken);

    public IAsyncEnumerable<T> QueryAsync(string? filter, CancellationToken cancellationToken = default) => QueryAsync(filter, null, null, cancellationToken);

    public virtual async IAsyncEnumerable<T> QueryAsync(string? filter, int? maxPerPage, IEnumerable<string>? select, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        TableClient client = await LazyClient;
        await foreach (T entity in client.QueryAsync<T>(filter, maxPerPage ?? Options.PageSize, select, cancellationToken))
        {
            yield return entity;
        }
    }

    public IAsyncEnumerable<T> QueryAsync(Expression<Func<T, bool>> filter, CancellationToken cancellationToken = default) => QueryAsync(filter, null, null, cancellationToken);

    public virtual IAsyncEnumerable<T> QueryAsync(Expression<Func<T, bool>> filter, int? maxPerPage, IEnumerable<string>? select, CancellationToken cancellationToken = default)
    {
        if (ModelInfo.HasProxies())
        {
            WhereVisitor visitor = new(ModelInfo);
            filter = (Expression<Func<T, bool>>)visitor.Visit(filter);
        }

        return InternalQueryAsync(filter, maxPerPage, select, cancellationToken);
    }

    internal async IAsyncEnumerable<T> InternalQueryAsync(Expression<Func<T, bool>> filter, int? maxPerPage, IEnumerable<string>? select, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        TableClient client = await LazyClient;
        maxPerPage ??= Options.PageSize;

        if (Options.OptimizeQueries)
        {
            List<Expression<Func<T, bool>>>? splitFilters = PartitionKeySplitVisitor.TrySplit(filter);

            if (splitFilters is { Count: > 1 })
            {
                foreach (Expression<Func<T, bool>> splitFilter in splitFilters)
                {
                    await foreach (T entity in client.QueryAsync(splitFilter, maxPerPage, select, cancellationToken))
                    {
                        yield return entity;
                    }
                }

                yield break;
            }
        }

        await foreach (T entity in client.QueryAsync(filter, maxPerPage, select, cancellationToken))
        {
            yield return entity;
        }
    }

    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default) => QueryAsync(cancellationToken).GetAsyncEnumerator(cancellationToken);

    #region Bulk Operations

    protected abstract Task ExecuteInBulkAsync(IEnumerable<T> entities, TableTransactionActionType tableTransactionActionType, CancellationToken cancellationToken);

    public Task BulkInsertAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
    {
        return ExecuteInBulkAsync(entities, TableTransactionActionType.Add, cancellationToken);
    }

    protected virtual BulkOperation GetBulkOperation(BulkOperation? bulkOperation)
    {
        if (bulkOperation.HasValue)
        {
            return bulkOperation.GetValueOrDefault();
        }

        return Options.BulkOperation;
    }

    public Task BulkUpdateAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default) => BulkUpdateAsync(entities, null, cancellationToken);

    public Task BulkUpdateAsync(IEnumerable<T> entities, BulkOperation? bulkOperation, CancellationToken cancellationToken = default)
    {
        TableTransactionActionType tableTransactionActionType = GetBulkOperation(bulkOperation) switch
        {
            BulkOperation.Replace => TableTransactionActionType.UpdateReplace,
            BulkOperation.Merge => TableTransactionActionType.UpdateMerge,
            _ => throw new NotSupportedException(),
        };

        return ExecuteInBulkAsync(entities, tableTransactionActionType, cancellationToken);
    }

    public Task BulkUpsertAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default) => BulkUpsertAsync(entities, null, cancellationToken);

    public Task BulkUpsertAsync(IEnumerable<T> entities, BulkOperation? bulkOperation, CancellationToken cancellationToken = default)
    {
        TableTransactionActionType tableTransactionActionType = GetBulkOperation(bulkOperation) switch
        {
            BulkOperation.Replace => TableTransactionActionType.UpsertReplace,
            BulkOperation.Merge => TableTransactionActionType.UpsertMerge,
            _ => throw new NotSupportedException(),
        };

        return ExecuteInBulkAsync(entities, tableTransactionActionType, cancellationToken);
    }

    public Task BulkDeleteAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
    {
        return ExecuteInBulkAsync(entities, TableTransactionActionType.Delete, cancellationToken);
    }
    #endregion Bulk Operations

    #region Merge Operations

    internal async Task UpdateAsync(ITableEntity entity, CancellationToken cancellationToken)
    {
        TableClient client = await LazyClient;
        await client.UpdateEntityAsync(entity, entity.ETag, TableUpdateMode.Merge, cancellationToken);
    }

    internal async Task UpsertAsync(ITableEntity entity, CancellationToken cancellationToken)
    {
        TableClient client = await LazyClient;
        await client.UpsertEntityAsync(entity, TableUpdateMode.Merge, cancellationToken);
    }
    #endregion Merge Operations
}