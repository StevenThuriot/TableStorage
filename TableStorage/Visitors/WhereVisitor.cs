using System.Linq.Expressions;
using System.Reflection;

namespace TableStorage.Visitors;

internal sealed class WhereVisitor(string? partitionKeyProxy, string? rowKeyProxy, Type entityType) : ExpressionVisitor
{
    private readonly string? _partitionKeyProxy = partitionKeyProxy;
    private readonly string? _rowKeyProxy = rowKeyProxy;
    private readonly Type _entityType = entityType;

    protected override Expression VisitMember(MemberExpression node)
    {
        if (node.Expression.NodeType is ExpressionType.Parameter)
        {
            if (node.Expression.Type == _entityType)
            {
                var name = node.Member.Name;

                if (name == _partitionKeyProxy)
                {
                    node = Expression.Property(Expression.Convert(node.Expression, typeof(ITableEntity)), nameof(ITableEntity.PartitionKey));
                }
                else if (name == _rowKeyProxy)
                {
                    node = Expression.Property(Expression.Convert(node.Expression, typeof(ITableEntity)), nameof(ITableEntity.RowKey));
                }
            }
        }
        else if (node.Expression.NodeType is ExpressionType.Constant)
        {
            object container = ((ConstantExpression)node.Expression).Value;
            var memberInfo = node.Member;

            if (memberInfo.MemberType is MemberTypes.Field)
            {
                object value = ((FieldInfo)memberInfo).GetValue(container);
                return Expression.Constant(value);
            }

            if (memberInfo.MemberType is MemberTypes.Property)
            {
                object value = ((PropertyInfo)memberInfo).GetValue(container, null);
                return Expression.Constant(value);
            }
        }

        return base.VisitMember(node);
    }
}