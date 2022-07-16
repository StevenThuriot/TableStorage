using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace TableStorage;

public sealed class TableSet<T>
    where T : class, ITableEntity, new()
{
    private readonly AsyncLazy<TableClient> _lazyClient;
    private readonly TableOptions _options;

    internal TableSet(TableStorageFactory factory, string tableName, TableOptions options)
    {
        _lazyClient = new AsyncLazy<TableClient>(() => factory.GetClient(tableName));
        _options = options;
    }

    public async Task AddEntityAsync(T entity, CancellationToken cancellationToken = default)
    {
        var client = await _lazyClient;

        if (_options.AutoTimestamps)
        {
            entity.Timestamp = DateTime.UtcNow;
        }

        await client.AddEntityAsync(entity, cancellationToken);
    }

    public async Task DeleteEntityAsync(string partitionKey, string rowKey, ETag ifMatch = default, CancellationToken cancellationToken = default)
    {
        var client = await _lazyClient;
        await client.DeleteEntityAsync(partitionKey, rowKey, ifMatch, cancellationToken);
    }

    public async Task SubmitTransactionAsync(IEnumerable<TableTransactionAction> transactionActions, CancellationToken cancellationToken = default)
    {
        var client = await _lazyClient;
        await client.SubmitTransactionAsync(transactionActions, cancellationToken);
    }

    public async Task UpdateEntityAsync(T entity, ETag ifMatch, TableUpdateMode? mode = null, CancellationToken cancellationToken = default)
    {
        var client = await _lazyClient;

        if (_options.AutoTimestamps)
        {
            entity.Timestamp = DateTime.UtcNow;
        }

        await client.UpdateEntityAsync(entity, ifMatch, mode ?? _options.TableMode, cancellationToken);
    }

    public async Task UpsertEntityAsync(T entity, TableUpdateMode? mode = null, CancellationToken cancellationToken = default)
    {
        var client = await _lazyClient;

        if (_options.AutoTimestamps)
        {
            entity.Timestamp = DateTime.UtcNow;
        }

        await client.UpsertEntityAsync(entity, mode ?? _options.TableMode, cancellationToken);
    }

    public async Task<T?> GetEntityAsync(string partitionKey, string rowKey, IEnumerable<string>? select = null, CancellationToken cancellationToken = default)
    {
        var client = await _lazyClient;
        var result = await client.GetEntityAsync<T>(partitionKey, rowKey, select, cancellationToken);
        return result.Value;
    }

    public async Task<T?> GetEntityOrDefaultAsync(string partitionKey, string rowKey, CancellationToken cancellationToken = default)
    {
        var client = await _lazyClient;

        try
        {
            var response = await client.GetEntityAsync<T>(partitionKey, rowKey, cancellationToken: cancellationToken);
            return response.Value;
        }
        catch (RequestFailedException e) when (e.Status == 404)
        {
            return null;
        }
    }

    public async IAsyncEnumerable<T> QueryAsync(string? filter = null, int? maxPerPage = null, IEnumerable<string>? select = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var client = await _lazyClient;
        await foreach (var item in client.QueryAsync<T>(filter, maxPerPage ?? _options.PageSize, select, cancellationToken))
        {
            yield return item;
        }
    }

    public async IAsyncEnumerable<T> QueryAsync(Expression<Func<T, bool>> filter, int? maxPerPage = null, IEnumerable<string>? select = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var client = await _lazyClient;
        await foreach (var item in client.QueryAsync(filter, maxPerPage ?? _options.PageSize, select, cancellationToken))
        {
            yield return item;
        }
    }
}