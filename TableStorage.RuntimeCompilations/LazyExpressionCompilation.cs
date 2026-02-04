namespace TableStorage;

internal static class LazyExpressionCompilation
{
    internal static ICompilationFactory CompilationFactory
    {
        get;
        set => field = value ?? throw new ArgumentNullException(nameof(value));
    } = new CompilationFactory();
}