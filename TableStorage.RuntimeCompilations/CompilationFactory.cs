using FastExpressionCompiler;
using System.Linq.Expressions;

namespace TableStorage;

internal interface ICompilationFactory
{
    public Func<T, TResult> Compile<T, TResult>(Expression<Func<T, TResult>> expression);
}

internal sealed class CompilationFactory : ICompilationFactory
{
    public Func<T, TResult> Compile<T, TResult>(Expression<Func<T, TResult>> expression) => expression.CompileFast();
}