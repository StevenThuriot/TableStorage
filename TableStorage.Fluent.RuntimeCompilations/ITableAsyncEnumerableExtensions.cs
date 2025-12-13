//using System.Linq.Expressions;
//using TableStorage.Linq;

//namespace TableStorage;

//public static class ITableAsyncEnumerableExtensions
//{
//    public static Task<int> BatchUpdateAsync<T>(this ITableAsyncEnumerable<T> table, Expression<Func<T, T>> update, CancellationToken token = default)
//        where T : class, ITableEntity, new()
//    {
//        if (table is not TableSetQueryHelper<T> helper)
//        {
//            throw new NotSupportedException("BatchUpdateTransactionAsync is not supported on the passed table type.");
//        }

//        CompilingTableSetQueryHelper<T> compilingHelper = new(helper);
//        return compilingHelper.BatchUpdateAsync(update, token);
//    }

//    public static Task<int> BatchUpdateTransactionAsync<T>(this ITableAsyncEnumerable<T> table, Expression<Func<T, T>> update, CancellationToken token = default)
//        where T : class, ITableEntity, new()
//    {
//        if (table is not TableSetQueryHelper<T> helper)
//        {
//            throw new NotSupportedException("BatchUpdateTransactionAsync is not supported on the passed table type.");
//        }

//        CompilingTableSetQueryHelper<T> compilingHelper = new(helper);
//        return compilingHelper.BatchUpdateTransactionAsync(update, token);
//    }
//}