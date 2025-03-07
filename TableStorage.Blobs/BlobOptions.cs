
namespace TableStorage;

public sealed class BlobOptions
{
    internal BlobOptions() { }

    public bool CreateTableIfNotExists { get; set; } = true;

    public IBlobSerializer Serializer { get; set; } = default!;

    public bool IsHierarchical { get; set; }

    public bool UseTags { get; set; } = true;
}

public interface IBlobSerializer
{
    public abstract BinaryData Serialize<T>(T entity) where T : IBlobEntity;
    public abstract ValueTask<T?> DeserializeAsync<T>(Stream entity, CancellationToken cancellationToken) where T : IBlobEntity;
}