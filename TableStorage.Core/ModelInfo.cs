namespace TableStorage;

public sealed class ModelInfo(string? partitionKey, string? rowKey, Type entityType)
{
    public string PartitionKey { get; } = partitionKey ?? "PartitionKey";
    public string RowKey { get; } = rowKey ?? "RowKey";
    public Type EntityType { get; } = entityType;

    public bool HasProxies() => PartitionKey is not "PartitionKey" || RowKey is not "RowKey";
}
