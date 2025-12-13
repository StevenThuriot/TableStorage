using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using TableStorage.Fluent;
using TableStorage.Visitors;

namespace TableStorage.Linq;

public static class FluentPartitionTableQueryHelper
{
    extension<T1, T2>(TableSet<FluentPartitionTableEntity<T1, T2>> table)
        where T1 : class, IDictionary<string, object>, ITableEntity, new()
        where T2 : class, IDictionary<string, object>, ITableEntity, new()
    {
        internal FluentPartitionTableSetQueryHelper<T, T1, T2> WhereTypeIs<T>() where T : class, IDictionary<string, object>, ITableEntity, new()
        {
            return new(TableSetQueryHelper.From(table));
        }

        public Task<FluentPartitionTableEntity<T1, T2>?> FindFirstAsync(string rowKey, CancellationToken token = default)
        {
            Expression<Func<FluentPartitionTableEntity<T1, T2>, bool>> predicate = Helpers.CreateFindPredicate<FluentPartitionTableEntity<T1, T2>>(typeof(T1).Name, rowKey, table.PartitionKeyProxy, table.RowKeyProxy);
            return table.Where(predicate).FirstOrDefaultAsync(token);
        }

        public IFilteredTableQueryable<FluentPartitionTableEntity<T1, T2>> FindFirstAsync(params IReadOnlyList<string> rowKeys)
        {
            var keys = rowKeys.Select(rk => (partitionKey: typeof(T1).Name, rowKey: rk)).ToList();
            Expression<Func<FluentPartitionTableEntity<T1, T2>, bool>> predicate = Helpers.CreateFindPredicate<FluentPartitionTableEntity<T1, T2>>(keys, table.PartitionKeyProxy, table.RowKeyProxy);
            return table.Where(predicate);
        }

        public Task<FluentPartitionTableEntity<T1, T2>?> FindSecondAsync(string rowKey, CancellationToken token = default)
        {
            Expression<Func<FluentPartitionTableEntity<T1, T2>, bool>> predicate = Helpers.CreateFindPredicate<FluentPartitionTableEntity<T1, T2>>(typeof(T1).Name, rowKey, table.PartitionKeyProxy, table.RowKeyProxy);
            return table.Where(predicate).FirstOrDefaultAsync(token);
        }

        public IFilteredTableQueryable<FluentPartitionTableEntity<T1, T2>> FindSecondAsync(params IReadOnlyList<string> rowKeys)
        {
            var keys = rowKeys.Select(rk => (partitionKey: typeof(T2).Name, rowKey: rk)).ToList();
            Expression<Func<FluentPartitionTableEntity<T1, T2>, bool>> predicate = Helpers.CreateFindPredicate<FluentPartitionTableEntity<T1, T2>>(keys, table.PartitionKeyProxy, table.RowKeyProxy);
            return table.Where(predicate);
        }

        public IFilteredTableQueryable<T1> WhereFirstType() => table.WhereTypeIs<T1, T2, T1>();
        public IFilteredTableQueryable<T2> WhereSecondType() => table.WhereTypeIs<T1, T2, T2>();
        public IFilteredTableQueryable<T1> Where(Expression<Func<T1, bool>> predicate) => table.WhereFirstType().Where(predicate);
        public IFilteredTableQueryable<T2> Where(Expression<Func<T2, bool>> predicate) => table.WhereSecondType().Where(predicate);
        public ISelectedTableQueryable<T1> SelectFields<TResult>(Expression<Func<T1, TResult>> selector) => table.WhereFirstType().SelectFields(selector);
        public ISelectedTableQueryable<T2> SelectFields<TResult>(Expression<Func<T2, TResult>> selector) => table.WhereSecondType().SelectFields(selector);
        public IFilteredTableQueryable<T1> ExistsIn<TElement>(Expression<Func<T1, TElement>> predicate, IEnumerable<TElement> elements) => table.WhereFirstType().ExistsIn(predicate, elements);
        public IFilteredTableQueryable<T2> ExistsIn<TElement>(Expression<Func<T2, TElement>> predicate, IEnumerable<TElement> elements) => table.WhereSecondType().ExistsIn(predicate, elements);
        public IFilteredTableQueryable<T1> NotExistsIn<TElement>(Expression<Func<T1, TElement>> predicate, IEnumerable<TElement> elements) => table.WhereFirstType().NotExistsIn(predicate, elements);
        public IFilteredTableQueryable<T2> NotExistsIn<TElement>(Expression<Func<T2, TElement>> predicate, IEnumerable<TElement> elements) => table.WhereSecondType().NotExistsIn(predicate, elements);
    }

    internal sealed class FluentPartitionTableSetQueryHelper<T, T1, T2>(TableSetQueryHelper<FluentPartitionTableEntity<T1, T2>> helper) :
        IAsyncEnumerable<T>,
        ISelectedTableQueryable<T>,
        ITakenTableQueryable<T>,
        IFilteredTableQueryable<T>,
        ISelectedTakenTableQueryable<T>
        where T : class, IDictionary<string, object>, ITableEntity, new() // T is either T1 or T2. We've already filtered by Type before this helper is created
        where T1 : class, IDictionary<string, object>, ITableEntity, new()
        where T2 : class, IDictionary<string, object>, ITableEntity, new()
    {
        private readonly TableSetQueryHelper<FluentPartitionTableEntity<T1, T2>> _helper = helper.AddFilter(x => x.PartitionKey == typeof(T).Name);

        private static Expression<Func<FluentPartitionTableEntity<T1, T2>, TResult>> AlterExpression<TResult>(Expression<Func<T, TResult>> expression)
        {
            // Fake our input type to be T, regardless if it compiles or not.
            // For runtime compilation, the expression must be rewritten by using indexers instead of direct property access.
            // ( That's a TODO for later... )
            ParameterExpression parameter = Expression.Parameter(typeof(FluentPartitionTableEntity<T1, T2>));
            return Expression.Lambda<Func<FluentPartitionTableEntity<T1, T2>, TResult>>(expression.Body, parameter);
        }

        public async IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken)
        {
            await foreach (FluentPartitionTableEntity<T1, T2> entity in _helper.WithCancellation(cancellationToken))
            {
                yield return Unsafe.As<T>(entity.GetRawValue());
            }
        }

        public Task<int> BatchDeleteAsync(CancellationToken token = default) => _helper.BatchDeleteAsync(token);

        public Task<int> BatchDeleteTransactionAsync(CancellationToken token = default) => _helper.BatchDeleteTransactionAsync(token);

        public async Task<T> FirstAsync(CancellationToken token = default) => Unsafe.As<T>((await _helper.FirstAsync(token)).GetRawValue());

        public async Task<T?> FirstOrDefaultAsync(CancellationToken token = default) => Unsafe.As<T>((await _helper.FirstOrDefaultAsync(token))?.GetRawValue());

        public async Task<T> SingleAsync(CancellationToken token = default) => Unsafe.As<T>((await _helper.SingleAsync(token)).GetRawValue());

        public async Task<T?> SingleOrDefaultAsync(CancellationToken token = default) => Unsafe.As<T>((await _helper.SingleOrDefaultAsync(token))?.GetRawValue());

        private void SelectFields<TResult>(Expression<Func<T, TResult>> selector)
        {
            SelectionVisitor visitor = new(_helper.Table.PartitionKeyProxy, _helper.Table.RowKeyProxy);
            visitor.Visit(selector);

            if (visitor.Members.Count is 0)
            {
                throw new NotSupportedException("Select expression is not supported");
            }
            else
            {
                visitor.Members.Add(FluentPartitionTableEntity<T1, T2>.Discriminator);
                _helper.SetFields(visitor.Members);
            }
        }

        ISelectedTableQueryable<T> IFilteredTableQueryable<T>.SelectFields<TResult>(Expression<Func<T, TResult>> selector)
        {
            SelectFields(selector);
            return this;
        }

        ISelectedTakenTableQueryable<T> ITakenTableQueryable<T>.SelectFields<TResult>(Expression<Func<T, TResult>> selector)
        {
            SelectFields(selector);
            return this;
        }

        ITakenTableQueryable<T> IFilteredTableQueryable<T>.Take(int amount)
        {
            _helper.SetAmount(amount);
            return this;
        }

        ISelectedTakenTableQueryable<T> ISelectedTableQueryable<T>.Take(int amount)
        {
            _helper.SetAmount(amount);
            return this;
        }

        IFilteredTableQueryable<T> IFilteredTableQueryable<T>.Where(Expression<Func<T, bool>> predicate)
        {
            _helper.AddFilter(AlterExpression(predicate));
            return this;
        }

        ISelectedTakenTableQueryable<T> ISelectedTakenTableQueryable<T>.Where(Expression<Func<T, bool>> predicate)
        {
            _helper.AddFilter(AlterExpression(predicate));
            return this;
        }

        ITakenTableQueryable<T> ITakenTableQueryable<T>.Where(Expression<Func<T, bool>> predicate)
        {
            _helper.AddFilter(AlterExpression(predicate));
            return this;
        }

        ISelectedTableQueryable<T> ISelectedTableQueryable<T>.Where(Expression<Func<T, bool>> predicate)
        {
            _helper.AddFilter(AlterExpression(predicate));
            return this;
        }

        ISelectedTakenTableQueryable<T> ISelectedTakenTableQueryable<T>.ExistsIn<TElement>(Expression<Func<T, TElement>> predicate, IEnumerable<TElement> elements)
        {
            _helper.AddExistsInFilter(AlterExpression(predicate), elements);
            return this;
        }

        IFilteredTableQueryable<T> IFilteredTableQueryable<T>.ExistsIn<TElement>(Expression<Func<T, TElement>> predicate, IEnumerable<TElement> elements)
        {
            _helper.AddExistsInFilter(AlterExpression(predicate), elements);
            return this;
        }

        ITakenTableQueryable<T> ITakenTableQueryable<T>.ExistsIn<TElement>(Expression<Func<T, TElement>> predicate, IEnumerable<TElement> elements)
        {
            _helper.AddExistsInFilter(AlterExpression(predicate), elements);
            return this;
        }

        ISelectedTableQueryable<T> ISelectedTableQueryable<T>.ExistsIn<TElement>(Expression<Func<T, TElement>> predicate, IEnumerable<TElement> elements)
        {
            _helper.AddExistsInFilter(AlterExpression(predicate), elements);
            return this;
        }

        ISelectedTakenTableQueryable<T> ISelectedTakenTableQueryable<T>.NotExistsIn<TElement>(Expression<Func<T, TElement>> predicate, IEnumerable<TElement> elements)
        {
            _helper.AddNotExistsInFilter(AlterExpression(predicate), elements);
            return this;
        }

        IFilteredTableQueryable<T> IFilteredTableQueryable<T>.NotExistsIn<TElement>(Expression<Func<T, TElement>> predicate, IEnumerable<TElement> elements)
        {
            _helper.AddNotExistsInFilter(AlterExpression(predicate), elements);
            return this;
        }

        ITakenTableQueryable<T> ITakenTableQueryable<T>.NotExistsIn<TElement>(Expression<Func<T, TElement>> predicate, IEnumerable<TElement> elements)
        {
            _helper.AddNotExistsInFilter(AlterExpression(predicate), elements);
            return this;
        }

        ISelectedTableQueryable<T> ISelectedTableQueryable<T>.NotExistsIn<TElement>(Expression<Func<T, TElement>> predicate, IEnumerable<TElement> elements)
        {
            _helper.AddNotExistsInFilter(AlterExpression(predicate), elements);
            return this;
        }
    }
}