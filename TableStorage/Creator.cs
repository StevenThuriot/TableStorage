namespace TableStorage;

public interface ICreator
{
    public TableSet<T> CreateSet<T>(string tableName) where T : class, ITableEntity, new();
    public TableSet<T> CreateSet<T>(string tableName, string partitionKeyProxy, string rowKeyProxy) where T : class, ITableEntity, new();
}

internal sealed class Creator(TableStorageFactory factory, TableOptions options) : ICreator
{
    private readonly TableStorageFactory _factory = factory;
    private readonly TableOptions _options = options;

    TableSet<T> ICreator.CreateSet<T>(string tableName) => new(_factory, tableName, _options);
    TableSet<T> ICreator.CreateSet<T>(string tableName, string partitionKeyProxy, string rowKeyProxy) => new(_factory, tableName, _options, partitionKeyProxy, rowKeyProxy);
}
