namespace TableStorage;

public interface ICreator
{
    public TableSet<T> CreateSet<T>(string tableName) where T : class, ITableEntity, new();
    public TableSet<T> CreateSet<T>(string tableName, string partitionKeyProxy, string rowKeyProxy) where T : class, ITableEntity, new();
    public TableSet<T> CreateSetWithChangeTracking<T>(string tableName) where T : class, ITableEntity, IChangeTracking, new();
    public TableSet<T> CreateSetWithChangeTracking<T>(string tableName, string partitionKeyProxy, string rowKeyProxy) where T : class, ITableEntity, IChangeTracking, new();
}
