using System.Linq.Expressions;

namespace TableStorage.Linq;

public interface ICanTakeOneBlobQueryable<T>
{
    Task<T> FirstAsync(CancellationToken token = default);
    Task<T?> FirstOrDefaultAsync(CancellationToken token = default);
    Task<T> SingleAsync(CancellationToken token = default);
    Task<T?> SingleOrDefaultAsync(CancellationToken token = default);
}

public interface IBlobAsyncEnumerable<T> : IAsyncEnumerable<T>
    where T : IBlobEntity
{
    Task<int> BatchDeleteAsync(CancellationToken token = default);
    Task<int> BatchUpdateAsync(Expression<Func<T, T>> update, CancellationToken token = default);
}

public interface IBlobEnumerable<T> : ICanTakeOneBlobQueryable<T>, IAsyncEnumerable<T>;

public interface IFilteredBlobQueryable<T> : IBlobAsyncEnumerable<T>, ICanTakeOneBlobQueryable<T>
    where T : IBlobEntity
{
    IFilteredBlobQueryable<T> Where(Expression<Func<T, bool>> predicate);
    IFilteredBlobQueryable<T> ExistsIn<TElement>(Expression<Func<T, TElement>> predicate, IEnumerable<TElement> elements);
    IFilteredBlobQueryable<T> NotExistsIn<TElement>(Expression<Func<T, TElement>> predicate, IEnumerable<TElement> elements);
}