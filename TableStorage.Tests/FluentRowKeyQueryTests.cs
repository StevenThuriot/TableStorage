using TableStorage.Tests.Contexts;
using TableStorage.Tests.Infrastructure;
using TableStorage.Tests.Models;

namespace TableStorage.Tests;

/// <summary>
/// Tests for FluentRowKeyTableEntity extension methods
/// Covers FindAsync, WhereFirstType, WhereSecondType, and type-specific query operations
/// </summary>
public class FluentRowKeyQueryTests(AzuriteFixture azuriteFixture) : AzuriteTestBase(azuriteFixture)
{
    #region FindAsync Tests

    [Fact]
    public async Task FindAsync_WithSinglePartitionKey_ShouldReturnCorrectEntity()
    {
        // Arrange
        string partitionKey = Guid.NewGuid().ToString("N");

        FluentTestModelA modelA = new()
        {
            PrettyPartitionA = partitionKey,
            PrettyRowA = "ignored",
            TypeA = "Test Type A",
            PropertyA = 123
        };

        FluentTestModelB modelB = new()
        {
            PrettyPartitionB = partitionKey,
            PrettyRowB = "ignored",
            TypeB = "Test Type B",
            PropertyB = false
        };

        await Context.FluentRowKeyModels.UpsertEntityAsync(modelA);
        await Context.FluentRowKeyModels.UpsertEntityAsync(modelB);

        // Act
        var retrieved = await Context.FluentRowKeyModels.FindFluentTestModelAAsync(partitionKey);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(partitionKey, retrieved.PrettyPartitionA);
        Assert.Equal("FluentTestModelA", retrieved.PrettyRowA);
    }

    [Fact]
    public async Task FindAsync_WithNonExistentPartitionKey_ShouldReturnNull()
    {
        // Arrange
        string partitionKey = Guid.NewGuid().ToString("N");

        FluentTestModelA modelA = new()
        {
            PrettyPartitionA = partitionKey,
            PrettyRowA = "ignored",
            TypeA = "Test Type A",
            PropertyA = 100
        };

        FluentTestModelB modelB = new()
        {
            PrettyPartitionB = partitionKey,
            PrettyRowB = "ignored",
            TypeB = "Test Type B",
            PropertyB = true
        };

        await Context.FluentRowKeyModels.UpsertEntityAsync(modelA);
        await Context.FluentRowKeyModels.UpsertEntityAsync(modelB);

        string nonExistentPartitionKey = Guid.NewGuid().ToString("N");

        // Act
        var retrieved = await Context.FluentRowKeyModels.FindFluentTestModelAAsync(nonExistentPartitionKey);

        // Assert
        Assert.Null(retrieved);
    }

    #endregion

    #region WhereFirstType Tests

    [Fact]
    public async Task WhereFirstType_ShouldReturnOnlyFirstType()
    {
        // Arrange
        string partitionKey1 = Guid.NewGuid().ToString("N");
        string partitionKey2 = Guid.NewGuid().ToString("N");
        string partitionKey3 = Guid.NewGuid().ToString("N");

        FluentTestModelA modelA1 = new()
        {
            PrettyPartitionA = partitionKey1,
            PrettyRowA = "ignored",
            TypeA = "Type A1",
            PropertyA = 100
        };

        FluentTestModelA modelA2 = new()
        {
            PrettyPartitionA = partitionKey2,
            PrettyRowA = "ignored",
            TypeA = "Type A2",
            PropertyA = 200
        };

        FluentTestModelB modelB = new()
        {
            PrettyPartitionB = partitionKey3,
            PrettyRowB = "ignored",
            TypeB = "Type B",
            PropertyB = true
        };

        await Context.FluentRowKeyModels.UpsertEntityAsync(modelA1);
        await Context.FluentRowKeyModels.UpsertEntityAsync(modelA2);
        await Context.FluentRowKeyModels.UpsertEntityAsync(modelB);

        // Act
        var results = await Context.FluentRowKeyModels
            .WhereFirstType()
            .ToListAsync();

        // Assert
        Assert.NotEmpty(results);
        Assert.All(results, x => Assert.IsType<FluentTestModelA>(x));
        Assert.True(results.Count >= 2);
    }

    [Fact]
    public async Task WhereFirstType_WithAdditionalFilter_ShouldCombineFilters()
    {
        // Arrange
        string partitionKey1 = Guid.NewGuid().ToString("N");
        string partitionKey2 = Guid.NewGuid().ToString("N");
        string partitionKey3 = Guid.NewGuid().ToString("N");

        FluentTestModelA modelA1 = new()
        {
            PrettyPartitionA = partitionKey1,
            PrettyRowA = "ignored",
            TypeA = "Match",
            PropertyA = 100
        };

        FluentTestModelA modelA2 = new()
        {
            PrettyPartitionA = partitionKey2,
            PrettyRowA = "ignored",
            TypeA = "NoMatch",
            PropertyA = 200
        };

        FluentTestModelB modelB = new()
        {
            PrettyPartitionB = partitionKey3,
            PrettyRowB = "ignored",
            TypeB = "Type B",
            PropertyB = true
        };

        await Context.FluentRowKeyModels.UpsertEntityAsync(modelA1);
        await Context.FluentRowKeyModels.UpsertEntityAsync(modelA2);
        await Context.FluentRowKeyModels.UpsertEntityAsync(modelB);

        // Act
        var results = await Context.FluentRowKeyModels
            .WhereFirstType()
            .Where(x => x.TypeA == "Match")
            .ToListAsync();

        // Assert
        Assert.NotEmpty(results);
        Assert.All(results, x =>
        {
            Assert.IsType<FluentTestModelA>(x);
            Assert.Equal("Match", x.TypeA);
        });
    }

    #endregion

    #region WhereSecondType Tests

    [Fact]
    public async Task WhereSecondType_ShouldReturnOnlySecondType()
    {
        // Arrange
        string partitionKey1 = Guid.NewGuid().ToString("N");
        string partitionKey2 = Guid.NewGuid().ToString("N");
        string partitionKey3 = Guid.NewGuid().ToString("N");

        FluentTestModelA modelA = new()
        {
            PrettyPartitionA = partitionKey1,
            PrettyRowA = "ignored",
            TypeA = "Type A",
            PropertyA = 100
        };

        FluentTestModelB modelB1 = new()
        {
            PrettyPartitionB = partitionKey2,
            PrettyRowB = "ignored",
            TypeB = "Type B1",
            PropertyB = true
        };

        FluentTestModelB modelB2 = new()
        {
            PrettyPartitionB = partitionKey3,
            PrettyRowB = "ignored",
            TypeB = "Type B2",
            PropertyB = false
        };

        await Context.FluentRowKeyModels.UpsertEntityAsync(modelA);
        await Context.FluentRowKeyModels.UpsertEntityAsync(modelB1);
        await Context.FluentRowKeyModels.UpsertEntityAsync(modelB2);

        // Act
        var results = await Context.FluentRowKeyModels
            .WhereSecondType()
            .ToListAsync();

        // Assert
        Assert.NotEmpty(results);
        Assert.All(results, x => Assert.IsType<FluentTestModelB>(x));
        Assert.True(results.Count >= 2);
    }

    [Fact]
    public async Task WhereSecondType_WithAdditionalFilter_ShouldCombineFilters()
    {
        // Arrange
        string partitionKey1 = Guid.NewGuid().ToString("N");
        string partitionKey2 = Guid.NewGuid().ToString("N");
        string partitionKey3 = Guid.NewGuid().ToString("N");

        FluentTestModelA modelA = new()
        {
            PrettyPartitionA = partitionKey1,
            PrettyRowA = "ignored",
            TypeA = "Type A",
            PropertyA = 150
        };

        FluentTestModelB modelB1 = new()
        {
            PrettyPartitionB = partitionKey2,
            PrettyRowB = "ignored",
            TypeB = "Type B1",
            PropertyB = true
        };

        FluentTestModelB modelB2 = new()
        {
            PrettyPartitionB = partitionKey3,
            PrettyRowB = "ignored",
            TypeB = "Type B2",
            PropertyB = false
        };

        await Context.FluentRowKeyModels.UpsertEntityAsync(modelA);
        await Context.FluentRowKeyModels.UpsertEntityAsync(modelB1);
        await Context.FluentRowKeyModels.UpsertEntityAsync(modelB2);

        // Act
        var results = await Context.FluentRowKeyModels
            .WhereSecondType()
            .Where(x => x.PropertyB == true)
            .ToListAsync();

        // Assert
        Assert.NotEmpty(results);
        Assert.All(results, x =>
        {
            Assert.IsType<FluentTestModelB>(x);
            Assert.True(x.PropertyB);
        });
    }

    #endregion

    #region Where with Type-Specific Predicates Tests

    [Fact]
    public async Task Where_WithFirstTypePredicateFromExtension_ShouldFilterCorrectly()
    {
        // Arrange
        string partitionKey1 = Guid.NewGuid().ToString("N");
        string partitionKey2 = Guid.NewGuid().ToString("N");
        string partitionKey3 = Guid.NewGuid().ToString("N");

        FluentTestModelA modelA1 = new()
        {
            PrettyPartitionA = partitionKey1,
            PrettyRowA = "ignored",
            TypeA = "Match",
            PropertyA = 150
        };

        FluentTestModelA modelA2 = new()
        {
            PrettyPartitionA = partitionKey2,
            PrettyRowA = "ignored",
            TypeA = "NoMatch",
            PropertyA = 50
        };

        FluentTestModelB modelB = new()
        {
            PrettyPartitionB = partitionKey3,
            PrettyRowB = "ignored",
            TypeB = "Type B",
            PropertyB = true
        };

        await Context.FluentRowKeyModels.UpsertEntityAsync(modelA1);
        await Context.FluentRowKeyModels.UpsertEntityAsync(modelA2);
        await Context.FluentRowKeyModels.UpsertEntityAsync(modelB);

        // Act
        var results = await Context.FluentRowKeyModels
            .WhereFluentTestModelA((x) => x.PropertyA > 100)
            .ToListAsync();

        // Assert
        Assert.NotEmpty(results);
        Assert.All(results, x =>
        {
            Assert.IsType<FluentTestModelA>(x);
            var typedModel = (FluentTestModelA)x;
            Assert.True(typedModel.PropertyA > 100);
        });
    }

    [Fact]
    public async Task Where_WithSecondTypePredicateFromExtension_ShouldFilterCorrectly()
    {
        // Arrange
        string partitionKey1 = Guid.NewGuid().ToString("N");
        string partitionKey2 = Guid.NewGuid().ToString("N");
        string partitionKey3 = Guid.NewGuid().ToString("N");

        FluentTestModelA modelA = new()
        {
            PrettyPartitionA = partitionKey1,
            PrettyRowA = "ignored",
            TypeA = "Type A",
            PropertyA = 100
        };

        FluentTestModelB modelB1 = new()
        {
            PrettyPartitionB = partitionKey2,
            PrettyRowB = "ignored",
            TypeB = "Type B1",
            PropertyB = true
        };

        FluentTestModelB modelB2 = new()
        {
            PrettyPartitionB = partitionKey3,
            PrettyRowB = "ignored",
            TypeB = "Type B2",
            PropertyB = false
        };

        await Context.FluentRowKeyModels.UpsertEntityAsync(modelA);
        await Context.FluentRowKeyModels.UpsertEntityAsync(modelB1);
        await Context.FluentRowKeyModels.UpsertEntityAsync(modelB2);

        // Act
        var results = await Context.FluentRowKeyModels
            .WhereFluentTestModelB((x) => x.PropertyB == false)
            .ToListAsync();

        // Assert
        Assert.NotEmpty(results);
        Assert.All(results, x =>
        {
            Assert.IsType<FluentTestModelB>(x);
            var typedModel = (FluentTestModelB)x;
            Assert.False(typedModel.PropertyB);
        });
    }

    #endregion

    #region SelectFields Tests

    [Fact]
    public async Task SelectFields_ForFirstType_ShouldProjectOnlySelectedFields()
    {
        // Arrange
        string partitionKey1 = Guid.NewGuid().ToString("N");
        string partitionKey2 = Guid.NewGuid().ToString("N");

        FluentTestModelA modelA = new()
        {
            PrettyPartitionA = partitionKey1,
            PrettyRowA = "ignored",
            TypeA = "Type A",
            PropertyA = 100
        };

        FluentTestModelB modelB = new()
        {
            PrettyPartitionB = partitionKey2,
            PrettyRowB = "ignored",
            TypeB = "Type B",
            PropertyB = true
        };

        await Context.FluentRowKeyModels.UpsertEntityAsync(modelA);
        await Context.FluentRowKeyModels.UpsertEntityAsync(modelB);

        // Act
        var results = await Context.FluentRowKeyModels
            .SelectFluentTestModelAFields((x) => x.TypeA)
            .ToListAsync();

        // Assert
        Assert.NotEmpty(results);
        Assert.All(results, x =>
        {
            Assert.Equal("Type A", x["TypeA"]);
            Assert.Equal(default(int), x["PropertyA"]); // because not initialized
        });
    }

    [Fact]
    public async Task SelectFields_ForSecondType_ShouldProjectOnlySelectedFields()
    {
        // Arrange
        string partitionKey1 = Guid.NewGuid().ToString("N");
        string partitionKey2 = Guid.NewGuid().ToString("N");

        FluentTestModelA modelA = new()
        {
            PrettyPartitionA = partitionKey1,
            PrettyRowA = "ignored",
            TypeA = "Type A",
            PropertyA = 100
        };

        FluentTestModelB modelB = new()
        {
            PrettyPartitionB = partitionKey2,
            PrettyRowB = "ignored",
            TypeB = "Type B",
            PropertyB = true
        };

        await Context.FluentRowKeyModels.UpsertEntityAsync(modelA);
        await Context.FluentRowKeyModels.UpsertEntityAsync(modelB);

        // Act
        var results = await Context.FluentRowKeyModels
            .SelectFluentTestModelBFields((x) => x.TypeB)
            .ToListAsync();

        // Assert
        Assert.NotEmpty(results);
        Assert.All(results, x =>
        {
            Assert.Equal("Type B", x["TypeB"]);
            Assert.False(x["PropertyB"] as bool?); // because not initialized
        });
    }

    #endregion

    #region ExistsIn Tests

    [Fact]
    public async Task ExistsIn_ForFirstType_ShouldFilterByProvidedValues()
    {
        // Arrange
        string partitionKey1 = Guid.NewGuid().ToString("N");
        string partitionKey2 = Guid.NewGuid().ToString("N");
        string partitionKey3 = Guid.NewGuid().ToString("N");
        string partitionKey4 = Guid.NewGuid().ToString("N");

        FluentTestModelA modelA1 = new()
        {
            PrettyPartitionA = partitionKey1,
            PrettyRowA = "ignored",
            TypeA = "Type A1",
            PropertyA = 100
        };

        FluentTestModelA modelA2 = new()
        {
            PrettyPartitionA = partitionKey2,
            PrettyRowA = "ignored",
            TypeA = "Type A2",
            PropertyA = 200
        };

        FluentTestModelA modelA3 = new()
        {
            PrettyPartitionA = partitionKey3,
            PrettyRowA = "ignored",
            TypeA = "Type A3",
            PropertyA = 300
        };

        FluentTestModelB modelB = new()
        {
            PrettyPartitionB = partitionKey4,
            PrettyRowB = "ignored",
            TypeB = "Type B",
            PropertyB = false
        };

        await Context.FluentRowKeyModels.UpsertEntityAsync(modelA1);
        await Context.FluentRowKeyModels.UpsertEntityAsync(modelA2);
        await Context.FluentRowKeyModels.UpsertEntityAsync(modelA3);
        await Context.FluentRowKeyModels.UpsertEntityAsync(modelB);

        // Act
        var results = await Context.FluentRowKeyModels
            .ExistsInFluentTestModelA((x) => x.PropertyA, [100, 200, 400])
            .ToListAsync();

        // Assert
        Assert.Equal(2, results.Count);
        Assert.All(results, x => Assert.IsType<FluentTestModelA>(x));
        Assert.Contains(results, x => ((FluentTestModelA)x).PropertyA == 100);
        Assert.Contains(results, x => ((FluentTestModelA)x).PropertyA == 200);
    }

    [Fact]
    public async Task ExistsIn_ForSecondType_ShouldFilterByProvidedValues()
    {
        // Arrange
        string partitionKey1 = Guid.NewGuid().ToString("N");
        string partitionKey2 = Guid.NewGuid().ToString("N");
        string partitionKey3 = Guid.NewGuid().ToString("N");
        string partitionKey4 = Guid.NewGuid().ToString("N");

        FluentTestModelA modelA = new()
        {
            PrettyPartitionA = partitionKey1,
            PrettyRowA = "ignored",
            TypeA = "Type A",
            PropertyA = 150
        };

        FluentTestModelB modelB1 = new()
        {
            PrettyPartitionB = partitionKey2,
            PrettyRowB = "ignored",
            TypeB = "Type B1",
            PropertyB = true
        };

        FluentTestModelB modelB2 = new()
        {
            PrettyPartitionB = partitionKey3,
            PrettyRowB = "ignored",
            TypeB = "Type B2",
            PropertyB = false
        };

        FluentTestModelB modelB3 = new()
        {
            PrettyPartitionB = partitionKey4,
            PrettyRowB = "ignored",
            TypeB = "Type B3",
            PropertyB = true
        };

        await Context.FluentRowKeyModels.UpsertEntityAsync(modelA);
        await Context.FluentRowKeyModels.UpsertEntityAsync(modelB1);
        await Context.FluentRowKeyModels.UpsertEntityAsync(modelB2);
        await Context.FluentRowKeyModels.UpsertEntityAsync(modelB3);

        // Act
        var results = await Context.FluentRowKeyModels
            .ExistsInFluentTestModelB((x) => x.TypeB, ["Type B1", "Type B3", "Type B4"])
            .ToListAsync();

        // Assert
        Assert.Equal(2, results.Count);
        Assert.All(results, x => Assert.IsType<FluentTestModelB>(x));
        Assert.Contains(results, x => ((FluentTestModelB)x).TypeB == "Type B1");
        Assert.Contains(results, x => ((FluentTestModelB)x).TypeB == "Type B3");
    }

    #endregion

    #region NotExistsIn Tests

    [Fact]
    public async Task NotExistsIn_ForFirstType_ShouldExcludeProvidedValues()
    {
        // Arrange
        string partitionKey1 = Guid.NewGuid().ToString("N");
        string partitionKey2 = Guid.NewGuid().ToString("N");
        string partitionKey3 = Guid.NewGuid().ToString("N");
        string partitionKey4 = Guid.NewGuid().ToString("N");

        FluentTestModelA modelA1 = new()
        {
            PrettyPartitionA = partitionKey1,
            PrettyRowA = "ignored",
            TypeA = "Type A1",
            PropertyA = 100
        };

        FluentTestModelA modelA2 = new()
        {
            PrettyPartitionA = partitionKey2,
            PrettyRowA = "ignored",
            TypeA = "Type A2",
            PropertyA = 200
        };

        FluentTestModelA modelA3 = new()
        {
            PrettyPartitionA = partitionKey3,
            PrettyRowA = "ignored",
            TypeA = "Type A3",
            PropertyA = 300
        };

        FluentTestModelB modelB = new()
        {
            PrettyPartitionB = partitionKey4,
            PrettyRowB = "ignored",
            TypeB = "Type B",
            PropertyB = true
        };

        await Context.FluentRowKeyModels.UpsertEntityAsync(modelA1);
        await Context.FluentRowKeyModels.UpsertEntityAsync(modelA2);
        await Context.FluentRowKeyModels.UpsertEntityAsync(modelA3);
        await Context.FluentRowKeyModels.UpsertEntityAsync(modelB);

        // Act
        var results = await Context.FluentRowKeyModels
            .NotExistsInFluentTestModelA((x) => x.PropertyA, [100, 400])
            .ToListAsync();

        // Assert
        Assert.NotEmpty(results);
        Assert.All(results, x =>
        {
            Assert.IsType<FluentTestModelA>(x);
            var typedModel = (FluentTestModelA)x;
            Assert.NotEqual(100, typedModel.PropertyA);
            Assert.NotEqual(400, typedModel.PropertyA);
        });
        Assert.Contains(results, x => ((FluentTestModelA)x).PropertyA == 200);
        Assert.Contains(results, x => ((FluentTestModelA)x).PropertyA == 300);
    }

    [Fact]
    public async Task NotExistsIn_ForSecondType_ShouldExcludeProvidedValues()
    {
        // Arrange
        string partitionKey1 = Guid.NewGuid().ToString("N");
        string partitionKey2 = Guid.NewGuid().ToString("N");
        string partitionKey3 = Guid.NewGuid().ToString("N");
        string partitionKey4 = Guid.NewGuid().ToString("N");

        FluentTestModelA modelA = new()
        {
            PrettyPartitionA = partitionKey1,
            PrettyRowA = "ignored",
            TypeA = "Type A",
            PropertyA = 250
        };

        FluentTestModelB modelB1 = new()
        {
            PrettyPartitionB = partitionKey2,
            PrettyRowB = "ignored",
            TypeB = "Type B1",
            PropertyB = true
        };

        FluentTestModelB modelB2 = new()
        {
            PrettyPartitionB = partitionKey3,
            PrettyRowB = "ignored",
            TypeB = "Type B2",
            PropertyB = false
        };

        FluentTestModelB modelB3 = new()
        {
            PrettyPartitionB = partitionKey4,
            PrettyRowB = "ignored",
            TypeB = "Type B3",
            PropertyB = true
        };

        await Context.FluentRowKeyModels.UpsertEntityAsync(modelA);
        await Context.FluentRowKeyModels.UpsertEntityAsync(modelB1);
        await Context.FluentRowKeyModels.UpsertEntityAsync(modelB2);
        await Context.FluentRowKeyModels.UpsertEntityAsync(modelB3);

        // Act
        var results = await Context.FluentRowKeyModels
            .NotExistsInFluentTestModelB((x) => x.TypeB, ["Type B1", "Type B4"])
            .ToListAsync();

        // Assert
        Assert.NotEmpty(results);
        Assert.All(results, x =>
        {
            Assert.IsType<FluentTestModelB>(x);
            var typedModel = (FluentTestModelB)x;
            Assert.NotEqual("Type B1", typedModel.TypeB);
            Assert.NotEqual("Type B4", typedModel.TypeB);
        });
        Assert.Contains(results, x => ((FluentTestModelB)x).TypeB == "Type B2");
        Assert.Contains(results, x => ((FluentTestModelB)x).TypeB == "Type B3");
    }

    #endregion

    #region Combined Query Tests

    [Fact]
    public async Task CombinedQuery_WhereFirstType_WithMultipleFilters_ShouldWork()
    {
        // Arrange
        string partitionKey1 = Guid.NewGuid().ToString("N");
        string partitionKey2 = Guid.NewGuid().ToString("N");
        string partitionKey3 = Guid.NewGuid().ToString("N");

        FluentTestModelA modelA1 = new()
        {
            PrettyPartitionA = partitionKey1,
            PrettyRowA = "ignored",
            TypeA = "Match",
            PropertyA = 150
        };

        FluentTestModelA modelA2 = new()
        {
            PrettyPartitionA = partitionKey2,
            PrettyRowA = "ignored",
            TypeA = "Match",
            PropertyA = 50
        };

        FluentTestModelA modelA3 = new()
        {
            PrettyPartitionA = partitionKey3,
            PrettyRowA = "ignored",
            TypeA = "NoMatch",
            PropertyA = 150
        };

        await Context.FluentRowKeyModels.UpsertEntityAsync(modelA1);
        await Context.FluentRowKeyModels.UpsertEntityAsync(modelA2);
        await Context.FluentRowKeyModels.UpsertEntityAsync(modelA3);

        // Act
        var results = await Context.FluentRowKeyModels
            .WhereFirstType()
            .Where(x => x.TypeA == "Match")
            .Where(x => x.PropertyA > 100)
            .ToListAsync();

        // Assert
        Assert.Single(results);
        Assert.Equal("Match", results[0].TypeA);
        Assert.Equal(150, results[0].PropertyA);
    }

    [Fact]
    public async Task CombinedQuery_WithExistsInAndNotExistsIn_ShouldWork()
    {
        // Arrange
        string partitionKey1 = Guid.NewGuid().ToString("N");
        string partitionKey2 = Guid.NewGuid().ToString("N");
        string partitionKey3 = Guid.NewGuid().ToString("N");
        string partitionKey4 = Guid.NewGuid().ToString("N");

        FluentTestModelA modelA1 = new()
        {
            PrettyPartitionA = partitionKey1,
            PrettyRowA = "ignored",
            TypeA = "Type A1",
            PropertyA = 100
        };

        FluentTestModelA modelA2 = new()
        {
            PrettyPartitionA = partitionKey2,
            PrettyRowA = "ignored",
            TypeA = "Type A2",
            PropertyA = 200
        };

        FluentTestModelA modelA3 = new()
        {
            PrettyPartitionA = partitionKey3,
            PrettyRowA = "ignored",
            TypeA = "Type A3",
            PropertyA = 300
        };

        FluentTestModelB modelB = new()
        {
            PrettyPartitionB = partitionKey4,
            PrettyRowB = "ignored",
            TypeB = "Type B",
            PropertyB = false
        };

        await Context.FluentRowKeyModels.UpsertEntityAsync(modelA1);
        await Context.FluentRowKeyModels.UpsertEntityAsync(modelA2);
        await Context.FluentRowKeyModels.UpsertEntityAsync(modelA3);
        await Context.FluentRowKeyModels.UpsertEntityAsync(modelB);

        // Act
        var results = await Context.FluentRowKeyModels
            .ExistsInFluentTestModelA((x) => x.PropertyA, [100, 200, 300])
            .NotExistsIn((FluentTestModelA x) => x.PropertyA, [300])
            .ToListAsync();

        // Assert
        Assert.Equal(2, results.Count);
        Assert.Contains(results, x => ((FluentTestModelA)x).PropertyA == 100);
        Assert.Contains(results, x => ((FluentTestModelA)x).PropertyA == 200);
        Assert.DoesNotContain(results, x => ((FluentTestModelA)x).PropertyA == 300);
    }

    [Fact]
    public async Task CombinedQuery_WithSelectFieldsAndWhere_ShouldWork()
    {
        // Arrange
        string partitionKey1 = Guid.NewGuid().ToString("N");
        string partitionKey2 = Guid.NewGuid().ToString("N");
        string partitionKey3 = Guid.NewGuid().ToString("N");

        FluentTestModelA modelA = new()
        {
            PrettyPartitionA = partitionKey1,
            PrettyRowA = "ignored",
            TypeA = "Type A",
            PropertyA = 175
        };

        FluentTestModelB modelB1 = new()
        {
            PrettyPartitionB = partitionKey2,
            PrettyRowB = "ignored",
            TypeB = "Type B1",
            PropertyB = true
        };

        FluentTestModelB modelB2 = new()
        {
            PrettyPartitionB = partitionKey3,
            PrettyRowB = "ignored",
            TypeB = "Type B2",
            PropertyB = false
        };

        await Context.FluentRowKeyModels.UpsertEntityAsync(modelA);
        await Context.FluentRowKeyModels.UpsertEntityAsync(modelB1);
        await Context.FluentRowKeyModels.UpsertEntityAsync(modelB2);

        // Act
        var results = await Context.FluentRowKeyModels
            .SelectFluentTestModelBFields((x) => x.PropertyB)
            .Where((FluentTestModelB x) => x.TypeB == "Type B1")
            .ToListAsync();

        // Assert
        Assert.NotEmpty(results);
        Assert.All(results, x =>
        {
            Assert.Equal(true, x["PropertyB"]);
            Assert.Null(x["TypeB"]);
        });
    }

    #endregion

    #region Async Enumeration Tests

    [Fact]
    public async Task WhereFirstType_WithAsyncEnumeration_ShouldWork()
    {
        // Arrange
        string partitionKey1 = Guid.NewGuid().ToString("N");
        string partitionKey2 = Guid.NewGuid().ToString("N");
        string partitionKey3 = Guid.NewGuid().ToString("N");

        FluentTestModelA modelA1 = new()
        {
            PrettyPartitionA = partitionKey1,
            PrettyRowA = "ignored",
            TypeA = "Type A1",
            PropertyA = 100
        };

        FluentTestModelA modelA2 = new()
        {
            PrettyPartitionA = partitionKey2,
            PrettyRowA = "ignored",
            TypeA = "Type A2",
            PropertyA = 200
        };

        FluentTestModelB modelB = new()
        {
            PrettyPartitionB = partitionKey3,
            PrettyRowB = "ignored",
            TypeB = "Type B",
            PropertyB = true
        };

        await Context.FluentRowKeyModels.UpsertEntityAsync(modelA1);
        await Context.FluentRowKeyModels.UpsertEntityAsync(modelA2);
        await Context.FluentRowKeyModels.UpsertEntityAsync(modelB);

        // Act
        var results = new List<FluentTestModelA>();
        await foreach (var item in Context.FluentRowKeyModels.WhereFirstType())
        {
            results.Add(item);
        }

        // Assert
        Assert.NotEmpty(results);
        Assert.All(results, x => Assert.IsType<FluentTestModelA>(x));
    }

    [Fact]
    public async Task WhereSecondType_WithAsyncEnumeration_ShouldWork()
    {
        // Arrange
        string partitionKey1 = Guid.NewGuid().ToString("N");
        string partitionKey2 = Guid.NewGuid().ToString("N");
        string partitionKey3 = Guid.NewGuid().ToString("N");

        FluentTestModelA modelA = new()
        {
            PrettyPartitionA = partitionKey1,
            PrettyRowA = "ignored",
            TypeA = "Type A",
            PropertyA = 125
        };

        FluentTestModelB modelB1 = new()
        {
            PrettyPartitionB = partitionKey2,
            PrettyRowB = "ignored",
            TypeB = "Type B1",
            PropertyB = true
        };

        FluentTestModelB modelB2 = new()
        {
            PrettyPartitionB = partitionKey3,
            PrettyRowB = "ignored",
            TypeB = "Type B2",
            PropertyB = false
        };

        await Context.FluentRowKeyModels.UpsertEntityAsync(modelA);
        await Context.FluentRowKeyModels.UpsertEntityAsync(modelB1);
        await Context.FluentRowKeyModels.UpsertEntityAsync(modelB2);

        // Act
        var results = new List<FluentTestModelB>();
        await foreach (var item in Context.FluentRowKeyModels.WhereSecondType())
        {
            results.Add(item);
        }

        // Assert
        Assert.NotEmpty(results);
        Assert.All(results, x => Assert.IsType<FluentTestModelB>(x));
    }

    #endregion

    #region FirstAsync / FirstOrDefaultAsync Tests

    [Fact]
    public async Task FirstAsync_WithWhereFirstType_ShouldReturnFirstMatch()
    {
        // Arrange
        string partitionKey1 = Guid.NewGuid().ToString("N");
        string partitionKey2 = Guid.NewGuid().ToString("N");

        FluentTestModelA modelA = new()
        {
            PrettyPartitionA = partitionKey1,
            PrettyRowA = "ignored",
            TypeA = "Type A",
            PropertyA = 100
        };

        FluentTestModelB modelB = new()
        {
            PrettyPartitionB = partitionKey2,
            PrettyRowB = "ignored",
            TypeB = "Type B",
            PropertyB = false
        };

        await Context.FluentRowKeyModels.UpsertEntityAsync(modelA);
        await Context.FluentRowKeyModels.UpsertEntityAsync(modelB);

        // Act
        var result = await Context.FluentRowKeyModels
            .WhereFirstType()
            .FirstAsync();

        // Assert
        Assert.NotNull(result);
        Assert.IsType<FluentTestModelA>(result);
    }

    [Fact]
    public async Task FirstOrDefaultAsync_WithNoMatches_ShouldReturnNull()
    {
        // Arrange
        string partitionKey1 = Guid.NewGuid().ToString("N");
        string partitionKey2 = Guid.NewGuid().ToString("N");

        FluentTestModelA modelA = new()
        {
            PrettyPartitionA = partitionKey1,
            PrettyRowA = "ignored",
            TypeA = "Type A",
            PropertyA = 100
        };

        FluentTestModelB modelB = new()
        {
            PrettyPartitionB = partitionKey2,
            PrettyRowB = "ignored",
            TypeB = "Type B",
            PropertyB = true
        };

        await Context.FluentRowKeyModels.UpsertEntityAsync(modelA);
        await Context.FluentRowKeyModels.UpsertEntityAsync(modelB);

        // Act
        var result = await Context.FluentRowKeyModels
            .WhereFirstType()
            .Where(x => x.PropertyA > 500)
            .FirstOrDefaultAsync();

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task SingleAsync_WithOneMatch_ShouldReturnMatch()
    {
        // Arrange
        string partitionKey1 = Guid.NewGuid().ToString("N");
        string partitionKey2 = Guid.NewGuid().ToString("N");
        string uniquePartitionKey = Guid.NewGuid().ToString("N");

        FluentTestModelA modelA = new()
        {
            PrettyPartitionA = partitionKey1,
            PrettyRowA = "ignored",
            TypeA = "Type A",
            PropertyA = 275
        };

        FluentTestModelB modelB = new()
        {
            PrettyPartitionB = uniquePartitionKey,
            PrettyRowB = "ignored",
            TypeB = "Unique Type",
            PropertyB = true
        };

        await Context.FluentRowKeyModels.UpsertEntityAsync(modelA);
        await Context.FluentRowKeyModels.UpsertEntityAsync(modelB);

        // Act
        var result = await Context.FluentRowKeyModels
            .WhereSecondType()
            .Where(x => x.TypeB == "Unique Type")
            .SingleAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Unique Type", result.TypeB);
    }

    [Fact]
    public async Task SingleOrDefaultAsync_WithNoMatches_ShouldReturnNull()
    {
        // Arrange
        string partitionKey1 = Guid.NewGuid().ToString("N");
        string partitionKey2 = Guid.NewGuid().ToString("N");

        FluentTestModelA modelA = new()
        {
            PrettyPartitionA = partitionKey1,
            PrettyRowA = "ignored",
            TypeA = "Type A",
            PropertyA = 325
        };

        FluentTestModelB modelB = new()
        {
            PrettyPartitionB = partitionKey2,
            PrettyRowB = "ignored",
            TypeB = "Type B",
            PropertyB = false
        };

        await Context.FluentRowKeyModels.UpsertEntityAsync(modelA);
        await Context.FluentRowKeyModels.UpsertEntityAsync(modelB);

        // Act
        var result = await Context.FluentRowKeyModels
            .WhereSecondType()
            .Where(x => x.TypeB == "NonExistentType")
            .SingleOrDefaultAsync();

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region CountAsync Tests

    [Fact]
    public async Task CountAsync_WithWhereFirstType_ShouldReturnCorrectCount()
    {
        // Arrange
        string partitionKey1 = Guid.NewGuid().ToString("N");
        string partitionKey2 = Guid.NewGuid().ToString("N");
        string partitionKey3 = Guid.NewGuid().ToString("N");

        FluentTestModelA modelA1 = new()
        {
            PrettyPartitionA = partitionKey1,
            PrettyRowA = "ignored",
            TypeA = "Type A1",
            PropertyA = 100
        };

        FluentTestModelA modelA2 = new()
        {
            PrettyPartitionA = partitionKey2,
            PrettyRowA = "ignored",
            TypeA = "Type A2",
            PropertyA = 200
        };

        FluentTestModelB modelB = new()
        {
            PrettyPartitionB = partitionKey3,
            PrettyRowB = "ignored",
            TypeB = "Type B",
            PropertyB = true
        };

        await Context.FluentRowKeyModels.UpsertEntityAsync(modelA1);
        await Context.FluentRowKeyModels.UpsertEntityAsync(modelA2);
        await Context.FluentRowKeyModels.UpsertEntityAsync(modelB);

        // Act
        var list = await Context.FluentRowKeyModels.WhereFirstType().ToListAsync();
        int count = await Context.FluentRowKeyModels.WhereFirstType().CountAsync();

        // Assert
        Assert.Equal(list.Count, count);
        Assert.True(count >= 2);
    }

    #endregion

    #region Delete Tests

    [Fact]
    public async Task DeleteFluentTestModelAAsync_ShouldRemoveEntity()
    {
        // Arrange
        string partitionKey = Guid.NewGuid().ToString("N");
        var modelA = new FluentTestModelA
        {
            PrettyPartitionA = partitionKey,
            PrettyRowA = "ignored",
            TypeA = "Delete Test",
            PropertyA = 456
        };
        await Context.FluentRowKeyModels.UpsertEntityAsync(modelA);

        // Act
        await Context.FluentRowKeyModels.DeleteFluentTestModelAAsync(partitionKey);

        // Assert
        var retrieved = await Context.FluentRowKeyModels.FindFluentTestModelAAsync(partitionKey);
        Assert.Null(retrieved);
    }

    #endregion
}