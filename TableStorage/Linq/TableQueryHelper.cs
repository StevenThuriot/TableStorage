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

    public static Task<T> FirstAsync<T>(this TableSet<T> table, CancellationToken token = default)
        where T : class, ITableEntity, new()
    {
        return new TableSetQueryHelper<T>(table).FirstAsync(token);
    }
    
    public static Task<T?> FirstOrDefaultAsync<T>(this TableSet<T> table, CancellationToken token = default)
        where T : class, ITableEntity, new()
    {
        return new TableSetQueryHelper<T>(table).FirstOrDefaultAsync(token);
    }
    
    public static Task<T> SingleAsync<T>(this TableSet<T> table, CancellationToken token = default)
        where T : class, ITableEntity, new()
    {
        return new TableSetQueryHelper<T>(table).SingleAsync(token);
    }

    public static Task<T?> SingleOrDefaultAsync<T>(this TableSet<T> table, CancellationToken token = default)
        where T : class, ITableEntity, new()
    {
        return new TableSetQueryHelper<T>(table).SingleOrDefaultAsync(token);
    }

    public static Task<List<T>> ToListAsync<T>(this TableSet<T> table, CancellationToken token = default)
        where T : class, ITableEntity, new()
    {
        return new TableSetQueryHelper<T>(table).ToListAsync(token);
    }

    public static IAsyncEnumerable<T> ToAsyncEnumerableAsync<T>(this TableSet<T> table, CancellationToken token = default)
        where T : class, ITableEntity, new()
    {
        return new TableSetQueryHelper<T>(table).ToAsyncEnumerableAsync(token);
    }
}