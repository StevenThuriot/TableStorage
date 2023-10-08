namespace TableStorage;

public interface ICreator
{
    public TableSet<T> CreateSet<T>(string tableName) where T : class, ITableEntity, new();
}

internal sealed class Creator : ICreator
{
    private readonly TableStorageFactory _factory;
    private readonly TableOptions _options;

    public Creator(TableStorageFactory factory, TableOptions options)
    {
        _factory = factory;
        _options = options;
    }

    TableSet<T> ICreator.CreateSet<T>(string tableName)
    {
        return new TableSet<T>(_factory, tableName, _options);
    }
}
