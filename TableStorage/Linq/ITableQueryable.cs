using System.Linq.Expressions;

namespace TableStorage.Linq;

public interface ICanTakeOneTableQueryable<T>
{
    public Task<T> FirstAsync(CancellationToken token = default);
    public Task<T?> FirstOrDefaultAsync(CancellationToken token = default);
    public Task<T> SingleAsync(CancellationToken token = default);
    public Task<T?> SingleOrDefaultAsync(CancellationToken token = default);
}

public interface ITableAsyncEnumerable<T> : IAsyncEnumerable<T>
    where T : class, ITableEntity, new()
{
    public Task<int> BatchDeleteAsync(CancellationToken token = default);
    public Task<int> BatchDeleteTransactionAsync(CancellationToken token = default);
    public Task<int> BatchUpdateAsync(Expression<Func<T, T>> update, CancellationToken token = default);
    public Task<int> BatchUpdateTransactionAsync(Expression<Func<T, T>> update, CancellationToken token = default);
}

public interface ITableEnumerable<T> : ICanTakeOneTableQueryable<T>, IAsyncEnumerable<T>;

public interface IFilteredTableQueryable<T> : ITableAsyncEnumerable<T>, ICanTakeOneTableQueryable<T>
    where T : class, ITableEntity, new()
{
    public ITableEnumerable<TResult> Select<TResult>(Expression<Func<T, TResult>> selector);
    public ISelectedTableQueryable<T> SelectFields<TResult>(Expression<Func<T, TResult>> selector);
    public ITakenTableQueryable<T> Take(int amount);
    public IFilteredTableQueryable<T> Where(Expression<Func<T, bool>> predicate);
    public IFilteredTableQueryable<T> ExistsIn<TElement>(Expression<Func<T, TElement>> predicate, IEnumerable<TElement> elements);
    public IFilteredTableQueryable<T> NotExistsIn<TElement>(Expression<Func<T, TElement>> predicate, IEnumerable<TElement> elements);
}

public interface ISelectedTableQueryable<T> : ITableAsyncEnumerable<T>, ICanTakeOneTableQueryable<T>
    where T : class, ITableEntity, new()
{
    public ISelectedTakenTableQueryable<T> Take(int amount);
    public ISelectedTableQueryable<T> Where(Expression<Func<T, bool>> predicate);
    public ISelectedTableQueryable<T> ExistsIn<TElement>(Expression<Func<T, TElement>> predicate, IEnumerable<TElement> elements);
    public ISelectedTableQueryable<T> NotExistsIn<TElement>(Expression<Func<T, TElement>> predicate, IEnumerable<TElement> elements);
}

public interface ITakenTableQueryable<T> : ITableAsyncEnumerable<T>
    where T : class, ITableEntity, new()
{
    public ITableEnumerable<TResult> Select<TResult>(Expression<Func<T, TResult>> selector);
    public ISelectedTakenTableQueryable<T> SelectFields<TResult>(Expression<Func<T, TResult>> selector);
    public ITakenTableQueryable<T> Where(Expression<Func<T, bool>> predicate);
    public ITakenTableQueryable<T> ExistsIn<TElement>(Expression<Func<T, TElement>> predicate, IEnumerable<TElement> elements);
    public ITakenTableQueryable<T> NotExistsIn<TElement>(Expression<Func<T, TElement>> predicate, IEnumerable<TElement> elements);
}

public interface ISelectedTakenTableQueryable<T> : ITableAsyncEnumerable<T>
    where T : class, ITableEntity, new()
{
    public ISelectedTakenTableQueryable<T> Where(Expression<Func<T, bool>> predicate);
    public ISelectedTakenTableQueryable<T> ExistsIn<TElement>(Expression<Func<T, TElement>> predicate, IEnumerable<TElement> elements);
    public ISelectedTakenTableQueryable<T> NotExistsIn<TElement>(Expression<Func<T, TElement>> predicate, IEnumerable<TElement> elements);
}