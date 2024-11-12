using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace TableStorage;

public sealed class TableSet<T> : IAsyncEnumerable<T>
    where T : class, ITableEntity, new()
{
    public string Name { get; }
    public string EntityType => typeof(T).Name;

    private readonly AsyncLazy<TableClient> _lazyClient;
    private readonly TableOptions _options;

    internal string? PartitionKeyProxy { get; }
    internal string? RowKeyProxy { get; }

    internal TableSet(TableStorageFactory factory, string tableName, TableOptions options, string? partitionKeyProxy, string? rowKeyProxy)
    {
        Name = tableName;
        _lazyClient = new(() => factory.GetClient(tableName));
        _options = options;
        PartitionKeyProxy = partitionKeyProxy;
        RowKeyProxy = rowKeyProxy;
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

    public Task SubmitTransactionAsync(IEnumerable<TableTransactionAction> transactionActions, CancellationToken cancellationToken = default)
    {
        return SubmitTransactionAsync(transactionActions, _options.TransactionSafety, cancellationToken);
    }

    public async Task SubmitTransactionAsync(IEnumerable<TableTransactionAction> transactionActions, TransactionSafety transactionSafety, CancellationToken cancellationToken = default)
    {
        var client = await _lazyClient;

        if (transactionSafety is TransactionSafety.Enabled)
        {
            foreach (var partition in transactionActions.GroupBy(x => x.Entity.PartitionKey))
            {
                foreach (var chunk in partition.Chunk(_options.TransactionChunkSize))
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
        return QueryAsync(cancellationToken).GetAsyncEnumerator(cancellationToken);
    }

#region Bulk Operations

    private Task ExecuteInBulkAsync(IEnumerable<T> entities, TableTransactionActionType tableTransactionActionType, CancellationToken cancellationToken)
    {
        return SubmitTransactionAsync(entities.Select(x => new TableTransactionAction(tableTransactionActionType, x)), TransactionSafety.Enabled, cancellationToken);
    }

    public Task BulkInsertAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
    {
        return ExecuteInBulkAsync(entities, TableTransactionActionType.Add, cancellationToken);
    }

    public Task BulkUpdateAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default) => BulkUpdateAsync(entities, _options.BulkOperation, cancellationToken);

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

    public Task BulkUpsertAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default) => BulkUpsertAsync(entities, _options.BulkOperation, cancellationToken);

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
    private sealed class MergeVisitor(string? partitionKeyProxy, string? rowKeyProxy) : ExpressionVisitor
    {
        private readonly string? _partitionKeyProxy = partitionKeyProxy;
        private readonly string? _rowKeyProxy = rowKeyProxy;

        public readonly TableEntity Entity = [];

        protected override Expression VisitMember(MemberExpression memberExpression)
        {
            var expression = Visit(memberExpression.Expression);

            if (expression is ConstantExpression constantExpression)
            {
                object container = constantExpression.Value;
                var member = memberExpression.Member;

                if (member is FieldInfo fieldInfo)
                {
                    object value = fieldInfo.GetValue(container);
                    return Expression.Constant(value);
                }
                
                if (member is PropertyInfo propertyInfo)
                {
                    object value = propertyInfo.GetValue(container, null);
                    return Expression.Constant(value);
                }
            }

            return base.VisitMember(memberExpression);
        }

        protected override MemberAssignment VisitMemberAssignment(MemberAssignment node)
        {
            node = base.VisitMemberAssignment(node);

            var name = node.Member.Name;

            if (name == _partitionKeyProxy)
            {
                name = nameof(ITableEntity.PartitionKey);
            }
            else if (name == _rowKeyProxy)
            {
                name = nameof(ITableEntity.RowKey);
            }

            Entity[name] = node.Expression switch
            {
                ConstantExpression constant => constant.Value,
                _ => throw new NotSupportedException("Merge expression is not supported")
            };

            return node;
        }
    }

    public Task UpdateAsync(Expression<Func<T>> exp, CancellationToken cancellationToken = default)
    {
        var entity = VisitForMerge(exp);

        if (entity.PartitionKey is null)
        {
            throw new NotSupportedException("PartitionKey is a required field to be able to merge");
        }

        if (entity.RowKey is null)
        {
            throw new NotSupportedException("RowKey is a required field to be able to merge");
        }

        return UpdateAsync(entity, cancellationToken);
    }

    internal TableEntity VisitForMerge(Expression<Func<T>> exp)
    {
        MergeVisitor visitor = new(PartitionKeyProxy, RowKeyProxy);
        _ = visitor.Visit(exp);

        if (visitor.Entity.Count == 0)
        {
            throw new NotSupportedException("Merge expression is not supported");
        }

        return visitor.Entity;
    }

    internal async Task UpdateAsync(TableEntity entity, CancellationToken cancellationToken)
    {
        var client = await _lazyClient;
        await client.UpdateEntityAsync(entity, ETag.All, TableUpdateMode.Merge, cancellationToken);
    }
    #endregion Merge Operations
}