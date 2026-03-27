using Azure.Data.Tables;
using Microsoft.Extensions.DependencyInjection;
using TableStorage.Tests.Contexts;

namespace TableStorage.Tests.Infrastructure;

/// <summary>
/// xUnit collection fixture for sharing Azurite container across tests
/// </summary>
public class AzuriteFixture : IAsyncLifetime
{
#if TestContainers
    private AzuriteContainer? _azuriteContainer;
#endif

    public string ConnectionString { get; private set; } = "UseDevelopmentStorage=true";

    public async ValueTask InitializeAsync()
    {
#if TestContainers
        _azuriteContainer = new AzuriteBuilder("mcr.microsoft.com/azure-storage/azurite:latest").Build();

        await _azuriteContainer.StartAsync(TestContext.Current.CancellationToken);
        ConnectionString = _azuriteContainer.GetConnectionString();
#endif
    }

    public async ValueTask DisposeAsync()
    {
#if TestContainers
        if (_azuriteContainer is not null)
        {
            await _azuriteContainer.DisposeAsync();
        }
#endif
    }
}

/// <summary>
/// xUnit collection definition to share Azurite fixture
/// </summary>
[CollectionDefinition("Azurite Collection")]
public class AzuriteCollection : ICollectionFixture<AzuriteFixture>;

/// <summary>
/// Base class for tests that need access to Azurite and the TableStorage context
/// </summary>
[Collection("Azurite Collection")]
public abstract class AzuriteTestBase(AzuriteFixture azuriteFixture) : IAsyncLifetime
{
    protected AzuriteFixture AzuriteFixture { get; } = azuriteFixture;
    protected MyTableContext Context { get; private set; } = null!;
    private ServiceProvider? _serviceProvider;

    public virtual ValueTask InitializeAsync()
    {
        var services = new ServiceCollection();

        services.AddMyTableContext(AzuriteFixture.ConnectionString,
            configure: x =>
            {
                x.CreateTableIfNotExists = CreateIfNotExistsMode.Once;
                x.EnableFluentCompilationAtRuntime();
            },
            configureBlobs: x =>
            {
                x.CreateContainerIfNotExists = CreateIfNotExistsMode.Once;
                x.Serializer = new HybridSerializer();
                x.EnableCompilationAtRuntime();
            });

        _serviceProvider = services.BuildServiceProvider();
        Context = _serviceProvider.GetRequiredService<MyTableContext>();

        // Clean up any existing data before each test
        return CleanupAllTables();
    }

    public virtual async ValueTask DisposeAsync()
    {
        if (_serviceProvider is not null)
        {
            await _serviceProvider.DisposeAsync();
        }
    }

    protected async ValueTask CleanupAllTables()
    {
        await CleanTable(Context.Models1);
        await CleanTable(Context.Models2);
        await CleanTable(Context.Models3);
        await CleanTable(Context.Models4);
        await CleanTable(Context.Models5);
        await CleanTable(Context.FluentModels);
        await CleanTable(Context.FluentPartitionModels);
        await CleanTable(Context.FluentRowKeyModels);
        await CleanBlobs(Context.Models1Blob);
        await CleanBlobs(Context.Models4Blob);
        await CleanBlobs(Context.Models2Blob);
        await CleanAppendBlobs(Context.Models5Blob);
        await CleanAppendBlobs(Context.Models5BlobInJson);

        static async ValueTask CleanTable<T>(TableSet<T> tableSet) where T : class, ITableEntity, new()
        {
            try
            {
                await tableSet.Where(_ => true).BatchDeleteAsync(TestContext.Current.CancellationToken);
            }
            catch
            {
                // Ignore errors during cleanup
            }
        }

        static async ValueTask CleanBlobs<T>(BlobSet<T> tableSet)
            where T : IBlobEntity
        {
            try
            {
                await tableSet.Where(_ => true).BatchDeleteAsync(TestContext.Current.CancellationToken);
            }
            catch
            {
                // Ignore errors during cleanup
            }
        }

        static async ValueTask CleanAppendBlobs<T>(AppendBlobSet<T> tableSet)
            where T : IBlobEntity
        {
            try
            {
                await tableSet.Where(_ => true).BatchDeleteAsync(TestContext.Current.CancellationToken);
            }
            catch
            {
                // Ignore errors during cleanup
            }
        }
    }
}
