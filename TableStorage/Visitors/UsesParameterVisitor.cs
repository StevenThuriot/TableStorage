using System.Linq.Expressions;

namespace TableStorage.Visitors;

internal sealed class UsesParameterVisitor : ExpressionVisitor
{
    private readonly HashSet<ParameterExpression> _parameters = [];

    public IReadOnlyCollection<ParameterExpression> Parameters => _parameters;
    public bool UsesParameter => _parameters.Count is not 0;

    protected override Expression VisitParameter(ParameterExpression node)
    {
        _parameters.Add(node);
        return base.VisitParameter(node);
    }

    public UsesParameterVisitor Reset()
    {
        _parameters.Clear();
        return this;
    }
}