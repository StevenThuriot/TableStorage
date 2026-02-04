using TableStorage.Fluent;

namespace TableStorage;

public static class TableOptionsExtensions
{
    public static void EnableFluentCompilationAtRuntime(this TableOptions options)
    {
        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        LazyExpressionCompilation.CompilationFactory = new FluentRuntimeCompilationFactory(LazyExpressionCompilation.CompilationFactory);
        TableSetExtensions.Visitor = new FluentMergeVisitorAndValidator();
    }
}