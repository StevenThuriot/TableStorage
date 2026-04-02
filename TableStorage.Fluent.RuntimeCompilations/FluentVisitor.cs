using System.Linq.Expressions;
using System.Reflection;

namespace TableStorage.Fluent;

internal sealed class FluentVisitor(ParameterExpression parameter) : ExpressionVisitor
{
    private readonly ParameterExpression _parameter = parameter;

    private static readonly MethodInfo s_getItemMethod = typeof(IDictionary<string, object>).GetMethod("get_Item")!;

    protected override Expression VisitParameter(ParameterExpression node) => _parameter;

    protected override Expression VisitMember(MemberExpression node)
    {
        if (node.Expression is ParameterExpression)
        {
            var constant = Expression.Constant(node.Member.Name);
            var indexerAccess = Expression.Call(_parameter, s_getItemMethod, constant);
            return Expression.Convert(indexerAccess, node.Type);
        }

        return base.VisitMember(node);
    }
}