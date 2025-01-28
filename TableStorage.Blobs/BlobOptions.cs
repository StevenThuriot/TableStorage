
namespace TableStorage;

public sealed class BlobOptions
{
    internal BlobOptions() { }

    public bool CreateTableIfNotExists { get; set; } = true;

    public JsonSerializerOptions? SerializerOptions { get; set; }

    public bool IsHierarchical { get; set; }
}