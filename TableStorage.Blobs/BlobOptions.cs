
namespace TableStorage;

public sealed class BlobOptions
{
    internal BlobOptions() { }

    public bool CreateTableIfNotExists { get; set; } = true;

    public BlobSerializer Serializer { get; set; } = default!;

    public bool IsHierarchical { get; set; }
}

public abstract class BlobSerializer
{
    public abstract byte[] Serialize<T>(T entity) where T : IBlobEntity;
    public abstract ValueTask<T?> DeserializeAsync<T>(Stream entity) where T : IBlobEntity;
}