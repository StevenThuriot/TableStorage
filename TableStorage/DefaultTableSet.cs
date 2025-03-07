namespace TableStorage;

internal sealed class DefaultTableSet<T> : TableSet<T>
    where T : class, ITableEntity, new()
{
    internal DefaultTableSet(TableStorageFactory factory, string tableName, TableOptions options)
        : base(factory, tableName, options)
    {
    }
    internal DefaultTableSet(TableStorageFactory factory, string tableName, TableOptions options, string? partitionKeyProxy, string? rowKeyProxy)
        : base(factory, tableName, options, partitionKeyProxy, rowKeyProxy)
    {
    }

    public override async Task AddEntityAsync(T entity, CancellationToken cancellationToken = default)
    {
        TableClient client = await LazyClient;
        await client.AddEntityAsync(entity, cancellationToken);
    }

    public override async Task UpdateEntityAsync(T entity, ETag ifMatch, TableUpdateMode? mode, CancellationToken cancellationToken = default)
    {
        TableClient client = await LazyClient;
        await client.UpdateEntityAsync(entity, ifMatch, mode ?? Options.TableMode, cancellationToken);
    }

    public override async Task UpsertEntityAsync(T entity, TableUpdateMode? mode, CancellationToken cancellationToken = default)
    {
        TableClient client = await LazyClient;
        await client.UpsertEntityAsync(entity, mode ?? Options.TableMode, cancellationToken);
    }

    protected override Task ExecuteInBulkAsync(IEnumerable<T> entities, TableTransactionActionType tableTransactionActionType, CancellationToken cancellationToken)
    {
        return SubmitTransactionAsync(entities.Select(x => new TableTransactionAction(tableTransactionActionType, x)), TransactionSafety.Enabled, cancellationToken);
    }
}
