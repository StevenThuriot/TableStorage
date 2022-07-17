using System.Linq.Expressions;

namespace TableStorage.Linq;

public interface ITakenTableQueryable<T> : ITableQueryable<T>
    where T : class, ITableEntity, new()
{
    ISelectedTakenTableQueryable<T> Select<TResult>(Expression<Func<T, TResult>> selector);
    ITakenTableQueryable<T> Where(Expression<Func<T, bool>> predicate);
}
