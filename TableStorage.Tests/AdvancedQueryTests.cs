using TableStorage.Tests.Infrastructure;
using TableStorage.Tests.Models;

namespace TableStorage.Tests;

/// <summary>
/// Tests for advanced query operations including Distinct, Take, ExistsIn, and NotExistsIn
/// Covers both table storage and blob storage advanced queries
/// </summary>
public class AdvancedQueryTests(AzuriteFixture azuriteFixture) : AzuriteTestBase(azuriteFixture)
{
    #region Table Storage Advanced Query Tests

    [Fact]
    public async Task Table_Where_WithTake_ShouldLimitResults()
    {
        // Arrange
        for (int i = 0; i < 5; i++)
        {
            await Context.Models1.UpsertEntityAsync(new()
            {
                PrettyName = "root",
                PrettyRow = Guid.NewGuid().ToString("N"),
                MyProperty1 = 5 + i,
                MyProperty2 = $"test {i}"
            }, TestContext.Current.CancellationToken);
        }

        // Act
        List<Model> list1 = await Context.Models1
            .Where(x => x.PrettyName == "root")
            .Where(x => x.MyProperty1 > 2)
            .Take(3)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.True(list1.Count <= 3);
        Assert.All(list1, x =>
        {
            Assert.NotNull(x.PrettyName);
            Assert.NotNull(x.PrettyRow);
            Assert.NotEqual(0, x.MyProperty1);
            Assert.NotNull(x.MyProperty2);
        });
    }

    [Fact]
    public async Task Table_Distinct_ShouldRemoveDuplicates()
    {
        // Arrange
        await Context.Models1.UpsertEntityAsync(new()
        {
            PrettyName = "root",
            PrettyRow = Guid.NewGuid().ToString("N"),
            MyProperty1 = 5,
            MyProperty2 = "duplicate"
        }, TestContext.Current.CancellationToken);

        await Context.Models1.UpsertEntityAsync(new()
        {
            PrettyName = "root",
            PrettyRow = Guid.NewGuid().ToString("N"),
            MyProperty1 = 5,
            MyProperty2 = "duplicate"
        }, TestContext.Current.CancellationToken);

        // Act
        List<Model> list2 = await Context.Models1
            .Where(x => x.PrettyName == "root")
            .Where(x => x.MyProperty1 > 2)
            .Take(3)
            .Distinct(FuncComparer.Create((Model x) => x.MyProperty1))
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(list2);
        Assert.All(list2, x =>
        {
            Assert.NotNull(x.PrettyName);
            Assert.NotNull(x.PrettyRow);
            Assert.NotEqual(0, x.MyProperty1);
            Assert.NotNull(x.MyProperty2);
        });
    }

    [Fact]
    public async Task Table_Distinct_WithStringComparer_ShouldUseCaseInsensitiveComparison()
    {
        // Arrange
        await Context.Models1.UpsertEntityAsync(new()
        {
            PrettyName = "root",
            PrettyRow = Guid.NewGuid().ToString("N"),
            MyProperty1 = 5,
            MyProperty2 = "Duplicate"
        }, TestContext.Current.CancellationToken);

        await Context.Models1.UpsertEntityAsync(new()
        {
            PrettyName = "root",
            PrettyRow = Guid.NewGuid().ToString("N"),
            MyProperty1 = 5,
            MyProperty2 = "duplicate"
        }, TestContext.Current.CancellationToken);

        // Act
        List<Model> list3 = await Context.Models1
            .Where(x => x.PrettyName == "root")
            .Where(x => x.MyProperty1 > 2)
            .Distinct(FuncComparer.Create((Model x) => x.MyProperty2, StringComparer.OrdinalIgnoreCase))
            .Take(3)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(list3);
        Assert.All(list3, x =>
        {
            Assert.NotNull(x.PrettyName);
            Assert.NotNull(x.PrettyRow);
            Assert.NotEqual(0, x.MyProperty1);
            Assert.NotNull(x.MyProperty2);
        });
    }

    [Fact]
    public async Task Table_ExistsIn_ShouldFilterByProvidedValues()
    {
        // Arrange
        await Context.Models1.UpsertEntityAsync(new()
        {
            PrettyName = "root",
            PrettyRow = Guid.NewGuid().ToString("N"),
            MyProperty1 = 1,
            MyProperty2 = "test"
        }, TestContext.Current.CancellationToken);

        await Context.Models1.UpsertEntityAsync(new()
        {
            PrettyName = "root",
            PrettyRow = Guid.NewGuid().ToString("N"),
            MyProperty1 = 2,
            MyProperty2 = "test2"
        }, TestContext.Current.CancellationToken);

        // Act
        List<Model> exists = await Context.Models1
            .ExistsIn(x => x.MyProperty1, [1, 2, 3, 4])
            .Where(x => x.PrettyName == "root")
            .Where(x => x.MyProperty1 < 3)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.NotEmpty(exists);
    }

    #endregion

    #region Blob Storage Advanced Query Tests

#if !PublishAot
    [Fact]
    public async Task Blob_ExistsIn_ShouldFilterBlobsByKey()
    {
        // Arrange
        await Context.Models4Blob.DeleteAllEntitiesAsync("root", TestContext.Current.CancellationToken);
        await Context.Models4Blob.AddEntityAsync(new()
        {
            PrettyPartition = "root",
            PrettyRow = "pretty1",
            MyProperty1 = 1,
            MyProperty2 = "test 1"
        }, TestContext.Current.CancellationToken);

        await Context.Models4Blob.AddEntityAsync(new()
        {
            PrettyPartition = "root",
            PrettyRow = "pretty2",
            MyProperty1 = 2,
            MyProperty2 = "test 2"
        }, TestContext.Current.CancellationToken);

        await Context.Models4Blob.AddEntityAsync(new()
        {
            PrettyPartition = "root",
            PrettyRow = "pretty3",
            MyProperty1 = 3,
            MyProperty2 = "test 3"
        }, TestContext.Current.CancellationToken);

        // Act
        List<Model4> blobSearch = await Context.Models4Blob
            .Where(x => x.PrettyPartition == "root")
            .ExistsIn(x => x.PrettyRow, ["pretty1", "pretty2", "pretty4"])
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(blobSearch);
        Assert.Equal(2, blobSearch.Count);
        Assert.All(blobSearch, x => Assert.Equal("root", x.PrettyPartition));
        Assert.Contains(blobSearch, x => x.PrettyRow == "pretty1");
        Assert.Contains(blobSearch, x => x.PrettyRow == "pretty2");
    }

    [Fact]
    public async Task Blob_ExistsIn_WithMultipleConditionsAndNotExistsIn_ShouldFilterCorrectly()
    {
        // Arrange
        await Context.Models4Blob.DeleteAllEntitiesAsync("root", TestContext.Current.CancellationToken);
        await Context.Models4Blob.AddEntityAsync(new()
        {
            PrettyPartition = "root",
            PrettyRow = "pretty1",
            MyProperty1 = 1,
            MyProperty2 = "test 1"
        }, TestContext.Current.CancellationToken);

        await Context.Models4Blob.AddEntityAsync(new()
        {
            PrettyPartition = "root",
            PrettyRow = "pretty2",
            MyProperty1 = 2,
            MyProperty2 = "test 2"
        }, TestContext.Current.CancellationToken);

        await Context.Models4Blob.AddEntityAsync(new()
        {
            PrettyPartition = "root",
            PrettyRow = "pretty3",
            MyProperty1 = 3,
            MyProperty2 = "test 3"
        }, TestContext.Current.CancellationToken);

        // Act
        List<Model4> blobSearch = await Context.Models4Blob
            .ExistsIn(x => x.PrettyPartition, ["root", "root2"])
            .ExistsIn(x => x.PrettyRow, ["pretty1", "pretty2", "pretty4"])
            .NotExistsIn(x => x.PrettyRow, ["pretty3"])
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(blobSearch);
        Assert.Equal(2, blobSearch.Count);
        Assert.All(blobSearch, x => Assert.Equal("root", x.PrettyPartition));
        Assert.Contains(blobSearch, x => x.PrettyRow == "pretty1");
        Assert.Contains(blobSearch, x => x.PrettyRow == "pretty2");
    }

    [Fact]
    public async Task Blob_ExistsIn_WithPropertyFilter_ShouldCombineFilters()
    {
        // Arrange
        await Context.Models4Blob.DeleteAllEntitiesAsync("root", TestContext.Current.CancellationToken);
        await Context.Models4Blob.AddEntityAsync(new()
        {
            PrettyPartition = "root",
            PrettyRow = "pretty1",
            MyProperty1 = 1,
            MyProperty2 = "test 1"
        }, TestContext.Current.CancellationToken);

        await Context.Models4Blob.AddEntityAsync(new()
        {
            PrettyPartition = "root",
            PrettyRow = "pretty2",
            MyProperty1 = 2,
            MyProperty2 = "test 2"
        }, TestContext.Current.CancellationToken);

        await Context.Models4Blob.AddEntityAsync(new()
        {
            PrettyPartition = "root",
            PrettyRow = "pretty3",
            MyProperty1 = 3,
            MyProperty2 = "test 3"
        }, TestContext.Current.CancellationToken);

        // Act
        List<Model4> blobSearch = await Context.Models4Blob
            .ExistsIn(x => x.PrettyPartition, ["root", "root2"])
            .ExistsIn(x => x.PrettyRow, ["pretty1", "pretty2", "pretty4"])
            .NotExistsIn(x => x.PrettyRow, ["pretty3"])
            .Where(x => x.MyProperty1 == 2)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(blobSearch);
        Assert.Single(blobSearch);
        Assert.All(blobSearch, x => Assert.Equal("root", x.PrettyPartition));
        Assert.All(blobSearch, x => Assert.Equal("pretty2", x.PrettyRow));
        Assert.All(blobSearch, x => Assert.Equal(2, x.MyProperty1));
    }
#else
    [Fact]
    public void ExistsIn_IsDisabledForPublishAot()
    {
        // This test exists to ensure the test project compiles with PublishAot
        Assert.True(true);
    }
#endif

    #endregion
}
