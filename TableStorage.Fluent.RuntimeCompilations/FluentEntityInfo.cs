namespace TableStorage.Fluent;

internal static class FluentEntityInfo<T>
{
    public static readonly bool IsFluent = typeof(IFluentTableEntity).IsAssignableFrom(typeof(T));
    public static readonly bool IsFluentPartition = typeof(IFluentPartitionTableEntity).IsAssignableFrom(typeof(T));
    public static readonly bool IsFluentRow = typeof(IFluentRowTableEntity).IsAssignableFrom(typeof(T));
}