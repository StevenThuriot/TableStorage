using System.Linq.Expressions;
using System.Reflection;

namespace TableStorage.Visitors;

internal sealed class WhereVisitor(ModelInfo modelInfo) : ExpressionVisitor
{
    private readonly string? _partitionKeyProxy = modelInfo.PartitionKey;
    private readonly string? _rowKeyProxy = modelInfo.RowKey;
    private readonly Type _entityType = modelInfo.EntityType;

    protected override Expression VisitMember(MemberExpression node)
    {
        if (node.Expression.NodeType is ExpressionType.Parameter)
        {
            if (node.Expression.Type == _entityType)
            {
                string name = node.Member.Name;

                if (name == _partitionKeyProxy && name is not nameof(ITableEntity.PartitionKey))
                {
                    node = Expression.Property(Expression.Convert(node.Expression, typeof(ITableEntity)), nameof(ITableEntity.PartitionKey));
                }
                else if (name == _rowKeyProxy && name is not nameof(ITableEntity.RowKey))
                {
                    node = Expression.Property(Expression.Convert(node.Expression, typeof(ITableEntity)), nameof(ITableEntity.RowKey));
                }
            }
        }
        else if (node.Expression.NodeType is ExpressionType.Constant)
        {
            object container = ((ConstantExpression)node.Expression).Value;
            MemberInfo memberInfo = node.Member;

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