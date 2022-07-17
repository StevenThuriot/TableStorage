using System.Linq.Expressions;

namespace TableStorage.Linq;

public interface ISelectedTakenTableQueryable<T> : ITableQueryable<T>
    where T : class, ITableEntity, new()
{
    ISelectedTakenTableQueryable<T> Where(Expression<Func<T, bool>> predicate);
}
