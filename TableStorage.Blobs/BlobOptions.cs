
namespace TableStorage;

public sealed class BlobOptions
{
    internal BlobOptions() { }

    public bool CreateTableIfNotExists { get; set; } = true;

    //TODO: Decouple serialization so we could potentially use protobuf-net or other serializers
    public JsonSerializerOptions? SerializerOptions { get; set; }

    public bool IsHierarchical { get; set; }
}