using System.Linq.Expressions;

namespace TableStorage.Visitors;

internal sealed class SelectionVisitor(ModelInfo modelInfo) : ExpressionVisitor
{
    private readonly string? _partitionKeyProxy = modelInfo.PartitionKey;
    private readonly string? _rowKeyProxy = modelInfo.RowKey;

    public readonly HashSet<string> Members = [];

    protected override Expression VisitMember(MemberExpression node)
    {
        string name = node.Member.Name;

        if (name == _partitionKeyProxy && name is not nameof(ITableEntity.PartitionKey))
        {
            name = nameof(ITableEntity.PartitionKey);
            node = Expression.Property(Expression.Convert(node.Expression, typeof(ITableEntity)), nameof(ITableEntity.PartitionKey));
        }
        else if (name == _rowKeyProxy && name is not nameof(ITableEntity.RowKey))
        {
            name = nameof(ITableEntity.RowKey);
            node = Expression.Property(Expression.Convert(node.Expression, typeof(ITableEntity)), nameof(ITableEntity.RowKey));
        }

        Members.Add(name);
        return base.VisitMember(node);
    }
}