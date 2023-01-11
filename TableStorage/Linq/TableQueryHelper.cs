using System.Linq.Expressions;

namespace TableStorage.Linq;

public static class TableQueryHelper
{
    public static ISelectedTableQueryable<T> Select<T, TResult>(this TableSet<T> table, Expression<Func<T, TResult>> selector)
        where T : class, ITableEntity, new()
    {
        return new TableSetQueryHelper<T>(table).SetFields(selector);
    }

    public static ITakenTableQueryable<T> Take<T>(this TableSet<T> table, int amount)
        where T : class, ITableEntity, new()
    {
        return new TableSetQueryHelper<T>(table).SetAmount(amount);
    }

    public static IFilteredTableQueryable<T> Where<T>(this TableSet<T> table, Expression<Func<T, bool>> predicate)
        where T : class, ITableEntity, new()
    {
        return new TableSetQueryHelper<T>(table).AddFilter(predicate);
    }

    public static IFilteredTableQueryable<T> ExistsIn<T, TElement>(this TableSet<T> table, Expression<Func<T, TElement>> predicate, IEnumerable<TElement> elements)
        where T : class, ITableEntity, new()
    {
        return new TableSetQueryHelper<T>(table).AddExistsInFilter(predicate, elements);
    }

    public static IDistinctedTableQueryable<T> DistinctBy<T, TResult>(this TableSet<T> table, Func<T, TResult> selector, IEqualityComparer<TResult>? equalityComparer = null)
        where T : class, ITableEntity, new()
    {
        return new TableSetQueryHelper<T>(table).SetDistinction(selector, equalityComparer);
    }
}