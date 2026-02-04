using System.Linq.Expressions;

namespace TableStorage.Visitors;

internal sealed class MergeVisitorAndValidator : IMergeVisitorAndValidator
{
    public TableEntity VisitForMergeAndValidate<T>(ModelInfo modelInfo, Func<Type, ModelInfo> getModelInfo, Expression<Func<T>> exp)
        where T : class, ITableEntity, new()
    {
        MergeVisitor visitor = new(modelInfo, getModelInfo);
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