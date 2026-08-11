using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Microsoft.Extensions.DependencyInjection;
using TableStorage.Tests.Contexts;

namespace TableStorage.Tests;

public sealed class ClientRegistrationTests
{
    [Fact]
    public void BuildTableCreator_WithServiceProvider_UsesRegisteredClientAndConfiguration()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new TableServiceClient("UseDevelopmentStorage=true"));
        using ServiceProvider provider = services.BuildServiceProvider();
        bool configured = false;

        ICreator creator = TableStorageSetup.BuildCreator(provider, options =>
        {
            configured = true;
            options.CreateTableIfNotExists = CreateIfNotExistsMode.Once;
        });

        Assert.NotNull(creator);
        Assert.True(configured);
    }

    [Fact]
    public void BuildTableCreator_WithoutRegisteredClient_Throws()
    {
        using ServiceProvider provider = new ServiceCollection().BuildServiceProvider();

        Assert.Throws<InvalidOperationException>(() => TableStorageSetup.BuildCreator(provider));
    }

    [Fact]
    public void BuildBlobCreator_WithServiceProvider_UsesRegisteredClientAndConfiguration()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new BlobServiceClient("UseDevelopmentStorage=true"));
        using ServiceProvider provider = services.BuildServiceProvider();
        bool configured = false;

        IBlobCreator creator = BlobStorageSetup.BuildCreator(provider, options =>
        {
            configured = true;
            options.CreateContainerIfNotExists = CreateIfNotExistsMode.Once;
        });

        Assert.NotNull(creator);
        Assert.True(configured);
    }

    [Fact]
    public void BuildBlobCreator_WithoutRegisteredClient_Throws()
    {
        using ServiceProvider provider = new ServiceCollection().BuildServiceProvider();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => BlobStorageSetup.BuildCreator(provider));

        Assert.Equal("BlobServiceClient not registered", exception.Message);
    }

    [Fact]
    public void AddMyTableContext_WithoutConnectionString_UsesRegisteredClients()
    {
        var tableClient = new TableServiceClient("UseDevelopmentStorage=true");
        var blobClient = new BlobServiceClient("UseDevelopmentStorage=true");
        var services = new ServiceCollection();
        services.AddSingleton(tableClient);
        services.AddSingleton(blobClient);

        IServiceCollection returned = services.AddMyTableContext();
        using ServiceProvider provider = services.BuildServiceProvider();

        MyTableContext context = provider.GetRequiredService<MyTableContext>();

        Assert.Same(services, returned);
        Assert.Same(tableClient, provider.GetRequiredService<TableServiceClient>());
        Assert.Same(blobClient, provider.GetRequiredService<BlobServiceClient>());
        Assert.NotNull(context);
    }
}
