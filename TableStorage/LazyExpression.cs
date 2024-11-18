using FastExpressionCompiler;
using System.Linq.Expressions;

namespace TableStorage;

internal class LazyExpression<T>(Expression<Func<T, T>> expression) : Lazy<Func<T, T>>(() => expression.CompileFast())
{
    public T Invoke(T entity) => Value(entity);

    public static implicit operator LazyExpression<T>(Expression<Func<T, T>> expression) => new(expression);
}