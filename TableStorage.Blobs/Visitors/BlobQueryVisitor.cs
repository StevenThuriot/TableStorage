using System.Linq.Expressions;
using System.Reflection;

namespace TableStorage.Visitors;

internal readonly struct TagCollectionEntry(string value, string operand)
{
    public string Operand { get; } = operand;
    public string Value { get; } = value;
}

internal readonly struct TagCollection
{
    private readonly Dictionary<string, HashSet<TagCollectionEntry>> _tags;

    public TagCollection()
    {
        _tags = [];
    }

    public void Set(string tag, string value, string operand)
    {
        if (!_tags.TryGetValue(tag, out HashSet<TagCollectionEntry>? count))
        {
            _tags[tag] = count = [];
        }

        count.Add(new(value, operand));
    }

    public bool IsUnique() => _tags.Values.All(x => x.Count is 1);

    public bool HasOthersThanDefaultKeys() => _tags.Keys.Count > 2 || _tags.Keys.Any(x => x is not ("partition" or "row"));

    public ILookup<string, TagCollectionEntry> ToLookup() => _tags.SelectMany(x => x.Value.Select(value => (x.Key, value))).ToLookup(x => x.Key, x => x.value);
}

internal sealed class BlobQueryVisitor(ModelInfo modelInfo, IEnumerable<string> tags) : ExpressionVisitor
{
    private readonly string _partitionKeyName = modelInfo.PartitionKey;
    private readonly string _rowKeyName = modelInfo.RowKey;
    private readonly IEnumerable<string> _tags = tags;

    private bool _simpleFilter = true;
    public bool SimpleFilter => _simpleFilter && Tags.IsUnique();

    private bool _error;
    private bool _operandError;
    public bool Error => _error || _operandError;
    public bool OperandError => !_error && _operandError;
    public string? Filter { get; private set; }

    public bool TagOnlyFilter { get; private set; } = true;

    public TagCollection Tags { get; } = new();
    private readonly Dictionary<Expression, string> _filters = [];

    protected override Expression VisitMember(MemberExpression node)
    {
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
            bool success = TryGetFilterFor(node.Left, node.Right, node.NodeType, out string? filter) ||
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
            if (_filters.TryGetValue(node.Left, out string? left) && _filters.TryGetValue(node.Right, out string? right))
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
                _error = true;
            }
        }

        return node;
    }

    private bool TryGetFilterFor(Expression left, Expression right, ExpressionType type, out string? filter)
    {
        if (left is MemberExpression member && member.Expression.NodeType is ExpressionType.Parameter or ExpressionType.Convert)
        {
            if (member.Member.Name == _partitionKeyName || member.Member.Name is nameof(IBlobEntity.PartitionKey))
            {
                string? value = GetValue(right)?.ToString();
                if (value is not null)
                {
                    string sqlOperand = ToSqlOperand(type);
                    filter = $"partition {sqlOperand} '{value}'";
                    Tags.Set("partition", value, sqlOperand);
                    return true;
                }
            }
            else if (member.Member.Name == _rowKeyName || member.Member.Name is nameof(IBlobEntity.RowKey))
            {
                string? value = GetValue(right)?.ToString();
                if (value is not null)
                {
                    string sqlOperand = ToSqlOperand(type);
                    filter = $"row {sqlOperand} '{value}'";
                    Tags.Set("row", value, sqlOperand);
                    return true;
                }
            }
            else if (_tags.Contains(member.Member.Name))
            {
                string? value = GetValue(right)?.ToString();
                if (value is not null)
                {
                    string sqlOperand = ToSqlOperand(type);
                    filter = $"""
                    "{member.Member.Name}" {sqlOperand} '{value}'
                    """;

                    Tags.Set(member.Member.Name, value, sqlOperand);
                    return true;
                }
            }
            else
            {
                _simpleFilter = false;
                TagOnlyFilter = false;
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
            object container = constant2.Value;
            MemberInfo memberInfo = member.Member;

            if (memberInfo.MemberType is MemberTypes.Field)
            {
                return ((FieldInfo)memberInfo).GetValue(container);
            }

            if (memberInfo.MemberType is MemberTypes.Property)
            {
                return ((PropertyInfo)memberInfo).GetValue(container, null);
            }
        }

        _error = true;
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
                _operandError = true;
                return "or";

            case ExpressionType.Equal:
                return "=";

            case ExpressionType.NotEqual:
                _operandError = true;
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
                _error = true;
                return "";
        }
    }
}