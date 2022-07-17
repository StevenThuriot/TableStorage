using System.Linq.Expressions;

namespace TableStorage.Linq;

public interface IFilteredTableQueryable<T> : ITableQueryable<T>
    where T : class, ITableEntity, new()
{
    ISelectedTableQueryable<T> Select<TResult>(Expression<Func<T, TResult>> selector);
    ITakenTableQueryable<T> Take(int amount);
    IFilteredTableQueryable<T> Where(Expression<Func<T, bool>> predicate);

    Task<T> FirstAsync(CancellationToken token = default);
    Task<T?> FirstOrDefaultAsync(CancellationToken token = default);
    Task<T> SingleAsync(CancellationToken token = default);
    Task<T?> SingleOrDefaultAsync(CancellationToken token = default);
}
