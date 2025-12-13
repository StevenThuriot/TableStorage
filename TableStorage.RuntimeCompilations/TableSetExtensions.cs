using System.Linq.Expressions;
using TableStorage.Linq;
using TableStorage.Visitors;

namespace TableStorage;

public static class TableSetExtensions
{
    public static Task<int> BatchUpdateAsync<T>(this TableSet<T> table, Expression<Func<T, T>> update, CancellationToken token = default)
        where T : class, ITableEntity, new()
    {
        CompilingTableSetQueryHelper<T> helper = new(table);
        return helper.BatchUpdateAsync(update, token);
    }

    public static Task<int> BatchUpdateTransactionAsync<T>(this TableSet<T> table, Expression<Func<T, T>> update, CancellationToken token = default)
        where T : class, ITableEntity, new()
    {
        CompilingTableSetQueryHelper<T> helper = new(table);
        return helper.BatchUpdateTransactionAsync(update, token);
    }

    public static ITableEnumerable<TResult> Select<T, TResult>(this TableSet<T> table, Expression<Func<T, TResult>> selector)
        where T : class, ITableEntity, new()
    {
        CompilingTableSetQueryHelper<T> helper = new(table);
        return helper.SetFieldsAndTransform(selector);
    }

    public static Task UpdateAsync<T>(this TableSet<T> table, Expression<Func<T>> exp, CancellationToken cancellationToken = default)
        where T : class, ITableEntity, new()
    {
        TableEntity entity = VisitForMergeAndValidate(table.PartitionKeyProxy, table.RowKeyProxy, exp);

        if (entity.ETag == default)
        {
            entity.ETag = ETag.All;
        }

        return table.UpdateAsync(entity, cancellationToken);
    }

    public static Task UpsertAsync<T>(this TableSet<T> table, Expression<Func<T>> exp, CancellationToken cancellationToken = default)
        where T : class, ITableEntity, new()
    {
        TableEntity entity = VisitForMergeAndValidate(table.PartitionKeyProxy, table.RowKeyProxy, exp);
        return table.UpsertAsync(entity, cancellationToken);
    }

    private static TableEntity VisitForMergeAndValidate<T>(string? partitionKeyProxy, string? rowKeyProxy, Expression<Func<T>> exp)
        where T : class, ITableEntity, new()
    {
        MergeVisitor visitor = new(partitionKeyProxy, rowKeyProxy);
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