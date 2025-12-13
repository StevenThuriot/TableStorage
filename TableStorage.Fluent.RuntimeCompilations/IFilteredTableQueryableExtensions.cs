using System.Linq.Expressions;
using TableStorage.Linq;

namespace TableStorage.Fluent;

public static class IFilteredTableQueryableExtensions
{
    extension<T1, T2>(IFilteredTableQueryable<FluentPartitionTableEntity<T1, T2>> table)
        where T1 : class, IDictionary<string, object>, ITableEntity, new()
        where T2 : class, IDictionary<string, object>, ITableEntity, new()
    {
        public ITableEnumerable<TResult> Select<TResult>(Expression<Func<T1, TResult>> selector)
        {
            if (table is not FluentTableEntity2QueryHelper.FluentTableEntitySetQueryHelper<T1, T1, T2> helper)
            {
                throw new NotSupportedException("BatchUpdateTransactionAsync is not supported on the passed table type.");
            }

            CompilingTableSetQueryHelper<T1> compilingHelper = new(helper);
            return compilingHelper.SetFieldsAndTransform(selector);
        }
    }
}