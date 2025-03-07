using System.Linq.Expressions;
using TableStorage.Visitors;

namespace TableStorage.Linq;

internal sealed class TableSetQueryHelper<T>(TableSet<T> table) :
    IAsyncEnumerable<T>,
    ISelectedTableQueryable<T>,
    ITakenTableQueryable<T>,
    IFilteredTableQueryable<T>,
    ISelectedTakenTableQueryable<T>
    where T : class, ITableEntity, new()
{
    private readonly TableSet<T> _table = table;

    private HashSet<string>? _fields;
    private Expression<Func<T, bool>>? _filter;
    private int? _amount;

    public Task<T> FirstAsync(CancellationToken token)
    {
        _amount = 1;
        return Helpers.FirstAsync(this, token);
    }

    public Task<T?> FirstOrDefaultAsync(CancellationToken token)
    {
        _amount = 1;
        return Helpers.FirstOrDefaultAsync(this, token);
    }

    public Task<T> SingleAsync(CancellationToken token = default)
    {
        return Helpers.SingleAsync(this, token);
    }

    public Task<T?> SingleOrDefaultAsync(CancellationToken token = default)
    {
        return Helpers.SingleOrDefaultAsync(this, token);
    }

    public async Task<int> BatchDeleteAsync(CancellationToken token)
    {
        _fields = [nameof(ITableEntity.PartitionKey), nameof(ITableEntity.RowKey)];

        await using IAsyncEnumerator<T> enumerator = GetAsyncEnumerator(token);

        int result = 0;

        while (await enumerator.MoveNextAsync())
        {
            T current = enumerator.Current;
            await _table.DeleteEntityAsync(current.PartitionKey, current.RowKey, current.ETag, token);
            result++;
        }

        return result;
    }

    public async Task<int> BatchDeleteTransactionAsync(CancellationToken token)
    {
        _fields = [nameof(ITableEntity.PartitionKey), nameof(ITableEntity.RowKey)];

        List<TableTransactionAction> entities = [];

        await using IAsyncEnumerator<T> enumerator = GetAsyncEnumerator(token);

        while (await enumerator.MoveNextAsync())
        {
            T current = enumerator.Current;
            entities.Add(new(TableTransactionActionType.Delete, current, current.ETag));
        }

        await _table.SubmitTransactionAsync(entities, TransactionSafety.Enabled, token);
        return entities.Count;
    }

    public async Task<int> BatchUpdateAsync(Expression<Func<T, T>> update, CancellationToken token = default)
    {
        (MergeVisitor visitor, LazyExpression<T> compiledUpdate) = PrepareExpression(update);

        int result = 0;

        await using IAsyncEnumerator<T> enumerator = GetAsyncEnumerator(token);

        while (await enumerator.MoveNextAsync())
        {
            T current = enumerator.Current;
            ITableEntity entity = PrepareEntity(visitor, compiledUpdate, current);
            await _table.UpdateAsync(entity, token);

            result++;
        }

        return result;
    }

    public async Task<int> BatchUpdateTransactionAsync(Expression<Func<T, T>> update, CancellationToken token)
    {
        (MergeVisitor visitor, LazyExpression<T> compiledUpdate) = PrepareExpression(update);

        List<TableTransactionAction> entities = [];

        await using IAsyncEnumerator<T> enumerator = GetAsyncEnumerator(token);

        while (await enumerator.MoveNextAsync())
        {
            T current = enumerator.Current;
            ITableEntity entity = PrepareEntity(visitor, compiledUpdate, current);
            entities.Add(new(TableTransactionActionType.UpdateMerge, entity, current.ETag));
        }

        await _table.SubmitTransactionAsync(entities, TransactionSafety.Enabled, token);
        return entities.Count;
    }

    private static ITableEntity PrepareEntity(MergeVisitor visitor, LazyExpression<T> compiledUpdate, T current)
    {
        TableEntity entity = new(visitor.Entity)
        {
            PartitionKey = current.PartitionKey,
            RowKey = current.RowKey
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

        MergeVisitor visitor = new(_table.PartitionKeyProxy, _table.RowKeyProxy);
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
            _fields = [nameof(ITableEntity.PartitionKey), nameof(ITableEntity.RowKey)];
        }
        else if (_fields is not null)
        {
            throw new NotSupportedException("Data loss might occur when doing a select before an update");
        }

        return (visitor, update);
    }

    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken)
    {
        IAsyncEnumerable<T> query = _filter is null
                    ? _table.QueryAsync((string?)null, _amount, _fields, cancellationToken)
                    : _table.QueryAsync(_filter, _amount, _fields, cancellationToken);

        if (_amount.HasValue)
        {
            query = IterateWithAmount(query);
        }

        return query.GetAsyncEnumerator(cancellationToken);

        async IAsyncEnumerable<T> IterateWithAmount(IAsyncEnumerable<T> values)
        {
            int count = _amount.GetValueOrDefault();
            await foreach (T item in values)
            {
                yield return item;

                if (--count == 0)
                {
                    yield break;
                }
            }
        }
    }

    #region Select
    internal TableSetQueryHelper<T> SetFields<TResult>(ref Expression<Func<T, TResult>> exp, bool throwIfNoArgumentsFound = true)
    {
        if (_fields is not null)
        {
            throw new NotSupportedException("Only one transformation is allowed at a time");
        }

        SelectionVisitor visitor = new(_table.PartitionKeyProxy, _table.RowKeyProxy);
        exp = (Expression<Func<T, TResult>>)visitor.Visit(exp);

        if (visitor.Members.Count == 0)
        {
            if (throwIfNoArgumentsFound)
            {
                throw new NotSupportedException("Select expression is not supported");
            }
        }
        else
        {
            _fields = visitor.Members;
        }

        return this;
    }

    internal TransformedTableSetQueryHelper<T, TResult> SetFieldsAndTransform<TResult>(Expression<Func<T, TResult>> exp)
    {
        TableSetQueryHelper<T> helper = SetFields(ref exp, throwIfNoArgumentsFound: false);
        return new TransformedTableSetQueryHelper<T, TResult>(helper, exp);
    }

    ISelectedTakenTableQueryable<T> ITakenTableQueryable<T>.SelectFields<TResult>(Expression<Func<T, TResult>> selector) => SetFields(ref selector);

    ISelectedTableQueryable<T> IFilteredTableQueryable<T>.SelectFields<TResult>(Expression<Func<T, TResult>> selector) => SetFields(ref selector);

    ITableEnumerable<TResult> ITakenTableQueryable<T>.Select<TResult>(Expression<Func<T, TResult>> selector) => SetFieldsAndTransform(selector);

    ITableEnumerable<TResult> IFilteredTableQueryable<T>.Select<TResult>(Expression<Func<T, TResult>> selector) => SetFieldsAndTransform(selector);
    #endregion Select

    #region Take
    internal TableSetQueryHelper<T> SetAmount(int amount)
    {
        if (amount < 1)
        {
            throw new InvalidOperationException("Amount must be a strictly postive integer.");
        }

        _amount = amount;
        return this;
    }

    ISelectedTakenTableQueryable<T> ISelectedTableQueryable<T>.Take(int amount) => SetAmount(amount);

    ITakenTableQueryable<T> IFilteredTableQueryable<T>.Take(int amount) => SetAmount(amount);
    #endregion Take

    #region Where
    internal TableSetQueryHelper<T> AddFilter(Expression<Func<T, bool>> predicate)
    {
        if (_table.PartitionKeyProxy is not null || _table.RowKeyProxy is not null)
        {
            WhereVisitor visitor = new(_table.PartitionKeyProxy, _table.RowKeyProxy, _table.Type);
            predicate = (Expression<Func<T, bool>>)visitor.Visit(predicate);
        }

        if (_filter is null)
        {
            _filter = predicate;
        }
        else
        {
            InvocationExpression invokedExpr = Expression.Invoke(predicate, _filter.Parameters);
            BinaryExpression combinedExpression = Expression.AndAlso(_filter.Body, invokedExpr);
            _filter = Expression.Lambda<Func<T, bool>>(combinedExpression, _filter.Parameters);
        }

        return this;
    }

    ISelectedTableQueryable<T> ISelectedTableQueryable<T>.Where(Expression<Func<T, bool>> predicate) => AddFilter(predicate);

    ITakenTableQueryable<T> ITakenTableQueryable<T>.Where(Expression<Func<T, bool>> predicate) => AddFilter(predicate);

    IFilteredTableQueryable<T> IFilteredTableQueryable<T>.Where(Expression<Func<T, bool>> predicate) => AddFilter(predicate);

    ISelectedTakenTableQueryable<T> ISelectedTakenTableQueryable<T>.Where(Expression<Func<T, bool>> predicate) => AddFilter(predicate);
    #endregion Where

    #region ExistsIn
    internal TableSetQueryHelper<T> AddExistsInFilter<TElement>(Expression<Func<T, TElement>> predicate, IEnumerable<TElement> elements)
    {
        Expression<Func<T, bool>> lambda = predicate.CreateExistsInFilter(elements);
        return AddFilter(lambda);
    }

    ISelectedTableQueryable<T> ISelectedTableQueryable<T>.ExistsIn<TElement>(Expression<Func<T, TElement>> predicate, IEnumerable<TElement> elements) => AddExistsInFilter(predicate, elements);

    ITakenTableQueryable<T> ITakenTableQueryable<T>.ExistsIn<TElement>(Expression<Func<T, TElement>> predicate, IEnumerable<TElement> elements) => AddExistsInFilter(predicate, elements);

    IFilteredTableQueryable<T> IFilteredTableQueryable<T>.ExistsIn<TElement>(Expression<Func<T, TElement>> predicate, IEnumerable<TElement> elements) => AddExistsInFilter(predicate, elements);

    ISelectedTakenTableQueryable<T> ISelectedTakenTableQueryable<T>.ExistsIn<TElement>(Expression<Func<T, TElement>> predicate, IEnumerable<TElement> elements) => AddExistsInFilter(predicate, elements);
    #endregion ExistsIn

    #region NotExistsIn
    internal TableSetQueryHelper<T> AddNotExistsInFilter<TElement>(Expression<Func<T, TElement>> predicate, IEnumerable<TElement> elements)
    {
        Expression<Func<T, bool>> lambda = predicate.CreateNotExistsInFilter(elements);
        return AddFilter(lambda);
    }

    ISelectedTableQueryable<T> ISelectedTableQueryable<T>.NotExistsIn<TElement>(Expression<Func<T, TElement>> predicate, IEnumerable<TElement> elements) => AddNotExistsInFilter(predicate, elements);

    ITakenTableQueryable<T> ITakenTableQueryable<T>.NotExistsIn<TElement>(Expression<Func<T, TElement>> predicate, IEnumerable<TElement> elements) => AddNotExistsInFilter(predicate, elements);

    IFilteredTableQueryable<T> IFilteredTableQueryable<T>.NotExistsIn<TElement>(Expression<Func<T, TElement>> predicate, IEnumerable<TElement> elements) => AddNotExistsInFilter(predicate, elements);

    ISelectedTakenTableQueryable<T> ISelectedTakenTableQueryable<T>.NotExistsIn<TElement>(Expression<Func<T, TElement>> predicate, IEnumerable<TElement> elements) => AddNotExistsInFilter(predicate, elements);
    #endregion NotExistsIn
}