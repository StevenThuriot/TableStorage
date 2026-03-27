using TableStorage.Tests.Infrastructure;
using TableStorage.Tests.Models;

namespace TableStorage.Tests;

/// <summary>
/// Tests for projection operations including SelectFields
/// Covers both table storage and blob storage projections
/// </summary>
public class ProjectionTests(AzuriteFixture azuriteFixture) : AzuriteTestBase(azuriteFixture)
{
    [Fact]
    public async Task SelectFields_ShouldReturnProxiedEntities()
    {
        // Arrange
        await Context.Models1.UpsertEntityAsync(new()
        {
            PrettyName = "root",
            PrettyRow = Guid.NewGuid().ToString("N"),
            MyProperty1 = 1,
            MyProperty2 = "test"
        }, TestContext.Current.CancellationToken);

        // Act
        List<Model> proxiedList = await Context.Models1
            .SelectFields(x => x.PrettyName == "root" && x.PrettyRow != "")
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.NotEmpty(proxiedList);
        Assert.All(proxiedList, x =>
        {
            Assert.Equal("root", x.PrettyName);
            Assert.NotEmpty(x.PrettyRow);
        });
    }

    [Fact]
    public async Task SelectFields_WithSpecificProperties_ShouldOnlyFillSelectedProperties()
    {
        // Arrange
        await Context.Models1.UpsertEntityAsync(new()
        {
            PrettyName = "root",
            PrettyRow = Guid.NewGuid().ToString("N"),
            MyProperty1 = 5,
            MyProperty2 = "test"
        }, TestContext.Current.CancellationToken);

        // Act
        Model? first1 = await Context.Models1
            .Where(x => x.PrettyName == "root")
            .Where(x => x.MyProperty1 > 2)
            .SelectFields(x => new { x.MyProperty2, x.MyProperty1 })
            .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(first1);
        Assert.Null(first1.PrettyName);
        Assert.Null(first1.PrettyRow);
        Assert.NotEqual(0, first1.MyProperty1);
        Assert.NotNull(first1.MyProperty2);
    }

    [Fact]
    public async Task SelectFields_WithSingleProperty_ShouldOnlyFillThatProperty()
    {
        // Arrange
        await Context.Models1.UpsertEntityAsync(new()
        {
            PrettyName = "root",
            PrettyRow = Guid.NewGuid().ToString("N"),
            MyProperty1 = 5,
            MyProperty2 = "test"
        }, TestContext.Current.CancellationToken);

        // Act
        Model? first2 = await Context.Models1
            .Where(x => x.PrettyName == "root")
            .Where(x => x.MyProperty1 > 2)
            .SelectFields(x => x.MyProperty1)
            .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(first2);
        Assert.Null(first2.PrettyName);
        Assert.Null(first2.PrettyRow);
        Assert.NotEqual(0, first2.MyProperty1);
        Assert.Null(first2.MyProperty2);
    }

    [Fact]
    public async Task SelectFields_WithCustomRecord_ShouldOnlyFillSelectedProperties()
    {
        // Arrange
        await Context.Models1.UpsertEntityAsync(new()
        {
            PrettyName = "root",
            PrettyRow = Guid.NewGuid().ToString("N"),
            MyProperty1 = 5,
            MyProperty2 = "test"
        }, TestContext.Current.CancellationToken);

        // Act
        Model? first3 = await Context.Models1
            .Where(x => x.PrettyName == "root")
            .Where(x => x.MyProperty1 > 2)
            .SelectFields(x => new TestTransformAndSelect(x.MyProperty1, x.MyProperty2))
            .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(first3);
        Assert.Null(first3.PrettyName);
        Assert.Null(first3.PrettyRow);
        Assert.NotEqual(0, first3.MyProperty1);
        Assert.NotNull(first3.MyProperty2);
    }
}
