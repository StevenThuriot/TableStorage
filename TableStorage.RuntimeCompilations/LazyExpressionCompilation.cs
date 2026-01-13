namespace TableStorage;

internal static class LazyExpressionCompilation
{
    internal static ICompilationFactory CompilationFactory { get; set; } = new CompilationFactory();
}