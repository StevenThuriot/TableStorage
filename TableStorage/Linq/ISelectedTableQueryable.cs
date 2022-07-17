using System.Linq.Expressions;

namespace TableStorage.Linq;

public interface ISelectedTableQueryable<T> : ITableQueryable<T>
    where T : class, ITableEntity, new()
{
    ISelectedTakenTableQueryable<T> Take(int amount);
    ISelectedTableQueryable<T> Where(Expression<Func<T, bool>> predicate);

    Task<T> FirstAsync(CancellationToken token = default);
    Task<T?> FirstOrDefaultAsync(CancellationToken token = default);
    Task<T> SingleAsync(CancellationToken token = default);
    Task<T?> SingleOrDefaultAsync(CancellationToken token = default);
}
