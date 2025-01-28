using System.Linq.Expressions;
using System.Threading;

namespace TableStorage.Linq;

internal sealed class BlobSetQueryHelper<T>(BlobSet<T> table) :
    IAsyncEnumerable<T>,
    IBlobEnumerable<T>,
    IFilteredBlobQueryable<T>
    where T : IBlobEntity
{
    private readonly BlobSet<T> _table = table;
    private Expression<Func<T, bool>>? _filter;

    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        if (_filter is null)
        {
            return _table.GetAsyncEnumerator(cancellationToken);
        }

        return Iterate();
        async IAsyncEnumerator<T> Iterate()
        {
            await foreach (var result in _table.QueryInternalAsync(_filter, cancellationToken))
            {
                var entity = await result.entity;

                if (entity is not null)
                {
                    yield return entity;
                }
            }
        }
    }

    public async Task<int> BatchDeleteAsync(CancellationToken token = default)
    {
        int count = 0;

        await foreach (var (client, _) in _table.QueryInternalAsync(_filter, token))
        {
            await client.DeleteIfExistsAsync(cancellationToken: token);
            count++;
        }

        return count;
    }

    public async Task<int> BatchUpdateAsync(Expression<Func<T, T>> update, CancellationToken token = default)
    {
        int count = 0;

        LazyExpression<T> compiledUpdate = update;
        await foreach (var entity in this.WithCancellation(token))
        {
            var updatedEntity = compiledUpdate.Invoke(entity);
            await _table.UpsertEntityAsync(updatedEntity, token);
            count++;
        }

        return count;
    }

    public IFilteredBlobQueryable<T> ExistsIn<TElement>(Expression<Func<T, TElement>> predicate, IEnumerable<TElement> elements)
    {
        var lambda = predicate.CreateExistsInFilter(elements);
        return Where(lambda);
    }

    public IFilteredBlobQueryable<T> NotExistsIn<TElement>(Expression<Func<T, TElement>> predicate, IEnumerable<TElement> elements)
    {
        var lambda = predicate.CreateNotExistsInFilter(elements);
        return Where(lambda);
    }

    public Task<T> FirstAsync(CancellationToken token)
    {
        return Helpers.FirstAsync(this, token);
    }

    public Task<T?> FirstOrDefaultAsync(CancellationToken token)
    {
        return Helpers.FirstOrDefaultAsync(this, token);
    }

    public Task<T> SingleAsync(CancellationToken token = default)
    {
        return Helpers.SingleAsync(this, token);
    }

    public Task<T?> SingleOrDefaultAsync(CancellationToken token = default)
    {
        return Helpers.SingleOrDefaultAsync(this, token);
    }

    public IFilteredBlobQueryable<T> Where(Expression<Func<T, bool>> predicate)
    {
        if (_filter is null)
        {
            _filter = predicate;
        }
        else
        {
            var invokedExpr = Expression.Invoke(predicate, _filter.Parameters);
            BinaryExpression combinedExpression = Expression.AndAlso(_filter.Body, invokedExpr);
            _filter = Expression.Lambda<Func<T, bool>>(combinedExpression, _filter.Parameters);
        }

        return this;
    }
}
