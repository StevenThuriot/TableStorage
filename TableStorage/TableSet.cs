using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using TableStorage.Visitors;

namespace TableStorage;

public abstract class TableSet<T> : IStorageSet<T> 
    where T : class, ITableEntity, new()
{
    public string Name { get; }
    public Type Type => typeof(T);
    public string EntityType => Type.Name;

    internal LazyAsync<TableClient> LazyClient { get; }
    internal TableOptions Options { get; }
    internal string? PartitionKeyProxy { get; }
    internal string? RowKeyProxy { get; }

    public static bool HasChangeTracking { get; } = typeof(IChangeTracking).IsAssignableFrom(typeof(T));

    internal TableSet(TableStorageFactory factory, string tableName, TableOptions options)
    {
        Name = tableName;
        LazyClient = new(() => factory.GetClient(tableName));
        Options = options;
    }

    internal TableSet(TableStorageFactory factory, string tableName, TableOptions options, string? partitionKeyProxy, string? rowKeyProxy)
        : this(factory, tableName, options)
    {
        PartitionKeyProxy = partitionKeyProxy;
        RowKeyProxy = rowKeyProxy;
    }

    public abstract Task AddEntityAsync(T entity, CancellationToken cancellationToken = default);

    public Task DeleteEntityAsync(string partitionKey, string rowKey, CancellationToken cancellationToken = default) => DeleteEntityAsync(partitionKey, rowKey, ETag.All, cancellationToken);

    public async Task DeleteEntityAsync(string partitionKey, string rowKey, ETag ifMatch, CancellationToken cancellationToken = default)
    {
        TableClient client = await LazyClient;
        await client.DeleteEntityAsync(partitionKey, rowKey, ifMatch, cancellationToken);
    }

    public Task SubmitTransactionAsync(IEnumerable<TableTransactionAction> transactionActions, CancellationToken cancellationToken = default)
    {
        return SubmitTransactionAsync(transactionActions, Options.TransactionSafety, cancellationToken);
    }

    public virtual async Task SubmitTransactionAsync(IEnumerable<TableTransactionAction> transactionActions, TransactionSafety transactionSafety, CancellationToken cancellationToken = default)
    {
        TableClient client = await LazyClient;

        if (transactionSafety is TransactionSafety.Enabled)
        {
            foreach (IGrouping<string, TableTransactionAction>? partition in transactionActions.GroupBy(x => x.Entity.PartitionKey))
            {
                foreach (IEnumerable<TableTransactionAction> chunk in partition.Chunk(Options.TransactionChunkSize))
                {
                    await client.SubmitTransactionAsync(chunk, cancellationToken);
                }
            }
        }
        else if (transactionSafety is TransactionSafety.Disabled)
        {
            await client.SubmitTransactionAsync(transactionActions, cancellationToken);
        }
        else
        {
            throw new NotSupportedException();
        }
    }

    public Task UpdateEntityAsync(T entity, CancellationToken cancellationToken = default) => UpdateEntityAsync(entity, ETag.All, null, cancellationToken);

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

    public virtual async IAsyncEnumerable<T> QueryAsync(Expression<Func<T, bool>> filter, int? maxPerPage, IEnumerable<string>? select, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        TableClient client = await LazyClient;
        await foreach (T entity in client.QueryAsync(filter, maxPerPage ?? Options.PageSize, select, cancellationToken))
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

    public Task BulkUpdateAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default) => BulkUpdateAsync(entities, Options.BulkOperation, cancellationToken);

    public Task BulkUpdateAsync(IEnumerable<T> entities, BulkOperation bulkOperation, CancellationToken cancellationToken = default)
    {
        TableTransactionActionType tableTransactionActionType = bulkOperation switch
        {
            BulkOperation.Replace => TableTransactionActionType.UpdateReplace,
            BulkOperation.Merge => TableTransactionActionType.UpdateMerge,
            _ => throw new NotSupportedException(),
        };

        return ExecuteInBulkAsync(entities, tableTransactionActionType, cancellationToken);
    }

    public Task BulkUpsertAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default) => BulkUpsertAsync(entities, Options.BulkOperation, cancellationToken);

    public Task BulkUpsertAsync(IEnumerable<T> entities, BulkOperation bulkOperation, CancellationToken cancellationToken = default)
    {
        TableTransactionActionType tableTransactionActionType = bulkOperation switch
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

    public Task UpdateAsync(Expression<Func<T>> exp, CancellationToken cancellationToken = default)
    {
        TableEntity entity = VisitForMergeAndValidate(exp);
        return UpdateAsync(entity, cancellationToken);
    }

    public Task UpsertAsync(Expression<Func<T>> exp, CancellationToken cancellationToken = default)
    {
        TableEntity entity = VisitForMergeAndValidate(exp);
        return UpsertAsync(entity, cancellationToken);
    }

    private TableEntity VisitForMergeAndValidate(Expression<Func<T>> exp)
    {
        MergeVisitor visitor = new(PartitionKeyProxy, RowKeyProxy);
        _ = visitor.Visit(exp);

        TableEntity entity = visitor.Entity;

        if (entity.Count == 0 || visitor.IsComplex)
        {
            throw new NotSupportedException("Merge expression is not supported");
        }

        if (entity.PartitionKey is null)
        {
            throw new NotSupportedException("PartitionKey is a required field to be able to merge");
        }

        if (entity.RowKey is null)
        {
            throw new NotSupportedException("RowKey is a required field to be able to merge");
        }

        return entity;
    }

    internal async Task UpdateAsync(ITableEntity entity, CancellationToken cancellationToken)
    {
        TableClient client = await LazyClient;
        await client.UpdateEntityAsync(entity, ETag.All, TableUpdateMode.Merge, cancellationToken);
    }

    internal async Task UpsertAsync(ITableEntity entity, CancellationToken cancellationToken)
    {
        TableClient client = await LazyClient;
        await client.UpsertEntityAsync(entity, TableUpdateMode.Merge, cancellationToken);
    }
    #endregion Merge Operations
}