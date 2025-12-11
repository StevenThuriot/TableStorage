using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

namespace TableStorage.Fluent;

public sealed class FluentVisitor(ParameterExpression parameter) : ExpressionVisitor
{
    private readonly ParameterExpression _parameter = parameter;

    protected override Expression VisitParameter(ParameterExpression node)
    {
        return _parameter;
    }

    protected override Expression VisitMember(MemberExpression node)
    {
        if (node.Expression is ParameterExpression)
        {
            // Replace property access with indexer access
            var constant = Expression.Constant(node.Member.Name);
            var indexerAccess = Expression.Call(_parameter, "get_Item", [], constant);

            // Convert the indexer access to the appropriate type
            return Expression.Convert(indexerAccess, node.Type);
        }

        return base.VisitMember(node);
    }
}