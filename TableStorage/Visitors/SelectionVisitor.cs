using System.Linq.Expressions;

namespace TableStorage.Visitors;

internal sealed class SelectionVisitor(string? partitionKeyProxy, string? rowKeyProxy) : ExpressionVisitor
{
    private readonly string? _partitionKeyProxy = partitionKeyProxy;
    private readonly string? _rowKeyProxy = rowKeyProxy;

    public readonly HashSet<string> Members = [];

    protected override Expression VisitMember(MemberExpression node)
    {
        string name = node.Member.Name;

        if (name == _partitionKeyProxy)
        {
            name = nameof(ITableEntity.PartitionKey);
            node = Expression.Property(Expression.Convert(node.Expression, typeof(ITableEntity)), nameof(ITableEntity.PartitionKey));
        }
        else if (name == _rowKeyProxy)
        {
            name = nameof(ITableEntity.RowKey);
            node = Expression.Property(Expression.Convert(node.Expression, typeof(ITableEntity)), nameof(ITableEntity.RowKey));
        }

        Members.Add(name);
        return base.VisitMember(node);
    }
}