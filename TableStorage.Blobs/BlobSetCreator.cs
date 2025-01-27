namespace TableStorage;

public interface IBlobCreator
{
    public BlobSet<T> CreateSet<T>(string tableName) where T : class, IBlobEntity, new();
    public BlobSet<T> CreateSet<T>(string tableName, string partitionKeyProxy, string rowKeyProxy) where T : class, IBlobEntity, new();
}
