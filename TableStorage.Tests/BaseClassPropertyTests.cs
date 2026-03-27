using TableStorage.Tests.Infrastructure;
using TableStorage.Tests.Models;

namespace TableStorage.Tests;

public class BaseClassPropertyTests(AzuriteFixture azuriteFixture) : AzuriteTestBase(azuriteFixture)
{
    [Fact]
    public async Task ModelBothKeysInBase_ShouldNotRegenerateProperties()
    {
        // Arrange
        var model = new ModelBothKeysInBase
        {
            PrettyPartition = "partition1",
            PrettyRow = "row1",
            MyProperty = "test"
        };

        // Act - Should be able to create and save without duplicate property errors
        await Context.ModelBothKeysInBase.UpsertEntityAsync(model, TestContext.Current.CancellationToken);

        // Assert - Should be able to retrieve and properties should match
        var retrieved = await Context.ModelBothKeysInBase
            .Where(x => x.PrettyPartition == "partition1" && x.PrettyRow == "row1")
            .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(retrieved);
        Assert.Equal("partition1", retrieved.PrettyPartition);
        Assert.Equal("row1", retrieved.PrettyRow);
        Assert.Equal("test", retrieved.MyProperty);
    }

    [Fact]
    public async Task ModelPartitionKeyInBase_ShouldOnlyRegenerateRowKey()
    {
        // Arrange
        var model = new ModelPartitionKeyInBase
        {
            PrettyPartition = "partition2",
            PrettyRow = "row2",
            MyProperty = "test2"
        };

        // Act
        await Context.ModelPartitionKeyInBase.UpsertEntityAsync(model, TestContext.Current.CancellationToken);

        // Assert
        var retrieved = await Context.ModelPartitionKeyInBase
            .Where(x => x.PrettyPartition == "partition2" && x.PrettyRow == "row2")

            .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(retrieved);
        Assert.Equal("partition2", retrieved.PrettyPartition);
        Assert.Equal("row2", retrieved.PrettyRow);
        Assert.Equal("test2", retrieved.MyProperty);
    }

    [Fact]
    public async Task ModelRowKeyInBase_ShouldOnlyRegeneratePartitionKey()
    {
        // Arrange
        var model = new ModelRowKeyInBase
        {
            PrettyPartition = "partition3",
            PrettyRow = "row3",
            MyProperty = "test3"
        };

        // Act
        await Context.ModelRowKeyInBase.UpsertEntityAsync(model, TestContext.Current.CancellationToken);

        // Assert
        var retrieved = await Context.ModelRowKeyInBase
            .Where(x => x.PrettyPartition == "partition3" && x.PrettyRow == "row3")

            .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(retrieved);
        Assert.Equal("partition3", retrieved.PrettyPartition);
        Assert.Equal("row3", retrieved.PrettyRow);
        Assert.Equal("test3", retrieved.MyProperty);
    }

    [Fact]
    public async Task ModelNoKeysInBase_ShouldRegenerateBothKeys()
    {
        // Arrange
        var model = new ModelNoKeysInBase
        {
            PrettyPartition = "partition4",
            PrettyRow = "row4",
            MyProperty = "test4",
            SomeOtherProperty = "other"
        };

        // Act
        await Context.ModelNoKeysInBase.UpsertEntityAsync(model, TestContext.Current.CancellationToken);

        // Assert
        var retrieved = await Context.ModelNoKeysInBase
            .Where(x => x.PrettyPartition == "partition4" && x.PrettyRow == "row4")

            .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(retrieved);
        Assert.Equal("partition4", retrieved.PrettyPartition);
        Assert.Equal("row4", retrieved.PrettyRow);
        Assert.Equal("test4", retrieved.MyProperty);
        Assert.Equal("other", retrieved.SomeOtherProperty);
    }

    [Fact]
    public async Task ModelBothKeysInBaseWithPartial_ShouldUsePartialDefinitions()
    {
        // Arrange
        var model = new ModelBothKeysInBaseWithPartial
        {
            PrettyPartition = "partition5",
            PrettyRow = "row5",
            MyProperty = "test5"
        };

        // Act
        await Context.ModelBothKeysInBaseWithPartial.UpsertEntityAsync(model, TestContext.Current.CancellationToken);

        // Assert
        var retrieved = await Context.ModelBothKeysInBaseWithPartial
            .Where(x => x.PrettyPartition == "partition5" && x.PrettyRow == "row5")

            .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(retrieved);
        Assert.Equal("partition5", retrieved.PrettyPartition);
        Assert.Equal("row5", retrieved.PrettyRow);
        Assert.Equal("test5", retrieved.MyProperty);
    }

    [Fact]
    public void ModelBothKeysInBase_ShouldNotHaveDuplicateProperties()
    {
        // This test validates at compile time that there are no duplicate property definitions
        // If the code compiles, this test passes

        // Should only have one PrettyPartition property
        var partitionProperty = typeof(ModelBothKeysInBase).GetProperty(nameof(ModelBothKeysInBase.PrettyPartition));
        Assert.NotNull(partitionProperty);

        // Should only have one PrettyRow property
        var rowProperty = typeof(ModelBothKeysInBase).GetProperty(nameof(ModelBothKeysInBase.PrettyRow));
        Assert.NotNull(rowProperty);

        // Check that declaring type is the base class, not the derived class
        Assert.Equal(typeof(BaseClassWithBothKeys), partitionProperty.DeclaringType);
        Assert.Equal(typeof(BaseClassWithBothKeys), rowProperty.DeclaringType);
    }

    [Fact]
    public void ModelPartitionKeyInBase_PartitionKeyShouldBeFromBase()
    {
        var partitionProperty = typeof(ModelPartitionKeyInBase).GetProperty(nameof(ModelPartitionKeyInBase.PrettyPartition));
        Assert.NotNull(partitionProperty);
        Assert.Equal(typeof(BaseClassWithOnlyPartitionKey), partitionProperty.DeclaringType);
    }

    [Fact]
    public void ModelRowKeyInBase_RowKeyShouldBeFromBase()
    {
        var rowProperty = typeof(ModelRowKeyInBase).GetProperty(nameof(ModelRowKeyInBase.PrettyRow));
        Assert.NotNull(rowProperty);
        Assert.Equal(typeof(BaseClassWithOnlyRowKey), rowProperty.DeclaringType);
    }

    [Fact]
    public async Task ModelBothKeysInBaseWithOverride_ShouldUseOverrideProperties()
    {
        // Arrange
        var model = new ModelBothKeysInBaseWithOverride
        {
            PrettyPartition = "partition6",
            PrettyRow = "row6",
            MyProperty = "test6"
        };

        // Act
        await Context.ModelBothKeysInBaseWithOverride.UpsertEntityAsync(model, TestContext.Current.CancellationToken);

        // Assert
        var retrieved = await Context.ModelBothKeysInBaseWithOverride
            .Where(x => x.PrettyPartition == "partition6" && x.PrettyRow == "row6")

            .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(retrieved);
        Assert.Equal("partition6", retrieved.PrettyPartition);
        Assert.Equal("row6", retrieved.PrettyRow);
        Assert.Equal("test6", retrieved.MyProperty);
    }

    [Fact]
    public void ModelBothKeysInBaseWithOverride_PropertiesShouldBeOverridden()
    {
        // Verify that properties are correctly overridden
        var partitionProperty = typeof(ModelBothKeysInBaseWithOverride).GetProperty(nameof(ModelBothKeysInBaseWithOverride.PrettyPartition));
        Assert.NotNull(partitionProperty);

        var rowProperty = typeof(ModelBothKeysInBaseWithOverride).GetProperty(nameof(ModelBothKeysInBaseWithOverride.PrettyRow));
        Assert.NotNull(rowProperty);

        // The declaring type should be the derived class since they override
        Assert.Equal(typeof(ModelBothKeysInBaseWithOverride), partitionProperty.DeclaringType);
        Assert.Equal(typeof(ModelBothKeysInBaseWithOverride), rowProperty.DeclaringType);
    }
}
