namespace TableStorage;

public interface ICreator
{
    public TableSet<T> CreateSet<T>(string tableName, Func<Type, ModelInfo> infoProvider) where T : class, ITableEntity, new();
    public TableSet<T> CreateSetWithChangeTracking<T>(string tableName, Func<Type, ModelInfo> infoProvider) where T : class, ITableEntity, IChangeTracking, new();
}