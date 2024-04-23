using System;
using System.Linq.Expressions;

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

    public async Task<T> FirstAsync(CancellationToken token)
    {
        T? result = await FirstOrDefaultAsync(token);
        return result ?? throw new InvalidOperationException("No element satisfies the condition in predicate. -or- The source sequence is empty.");
    }

    public async Task<T?> FirstOrDefaultAsync(CancellationToken token)
    {
        _amount = 1;
        await foreach (var item in this.WithCancellation(token))
        {
            return item;
        }

        return default;
    }

    public async Task<T> SingleAsync(CancellationToken token)
    {
        var result = await SingleOrDefaultAsync(token);
        return result ?? throw new InvalidOperationException("No element satisfies the condition in predicate. -or- The source sequence is empty.");
    }

    public async Task<T?> SingleOrDefaultAsync(CancellationToken token)
    {
        T? result = default;
        bool gotOne = false;

        _amount = 2;

        await foreach (var item in this.WithCancellation(token))
        {
            if (gotOne)
            {
                throw new InvalidOperationException("The input sequence contains more than one element.");
            }

            result = item;
            gotOne = true;
        }

        return result;
    }

    public async Task<int> BatchDeleteAsync(CancellationToken token)
    {
        _fields = [nameof(ITableEntity.PartitionKey), nameof(ITableEntity.RowKey)];

        await using var enumerator = GetAsyncEnumerator(token);

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

        await using var enumerator = GetAsyncEnumerator(token);

        while (await enumerator.MoveNextAsync())
        {
            T current = enumerator.Current;
            entities.Add(new(TableTransactionActionType.Delete, current, current.ETag));
        }

        await _table.SubmitTransactionAsync(entities, TransactionSafety.Enabled, token);
        return entities.Count;
    }

    public async Task<int> BatchUpdateAsync(Action<T> update, CancellationToken token)
    {
        if (update is null)
        {
            throw new ArgumentNullException(nameof(update), "update action should not be null");
        }

        if (_fields is not null)
        {
            throw new NotSupportedException("Using a select in a batch update will result in data loss");
        }

        await using var enumerator = GetAsyncEnumerator(token);

        int result = 0;

        while (await enumerator.MoveNextAsync())
        {
            var current = enumerator.Current;
            update(current);

            await _table.UpdateEntityAsync(current, current.ETag, TableUpdateMode.Replace, token);
            result++;
        }

        return result;
    }

    public async Task<int> BatchUpdateTransactionAsync(Action<T> update, CancellationToken token)
    {
        if (update is null)
        {
            throw new ArgumentNullException(nameof(update), "update action should not be null");
        }

        if (_fields is not null)
        {
            throw new NotSupportedException("Using a select in a batch update will result in data loss");
        }

        List<TableTransactionAction> entities = [];

        await using var enumerator = GetAsyncEnumerator(token);

        while (await enumerator.MoveNextAsync())
        {
            T current = enumerator.Current;
            update(current);
            entities.Add(new(TableTransactionActionType.UpdateReplace, current, current.ETag));
        }

        await _table.SubmitTransactionAsync(entities, TransactionSafety.Enabled, token);
        return entities.Count;
    }

    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken)
    {
        var query = _filter is null
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
            await foreach (var item in values)
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
    private sealed class SelectionVisitor(string? partitionKeyProxy, string? rowKeyProxy) : ExpressionVisitor
    {
        private readonly string? _partitionKeyProxy = partitionKeyProxy;
        private readonly string? _rowKeyProxy = rowKeyProxy;

        public readonly HashSet<string> Members = [];

        protected override Expression VisitMember(MemberExpression node)
        {
            var name = node.Member.Name;

            if (name == _partitionKeyProxy)
            {
                name = nameof(ITableEntity.PartitionKey);
                node = Expression.Property(Expression.Convert(node.Expression, typeof(ITableEntity)), nameof(ITableEntity.PartitionKey));
            }
            else if (name == _rowKeyProxy)
            {
                name = nameof(ITableEntity.RowKey);
                node = Expression.Property(Expression.Convert(node.Expression, typeof(ITableEntity)), nameof(ITableEntity.RowKey));
            }

            Members.Add(name);
            return base.VisitMember(node);
        }
    }

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
        var helper = SetFields(ref exp, throwIfNoArgumentsFound: false);
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
    private sealed class WhereVisitor(string? partitionKeyProxy, string? rowKeyProxy) : ExpressionVisitor
    {
        private readonly string? _partitionKeyProxy = partitionKeyProxy;
        private readonly string? _rowKeyProxy = rowKeyProxy;

        protected override Expression VisitMember(MemberExpression node)
        {
            if (node.Expression.NodeType == ExpressionType.Parameter && node.Expression.Type == typeof(T))
            {
                var name = node.Member.Name;

                if (name == _partitionKeyProxy)
                {
                    node = Expression.Property(Expression.Convert(node.Expression, typeof(ITableEntity)), nameof(ITableEntity.PartitionKey));
                }
                else if (name == _rowKeyProxy)
                {
                    node = Expression.Property(Expression.Convert(node.Expression, typeof(ITableEntity)), nameof(ITableEntity.RowKey));
                }
            }

            return base.VisitMember(node);
        }
    }

    internal TableSetQueryHelper<T> AddFilter(Expression<Func<T, bool>> predicate)
    {
        if (_table.PartitionKeyProxy is not null || _table.RowKeyProxy is not null)
        {
            WhereVisitor visitor = new(_table.PartitionKeyProxy, _table.RowKeyProxy);
            predicate = (Expression<Func<T, bool>>)visitor.Visit(predicate);
        }

        if (_filter is null)
        {
            _filter = predicate;
        }
        else
        {
            var invokedExpr = Expression.Invoke(predicate, _filter.Parameters);
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
        if (elements is null)
        {
            throw new ArgumentNullException(nameof(elements));
        }

        Expression filter = BuildFilterExpression();

        var lambda = Expression.Lambda<Func<T, bool>>(filter, predicate.Parameters);
        return AddFilter(lambda);

        Expression BuildFilterExpression()
        {
            using var enumerator = elements.GetEnumerator();

            if (!enumerator.MoveNext())
            {
                return Expression.Constant(false);
            }

            var filter = GetFilterForElement();
            while (enumerator.MoveNext())
            {
                filter = Expression.OrElse(filter, GetFilterForElement());
            }

            return filter;

            BinaryExpression GetFilterForElement()
            {
                return Expression.Equal(predicate.Body, Expression.Constant(enumerator.Current));
            }
        }
    }

    ISelectedTableQueryable<T> ISelectedTableQueryable<T>.ExistsIn<TElement>(Expression<Func<T, TElement>> predicate, IEnumerable<TElement> elements) => AddExistsInFilter(predicate, elements);

    ITakenTableQueryable<T> ITakenTableQueryable<T>.ExistsIn<TElement>(Expression<Func<T, TElement>> predicate, IEnumerable<TElement> elements) => AddExistsInFilter(predicate, elements);

    IFilteredTableQueryable<T> IFilteredTableQueryable<T>.ExistsIn<TElement>(Expression<Func<T, TElement>> predicate, IEnumerable<TElement> elements) => AddExistsInFilter(predicate, elements);

    ISelectedTakenTableQueryable<T> ISelectedTakenTableQueryable<T>.ExistsIn<TElement>(Expression<Func<T, TElement>> predicate, IEnumerable<TElement> elements) => AddExistsInFilter(predicate, elements);
    #endregion ExistsIn

    #region NotExistsIn
    internal TableSetQueryHelper<T> AddNotExistsInFilter<TElement>(Expression<Func<T, TElement>> predicate, IEnumerable<TElement> elements)
    {
        if (elements is null)
        {
            throw new ArgumentNullException(nameof(elements));
        }

        Expression filter = BuildFilterExpression();

        var lambda = Expression.Lambda<Func<T, bool>>(filter, predicate.Parameters);
        return AddFilter(lambda);

        Expression BuildFilterExpression()
        {
            using var enumerator = elements.GetEnumerator();

            if (!enumerator.MoveNext())
            {
                return Expression.Constant(true);
            }

            var filter = GetFilterForElement();
            while (enumerator.MoveNext())
            {
                filter = Expression.AndAlso(filter, GetFilterForElement());
            }

            return filter;

            BinaryExpression GetFilterForElement()
            {
                return Expression.NotEqual(predicate.Body, Expression.Constant(enumerator.Current));
            }
        }
    }

    ISelectedTableQueryable<T> ISelectedTableQueryable<T>.NotExistsIn<TElement>(Expression<Func<T, TElement>> predicate, IEnumerable<TElement> elements) => AddNotExistsInFilter(predicate, elements);

    ITakenTableQueryable<T> ITakenTableQueryable<T>.NotExistsIn<TElement>(Expression<Func<T, TElement>> predicate, IEnumerable<TElement> elements) => AddNotExistsInFilter(predicate, elements);

    IFilteredTableQueryable<T> IFilteredTableQueryable<T>.NotExistsIn<TElement>(Expression<Func<T, TElement>> predicate, IEnumerable<TElement> elements) => AddNotExistsInFilter(predicate, elements);

    ISelectedTakenTableQueryable<T> ISelectedTakenTableQueryable<T>.NotExistsIn<TElement>(Expression<Func<T, TElement>> predicate, IEnumerable<TElement> elements) => AddNotExistsInFilter(predicate, elements);
    #endregion NotExistsIn
}