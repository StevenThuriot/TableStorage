namespace TableStorage;

public interface IBlobEntity
{
    string PartitionKey { get; }
    string RowKey { get; }
    object this[string key] { get; }
}
