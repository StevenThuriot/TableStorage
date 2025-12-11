using System.Linq.Expressions;

namespace TableStorage.Linq;

public static class TableQueryHelper
{
    public static ISelectedTableQueryable<T> SelectFields<T, TResult>(this TableSet<T> table, Expression<Func<T, TResult>> selector)
        where T : class, ITableEntity, new()
    {
        return TableSetQueryHelper.From(table).SetFields(selector);
    }

    public static ITakenTableQueryable<T> Take<T>(this TableSet<T> table, int amount)
        where T : class, ITableEntity, new()
    {
        return TableSetQueryHelper.From(table).SetAmount(amount);
    }

    public static IFilteredTableQueryable<T> Where<T>(this TableSet<T> table, Expression<Func<T, bool>> predicate)
        where T : class, ITableEntity, new()
    {
        return TableSetQueryHelper.From(table).AddFilter(predicate);
    }

    public static IFilteredTableQueryable<T> ExistsIn<T, TElement>(this TableSet<T> table, Expression<Func<T, TElement>> predicate, IEnumerable<TElement> elements)
        where T : class, ITableEntity, new()
    {
        return TableSetQueryHelper.From(table).AddExistsInFilter(predicate, elements);
    }

    public static IFilteredTableQueryable<T> NotExistsIn<T, TElement>(this TableSet<T> table, Expression<Func<T, TElement>> predicate, IEnumerable<TElement> elements)
        where T : class, ITableEntity, new()
    {
        return TableSetQueryHelper.From(table).AddNotExistsInFilter(predicate, elements);
    }

    public static Task<T> FirstAsync<T>(this TableSet<T> table, CancellationToken token = default)
        where T : class, ITableEntity, new()
    {
        return TableSetQueryHelper.From(table).FirstAsync(token);
    }

    public static Task<T?> FirstOrDefaultAsync<T>(this TableSet<T> table, CancellationToken token = default)
        where T : class, ITableEntity, new()
    {
        return TableSetQueryHelper.From(table).FirstOrDefaultAsync(token);
    }

    public static Task<T> SingleAsync<T>(this TableSet<T> table, CancellationToken token = default)
        where T : class, ITableEntity, new()
    {
        return TableSetQueryHelper.From(table).SingleAsync(token);
    }

    public static Task<T?> SingleOrDefaultAsync<T>(this TableSet<T> table, CancellationToken token = default)
        where T : class, ITableEntity, new()
    {
        return TableSetQueryHelper.From(table).SingleOrDefaultAsync(token);
    }

    public static Task<T?> FindAsync<T>(this TableSet<T> table, string partitionKey, string rowKey, CancellationToken token = default)
        where T : class, ITableEntity, new()
    {
        Expression<Func<T, bool>> predicate = Helpers.CreateFindPredicate<T>(partitionKey, rowKey, table.PartitionKeyProxy, table.RowKeyProxy);
        return table.Where(predicate).FirstOrDefaultAsync(token);
    }

    public static IFilteredTableQueryable<T> FindAsync<T>(this TableSet<T> table, params IReadOnlyList<(string partitionKey, string rowKey)> keys)
        where T : class, ITableEntity, new()
    {
        Expression<Func<T, bool>> predicate = Helpers.CreateFindPredicate<T>(keys, table.PartitionKeyProxy, table.RowKeyProxy);
        return table.Where(predicate);
    }
}