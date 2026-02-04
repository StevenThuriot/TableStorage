using FastExpressionCompiler;
using System.Linq.Expressions;
using System.Reflection;

namespace TableStorage.Visitors;

internal sealed class MergeVisitor(ModelInfo? modelInfo, Func<Type, ModelInfo> getModelInfo) : ExpressionVisitor
{
    private readonly Func<Type, ModelInfo> _getModelInfo = getModelInfo;
    private ModelInfo? _modelInfo = modelInfo;
    private string? _partitionKeyProxy = modelInfo?.PartitionKey;
    private string? _rowKeyProxy = modelInfo?.RowKey;

    private readonly HashSet<string> _members = [];
    private readonly HashSet<string> _complexMembers = [];

    public TableEntity Entity { get; } = [];

    public IReadOnlyCollection<string> Members => _members;
    public IReadOnlyCollection<string> ComplexMembers => _complexMembers;

    public bool IsComplex => _complexMembers.Count > 0;
    public bool HasMerges => _members.Count > 0 || _complexMembers.Count > 0;

    private readonly Lazy<UsesParameterVisitor> _usesParameterVisitor = new(() => new());

    public Type? ConstructedType { get; private set; }

    protected override Expression VisitNew(NewExpression node)
    {
        ConstructedType = node.Type;

        if (_modelInfo is null)
        {
            ModelInfo modelInfo = _getModelInfo(node.Type);
            _partitionKeyProxy = modelInfo.PartitionKey;
            _rowKeyProxy = modelInfo.RowKey;
            _modelInfo = modelInfo;
        }

        return base.VisitNew(node);
    }

    protected override Expression VisitMember(MemberExpression memberExpression)
    {
        Expression expression = Visit(memberExpression.Expression);

        if (expression is ConstantExpression constantExpression)
        {
            object container = constantExpression.Value;
            MemberInfo member = memberExpression.Member;

            if (member.MemberType is MemberTypes.Field)
            {
                object value = ((FieldInfo)member).GetValue(container);
                return Expression.Constant(value);
            }

            if (member.MemberType is MemberTypes.Property)
            {
                object value = ((PropertyInfo)member).GetValue(container, null);
                return Expression.Constant(value);
            }
        }

        return base.VisitMember(memberExpression);
    }

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        Expression result = base.VisitMethodCall(node);

        if (result is MethodCallExpression call)
        {
            UsesParameterVisitor visitor = _usesParameterVisitor.Value.Reset();
            _ = visitor.Visit(call);

            if (!visitor.UsesParameter)
            {
                object value = Expression.Lambda(call, visitor.Parameters).CompileFast().DynamicInvoke();
                return Expression.Constant(value);
            }
        }

        return result;
    }

    protected override Expression VisitUnary(UnaryExpression node)
    {
        if (node.NodeType is ExpressionType.Convert)
        {
            Expression expression = Visit(node.Operand);
            if (expression is ConstantExpression constantExpression)
            {
                if (constantExpression.Value is null)
                {
                    return Expression.Constant(null, node.Type);
                }

                Type conversionType = Nullable.GetUnderlyingType(node.Type) ?? node.Type;

                object value = constantExpression.Value;

                if (constantExpression.Type != conversionType)
                {
                    value = Convert.ChangeType(value, conversionType);
                }

                return Expression.Constant(value, node.Type);
            }
        }

        return node;
    }

    protected override MemberAssignment VisitMemberAssignment(MemberAssignment node)
    {
        node = base.VisitMemberAssignment(node);

        string name = GetMemberName(node);

        if (node.Expression is ConstantExpression memberExpression)
        {
            _members.Add(name);
            object value = memberExpression.Value;

            if (memberExpression.Type.IsEnum || (Nullable.GetUnderlyingType(memberExpression.Type)?.IsEnum == true))
            {
                value = (int)value;
            }

            Entity[name] = value;
        }
        else
        {
            _complexMembers.Add(name);
        }

        return node;
    }

    private string GetMemberName(MemberAssignment node)
    {
        if (node.Member.Name == _partitionKeyProxy)
        {
            return nameof(ITableEntity.PartitionKey);
        }

        if (node.Member.Name == _rowKeyProxy)
        {
            return nameof(ITableEntity.RowKey);
        }

        return node.Member.Name;
    }
}