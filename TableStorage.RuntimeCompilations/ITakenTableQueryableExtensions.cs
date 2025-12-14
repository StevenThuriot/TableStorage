using System.Linq.Expressions;
using TableStorage.Linq;

namespace TableStorage;

public static class ITakenTableQueryableExtensions
{
    public static ITableEnumerable<TResult> Select<T, TResult>(this ITakenTableQueryable<T> table, Expression<Func<T, TResult>> selector)
        where T : class, ITableEntity, new()
    {
        if (table is not ITableSetQueryHelper<T> helper)
        {
            throw new NotSupportedException("BatchUpdateTransactionAsync is not supported on the passed table type.");
        }

        CompilingTableSetQueryHelper<T> compilingHelper = new(helper);
        return compilingHelper.SetFieldsAndTransform(selector);
    }
}