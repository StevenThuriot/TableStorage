using System.Linq.Expressions;

namespace TableStorage.Linq;

public interface ICanTakeOneTableQueryable<T>
    where T : class, ITableEntity, new()
{
    Task<T> FirstAsync(CancellationToken token = default);
    Task<T?> FirstOrDefaultAsync(CancellationToken token = default);
    Task<T> SingleAsync(CancellationToken token = default);
    Task<T?> SingleOrDefaultAsync(CancellationToken token = default);
}

public interface ITableQueryable<T>
    where T : class, ITableEntity, new()
{
    Task<List<T>> ToListAsync(CancellationToken token = default);
    IAsyncEnumerable<T> ToAsyncEnumerableAsync(CancellationToken token = default);
}

public interface IFilteredTableQueryable<T> : ITableQueryable<T>, ICanTakeOneTableQueryable<T>
    where T : class, ITableEntity, new()
{
    ISelectedTableQueryable<T> Select<TResult>(Expression<Func<T, TResult>> selector);
    ITakenTableQueryable<T> Take(int amount);
    IFilteredTableQueryable<T> Where(Expression<Func<T, bool>> predicate);
    IDistinctedTableQueryable<T> DistinctBy<TResult>(Func<T, TResult> selector, IEqualityComparer<TResult>? equalityComparer = null);
}

public interface ISelectedTableQueryable<T> : ITableQueryable<T>, ICanTakeOneTableQueryable<T>
    where T : class, ITableEntity, new()
{
    ISelectedTakenTableQueryable<T> Take(int amount);
    ISelectedTableQueryable<T> Where(Expression<Func<T, bool>> predicate);
    ISelectedDistinctedTableQueryable<T> DistinctBy<TResult>(Func<T, TResult> selector, IEqualityComparer<TResult>? equalityComparer = null);
}

public interface ITakenTableQueryable<T> : ITableQueryable<T>
    where T : class, ITableEntity, new()
{
    ISelectedTakenTableQueryable<T> Select<TResult>(Expression<Func<T, TResult>> selector);
    ITakenTableQueryable<T> Where(Expression<Func<T, bool>> predicate);
    ITakenDistinctedTableQueryable<T> DistinctBy<TResult>(Func<T, TResult> selector, IEqualityComparer<TResult>? equalityComparer = null);
}

public interface IDistinctedTableQueryable<T> : ITableQueryable<T>, ICanTakeOneTableQueryable<T>
    where T : class, ITableEntity, new()
{
    ISelectedDistinctedTableQueryable<T> Select<TResult>(Expression<Func<T, TResult>> selector);
    IDistinctedTableQueryable<T> Where(Expression<Func<T, bool>> predicate);
    ISelectedDistinctedTableQueryable<T> Take(int amount);
}

public interface ISelectedTakenTableQueryable<T> : ITableQueryable<T>
    where T : class, ITableEntity, new()
{
    ISelectedTakenTableQueryable<T> Where(Expression<Func<T, bool>> predicate);
    ISelectedTakenDistinctedTableQueryable<T> DistinctBy<TResult>(Func<T, TResult> selector, IEqualityComparer<TResult>? equalityComparer = null);
}

public interface ISelectedDistinctedTableQueryable<T> : ITableQueryable<T>, ICanTakeOneTableQueryable<T>
    where T : class, ITableEntity, new()
{
    ISelectedTakenDistinctedTableQueryable<T> Take(int amount);
    ISelectedDistinctedTableQueryable<T> Where(Expression<Func<T, bool>> predicate);
}

public interface ITakenDistinctedTableQueryable<T> : ITableQueryable<T>
    where T : class, ITableEntity, new()
{
    ISelectedTakenTableQueryable<T> Select<TResult>(Expression<Func<T, TResult>> selector);
    ITakenDistinctedTableQueryable<T> Where(Expression<Func<T, bool>> predicate);
}

public interface ISelectedTakenDistinctedTableQueryable<T> : ITableQueryable<T>
    where T : class, ITableEntity, new()
{
    ISelectedTakenDistinctedTableQueryable<T> Where(Expression<Func<T, bool>> predicate);
}