namespace TableStorage;

public sealed class ModelInfo(string? partitionKey, string? rowKey, Type entityType)
{
    public const string DefaultPartitionKey = "PartitionKey";
    public const string DefaultRowKey = "RowKey";

    public string PartitionKey { get; } = partitionKey ?? DefaultPartitionKey;
    public string RowKey { get; } = rowKey ?? DefaultRowKey;
    public Type EntityType { get; } = entityType;

    public bool HasProxies() => PartitionKey is not DefaultPartitionKey || RowKey is not DefaultRowKey;
}