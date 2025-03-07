using System.Text.Json.Serialization;

namespace TableStorage;

public static class BlobStorageSetup
{
    public static IBlobCreator BuildCreator(string connectionString, Action<BlobOptions>? configure = null)
    {
        BlobOptions options = new();

        if (configure is not null)
        {
            configure(options);
        }

        options.Serializer ??= JsonBlobSerializer.Instance;

        BlobStorageFactory factory = new(connectionString, options.CreateTableIfNotExists);
        return new BlobSetCreator(factory, options);
    }

    private sealed class BlobSetCreator(BlobStorageFactory factory, BlobOptions options) : IBlobCreator
    {
        private readonly BlobStorageFactory _factory = factory;
        private readonly BlobOptions _options = options;

        BlobSet<T> IBlobCreator.CreateSet<T>(string tableName) => new(_factory, tableName, _options, null, null, []);
        BlobSet<T> IBlobCreator.CreateSet<T>(string tableName, params IReadOnlyCollection<string> tags) => new(_factory, tableName, _options, null, null, tags);

        BlobSet<T> IBlobCreator.CreateSet<T>(string tableName, string partitionKeyProxy, string rowKeyProxy, params IReadOnlyCollection<string> tags) => new(_factory, tableName, _options, partitionKeyProxy, rowKeyProxy, tags);
    }

    private sealed class JsonBlobSerializer : IBlobSerializer
    {
        public static readonly IBlobSerializer Instance = new JsonBlobSerializer();

        private JsonBlobSerializer() { }

        private readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault
        };

        public BinaryData Serialize<T>(T entity) where T : IBlobEntity => BinaryData.FromObjectAsJson(entity, _options);

        public async ValueTask<T?> DeserializeAsync<T>(Stream entity, CancellationToken cancellationToken) where T : IBlobEntity
        {
            BinaryData data = await BinaryData.FromStreamAsync(entity, cancellationToken);
            return data.ToObjectFromJson<T>(_options);
        }
    }
}