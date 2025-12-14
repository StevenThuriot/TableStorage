using System.Linq.Expressions;

namespace TableStorage.Fluent;

internal sealed class FluentRuntimeCompilationFactory(ICompilationFactory innerFactory) : ICompilationFactory
{
    private readonly ICompilationFactory _innerFactory = innerFactory;

    public Func<T, TResult> Compile<T, TResult>(Expression<Func<T, TResult>> expression)
    {
        ParameterExpression parameterExpression = expression.Parameters[0];

        if (typeof(IFluentTableEntity).IsAssignableFrom(parameterExpression.Type))
        {
            FluentVisitor visitor = new(parameterExpression);
            expression = visitor.VisitAndConvert(expression, nameof(Compile));
        }

        return _innerFactory.Compile(expression);
    }
}