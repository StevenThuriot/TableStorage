using System.Linq.Expressions;

namespace TableStorage;
public interface IStorageSet<T> : IAsyncEnumerable<T>
{
    string EntityType { get; }
    string Name { get; }
    Type Type { get; }

    Task AddEntityAsync(T entity, CancellationToken cancellationToken = default);
    Task DeleteEntityAsync(string partitionKey, string rowKey, CancellationToken cancellationToken = default);
    Task<T?> GetEntityAsync(string partitionKey, string rowKey, CancellationToken cancellationToken = default);
    Task<T?> GetEntityOrDefaultAsync(string partitionKey, string rowKey, CancellationToken cancellationToken = default);
    IAsyncEnumerable<T> QueryAsync(CancellationToken cancellationToken = default);
    IAsyncEnumerable<T> QueryAsync(Expression<Func<T, bool>> filter, CancellationToken cancellationToken = default);
    Task<(bool success, T? entity)> TryGetEntityAsync(string partitionKey, string rowKey, CancellationToken cancellationToken = default);
    Task UpdateEntityAsync(T entity, CancellationToken cancellationToken = default);
    Task UpsertEntityAsync(T entity, CancellationToken cancellationToken = default);
}