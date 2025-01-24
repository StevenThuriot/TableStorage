
namespace TableStorage;

public sealed class BlobOptions
{
    internal BlobOptions() { }

    public JsonSerializerOptions? SerializerOptions { get; set; }
}