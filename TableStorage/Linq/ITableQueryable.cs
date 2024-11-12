using System.Linq.Expressions;

namespace TableStorage.Linq;

public interface ICanTakeOneTableQueryable<T>
{
    Task<T> FirstAsync(CancellationToken token = default);
    Task<T?> FirstOrDefaultAsync(CancellationToken token = default);
    Task<T> SingleAsync(CancellationToken token = default);
    Task<T?> SingleOrDefaultAsync(CancellationToken token = default);
}

public interface ITableAsyncEnumerable<T> : IAsyncEnumerable<T>
    where T : class, ITableEntity, new()
{
    Task<int> BatchDeleteAsync(CancellationToken token = default);
    Task<int> BatchDeleteTransactionAsync(CancellationToken token = default);
    Task<int> BatchUpdateAsync(Action<T> update, CancellationToken token = default);
    Task<int> BatchUpdateAsync(Expression<Func<T>> exp, CancellationToken token = default);
    Task<int> BatchUpdateTransactionAsync(Action<T> update, CancellationToken token = default);
    Task<int> BatchUpdateTransactionAsync(Expression<Func<T>> update, CancellationToken token = default);
}

public interface ITableEnumerable<T> : ICanTakeOneTableQueryable<T>, IAsyncEnumerable<T>;

public interface IFilteredTableQueryable<T> : ITableAsyncEnumerable<T>, ICanTakeOneTableQueryable<T>
    where T : class, ITableEntity, new()
{
    ITableEnumerable<TResult> Select<TResult>(Expression<Func<T, TResult>> selector);
    ISelectedTableQueryable<T> SelectFields<TResult>(Expression<Func<T, TResult>> selector);
    ITakenTableQueryable<T> Take(int amount);
    IFilteredTableQueryable<T> Where(Expression<Func<T, bool>> predicate);
    IFilteredTableQueryable<T> ExistsIn<TElement>(Expression<Func<T, TElement>> predicate, IEnumerable<TElement> elements);
    IFilteredTableQueryable<T> NotExistsIn<TElement>(Expression<Func<T, TElement>> predicate, IEnumerable<TElement> elements);
}

public interface ISelectedTableQueryable<T> : ITableAsyncEnumerable<T>, ICanTakeOneTableQueryable<T>
    where T : class, ITableEntity, new()
{
    ISelectedTakenTableQueryable<T> Take(int amount);
    ISelectedTableQueryable<T> Where(Expression<Func<T, bool>> predicate);
    ISelectedTableQueryable<T> ExistsIn<TElement>(Expression<Func<T, TElement>> predicate, IEnumerable<TElement> elements);
    ISelectedTableQueryable<T> NotExistsIn<TElement>(Expression<Func<T, TElement>> predicate, IEnumerable<TElement> elements);
}

public interface ITakenTableQueryable<T> : ITableAsyncEnumerable<T>
    where T : class, ITableEntity, new()
{
    ITableEnumerable<TResult> Select<TResult>(Expression<Func<T, TResult>> selector);
    ISelectedTakenTableQueryable<T> SelectFields<TResult>(Expression<Func<T, TResult>> selector);
    ITakenTableQueryable<T> Where(Expression<Func<T, bool>> predicate);
    ITakenTableQueryable<T> ExistsIn<TElement>(Expression<Func<T, TElement>> predicate, IEnumerable<TElement> elements);
    ITakenTableQueryable<T> NotExistsIn<TElement>(Expression<Func<T, TElement>> predicate, IEnumerable<TElement> elements);
}

public interface ISelectedTakenTableQueryable<T> : ITableAsyncEnumerable<T>
    where T : class, ITableEntity, new()
{
    ISelectedTakenTableQueryable<T> Where(Expression<Func<T, bool>> predicate);
    ISelectedTakenTableQueryable<T> ExistsIn<TElement>(Expression<Func<T, TElement>> predicate, IEnumerable<TElement> elements);
    ISelectedTakenTableQueryable<T> NotExistsIn<TElement>(Expression<Func<T, TElement>> predicate, IEnumerable<TElement> elements);
}