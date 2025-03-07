using System.Linq.Expressions;

namespace TableStorage;
public interface IStorageSet<T> : IAsyncEnumerable<T>
{
    public string EntityType { get; }
    public string Name { get; }
    public Type Type { get; }

    public Task AddEntityAsync(T entity, CancellationToken cancellationToken = default);
    public Task DeleteEntityAsync(string partitionKey, string rowKey, CancellationToken cancellationToken = default);
    public Task<T?> GetEntityAsync(string partitionKey, string rowKey, CancellationToken cancellationToken = default);
    public Task<T?> GetEntityOrDefaultAsync(string partitionKey, string rowKey, CancellationToken cancellationToken = default);
    public IAsyncEnumerable<T> QueryAsync(CancellationToken cancellationToken = default);
    public IAsyncEnumerable<T> QueryAsync(Expression<Func<T, bool>> filter, CancellationToken cancellationToken = default);
    public Task<(bool success, T? entity)> TryGetEntityAsync(string partitionKey, string rowKey, CancellationToken cancellationToken = default);
    public Task UpdateEntityAsync(T entity, CancellationToken cancellationToken = default);
    public Task UpsertEntityAsync(T entity, CancellationToken cancellationToken = default);
}