namespace TableStorage;

public interface IBlobEntity
{
    public string PartitionKey { get; }
    public string RowKey { get; }
    public object this[string key] { get; }
}
