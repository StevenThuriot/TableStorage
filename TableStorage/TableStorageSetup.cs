namespace TableStorage;
public static class TableStorageSetup
{
    public static ICreator BuildCreator(string connectionString, Action<TableOptions>? configure = null)
    {
        TableOptions options = new();

        if (configure is not null)
        {
            configure(options);
        }

        TableStorageFactory factory = new(connectionString, options.CreateTableIfNotExists);
        return new Creator(factory, options);
    }
}
