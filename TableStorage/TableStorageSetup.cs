using Microsoft.Extensions.DependencyInjection;

namespace TableStorage;
public static class TableStorageSetup
{
    public static ICreator BuildCreator(string connectionString, Action<TableOptions>? configure = null)
    {
        TableStorageFactory factory = new(connectionString);
        TableOptions options = new();

        if (configure is not null)
        {
            configure(options);
        }

        return new Creator(factory, options);
    }
}
