using TableStorage.Tests.Infrastructure;
using TableStorage.Tests.Models;

namespace TableStorage.Tests;

/// <summary>
/// Tests for mutation operations including Add, Upsert, Update, and Delete
/// Covers both table storage and blob storage mutations
/// </summary>
public class MutationTests(AzuriteFixture azuriteFixture) : AzuriteTestBase(azuriteFixture)
{
    #region Table Storage Mutation Tests

    [Fact]
    public async Task Table_UpdateAsync_ShouldMergeProperties()
    {
        // Arrange
        Model mergeTest = new()
        {
            PrettyName = "root",
            PrettyRow = Guid.NewGuid().ToString("N"),
            MyProperty1 = 1,
            MyProperty2 = "original"
        };
        await Context.Models1.UpsertEntityAsync(mergeTest, TestContext.Current.CancellationToken);

        // Act
        await Context.Models1.UpdateAsync(() => new()
        {
            PrettyName = "root",
            PrettyRow = mergeTest.PrettyRow,
            MyProperty1 = 5
        }, TestContext.Current.CancellationToken);

        int result = await Context.Models1
            .Where(x => x.PrettyName == "root" && x.PrettyRow == mergeTest.PrettyRow)
            .Select(x => x.MyProperty1)
            .FirstAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(5, result);
    }

    [Fact]
    public async Task Table_UpsertAsync_ShouldInsertOrUpdate()
    {
        // Arrange
        string rowKey = Guid.NewGuid().ToString("N");

        // Act - First upsert (insert)
        await Context.Models1.UpsertAsync(() => new()
        {
            PrettyName = "root",
            PrettyRow = rowKey,
            MyProperty1 = 5,
            MyProperty6 = ModelEnum.No
        }, TestContext.Current.CancellationToken);

        Model? first = await Context.Models1
            .Where(x => x.PrettyName == "root" && x.PrettyRow == rowKey)
            .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        // Act - Second upsert (update)
        await Context.Models1.UpsertAsync(() => new()
        {
            PrettyName = "root",
            PrettyRow = rowKey,
            MyProperty1 = 10,
            MyProperty6 = ModelEnum.Yes
        }, TestContext.Current.CancellationToken);

        Model? second = await Context.Models1
            .Where(x => x.PrettyName == "root" && x.PrettyRow == rowKey)
            .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(first);
        Assert.Equal(5, first.MyProperty1);
        Assert.NotNull(second);
        Assert.Equal(10, second.MyProperty1);
    }

    #endregion

    #region Blob Storage Mutation Tests

    [Fact]
    public async Task Blob_AddEntityAsync_ShouldAddBlobEntity()
    {
        // Arrange
        await Context.Models4Blob.DeleteAllEntitiesAsync("root", TestContext.Current.CancellationToken);
        string blobId1 = Guid.NewGuid().ToString("N");

        // Act
        await Context.Models4Blob.AddEntityAsync(new()
        {
            PrettyPartition = "root",
            PrettyRow = blobId1,
            MyProperty1 = 1,
            MyProperty2 = "test 1"
        }, TestContext.Current.CancellationToken);

        // Assert
        Model4? result = await Context.Models4Blob.GetEntityOrDefaultAsync("root", blobId1, TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Equal(1, result.MyProperty1);
        Assert.Equal("test 1", result.MyProperty2);
    }

    [Fact]
    public async Task Blob_DeleteAllEntitiesAsync_ShouldRemoveAllEntities()
    {
        // Arrange
        await Context.Models4Blob.DeleteAllEntitiesAsync("root", TestContext.Current.CancellationToken);
        await Context.Models4Blob.AddEntityAsync(new()
        {
            PrettyPartition = "root",
            PrettyRow = "test1",
            MyProperty1 = 1,
            MyProperty2 = "test"
        }, TestContext.Current.CancellationToken);

        await Context.Models4Blob.AddEntityAsync(new()
        {
            PrettyPartition = "root",
            PrettyRow = "test2",
            MyProperty1 = 2,
            MyProperty2 = "test"
        }, TestContext.Current.CancellationToken);

        // Act
        await Context.Models4Blob.DeleteAllEntitiesAsync("root", TestContext.Current.CancellationToken);
        List<Model4> result = await Context.Models4Blob
            .Where(x => x.PrettyPartition == "root")
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(result);
    }

    #endregion

    #region Utility Tests

    [Fact]
    public void GetTableSet_WithRandomName_ShouldReturnTableSet()
    {
        // Act
        TableSet<Model> unknown = Context.GetTableSet<Model>("randomname");

        // Assert
        Assert.NotNull(unknown);
    }

    #endregion
}
