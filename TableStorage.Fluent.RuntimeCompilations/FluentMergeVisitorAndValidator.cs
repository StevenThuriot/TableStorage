using System.Linq.Expressions;
using TableStorage.Visitors;

namespace TableStorage.Fluent;

internal sealed class FluentMergeVisitorAndValidator : IMergeVisitorAndValidator
{
    public TableEntity VisitForMergeAndValidate<T>(ModelInfo modelInfo, Func<Type, ModelInfo> getModelInfo, Expression<Func<T>> exp)
        where T : class, ITableEntity, new()
    {
        MergeVisitor visitor = new(null, getModelInfo);
        _ = visitor.Visit(exp);

        TableEntity entity = visitor.Entity;

        if (entity.Count is 0 || visitor.IsComplex)
        {
            throw new NotSupportedException("Merge expression is not supported");
        }

        string? constructedTypeName = visitor.ConstructedType?.Name;

        entity.PartitionKey ??= constructedTypeName is not null && FluentEntityInfo<T>.IsFluentPartition
            ? constructedTypeName
            : throw new NotSupportedException("PartitionKey is a required field to be able to merge");

        entity.RowKey ??= constructedTypeName is not null && FluentEntityInfo<T>.IsFluentRow
            ? constructedTypeName
            : throw new NotSupportedException("RowKey is a required field to be able to merge");

        return entity;
    }
}