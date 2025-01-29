namespace TableStorage;

public interface IBlobCreator
{
    public BlobSet<T> CreateSet<T>(string tableName) where T : class, IBlobEntity, new();
    public BlobSet<T> CreateSet<T>(string tableName, params IReadOnlyCollection<string> tags) where T : class, IBlobEntity, new();
    public BlobSet<T> CreateSet<T>(string tableName, string partitionKeyProxy, string rowKeyProxy, params IReadOnlyCollection<string> tags) where T : class, IBlobEntity, new();
}
