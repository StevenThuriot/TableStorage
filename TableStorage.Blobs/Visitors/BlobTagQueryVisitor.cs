using System.Linq.Expressions;
using System.Reflection;

namespace TableStorage.Visitors;

internal sealed class BlobTagAccessor(IDictionary<string, string> dictionary)
{
    private readonly IDictionary<string, string> _dictionary = dictionary;

    public string? Get(string key) => _dictionary.TryGetValue(key, out string? value) ? value : null;

    public static readonly MethodInfo MethodInfo = typeof(BlobTagAccessor).GetMethod(nameof(Get))!;
}

internal sealed class BlobTagQueryVisitor<T>(ModelInfo modelInfo, IReadOnlyCollection<string> tags) : ExpressionVisitor
    where T : IBlobEntity
{
    private readonly string _partitionKeyProxy = modelInfo.PartitionKey;
    private readonly string _rowKeyProxy = modelInfo.RowKey;
    private readonly IReadOnlyCollection<string> _tags = ["partition", "row", .. tags];
    private readonly Dictionary<ParameterExpression, ParameterExpression> _parameterMap = [];

    protected override Expression VisitParameter(ParameterExpression node)
    {
        if (node.Type != typeof(T))
        {
            return node;
        }

        if (!_parameterMap.TryGetValue(node, out ParameterExpression? parameter))
        {
            _parameterMap[node] = parameter = Expression.Parameter(typeof(BlobTagAccessor), node.Name);
        }

        return parameter;
    }

    protected override Expression VisitMember(MemberExpression node)
    {
        string name = node.Member.Name;
        if (name == _partitionKeyProxy || name is nameof(IBlobEntity.PartitionKey))
        {
            name = "partition";
        }
        else if (name == _rowKeyProxy || name is nameof(IBlobEntity.RowKey))
        {
            name = "row";
        }

        if (_tags.Contains(name))
        {
            var expression = Visit(node.Expression);

            if (expression.NodeType is ExpressionType.Convert && expression is UnaryExpression unaryExpression)
            {
                expression = unaryExpression.Operand;
            }

            return Expression.Call(expression, BlobTagAccessor.MethodInfo, Expression.Constant(name));
        }

        if (node.Expression is ConstantExpression constant)
        {
            object container = constant.Value;
            MemberInfo memberInfo = node.Member;

            if (memberInfo.MemberType is MemberTypes.Field)
            {
                return Expression.Constant(((FieldInfo)memberInfo).GetValue(container));
            }

            if (memberInfo.MemberType is MemberTypes.Property)
            {
                return Expression.Constant(((PropertyInfo)memberInfo).GetValue(container, null));
            }
        }

        return node;
    }

    protected override Expression VisitInvocation(InvocationExpression node)
    {
        if (node.Expression is LambdaExpression lambdaExpression)
        {
            List<ParameterExpression> parameters = [];
            List<Expression> arguments = [];

            var body = Visit(lambdaExpression.Body);

            if (body is not ConstantExpression)
            {
                parameters = [.. lambdaExpression.Parameters.Select(Visit).Cast<ParameterExpression>()];
                arguments = [.. node.Arguments.Select(Visit)];
                var lambda = Expression.Lambda(body, parameters);
                node = Expression.Invoke(lambda, arguments);
            }
            else
            {
                return body;
            }
        }

        return base.VisitInvocation(node);
    }

    protected override Expression VisitLambda<Tl>(Expression<Tl> node)
    {
        var parameters = node.Parameters.Select(Visit).Cast<ParameterExpression>().ToList();
        var body = Visit(node.Body);
        return Expression.Lambda(body, parameters);
    }

    protected override Expression VisitBinary(BinaryExpression node)
    {
        var left = Visit(node.Left);
        if (!IsTag(left))
        {
            return Expression.Constant(true);
        }

        var right = Visit(node.Right);
        if (!IsTag(right))
        {
            return Expression.Constant(true);
        }

        if (node.NodeType is not (ExpressionType.And
                                  or ExpressionType.AndAlso
                                  or ExpressionType.Or
                                  or ExpressionType.OrElse
                                  or ExpressionType.ExclusiveOr))
        {
            if (left is ConstantExpression leftConstant && leftConstant.Type != typeof(string))
            {
                left = Expression.Constant(leftConstant.Value?.ToString());
            }

            if (right is ConstantExpression rightConstant && rightConstant.Type != typeof(string))
            {
                right = Expression.Constant(rightConstant.Value?.ToString());
            }
        }

        return Expression.MakeBinary(node.NodeType, left, right);

        bool IsTag(Expression expression)
        {
            if (expression is MemberExpression member)
            {
                string name = member.Member.Name;
                if (name == _partitionKeyProxy || name is nameof(IBlobEntity.PartitionKey))
                {
                    name = "partition";
                }
                else if (name == _rowKeyProxy || name is nameof(IBlobEntity.RowKey))
                {
                    name = "row";
                }

                if (!_tags.Contains(name))
                {
                    return false;
                }
            }

            return true;
        }
    }
}