using TableStorage.Tests.Infrastructure;
using TableStorage.Tests.Models;

namespace TableStorage.Tests;

/// <summary>
/// Tests for batch operations including BatchUpdate and BatchDelete transactions
/// Covers both table storage batch operations
/// </summary>
public class BatchOperationTests(AzuriteFixture azuriteFixture) : AzuriteTestBase(azuriteFixture)
{
    [Fact]
    public async Task BatchDeleteTransactionAsync_ShouldDeleteMatchingEntities()
    {
        // Arrange
        await Context.Models2.UpsertEntityAsync(new()
        {
            PartitionKey = "root",
            PrettyRow = Guid.NewGuid().ToString("N"),
            MyProperty1 = 5,
            MyProperty2 = "test"
        }, TestContext.Current.CancellationToken);

        await Context.Models2.UpsertEntityAsync(new()
        {
            PartitionKey = "root",
            PrettyRow = Guid.NewGuid().ToString("N"),
            MyProperty1 = 5,
            MyProperty2 = "test2"
        }, TestContext.Current.CancellationToken);

        // Act
        int fiveCount = await Context.Models2.Where(x => x.MyProperty1 == 5).CountAsync(TestContext.Current.CancellationToken);
        int deleteCount = await Context.Models2.Where(x => x.MyProperty1 == 5).BatchDeleteTransactionAsync(TestContext.Current.CancellationToken);
        List<Model2> newModels2 = await Context.Models2.ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(fiveCount, deleteCount);
    }

    [Fact]
    public async Task BatchUpdateTransactionAsync_ShouldUpdateMatchingEntities()
    {
        // Arrange
        await Context.Models2.UpsertEntityAsync(new()
        {
            PartitionKey = "root",
            PrettyRow = Guid.NewGuid().ToString("N"),
            MyProperty1 = 1,
            MyProperty2 = "original"
        }, TestContext.Current.CancellationToken);

        await Context.Models2.UpsertEntityAsync(new()
        {
            PartitionKey = "root",
            PrettyRow = Guid.NewGuid().ToString("N"),
            MyProperty1 = 1,
            MyProperty2 = "original2"
        }, TestContext.Current.CancellationToken);

        // Act
        int updateCount = await Context.Models2
            .Where(x => x.MyProperty1 == 1)
            .BatchUpdateTransactionAsync(x => new() { MyProperty2 = "updated" }, TestContext.Current.CancellationToken);
        List<Model2> updatedModels = await Context.Models2
            .Where(x => x.MyProperty2 == "updated")
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(updateCount, updatedModels.Count);
    }
}
