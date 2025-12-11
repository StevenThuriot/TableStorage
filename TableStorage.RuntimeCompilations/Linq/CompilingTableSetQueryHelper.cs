using System.Linq.Expressions;
using TableStorage.Visitors;

namespace TableStorage.Linq;

internal sealed class CompilingTableSetQueryHelper<T>
    where T : class, ITableEntity, new()
{
    private readonly TableSetQueryHelper<T> _helper;

    public CompilingTableSetQueryHelper(TableSet<T> table)
    {
        _helper = TableSetQueryHelper.From(table);
    }

    public CompilingTableSetQueryHelper(TableSetQueryHelper<T> table)
    {
        _helper = table;
    }

    public TransformedTableSetQueryHelper<T, TResult> SetFieldsAndTransform<TResult>(Expression<Func<T, TResult>> exp)
    {
        TableSetQueryHelper<T> helper = _helper.SetFields(ref exp, throwIfNoArgumentsFound: false);
        return new TransformedTableSetQueryHelper<T, TResult>(helper, exp);
    }

    public async Task<int> BatchUpdateAsync(Expression<Func<T, T>> update, CancellationToken token = default)
    {
        (MergeVisitor visitor, LazyExpression<T> compiledUpdate) = PrepareExpression(update);

        int result = 0;

        await using IAsyncEnumerator<T> enumerator = _helper.GetAsyncEnumerator(token);

        while (await enumerator.MoveNextAsync())
        {
            T current = enumerator.Current;
            ITableEntity entity = PrepareEntity(visitor, compiledUpdate, current);
            await _helper.Table.UpdateAsync(entity, token);

            result++;
        }

        return result;
    }

    public async Task<int> BatchUpdateTransactionAsync(Expression<Func<T, T>> update, CancellationToken token)
    {
        (MergeVisitor visitor, LazyExpression<T> compiledUpdate) = PrepareExpression(update);

        List<TableTransactionAction> entities = [];

        await using IAsyncEnumerator<T> enumerator = _helper.GetAsyncEnumerator(token);

        while (await enumerator.MoveNextAsync())
        {
            T current = enumerator.Current;
            ITableEntity entity = PrepareEntity(visitor, compiledUpdate, current);
            entities.Add(new(TableTransactionActionType.UpdateMerge, entity, current.ETag));
        }

        await _helper.Table.SubmitTransactionAsync(entities, TransactionSafety.Enabled, token);
        return entities.Count;
    }

    private static ITableEntity PrepareEntity(MergeVisitor visitor, LazyExpression<T> compiledUpdate, T current)
    {
        TableEntity entity = new(visitor.Entity)
        {
            PartitionKey = current.PartitionKey,
            RowKey = current.RowKey,
            ETag = current.ETag
        };

        if (visitor.IsComplex)
        {
            current = compiledUpdate.Invoke(current);

            if (current is not IDictionary<string, object> currentEntity)
            {
                //throw new NotSupportedException("Complex entity must have an indexer");
                return current;
            }

            foreach (string member in visitor.ComplexMembers)
            {
                entity[member] = currentEntity[member];
            }
        }

        return entity;
    }

    private (MergeVisitor, LazyExpression<T>) PrepareExpression(Expression<Func<T, T>> update)
    {
        if (update is null)
        {
            throw new ArgumentNullException(nameof(update), "update action should not be null");
        }

        MergeVisitor visitor = new(_helper.Table.PartitionKeyProxy, _helper.Table.RowKeyProxy);
        update = (Expression<Func<T, T>>)visitor.Visit(update);

        if (!visitor.HasMerges)
        {
            throw new NotSupportedException("Expression is not supported");
        }

        if (visitor.Entity.PartitionKey is not null)
        {
            throw new NotSupportedException("PartitionKey is a readonly field");
        }

        if (visitor.Entity.RowKey is not null)
        {
            throw new NotSupportedException("RowKey is a readonly field");
        }

        if (!visitor.IsComplex)
        {
            _helper.SetFields([nameof(ITableEntity.PartitionKey), nameof(ITableEntity.RowKey)]);
        }
        else if (_helper.HasFields())
        {
            throw new NotSupportedException("Data loss might occur when doing a select before an update");
        }

        return (visitor, update);
    }
}