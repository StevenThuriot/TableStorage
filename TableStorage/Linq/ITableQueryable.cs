namespace TableStorage.Linq;

public interface ITableQueryable<T>
    where T : class, ITableEntity, new()
{
    Task<List<T>> ToListAsync(CancellationToken token = default);
    IAsyncEnumerable<T> ToAsyncEnumerableAsync(CancellationToken token = default);
}
