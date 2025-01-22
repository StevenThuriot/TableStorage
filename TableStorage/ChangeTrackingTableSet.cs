using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace TableStorage;

internal sealed class ChangeTrackingTableSet<T> : TableSet<T>
    where T : class, ITableEntity, IChangeTracking, new()
{
    private ITableEntity GetEntity(T entity)
    {
        if (Options.ChangesOnly)
        {
            return entity.GetEntity();
        }

        return entity;
    }

    internal ChangeTrackingTableSet(TableStorageFactory factory, string tableName, TableOptions options)
        : base(factory, tableName, options)
    {
    }

    internal ChangeTrackingTableSet(TableStorageFactory factory, string tableName, TableOptions options, string? partitionKeyProxy, string? rowKeyProxy)
        : base(factory, tableName, options, partitionKeyProxy, rowKeyProxy)
    {
    }

    public override async Task AddEntityAsync(T entity, CancellationToken cancellationToken = default)
    {
        var client = await LazyClient;
        await client.AddEntityAsync(GetEntity(entity), cancellationToken);
    }

    public override async Task SubmitTransactionAsync(IEnumerable<TableTransactionAction> transactionActions, TransactionSafety transactionSafety, CancellationToken cancellationToken = default)
    {
        await base.SubmitTransactionAsync(transactionActions, transactionSafety, cancellationToken);
        foreach (var entity in transactionActions.Select(x => x.Entity).OfType<IChangeTracking>())
        {
            entity.AcceptChanges();
        }
    }

    public async override Task UpdateEntityAsync(T entity, ETag ifMatch, TableUpdateMode? mode, CancellationToken cancellationToken = default)
    {
        var client = await LazyClient;
        await client.UpdateEntityAsync(GetEntity(entity), ifMatch, mode ?? Options.TableMode, cancellationToken);
    }

    public async override Task UpsertEntityAsync(T entity, TableUpdateMode? mode, CancellationToken cancellationToken = default)
    {
        var client = await LazyClient;
        await client.UpsertEntityAsync(GetEntity(entity), mode ?? Options.TableMode, cancellationToken);
    }

    public override async Task<T?> GetEntityAsync(string partitionKey, string rowKey, IEnumerable<string>? select, CancellationToken cancellationToken = default)
    {
        var entity = await base.GetEntityAsync(partitionKey, rowKey, select, cancellationToken);
        entity?.AcceptChanges();
        return entity;
    }

    public async override Task<(bool success, T? entity)> TryGetEntityAsync(string partitionKey, string rowKey, IEnumerable<string>? select, CancellationToken cancellationToken = default)
    {
        var result = await base.TryGetEntityAsync(partitionKey, rowKey, select, cancellationToken);
        
        if (result.success)
        {
            result.entity!.AcceptChanges();
        }

        return result;
    }

    public override IAsyncEnumerable<T> QueryAsync(string? filter, int? maxPerPage, IEnumerable<string>? select, CancellationToken cancellationToken = default)
    {
        return base.QueryAsync(filter, maxPerPage, select, cancellationToken);
    }

    public async override IAsyncEnumerable<T> QueryAsync(Expression<Func<T, bool>> filter, int? maxPerPage, IEnumerable<string>? select, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var entity in base.QueryAsync(filter, maxPerPage, select, cancellationToken))
        {
            entity.AcceptChanges();
            yield return entity;
        }
    }

    protected override Task ExecuteInBulkAsync(IEnumerable<T> entities, TableTransactionActionType tableTransactionActionType, CancellationToken cancellationToken)
    {
        return SubmitTransactionAsync(entities.Select(GetEntity).Select(x => new TableTransactionAction(tableTransactionActionType, x)), TransactionSafety.Enabled, cancellationToken);
    }
}
