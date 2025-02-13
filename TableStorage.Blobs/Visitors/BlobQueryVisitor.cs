using System.Linq.Expressions;
using System.Reflection;

namespace TableStorage.Visitors;

internal readonly struct TagCollection
{
    private readonly Dictionary<string, HashSet<string>> _tags;

    public TagCollection()
    {
        _tags = [];
    }

    public void Set(string tag, string value)
    {
        if (!_tags.TryGetValue(tag, out var count))
        {
            _tags[tag] = count = [];
        }

        count.Add(value);
    }

    public bool IsUnique() => _tags.Values.All(x => x.Count is 1);

    public bool HasOthersThanDefaultKeys() => _tags.Keys.Any(x => x is not "partition" and not "row");

    public ILookup<string, string> ToLookup() => _tags.SelectMany(x => x.Value.Select(value => (x.Key, value))).ToLookup(x => x.Key, x => x.value);
}

internal sealed class BlobQueryVisitor(string? partitionKeyProxy, string? rowKeyProxy, IEnumerable<string> tags) : ExpressionVisitor
{
    private readonly string _partitionKeyName = partitionKeyProxy ?? nameof(IBlobEntity.PartitionKey);
    private readonly string _rowKeyName = rowKeyProxy ?? nameof(IBlobEntity.RowKey);
    private readonly IEnumerable<string> _tags = tags;

    private bool _simpleFilter = true;
    public bool SimpleFilter
    {
        get => _simpleFilter && Tags.IsUnique();
    }

    public bool Error { get; private set; }
    public string? Filter { get; private set; }

    public TagCollection Tags { get; } = new();
    private readonly Dictionary<Expression, string> _filters = [];

    protected override Expression VisitMember(MemberExpression node)
    {
        if (node.Expression is ConstantExpression constant)
        {
            var container = constant.Value;
            var memberInfo = node.Member;

            if (memberInfo.MemberType is MemberTypes.Field)
            {
                return Expression.Constant(((FieldInfo)memberInfo).GetValue(container));
            }

            if (memberInfo.MemberType is MemberTypes.Property)
            {
                return Expression.Constant(((PropertyInfo)memberInfo).GetValue(container, null));
            }
        }

        return base.VisitMember(node);
    }

    protected override Expression VisitInvocation(InvocationExpression node)
    {
        if (node.Expression is LambdaExpression lambda)
        {
            return Visit(lambda.Body);
        }

        return base.VisitInvocation(node);
    }

    protected override Expression VisitBinary(BinaryExpression node)
    {
        node = (BinaryExpression)base.VisitBinary(node);

        if (node.Left is not BinaryExpression && node.Right is not BinaryExpression)
        {
            bool success = TryGetFilterFor(node.Left, node.Right, node.NodeType, out var filter) ||
                            TryGetFilterFor(node.Right, node.Left, node.NodeType, out filter);

            if (success)
            {
                _filters[node] = Filter = filter!;
            }
            else
            {
                _filters[node] = "";
                _simpleFilter = false;
            }
        }
        else
        {
            if (_filters.TryGetValue(node.Left, out var left) && _filters.TryGetValue(node.Right, out var right))
            {
                if (left is "")
                {
                    _filters[node] = Filter = right;
                }
                else if (right is "")
                {
                    _filters[node] = Filter = left;
                }
                else
                {
                    _filters[node] = Filter = $"{left} {ToSqlOperand(node.NodeType)} {right}";
                }
            }
            else if (_simpleFilter)
            {
                Error = true;
            }
        }

        return node;
    }

    private bool TryGetFilterFor(Expression left, Expression right, ExpressionType type, out string? filter)
    {
        if (left is MemberExpression member && member.Expression is ParameterExpression)
        {
            if (member.Member.Name == _partitionKeyName)
            {
                var value = GetValue(right)?.ToString();
                if (value is not null)
                {
                    filter = $"partition {ToSqlOperand(type)} '{value}'";
                    Tags.Set("partition", value);
                    return true;
                }
            }
            else if (member.Member.Name == _rowKeyName)
            {
                var value = GetValue(right)?.ToString();
                if (value is not null)
                {
                    filter = $"row {ToSqlOperand(type)} '{value}'";
                    Tags.Set("row", value);
                    return true;
                }
            }
            else if (_tags.Contains(member.Member.Name))
            {
                var value = GetValue(right)?.ToString();
                if (value is not null)
                {
                    filter = $"""
                    "{member.Member.Name}" {ToSqlOperand(type)} '{value}'
                    """;

                    Tags.Set(member.Member.Name, value);
                    return true;
                }
            }
            else
            {
                _simpleFilter = false;
            }
        }

        filter = null;
        return false;
    }

    private object? GetValue(Expression node)
    {
        if (node is ConstantExpression constant)
        {
            return constant.Value;
        }
        else if (node is MemberExpression member && member.Expression is ConstantExpression constant2)
        {
            var container = constant2.Value;
            var memberInfo = member.Member;

            if (memberInfo.MemberType is MemberTypes.Field)
            {
                return ((FieldInfo)memberInfo).GetValue(container);
            }

            if (memberInfo.MemberType is MemberTypes.Property)
            {
                return ((PropertyInfo)memberInfo).GetValue(container, null);
            }
        }

        Error = true;
        return null;
    }

    private string ToSqlOperand(ExpressionType type)
    {
        // https://learn.microsoft.com/en-us/rest/api/storageservices/find-blobs-by-tags?tabs=microsoft-entra-id#remarks
        switch (type)
        {
            case ExpressionType.And:
            case ExpressionType.AndAlso:
                return "and";

            case ExpressionType.Or:
            case ExpressionType.OrElse:
                Error = true; // Not supported ?
                return "or";

            case ExpressionType.Equal:
                return "=";

            case ExpressionType.NotEqual:
                Error = true; // Not supported ?
                return "!=";

            case ExpressionType.GreaterThan:
                return ">";

            case ExpressionType.GreaterThanOrEqual:
                return ">=";

            case ExpressionType.LessThan:
                return "<";

            case ExpressionType.LessThanOrEqual:
                return "<=";

            default:
                Error = true;
                return "";
        }
    }
}