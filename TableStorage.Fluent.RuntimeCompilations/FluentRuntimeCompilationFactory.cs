using System.Linq.Expressions;

namespace TableStorage.Fluent;

internal sealed class FluentRuntimeCompilationFactory(ICompilationFactory innerFactory) : ICompilationFactory
{
    private readonly ICompilationFactory _innerFactory = innerFactory;

    public Func<T, TResult> Compile<T, TResult>(Expression<Func<T, TResult>> expression)
    {
        if (FluentEntityInfo<T>.IsFluent)
        {
            ParameterExpression parameterExpression = expression.Parameters[0];
            FluentVisitor visitor = new(parameterExpression);
            expression = visitor.VisitAndConvert(expression, nameof(Compile));
        }

        return _innerFactory.Compile(expression);
    }
}