namespace TableStorage;

public interface IBlobEntity
{
    string PartitionKey { get; }
    string RowKey { get; }
}
