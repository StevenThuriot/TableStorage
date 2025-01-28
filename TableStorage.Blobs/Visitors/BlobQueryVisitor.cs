using System.Linq.Expressions;
using System.Reflection;

namespace TableStorage.Visitors;

internal sealed class BlobQueryVisitor(string? partitionKeyProxy, string? rowKeyProxy) : ExpressionVisitor
{
    private readonly string _partitionKeyName = partitionKeyProxy ?? nameof(IBlobEntity.PartitionKey);
    private readonly string _rowKeyName = rowKeyProxy ?? nameof(IBlobEntity.RowKey);

    private bool _simpleFilter = true;
    public bool SimpleFilter
    {
        get => _simpleFilter && _partitionKeyValues.Count < 2 && _rowKeyValues.Count < 2;
    }

    public IReadOnlyCollection<string> PartitionKeys => _partitionKeyValues;
    public IReadOnlyCollection<string> RowKeys => _rowKeyValues;

    public bool Error { get; private set; }
    public string? Filter { get; private set; }

    private readonly HashSet<string> _partitionKeyValues = [];
    private readonly HashSet<string> _rowKeyValues = [];
    private readonly Dictionary<Expression, string> _filters = [];

    protected override Expression VisitBinary(BinaryExpression node)
    {
        var result = base.VisitBinary(node);

        if (node.Left is not BinaryExpression && node.Right is not BinaryExpression)
        {
            bool success = TryGetFilterFor(node.Left, node.Right, node.NodeType, out var filter) ||
                            TryGetFilterFor(node.Right, node.Left, node.NodeType, out filter);

            if (success)
            {
                _filters[node] = filter!;
            }
            else
            {
                Error = true;
            }
        }
        else
        {
            if (_filters.TryGetValue(node.Left, out var left) && _filters.TryGetValue(node.Right, out var right))
            {
                _filters[node] = Filter = $"{left} {ToSqlOperand(node.NodeType)} {right}";
            }
            else
            {
                Error = true;
            }
        }

        return result;
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
                    _partitionKeyValues.Add(value);
                    return true;
                }
            }
            else if (member.Member.Name == _rowKeyName)
            {
                var value = GetValue(right)?.ToString();
                if (value is not null)
                {
                    filter = $"row {ToSqlOperand(type)} '{GetValue(right)}'";
                    _rowKeyValues.Add(value);
                    return true;
                }
            }
            //TODO:
            //else if (_tagList.Contains(member.Member.Name))
            //{
            //    filter = $"""
            //        "{member.Member.Name}" {ToSqlOperand(type)} '{GetValue(right)}'
            //        """;
            //    return true;
            //}
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