using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace TableStorage;

public static class BlobStorageSetup
{
    public static IBlobCreator BuildCreator(string connectionString, Action<BlobOptions>? configure = null)
    {
        BlobServiceClient client = new(connectionString ?? throw new ArgumentNullException(nameof(connectionString)));
        return BuildCreator(client, configure);
    }

    public static IBlobCreator BuildCreator(IServiceProvider services, Action<BlobOptions>? configure = null)
    {
        BlobServiceClient client = ((BlobServiceClient)services.GetService(typeof(BlobServiceClient))) ?? throw new InvalidOperationException("BlobServiceClient not registered");
        return BuildCreator(client, configure);
    }

    private static BlobSetCreator BuildCreator(BlobServiceClient client, Action<BlobOptions>? configure)
    {
        BlobOptions options = new();

        if (configure is not null)
        {
            configure(options);
        }

        options.Serializer ??= JsonBlobSerializer.Instance;

        BlobStorageFactory factory = new(client, options.CreateContainerIfNotExists);
        return new(factory, options);
    }

    private sealed class BlobSetCreator(BlobStorageFactory factory, BlobOptions options) : IBlobCreator
    {
        private readonly BlobStorageFactory _factory = factory;
        private readonly BlobOptions _options = options;

        BlobSet<T> IBlobCreator.CreateSet<T>(string tableName, Func<Type, ModelInfo> infoProvider, params IReadOnlyCollection<string> tags) => new(_factory, tableName, _options, infoProvider, tags);
        AppendBlobSet<T> IBlobCreator.CreateAppendSet<T>(string tableName, Func<Type, ModelInfo> infoProvider, params IReadOnlyCollection<string> tags) => new(_factory, tableName, _options, infoProvider, tags);
    }

    private sealed class JsonBlobSerializer : IBlobSerializer
    {
        public static readonly IBlobSerializer Instance = new JsonBlobSerializer();

        private JsonBlobSerializer() { }

        private readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault
        };

        public BinaryData Serialize<T>(string _, T entity) where T : IBlobEntity => BinaryData.FromObjectAsJson(entity, _options);

        public async ValueTask<T?> DeserializeAsync<T>(string _, Stream entity, CancellationToken cancellationToken) where T : IBlobEntity
        {
            BinaryData data = await BinaryData.FromStreamAsync(entity, cancellationToken);
            return data.ToObjectFromJson<T>(_options);
        }
    }
}

public sealed class AotJsonBlobSerializer(JsonSerializerContext context) : IBlobSerializer
{
    private readonly JsonSerializerContext _context = context;

    public BinaryData Serialize<T>(string _, T entity) where T : IBlobEntity
        => BinaryData.FromBytes(JsonSerializer.SerializeToUtf8Bytes(entity, GetTypeInfo<T>()));

    public ValueTask<T?> DeserializeAsync<T>(string _, Stream entity, CancellationToken cancellationToken) where T : IBlobEntity
        => JsonSerializer.DeserializeAsync(entity, GetTypeInfo<T>(), cancellationToken);

    private JsonTypeInfo<T> GetTypeInfo<T>()
        => _context.GetTypeInfo(typeof(T)) as JsonTypeInfo<T> ?? throw new InvalidOperationException("No JsonTypeInfo for type " + typeof(T).FullName);
}
