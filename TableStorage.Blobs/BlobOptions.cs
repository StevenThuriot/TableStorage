using Azure.Storage.Blobs.Specialized;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace TableStorage;

public sealed class BlobOptions
{
    internal BlobOptions()
    {
        QueryHandlerFactory = new QueryHandlerFactory();
    }

    public CreateIfNotExistsMode CreateContainerIfNotExists { get; set; } = CreateIfNotExistsMode.Always;

    public IBlobSerializer Serializer { get; set; } = default!;

    public bool UseTags { get; set; } = true;

    internal IQueryHandlerFactory QueryHandlerFactory { get; set; }
}

internal interface IQueryHandlerFactory
{
    public IAsyncEnumerable<(TClient client, LazyAsync<T?> entity)> QueryAsync<T, TClient>(BaseBlobSet<T, TClient> baseBlobSet, Expression<Func<T, bool>> filter, CancellationToken cancellationToken)
        where T : IBlobEntity
        where TClient : BlobBaseClient;
}

internal sealed class QueryHandlerFactory : IQueryHandlerFactory
{
    public async IAsyncEnumerable<(TClient client, LazyAsync<T?> entity)> QueryAsync<T, TClient>(BaseBlobSet<T, TClient> baseBlobSet, Expression<Func<T, bool>> filter, [EnumeratorCancellation] CancellationToken cancellationToken)
        where T : IBlobEntity
        where TClient : BlobBaseClient
    {
        BlobQueryHandler<T, TClient> handler = new(baseBlobSet);

        await foreach ((TClient client, LazyAsync<T?> entity) result in handler.QueryAsync(filter, cancellationToken))
        {
            yield return result;
        }
    }
}