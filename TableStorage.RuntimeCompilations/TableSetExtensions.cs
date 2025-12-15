using System.Linq.Expressions;
using TableStorage.Linq;
using TableStorage.Visitors;

namespace TableStorage;

public static class TableSetExtensions
{
    extension<T>(TableSet<T> table) where T : class, ITableEntity, new()
    {
        public Task<int> BatchUpdateAsync(Expression<Func<T, T>> update, CancellationToken token = default)
        {
            CompilingTableSetQueryHelper<T> helper = new(table);
            return helper.BatchUpdateAsync(update, token);
        }

        public Task<int> BatchUpdateTransactionAsync(Expression<Func<T, T>> update, CancellationToken token = default)
        {
            CompilingTableSetQueryHelper<T> helper = new(table);
            return helper.BatchUpdateTransactionAsync(update, token);
        }

        public ITableEnumerable<TResult> Select<TResult>(Expression<Func<T, TResult>> selector)
        {
            CompilingTableSetQueryHelper<T> helper = new(table);
            return helper.SetFieldsAndTransform(selector);
        }

        public Task UpdateAsync(Expression<Func<T>> exp, CancellationToken cancellationToken = default)
        {
            TableEntity entity = VisitForMergeAndValidate(table.ModelInfo, exp);

            if (entity.ETag == default)
            {
                entity.ETag = ETag.All;
            }

            return table.UpdateAsync(entity, cancellationToken);
        }

        public Task UpsertAsync(Expression<Func<T>> exp, CancellationToken cancellationToken = default)
        {
            TableEntity entity = VisitForMergeAndValidate(table.ModelInfo, exp);
            return table.UpsertAsync(entity, cancellationToken);
        }
    }

    internal static TableEntity VisitForMergeAndValidate<T>(ModelInfo modelInfo, Expression<Func<T>> exp)
        where T : class, ITableEntity, new()
    {
        MergeVisitor visitor = new(modelInfo);
        _ = visitor.Visit(exp);

        TableEntity entity = visitor.Entity;

        if (entity.Count is 0 || visitor.IsComplex)
        {
            throw new NotSupportedException("Merge expression is not supported");
        }

        if (entity.PartitionKey is null)
        {
            throw new NotSupportedException("PartitionKey is a required field to be able to merge");
        }

        if (entity.RowKey is null)
        {
            throw new NotSupportedException("RowKey is a required field to be able to merge");
        }

        return entity;
    }
}