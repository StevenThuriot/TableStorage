using System.Linq.Expressions;
using TableStorage.Fluent;

namespace TableStorage.Linq;

public static class FluentTableQueryHelper
{
    private static Expression<Func<FluentPartitionTableEntity<T1, T2>, bool>> FakeExpression<T1, T2>(LambdaExpression predicate)
        where T1 : class, IDictionary<string, object>, ITableEntity, new()
        where T2 : class, IDictionary<string, object>, ITableEntity, new()
    {
        return FakeExpression<T1, T2, bool>(predicate);
    }
    
    private static Expression<Func<FluentPartitionTableEntity<T1, T2>, TResult>> FakeExpression<T1, T2, TResult>(LambdaExpression predicate)
        where T1 : class, IDictionary<string, object>, ITableEntity, new()
        where T2 : class, IDictionary<string, object>, ITableEntity, new()
    {
        ParameterExpression param = Expression.Parameter(typeof(FluentPartitionTableEntity<T1, T2>), "e");
        return Expression.Lambda<Func<FluentPartitionTableEntity<T1, T2>, TResult>>(predicate.Body, param);
    }
    
    extension<T1, T2>(TableSet<FluentPartitionTableEntity<T1, T2>> table)
        where T1 : class, IDictionary<string, object>, ITableEntity, new()
        where T2 : class, IDictionary<string, object>, ITableEntity, new()
    {
        public Task<FluentPartitionTableEntity<T1, T2>?> FindAsync(string rowKey, CancellationToken token = default)
        {
            Expression<Func<FluentPartitionTableEntity<T1, T2>, bool>> predicate = Helpers.CreateFindPredicate<FluentPartitionTableEntity<T1, T2>>(FluentPartitionTableEntity<T1, T2>.Discriminator, rowKey, table.PartitionKeyProxy, table.RowKeyProxy);
            return table.Where(predicate).FirstOrDefaultAsync(token);
        }

        public IFilteredTableQueryable<FluentPartitionTableEntity<T1, T2>> FindAsync(params IReadOnlyList<string> rowKeys)
        {
            var keys = rowKeys.Select(rk => (partitionKey: FluentPartitionTableEntity<T1, T2>.Discriminator, rowKey: rk)).ToList();
            Expression<Func<FluentPartitionTableEntity<T1, T2>, bool>> predicate = Helpers.CreateFindPredicate<FluentPartitionTableEntity<T1, T2>>(keys, table.PartitionKeyProxy, table.RowKeyProxy);
            return table.Where(predicate);
        }

        public IFilteredTableQueryable<FluentPartitionTableEntity<T1, T2>> Where(Expression<Func<T1, bool>> predicate)
        {
            return TableSetQueryHelper.From(table).AddFilter(FakeExpression<T1, T2>(predicate));
        }

        public IFilteredTableQueryable<FluentPartitionTableEntity<T1, T2>> Where(Expression<Func<T2, bool>> predicate)
        {
            return TableSetQueryHelper.From(table).AddFilter(FakeExpression<T1, T2>(predicate));
        }

        public ISelectedTableQueryable<FluentPartitionTableEntity<T1, T2>> SelectFields<TResult>(Expression<Func<T1, TResult>> selector)
        {
            var fakeSelector = FakeExpression<T1, T2>(selector);
            return TableSetQueryHelper.From(table).SetFields(ref fakeSelector);
        }

        public ISelectedTableQueryable<FluentPartitionTableEntity<T1, T2>> SelectFields<TResult>(Expression<Func<T2, TResult>> selector)
        {
            var fakeSelector = FakeExpression<T1, T2>(selector);
            return TableSetQueryHelper.From(table).SetFields(ref fakeSelector);
        }

        public IFilteredTableQueryable<FluentPartitionTableEntity<T1, T2>> ExistsIn<TElement>(Expression<Func<T1, TElement>> predicate, IEnumerable<TElement> elements)
        {
            return TableSetQueryHelper.From(table).AddExistsInFilter(FakeExpression<T1, T2, TElement>(predicate), elements);
        }

        public IFilteredTableQueryable<FluentPartitionTableEntity<T1, T2>> ExistsIn<TElement>(Expression<Func<T2, TElement>> predicate, IEnumerable<TElement> elements)
        {
            return TableSetQueryHelper.From(table).AddExistsInFilter(FakeExpression<T1, T2, TElement>(predicate), elements);
        }

        public IFilteredTableQueryable<FluentPartitionTableEntity<T1, T2>> NotExistsIn<TElement>(Expression<Func<T1, TElement>> predicate, IEnumerable<TElement> elements)
        {
            return TableSetQueryHelper.From(table).AddNotExistsInFilter(FakeExpression<T1, T2, TElement>(predicate), elements);
        }

        public IFilteredTableQueryable<FluentPartitionTableEntity<T1, T2>> NotExistsIn<TElement>(Expression<Func<T2, TElement>> predicate, IEnumerable<TElement> elements)
        {
            return TableSetQueryHelper.From(table).AddNotExistsInFilter(FakeExpression<T1, T2, TElement>(predicate), elements);
        }
    }
}