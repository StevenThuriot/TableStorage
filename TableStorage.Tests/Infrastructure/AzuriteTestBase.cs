using Azure.Data.Tables;
using Microsoft.Extensions.DependencyInjection;
using TableStorage.Tests.Contexts;
using Testcontainers.Azurite;

namespace TableStorage.Tests.Infrastructure;

/// <summary>
/// xUnit collection fixture for sharing Azurite container across tests
/// </summary>
public class AzuriteFixture : IAsyncLifetime
{
    private AzuriteContainer? _azuriteContainer;

    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        _azuriteContainer = new AzuriteBuilder()
            .WithImage("mcr.microsoft.com/azure-storage/azurite:latest")
            .Build();

        await _azuriteContainer.StartAsync();
        ConnectionString = _azuriteContainer.GetConnectionString();
    }

    public async Task DisposeAsync()
    {
        if (_azuriteContainer != null)
        {
            await _azuriteContainer.DisposeAsync();
        }
    }
}

/// <summary>
/// xUnit collection definition to share Azurite fixture
/// </summary>
[CollectionDefinition("Azurite Collection")]
public class AzuriteCollection : ICollectionFixture<AzuriteFixture>
{
}

/// <summary>
/// Base class for tests that need access to Azurite and the TableStorage context
/// </summary>
[Collection("Azurite Collection")]
public abstract class AzuriteTestBase(AzuriteFixture azuriteFixture) : IAsyncLifetime
{
    protected AzuriteFixture AzuriteFixture { get; } = azuriteFixture;
    protected MyTableContext Context { get; private set; } = null!;
    private ServiceProvider? _serviceProvider;

    public virtual Task InitializeAsync()
    {
        var services = new ServiceCollection();

        services.AddMyTableContext(AzuriteFixture.ConnectionString,
            configure: x =>
            {
                x.CreateTableIfNotExists = true;
                x.EnableFluentCompilationAtRuntime();
            },
            configureBlobs: x =>
            {
                x.CreateContainerIfNotExists = true;
                x.Serializer = new HybridSerializer();
                x.EnableCompilationAtRuntime();
            });

        _serviceProvider = services.BuildServiceProvider();
        Context = _serviceProvider.GetRequiredService<MyTableContext>();

        // Clean up any existing data before each test
        return CleanupAllTables();
    }

    public virtual async Task DisposeAsync()
    {
        if (_serviceProvider != null)
        {
            await _serviceProvider.DisposeAsync();
        }
    }

    protected async Task CleanupAllTables()
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

        static async Task CleanTable<T>(TableSet<T> tableSet) where T : class, ITableEntity, new()
        {
            try
            {
                await tableSet.Where(x => true).BatchDeleteAsync();
            }
            catch
            {
                // Ignore errors during cleanup
            }
        }

        static async Task CleanBlobs<T>(BlobSet<T> tableSet)
            where T : IBlobEntity
        {
            try
            {
                await tableSet.Where(x => true).BatchDeleteAsync();
            }
            catch
            {
                // Ignore errors during cleanup
            }
        }

        static async Task CleanAppendBlobs<T>(AppendBlobSet<T> tableSet)
            where T : IBlobEntity
        {
            try
            {
                await tableSet.Where(x => true).BatchDeleteAsync();
            }
            catch
            {
                // Ignore errors during cleanup
            }
        }
    }
}