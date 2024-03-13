using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using TableStorage.Linq;

namespace TableStorage;

public sealed class TableSet<T> : IAsyncEnumerable<T>
    where T : class, ITableEntity, new()
{
    private readonly AsyncLazy<TableClient> _lazyClient;
    private readonly TableOptions _options;

    internal TableSet(TableStorageFactory factory, string tableName, TableOptions options)
    {
        _lazyClient = new AsyncLazy<TableClient>(() => factory.GetClient(tableName));
        _options = options;
    }

    public async Task AddEntityAsync(T entity, CancellationToken cancellationToken = default)
    {
        var client = await _lazyClient;
        await client.AddEntityAsync(entity, cancellationToken);
    }

    public Task DeleteEntityAsync(string partitionKey, string rowKey, CancellationToken cancellationToken = default) => DeleteEntityAsync(partitionKey, rowKey, ETag.All, cancellationToken);

    public async Task DeleteEntityAsync(string partitionKey, string rowKey, ETag ifMatch, CancellationToken cancellationToken = default)
    {
        var client = await _lazyClient;
        await client.DeleteEntityAsync(partitionKey, rowKey, ifMatch, cancellationToken);
    }

    public async Task SubmitTransactionAsync(IEnumerable<TableTransactionAction> transactionActions, CancellationToken cancellationToken = default)
    {
        var client = await _lazyClient;
        await client.SubmitTransactionAsync(transactionActions, cancellationToken);
    }

    public Task UpdateEntityAsync(T entity, CancellationToken cancellationToken = default) => UpdateEntityAsync(entity, ETag.All, null, cancellationToken);

    public async Task UpdateEntityAsync(T entity, ETag ifMatch, TableUpdateMode? mode, CancellationToken cancellationToken = default)
    {
        var client = await _lazyClient;
        await client.UpdateEntityAsync(entity, ifMatch, mode ?? _options.TableMode, cancellationToken);
    }

    public Task UpsertEntityAsync(T entity, CancellationToken cancellationToken = default) => UpsertEntityAsync(entity, null, cancellationToken);

    public async Task UpsertEntityAsync(T entity, TableUpdateMode? mode, CancellationToken cancellationToken = default)
    {
        var client = await _lazyClient;
        await client.UpsertEntityAsync(entity, mode ?? _options.TableMode, cancellationToken);
    }

    public Task<T?> GetEntityAsync(string partitionKey, string rowKey, CancellationToken cancellationToken = default) => GetEntityAsync(partitionKey, rowKey, null, cancellationToken);

    public async Task<T?> GetEntityAsync(string partitionKey, string rowKey, IEnumerable<string>? select, CancellationToken cancellationToken = default)
    {
        var client = await _lazyClient;
        var result = await client.GetEntityAsync<T>(partitionKey, rowKey, select, cancellationToken);
        return result.Value;
    }

    public Task<T?> GetEntityOrDefaultAsync(string partitionKey, string rowKey, CancellationToken cancellationToken = default) => GetEntityOrDefaultAsync(partitionKey, rowKey, null, cancellationToken);

    public async Task<T?> GetEntityOrDefaultAsync(string partitionKey, string rowKey, IEnumerable<string>? select, CancellationToken cancellationToken = default)
    {
        try
        {
            return await GetEntityAsync(partitionKey, rowKey, select, cancellationToken);
        }
        catch (RequestFailedException e) when (e.Status == 404)
        {
            return null;
        }
    }

    public IAsyncEnumerable<T> QueryAsync(CancellationToken cancellationToken = default) => QueryAsync((string?)null, null, null, cancellationToken);

    public IAsyncEnumerable<T> QueryAsync(string? filter, CancellationToken cancellationToken = default) => QueryAsync(filter, null, null, cancellationToken);

    public async IAsyncEnumerable<T> QueryAsync(string? filter, int? maxPerPage, IEnumerable<string>? select, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var client = await _lazyClient;
        await foreach (var item in client.QueryAsync<T>(filter, maxPerPage ?? _options.PageSize, select, cancellationToken))
        {
            yield return item;
        }
    }

    public IAsyncEnumerable<T> QueryAsync(Expression<Func<T, bool>> filter, CancellationToken cancellationToken = default) => QueryAsync(filter, null, null, cancellationToken);

    public async IAsyncEnumerable<T> QueryAsync(Expression<Func<T, bool>> filter, int? maxPerPage, IEnumerable<string>? select, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var client = await _lazyClient;
        await foreach (var item in client.QueryAsync(filter, maxPerPage ?? _options.PageSize, select, cancellationToken))
        {
            yield return item;
        }
    }

    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        return new TableSetQueryHelper<T>(this).GetAsyncEnumerator(cancellationToken);
    }

    #region Bulk Operations

    private async Task ExecuteInBulk(IEnumerable<T> entities, TableTransactionActionType tableTransactionActionType, CancellationToken cancellationToken)
    {
        foreach (var group in entities.GroupBy(x => x.PartitionKey))
        {
            await SubmitTransactionAsync(group.Select(x => new TableTransactionAction(tableTransactionActionType, x)), cancellationToken);
        }
    }

    public Task BulkInsert(IEnumerable<T> entities, CancellationToken cancellationToken = default)
    {
        return ExecuteInBulk(entities, TableTransactionActionType.Add, cancellationToken);
    }

    public Task BulkUpdate(IEnumerable<T> entities, BulkOperation bulkOperation, CancellationToken cancellationToken = default)
    {
        TableTransactionActionType tableTransactionActionType = bulkOperation switch
        {
            BulkOperation.Replace => TableTransactionActionType.UpdateReplace,
            BulkOperation.Merge => TableTransactionActionType.UpdateMerge,
            _ => throw new NotSupportedException(),
        };

        return ExecuteInBulk(entities, tableTransactionActionType, cancellationToken);
    }

    public Task BulkUpsert(IEnumerable<T> entities, CancellationToken cancellationToken = default) => BulkUpsert(entities, BulkOperation.Replace, cancellationToken);

    public Task BulkUpsert(IEnumerable<T> entities, BulkOperation bulkOperation, CancellationToken cancellationToken = default)
    {
        TableTransactionActionType tableTransactionActionType = bulkOperation switch
        {
            BulkOperation.Replace => TableTransactionActionType.UpsertReplace,
            BulkOperation.Merge => TableTransactionActionType.UpsertMerge,
            _ => throw new NotSupportedException(),
        };

        return ExecuteInBulk(entities, tableTransactionActionType, cancellationToken);
    }

    public Task BulkDelete(IEnumerable<T> entities, CancellationToken cancellationToken = default)
    {
        return ExecuteInBulk(entities, TableTransactionActionType.Delete, cancellationToken);
    }

    #endregion Bulk Operations
}

public enum BulkOperation
{
    Replace,
    Merge
}