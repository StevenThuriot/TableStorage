using Azure.Storage.Blobs.Specialized;
using System.Linq.Expressions;
using TableStorage.Visitors;

namespace TableStorage;

internal readonly struct BlobQueryHandler<T, TClient>(BaseBlobSet<T, TClient> blobset)
    where TClient : BlobBaseClient
    where T : IBlobEntity
{
    private readonly BaseBlobSet<T, TClient> _blobset = blobset;

    public IAsyncEnumerable<(TClient client, LazyAsync<T?> entity)> QueryAsync(Expression<Func<T, bool>>? filter, CancellationToken cancellationToken = default)
    {
        if (filter is null)
        {
            return _blobset.IterateAllBlobs(cancellationToken);
        }

        BlobQueryVisitor visitor = new(_blobset.ModelInfo, _blobset.Tags);
        Expression<Func<T, bool>> visitedFilter = visitor.VisitAndConvert(filter, nameof(QueryAsync));

        if (!visitor.Error)
        {
            if (_blobset.Options.UseTags)
            {
                if (visitor.SimpleFilter)
                {
                    return _blobset.IterateBlobsByTag(visitor.Filter!, cancellationToken);
                }
            }
        }

        throw new NotSupportedException("The filter is not supported");
    }
}