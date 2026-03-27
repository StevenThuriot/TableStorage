using TableStorage.Tests.Infrastructure;
using TableStorage.Tests.Models;

namespace TableStorage.Tests;

/// <summary>
/// Tests for QueryAsync with Expression&lt;Func&lt;T, bool&gt;&gt; parameter when using partition key and row key proxies
/// Validates that the WhereVisitor correctly rewrites expressions to use actual PartitionKey and RowKey properties
/// </summary>
public class QueryAsyncProxyTests(AzuriteFixture azuriteFixture) : AzuriteTestBase(azuriteFixture)
{
    #region PartitionKey Proxy Tests

    [Fact]
    public async Task QueryAsync_WithPartitionKeyProxy_ShouldFilterCorrectly()
    {
        // Arrange
        const string partitionKey = "test-partition";
        await Context.Models1.UpsertEntityAsync(new()
        {
            PrettyName = partitionKey,
            PrettyRow = Guid.NewGuid().ToString("N"),
            MyProperty1 = 1,
            MyProperty2 = "test 1"
        }, TestContext.Current.CancellationToken);

        await Context.Models1.UpsertEntityAsync(new()
        {
            PrettyName = "other-partition",
            PrettyRow = Guid.NewGuid().ToString("N"),
            MyProperty1 = 2,
            MyProperty2 = "test 2"
        }, TestContext.Current.CancellationToken);

        // Act
        List<Model> results = await Context.Models1
            .QueryAsync(x => x.PrettyName == partitionKey, TestContext.Current.CancellationToken)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.NotEmpty(results);
        Assert.All(results, x => Assert.Equal(partitionKey, x.PrettyName));
    }

    [Fact]
    public async Task QueryAsync_WithPartitionKeyProxyAndNotEquals_ShouldFilterCorrectly()
    {
        // Arrange
        const string partitionKey = "test-partition";
        await Context.Models1.UpsertEntityAsync(new()
        {
            PrettyName = partitionKey,
            PrettyRow = Guid.NewGuid().ToString("N"),
            MyProperty1 = 1,
            MyProperty2 = "test 1"
        }, TestContext.Current.CancellationToken);

        await Context.Models1.UpsertEntityAsync(new()
        {
            PrettyName = "other-partition",
            PrettyRow = Guid.NewGuid().ToString("N"),
            MyProperty1 = 2,
            MyProperty2 = "test 2"
        }, TestContext.Current.CancellationToken);

        // Act
        List<Model> results = await Context.Models1
            .QueryAsync(x => x.PrettyName != partitionKey, TestContext.Current.CancellationToken)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.NotEmpty(results);
        Assert.All(results, x => Assert.NotEqual(partitionKey, x.PrettyName));
    }

    [Fact]
    public async Task QueryAsync_WithPartitionKeyProxyAndAdditionalFilters_ShouldFilterCorrectly()
    {
        // Arrange
        const string partitionKey = "test-partition";
        await Context.Models1.UpsertEntityAsync(new()
        {
            PrettyName = partitionKey,
            PrettyRow = Guid.NewGuid().ToString("N"),
            MyProperty1 = 1,
            MyProperty2 = "test 1"
        }, TestContext.Current.CancellationToken);

        await Context.Models1.UpsertEntityAsync(new()
        {
            PrettyName = partitionKey,
            PrettyRow = Guid.NewGuid().ToString("N"),
            MyProperty1 = 2,
            MyProperty2 = "test 2"
        }, TestContext.Current.CancellationToken);

        await Context.Models1.UpsertEntityAsync(new()
        {
            PrettyName = "other-partition",
            PrettyRow = Guid.NewGuid().ToString("N"),
            MyProperty1 = 1,
            MyProperty2 = "test 3"
        }, TestContext.Current.CancellationToken);

        // Act
        List<Model> results = await Context.Models1
            .QueryAsync(x => x.PrettyName == partitionKey && x.MyProperty1 == 1, TestContext.Current.CancellationToken)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(results);
        Assert.Equal(partitionKey, results[0].PrettyName);
        Assert.Equal(1, results[0].MyProperty1);
    }

    #endregion

    #region RowKey Proxy Tests

    [Fact]
    public async Task QueryAsync_WithRowKeyProxy_ShouldFilterCorrectly()
    {
        // Arrange
        const string rowKey = "test-row-key";
        await Context.Models2.UpsertEntityAsync(new()
        {
            PartitionKey = "root",
            PrettyRow = rowKey,
            MyProperty1 = 1,
            MyProperty2 = "test 1"
        }, TestContext.Current.CancellationToken);

        await Context.Models2.UpsertEntityAsync(new()
        {
            PartitionKey = "root",
            PrettyRow = "other-row-key",
            MyProperty1 = 2,
            MyProperty2 = "test 2"
        }, TestContext.Current.CancellationToken);

        // Act
        List<Model2> results = await Context.Models2
            .QueryAsync(x => x.PrettyRow == rowKey, TestContext.Current.CancellationToken)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.NotEmpty(results);
        Assert.All(results, x => Assert.Equal(rowKey, x.PrettyRow));
    }

    [Fact]
    public async Task QueryAsync_WithRowKeyProxyAndNotEquals_ShouldFilterCorrectly()
    {
        // Arrange
        const string rowKey = "test-row-key";
        await Context.Models2.UpsertEntityAsync(new()
        {
            PartitionKey = "root",
            PrettyRow = rowKey,
            MyProperty1 = 1,
            MyProperty2 = "test 1"
        }, TestContext.Current.CancellationToken);

        await Context.Models2.UpsertEntityAsync(new()
        {
            PartitionKey = "root",
            PrettyRow = "other-row-key",
            MyProperty1 = 2,
            MyProperty2 = "test 2"
        }, TestContext.Current.CancellationToken);

        // Act
        List<Model2> results = await Context.Models2
            .QueryAsync(x => x.PrettyRow != rowKey, TestContext.Current.CancellationToken)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.NotEmpty(results);
        Assert.All(results, x => Assert.NotEqual(rowKey, x.PrettyRow));
    }

    [Fact]
    public async Task QueryAsync_WithRowKeyProxyAndAdditionalFilters_ShouldFilterCorrectly()
    {
        // Arrange
        string rowKey = Guid.NewGuid().ToString("N");
        await Context.Models2.UpsertEntityAsync(new()
        {
            PartitionKey = "root",
            PrettyRow = rowKey,
            MyProperty1 = 1,
            MyProperty2 = "test 1"
        }, TestContext.Current.CancellationToken);

        await Context.Models2.UpsertEntityAsync(new()
        {
            PartitionKey = "root",
            PrettyRow = Guid.NewGuid().ToString("N"),
            MyProperty1 = 2,
            MyProperty2 = "test 2"
        }, TestContext.Current.CancellationToken);

        await Context.Models2.UpsertEntityAsync(new()
        {
            PartitionKey = "root",
            PrettyRow = "other-row-key",
            MyProperty1 = 1,
            MyProperty2 = "test 3"
        }, TestContext.Current.CancellationToken);

        // Act
        List<Model2> results = await Context.Models2
            .QueryAsync(x => x.PrettyRow == rowKey && x.MyProperty1 == 1, TestContext.Current.CancellationToken)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(results);
        Assert.Equal(rowKey, results[0].PrettyRow);
        Assert.Equal(1, results[0].MyProperty1);
    }

    #endregion

    #region Both PartitionKey and RowKey Proxy Tests

    [Fact]
    public async Task QueryAsync_WithBothProxies_ShouldFilterCorrectly()
    {
        // Arrange
        const string partitionKey = "test-partition";
        const string rowKey = "test-row-key";
        await Context.Models1.UpsertEntityAsync(new()
        {
            PrettyName = partitionKey,
            PrettyRow = rowKey,
            MyProperty1 = 1,
            MyProperty2 = "test 1"
        }, TestContext.Current.CancellationToken);

        await Context.Models1.UpsertEntityAsync(new()
        {
            PrettyName = partitionKey,
            PrettyRow = "other-row-key",
            MyProperty1 = 2,
            MyProperty2 = "test 2"
        }, TestContext.Current.CancellationToken);

        await Context.Models1.UpsertEntityAsync(new()
        {
            PrettyName = "other-partition",
            PrettyRow = rowKey,
            MyProperty1 = 3,
            MyProperty2 = "test 3"
        }, TestContext.Current.CancellationToken);

        // Act
        List<Model> results = await Context.Models1
            .QueryAsync(x => x.PrettyName == partitionKey && x.PrettyRow == rowKey, TestContext.Current.CancellationToken)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(results);
        Assert.Equal(partitionKey, results[0].PrettyName);
        Assert.Equal(rowKey, results[0].PrettyRow);
    }

    [Fact]
    public async Task QueryAsync_WithBothProxiesAndAdditionalFilters_ShouldFilterCorrectly()
    {
        // Arrange
        string partitionKey = Guid.NewGuid().ToString("N");
        string rowKey = Guid.NewGuid().ToString("N");
        await Context.Models1.UpsertEntityAsync(new()
        {
            PrettyName = partitionKey,
            PrettyRow = rowKey,
            MyProperty1 = 1,
            MyProperty2 = "test 1"
        }, TestContext.Current.CancellationToken);

        await Context.Models1.UpsertEntityAsync(new()
        {
            PrettyName = partitionKey,
            PrettyRow = Guid.NewGuid().ToString("N"),
            MyProperty1 = 2,
            MyProperty2 = "test 2"
        }, TestContext.Current.CancellationToken);

        await Context.Models1.UpsertEntityAsync(new()
        {
            PrettyName = "other-partition",
            PrettyRow = "other-row-key",
            MyProperty1 = 1,
            MyProperty2 = "test 3"
        }, TestContext.Current.CancellationToken);

        // Act
        List<Model> results = await Context.Models1
            .QueryAsync(x => x.PrettyName == partitionKey && x.PrettyRow == rowKey && x.MyProperty1 == 1, TestContext.Current.CancellationToken)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(results);
        Assert.Equal(partitionKey, results[0].PrettyName);
        Assert.Equal(rowKey, results[0].PrettyRow);
        Assert.Equal(1, results[0].MyProperty1);
    }

    [Fact]
    public async Task QueryAsync_WithBothProxiesUsingSeparateConditions_ShouldFilterCorrectly()
    {
        // Arrange
        const string partitionKey = "test-partition";
        const string rowKey = "test-row-key";
        await Context.Models1.UpsertEntityAsync(new()
        {
            PrettyName = partitionKey,
            PrettyRow = rowKey,
            MyProperty1 = 1,
            MyProperty2 = "test 1"
        }, TestContext.Current.CancellationToken);

        await Context.Models1.UpsertEntityAsync(new()
        {
            PrettyName = partitionKey,
            PrettyRow = "other-row-key",
            MyProperty1 = 2,
            MyProperty2 = "test 2"
        }, TestContext.Current.CancellationToken);

        // Act
        List<Model> results = await Context.Models1
            .QueryAsync(x => x.PrettyName == partitionKey, TestContext.Current.CancellationToken)
            .Where(x => x.PrettyRow == rowKey)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(results);
        Assert.Equal(partitionKey, results[0].PrettyName);
        Assert.Equal(rowKey, results[0].PrettyRow);
    }

    #endregion

    #region Variable Capture Tests

    [Fact]
    public async Task QueryAsync_WithProxyUsingVariableCapture_ShouldFilterCorrectly()
    {
        // Arrange
        var testItem = new { PrettyName = "test-partition", PrettyRow = "test-row-key" };
        await Context.Models1.UpsertEntityAsync(new()
        {
            PrettyName = testItem.PrettyName,
            PrettyRow = testItem.PrettyRow,
            MyProperty1 = 1,
            MyProperty2 = "test 1"
        }, TestContext.Current.CancellationToken);

        await Context.Models1.UpsertEntityAsync(new()
        {
            PrettyName = "other-partition",
            PrettyRow = "other-row-key",
            MyProperty1 = 2,
            MyProperty2 = "test 2"
        }, TestContext.Current.CancellationToken);

        // Act
        List<Model> results = await Context.Models1
            .QueryAsync(x => x.PrettyName == testItem.PrettyName && x.PrettyRow == testItem.PrettyRow, TestContext.Current.CancellationToken)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(results);
        Assert.Equal(testItem.PrettyName, results[0].PrettyName);
        Assert.Equal(testItem.PrettyRow, results[0].PrettyRow);
    }

    [Fact]
    public async Task QueryAsync_WithRowKeyProxyUsingVariableCapture_ShouldFilterCorrectly()
    {
        // Arrange
        var testItem = new { PrettyRow = "test-row-key" };
        await Context.Models2.UpsertEntityAsync(new()
        {
            PartitionKey = "root",
            PrettyRow = testItem.PrettyRow,
            MyProperty1 = 1,
            MyProperty2 = "test 1"
        }, TestContext.Current.CancellationToken);

        await Context.Models2.UpsertEntityAsync(new()
        {
            PartitionKey = "root",
            PrettyRow = "other-row-key",
            MyProperty1 = 2,
            MyProperty2 = "test 2"
        }, TestContext.Current.CancellationToken);

        // Act
        List<Model2> results = await Context.Models2
            .QueryAsync(x => x.PrettyRow == testItem.PrettyRow, TestContext.Current.CancellationToken)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.NotEmpty(results);
        Assert.All(results, x => Assert.Equal(testItem.PrettyRow, x.PrettyRow));
    }

    #endregion

    #region Complex Filter Tests

    [Fact]
    public async Task QueryAsync_WithProxyAndComplexOrConditions_ShouldFilterCorrectly()
    {
        // Arrange
        const string partitionKey1 = "test-partition-1";
        const string partitionKey2 = "test-partition-2";
        await Context.Models1.UpsertEntityAsync(new()
        {
            PrettyName = partitionKey1,
            PrettyRow = Guid.NewGuid().ToString("N"),
            MyProperty1 = 1,
            MyProperty2 = "test 1"
        }, TestContext.Current.CancellationToken);

        await Context.Models1.UpsertEntityAsync(new()
        {
            PrettyName = partitionKey2,
            PrettyRow = Guid.NewGuid().ToString("N"),
            MyProperty1 = 2,
            MyProperty2 = "test 2"
        }, TestContext.Current.CancellationToken);

        await Context.Models1.UpsertEntityAsync(new()
        {
            PrettyName = "other-partition",
            PrettyRow = Guid.NewGuid().ToString("N"),
            MyProperty1 = 3,
            MyProperty2 = "test 3"
        }, TestContext.Current.CancellationToken);

        // Act
        List<Model> results = await Context.Models1
            .QueryAsync(x => x.PrettyName == partitionKey1 || x.PrettyName == partitionKey2, TestContext.Current.CancellationToken)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.NotEmpty(results);
        Assert.All(results, x => Assert.True(x.PrettyName == partitionKey1 || x.PrettyName == partitionKey2));
    }

    [Fact]
    public async Task QueryAsync_WithProxyAndGreaterThanComparison_ShouldFilterCorrectly()
    {
        // Arrange
        const string partitionKey = "test-partition";
        const string rowKey = "row-005";
        await Context.Models1.UpsertEntityAsync(new()
        {
            PrettyName = partitionKey,
            PrettyRow = "row-001",
            MyProperty1 = 1,
            MyProperty2 = "test 1"
        }, TestContext.Current.CancellationToken);

        await Context.Models1.UpsertEntityAsync(new()
        {
            PrettyName = partitionKey,
            PrettyRow = "row-010",
            MyProperty1 = 2,
            MyProperty2 = "test 2"
        }, TestContext.Current.CancellationToken);

        await Context.Models1.UpsertEntityAsync(new()
        {
            PrettyName = partitionKey,
            PrettyRow = "row-003",
            MyProperty1 = 3,
            MyProperty2 = "test 3"
        }, TestContext.Current.CancellationToken);

        // Act
        List<Model> results = await Context.Models1
            .QueryAsync(x => x.PrettyName == partitionKey && x.PrettyRow.CompareTo(rowKey) > 0, TestContext.Current.CancellationToken)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.NotEmpty(results);
        Assert.All(results, x =>
        {
            Assert.Equal(partitionKey, x.PrettyName);
            Assert.True(x.PrettyRow.CompareTo(rowKey) > 0);
        });
    }

    #endregion

    #region No Proxy Tests (Baseline)

    [Fact]
    public async Task QueryAsync_WithoutProxies_ShouldStillWork()
    {
        // Arrange - Model3 has no proxy keys
        await Context.Models5.UpsertEntityAsync(new()
        {
            PartitionKey = "test-partition",
            RowKey = "test-row-key"
        }, TestContext.Current.CancellationToken);

        await Context.Models5.UpsertEntityAsync(new()
        {
            PartitionKey = "other-partition",
            RowKey = "other-row-key"
        }, TestContext.Current.CancellationToken);

        // Act
        List<Model3> results = await Context.Models5
            .QueryAsync(x => x.PartitionKey == "test-partition", TestContext.Current.CancellationToken)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.NotEmpty(results);
        Assert.All(results, x => Assert.Equal("test-partition", x.PartitionKey));
    }

    #endregion
}
