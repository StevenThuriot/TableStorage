using System.Linq.Expressions;

namespace TableStorage.Linq;

public interface ICanTakeOneBlobQueryable<T>
{
    public Task<T> FirstAsync(CancellationToken token = default);
    public Task<T?> FirstOrDefaultAsync(CancellationToken token = default);
    public Task<T> SingleAsync(CancellationToken token = default);
    public Task<T?> SingleOrDefaultAsync(CancellationToken token = default);
}

public interface IBlobAsyncEnumerable<T> : IAsyncEnumerable<T>
    where T : IBlobEntity
{
    public Task<int> BatchDeleteAsync(CancellationToken token = default);
    public Task<int> BatchUpdateAsync(Expression<Func<T, T>> update, CancellationToken token = default);
}

public interface IBlobEnumerable<T> : ICanTakeOneBlobQueryable<T>, IAsyncEnumerable<T>;

public interface IFilteredBlobQueryable<T> : IBlobAsyncEnumerable<T>, ICanTakeOneBlobQueryable<T>
    where T : IBlobEntity
{
    public IFilteredBlobQueryable<T> Where(Expression<Func<T, bool>> predicate);
    public IFilteredBlobQueryable<T> ExistsIn<TElement>(Expression<Func<T, TElement>> predicate, IEnumerable<TElement> elements);
    public IFilteredBlobQueryable<T> NotExistsIn<TElement>(Expression<Func<T, TElement>> predicate, IEnumerable<TElement> elements);
}