namespace TableStorage;

public interface IBlobCreator
{
    public BlobSet<T> CreateSet<T>(string tableName, Func<Type, ModelInfo> infoProvider, params IReadOnlyCollection<string> tags) where T : class, IBlobEntity, new();
    public AppendBlobSet<T> CreateAppendSet<T>(string tableName, Func<Type, ModelInfo> infoProvider, params IReadOnlyCollection<string> tags) where T : class, IBlobEntity, new();
}