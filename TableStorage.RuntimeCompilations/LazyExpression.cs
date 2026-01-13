using System.Linq.Expressions;

namespace TableStorage;

internal sealed class LazyExpression<T, TResult>(Expression<Func<T, TResult>> expression) : Lazy<Func<T, TResult>>(() => LazyExpressionCompilation.CompilationFactory.Compile(expression))
{
    public TResult Invoke(T entity) => Value(entity);
    public static implicit operator LazyExpression<T, TResult>(Expression<Func<T, TResult>> expression) => new(expression);
}