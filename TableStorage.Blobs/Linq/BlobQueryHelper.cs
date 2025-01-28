using System.Linq.Expressions;

namespace TableStorage.Linq;

public static class BlobSetQueryHelper
{
    public static IFilteredBlobQueryable<T> Where<T>(this BlobSet<T> table, Expression<Func<T, bool>> predicate)
        where T : IBlobEntity
    {
        return new BlobSetQueryHelper<T>(table).Where(predicate);
    }

    public static IFilteredBlobQueryable<T> ExistsIn<T, TElement>(this BlobSet<T> table, Expression<Func<T, TElement>> predicate, IEnumerable<TElement> elements)
        where T : IBlobEntity
    {
        return new BlobSetQueryHelper<T>(table).ExistsIn(predicate, elements);
    }

    public static IFilteredBlobQueryable<T> NotExistsIn<T, TElement>(this BlobSet<T> table, Expression<Func<T, TElement>> predicate, IEnumerable<TElement> elements)
        where T : IBlobEntity
    {
        return new BlobSetQueryHelper<T>(table).NotExistsIn(predicate, elements);
    }

    public static Task<T> FirstAsync<T>(this BlobSet<T> table, CancellationToken token = default)
        where T : IBlobEntity
    {
        return new BlobSetQueryHelper<T>(table).FirstAsync(token);
    }

    public static Task<T?> FirstOrDefaultAsync<T>(this BlobSet<T> table, CancellationToken token = default)
        where T : IBlobEntity
    {
        return new BlobSetQueryHelper<T>(table).FirstOrDefaultAsync(token);
    }

    public static Task<T> SingleAsync<T>(this BlobSet<T> table, CancellationToken token = default)
        where T : IBlobEntity
    {
        return new BlobSetQueryHelper<T>(table).SingleAsync(token);
    }

    public static Task<T?> SingleOrDefaultAsync<T>(this BlobSet<T> table, CancellationToken token = default)
        where T : IBlobEntity
    {
        return new BlobSetQueryHelper<T>(table).SingleOrDefaultAsync(token);
    }
}
