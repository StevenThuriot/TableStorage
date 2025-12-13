using System.Linq.Expressions;
using TableStorage.Tests.Infrastructure;
using TableStorage.Tests.Models;

namespace TableStorage.Tests;

/// <summary>
/// Tests for FluentTableEntity extension methods
/// Covers FindAsync, WhereFirstType, WhereSecondType, and type-specific query operations
/// </summary>
public class FluentQueryTests(AzuriteFixture azuriteFixture) : AzuriteTestBase(azuriteFixture)
{
    #region FindAsync Tests

    [Fact]
    public async Task FindAsync_WithSingleRowKey_ShouldReturnCorrectEntity()
    {
        // Arrange
        FluentTestModelA modelA = new()
        {
            PrettyPartitionA = Guid.NewGuid().ToString("N"),
            PrettyRowA = Guid.NewGuid().ToString("N"),
            TypeA = "Test Type A",
            PropertyA = 123
        };

        FluentTestModelB modelB = new()
        {
            PrettyPartitionB = Guid.NewGuid().ToString("N"),
            PrettyRowB = Guid.NewGuid().ToString("N"),
            TypeB = "Test Type B",
            PropertyB = false
        };

        await Context.FluentModels.UpsertEntityAsync(modelA);
        await Context.FluentModels.UpsertEntityAsync(modelB);

        // Act
        var retrieved = await Context.FluentModels.FindAsync(modelA.PrettyPartitionA, modelA.PrettyRowA);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(modelA.PrettyPartitionA, retrieved.PartitionKey);
        Assert.Equal(modelA.PrettyRowA, retrieved.RowKey);
    }

    [Fact]
    public async Task FindAsync_WithNonExistentRowKey_ShouldReturnNull()
    {
        // Arrange
        FluentTestModelA modelA = new()
        {
            PrettyPartitionA = Guid.NewGuid().ToString("N"),
            PrettyRowA = Guid.NewGuid().ToString("N"),
            TypeA = "Test Type A",
            PropertyA = 100
        };

        FluentTestModelB modelB = new()
        {
            PrettyPartitionB = Guid.NewGuid().ToString("N"),
            PrettyRowB = Guid.NewGuid().ToString("N"),
            TypeB = "Test Type B",
            PropertyB = true
        };

        await Context.FluentModels.UpsertEntityAsync(modelA);
        await Context.FluentModels.UpsertEntityAsync(modelB);

        string nonExistentPartitionKey = Guid.NewGuid().ToString("N");
        string nonExistentRowKey = Guid.NewGuid().ToString("N");

        // Act
        var retrieved = await Context.FluentModels.FindAsync(nonExistentPartitionKey, nonExistentRowKey);

        // Assert
        Assert.Null(retrieved);
    }

    #endregion

    #region WhereFirstType Tests

    [Fact]
    public async Task WhereFirstType_ShouldReturnOnlyFirstType()
    {
        // Arrange
        FluentTestModelA modelA1 = new()
        {
            PrettyPartitionA = Guid.NewGuid().ToString("N"),
            PrettyRowA = Guid.NewGuid().ToString("N"),
            TypeA = "Type A1",
            PropertyA = 100
        };

        FluentTestModelA modelA2 = new()
        {
            PrettyPartitionA = Guid.NewGuid().ToString("N"),
            PrettyRowA = Guid.NewGuid().ToString("N"),
            TypeA = "Type A2",
            PropertyA = 200
        };

        FluentTestModelB modelB = new()
        {
            PrettyPartitionB = Guid.NewGuid().ToString("N"),
            PrettyRowB = Guid.NewGuid().ToString("N"),
            TypeB = "Type B",
            PropertyB = true
        };

        await Context.FluentModels.UpsertEntityAsync(modelA1);
        await Context.FluentModels.UpsertEntityAsync(modelA2);
        await Context.FluentModels.UpsertEntityAsync(modelB);

        // Act
        var results = await Context.FluentModels
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
        FluentTestModelA modelA1 = new()
        {
            PrettyPartitionA = Guid.NewGuid().ToString("N"),
            PrettyRowA = Guid.NewGuid().ToString("N"),
            TypeA = "Match",
            PropertyA = 100
        };

        FluentTestModelA modelA2 = new()
        {
            PrettyPartitionA = Guid.NewGuid().ToString("N"),
            PrettyRowA = Guid.NewGuid().ToString("N"),
            TypeA = "NoMatch",
            PropertyA = 200
        };

        FluentTestModelB modelB = new()
        {
            PrettyPartitionB = Guid.NewGuid().ToString("N"),
            PrettyRowB = Guid.NewGuid().ToString("N"),
            TypeB = "Type B",
            PropertyB = true
        };

        await Context.FluentModels.UpsertEntityAsync(modelA1);
        await Context.FluentModels.UpsertEntityAsync(modelA2);
        await Context.FluentModels.UpsertEntityAsync(modelB);

        // Act
        var results = await Context.FluentModels
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
        FluentTestModelA modelA = new()
        {
            PrettyPartitionA = Guid.NewGuid().ToString("N"),
            PrettyRowA = Guid.NewGuid().ToString("N"),
            TypeA = "Type A",
            PropertyA = 100
        };

        FluentTestModelB modelB1 = new()
        {
            PrettyPartitionB = Guid.NewGuid().ToString("N"),
            PrettyRowB = Guid.NewGuid().ToString("N"),
            TypeB = "Type B1",
            PropertyB = true
        };

        FluentTestModelB modelB2 = new()
        {
            PrettyPartitionB = Guid.NewGuid().ToString("N"),
            PrettyRowB = Guid.NewGuid().ToString("N"),
            TypeB = "Type B2",
            PropertyB = false
        };

        await Context.FluentModels.UpsertEntityAsync(modelA);
        await Context.FluentModels.UpsertEntityAsync(modelB1);
        await Context.FluentModels.UpsertEntityAsync(modelB2);

        // Act
        var results = await Context.FluentModels
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
        FluentTestModelA modelA = new()
        {
            PrettyPartitionA = Guid.NewGuid().ToString("N"),
            PrettyRowA = Guid.NewGuid().ToString("N"),
            TypeA = "Type A",
            PropertyA = 150
        };

        FluentTestModelB modelB1 = new()
        {
            PrettyPartitionB = Guid.NewGuid().ToString("N"),
            PrettyRowB = Guid.NewGuid().ToString("N"),
            TypeB = "Type B1",
            PropertyB = true
        };

        FluentTestModelB modelB2 = new()
        {
            PrettyPartitionB = Guid.NewGuid().ToString("N"),
            PrettyRowB = Guid.NewGuid().ToString("N"),
            TypeB = "Type B2",
            PropertyB = false
        };

        await Context.FluentModels.UpsertEntityAsync(modelA);
        await Context.FluentModels.UpsertEntityAsync(modelB1);
        await Context.FluentModels.UpsertEntityAsync(modelB2);

        // Act
        var results = await Context.FluentModels
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
        FluentTestModelA modelA1 = new()
        {
            PrettyPartitionA = Guid.NewGuid().ToString("N"),
            PrettyRowA = Guid.NewGuid().ToString("N"),
            TypeA = "Match",
            PropertyA = 150
        };

        FluentTestModelA modelA2 = new()
        {
            PrettyPartitionA = Guid.NewGuid().ToString("N"),
            PrettyRowA = Guid.NewGuid().ToString("N"),
            TypeA = "NoMatch",
            PropertyA = 50
        };

        FluentTestModelB modelB = new()
        {
            PrettyPartitionB = Guid.NewGuid().ToString("N"),
            PrettyRowB = Guid.NewGuid().ToString("N"),
            TypeB = "Type B",
            PropertyB = true
        };

        await Context.FluentModels.UpsertEntityAsync(modelA1);
        await Context.FluentModels.UpsertEntityAsync(modelA2);
        await Context.FluentModels.UpsertEntityAsync(modelB);

        // Act
        var results = await Context.FluentModels
            .Where((FluentTestModelA x) => x.PropertyA > 100)
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
        FluentTestModelA modelA = new()
        {
            PrettyPartitionA = Guid.NewGuid().ToString("N"),
            PrettyRowA = Guid.NewGuid().ToString("N"),
            TypeA = "Type A",
            PropertyA = 100
        };

        FluentTestModelB modelB1 = new()
        {
            PrettyPartitionB = Guid.NewGuid().ToString("N"),
            PrettyRowB = Guid.NewGuid().ToString("N"),
            TypeB = "Type B1",
            PropertyB = true
        };

        FluentTestModelB modelB2 = new()
        {
            PrettyPartitionB = Guid.NewGuid().ToString("N"),
            PrettyRowB = Guid.NewGuid().ToString("N"),
            TypeB = "Type B2",
            PropertyB = false
        };

        await Context.FluentModels.UpsertEntityAsync(modelA);
        await Context.FluentModels.UpsertEntityAsync(modelB1);
        await Context.FluentModels.UpsertEntityAsync(modelB2);

        // Act
        var results = await Context.FluentModels
            .Where((FluentTestModelB x) => x.PropertyB == false)
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
        FluentTestModelA modelA = new()
        {
            PrettyPartitionA = Guid.NewGuid().ToString("N"),
            PrettyRowA = Guid.NewGuid().ToString("N"),
            TypeA = "Type A",
            PropertyA = 100
        };

        FluentTestModelB modelB = new()
        {
            PrettyPartitionB = Guid.NewGuid().ToString("N"),
            PrettyRowB = Guid.NewGuid().ToString("N"),
            TypeB = "Type B",
            PropertyB = true
        };

        await Context.FluentModels.UpsertEntityAsync(modelA);
        await Context.FluentModels.UpsertEntityAsync(modelB);

        // Act
        var results = await Context.FluentModels
            .SelectFields((FluentTestModelA x) => x.TypeA)
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
        FluentTestModelA modelA = new()
        {
            PrettyPartitionA = Guid.NewGuid().ToString("N"),
            PrettyRowA = Guid.NewGuid().ToString("N"),
            TypeA = "Type A",
            PropertyA = 100
        };

        FluentTestModelB modelB = new()
        {
            PrettyPartitionB = Guid.NewGuid().ToString("N"),
            PrettyRowB = Guid.NewGuid().ToString("N"),
            TypeB = "Type B",
            PropertyB = true
        };

        await Context.FluentModels.UpsertEntityAsync(modelA);
        await Context.FluentModels.UpsertEntityAsync(modelB);

        // Act
        var results = await Context.FluentModels
            .SelectFields((FluentTestModelB x) => x.TypeB)
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
        FluentTestModelA modelA1 = new()
        {
            PrettyPartitionA = Guid.NewGuid().ToString("N"),
            PrettyRowA = Guid.NewGuid().ToString("N"),
            TypeA = "Type A1",
            PropertyA = 100
        };

        FluentTestModelA modelA2 = new()
        {
            PrettyPartitionA = Guid.NewGuid().ToString("N"),
            PrettyRowA = Guid.NewGuid().ToString("N"),
            TypeA = "Type A2",
            PropertyA = 200
        };

        FluentTestModelA modelA3 = new()
        {
            PrettyPartitionA = Guid.NewGuid().ToString("N"),
            PrettyRowA = Guid.NewGuid().ToString("N"),
            TypeA = "Type A3",
            PropertyA = 300
        };

        FluentTestModelB modelB = new()
        {
            PrettyPartitionB = Guid.NewGuid().ToString("N"),
            PrettyRowB = Guid.NewGuid().ToString("N"),
            TypeB = "Type B",
            PropertyB = false
        };

        await Context.FluentModels.UpsertEntityAsync(modelA1);
        await Context.FluentModels.UpsertEntityAsync(modelA2);
        await Context.FluentModels.UpsertEntityAsync(modelA3);
        await Context.FluentModels.UpsertEntityAsync(modelB);

        // Act
        var results = await Context.FluentModels
            .ExistsIn((FluentTestModelA x) => x.PropertyA, [100, 200, 400])
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
        FluentTestModelA modelA = new()
        {
            PrettyPartitionA = Guid.NewGuid().ToString("N"),
            PrettyRowA = Guid.NewGuid().ToString("N"),
            TypeA = "Type A",
            PropertyA = 150
        };

        FluentTestModelB modelB1 = new()
        {
            PrettyPartitionB = Guid.NewGuid().ToString("N"),
            PrettyRowB = Guid.NewGuid().ToString("N"),
            TypeB = "Type B1",
            PropertyB = true
        };

        FluentTestModelB modelB2 = new()
        {
            PrettyPartitionB = Guid.NewGuid().ToString("N"),
            PrettyRowB = Guid.NewGuid().ToString("N"),
            TypeB = "Type B2",
            PropertyB = false
        };

        FluentTestModelB modelB3 = new()
        {
            PrettyPartitionB = Guid.NewGuid().ToString("N"),
            PrettyRowB = Guid.NewGuid().ToString("N"),
            TypeB = "Type B3",
            PropertyB = true
        };

        await Context.FluentModels.UpsertEntityAsync(modelA);
        await Context.FluentModels.UpsertEntityAsync(modelB1);
        await Context.FluentModels.UpsertEntityAsync(modelB2);
        await Context.FluentModels.UpsertEntityAsync(modelB3);

        // Act
        var results = await Context.FluentModels
            .ExistsIn((FluentTestModelB x) => x.TypeB, ["Type B1", "Type B3", "Type B4"])
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
        FluentTestModelA modelA1 = new()
        {
            PrettyPartitionA = Guid.NewGuid().ToString("N"),
            PrettyRowA = Guid.NewGuid().ToString("N"),
            TypeA = "Type A1",
            PropertyA = 100
        };

        FluentTestModelA modelA2 = new()
        {
            PrettyPartitionA = Guid.NewGuid().ToString("N"),
            PrettyRowA = Guid.NewGuid().ToString("N"),
            TypeA = "Type A2",
            PropertyA = 200
        };

        FluentTestModelA modelA3 = new()
        {
            PrettyPartitionA = Guid.NewGuid().ToString("N"),
            PrettyRowA = Guid.NewGuid().ToString("N"),
            TypeA = "Type A3",
            PropertyA = 300
        };

        FluentTestModelB modelB = new()
        {
            PrettyPartitionB = Guid.NewGuid().ToString("N"),
            PrettyRowB = Guid.NewGuid().ToString("N"),
            TypeB = "Type B",
            PropertyB = true
        };

        await Context.FluentModels.UpsertEntityAsync(modelA1);
        await Context.FluentModels.UpsertEntityAsync(modelA2);
        await Context.FluentModels.UpsertEntityAsync(modelA3);
        await Context.FluentModels.UpsertEntityAsync(modelB);

        // Act
        var results = await Context.FluentModels
            .NotExistsIn((FluentTestModelA x) => x.PropertyA, [100, 400])
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
        FluentTestModelA modelA = new()
        {
            PrettyPartitionA = Guid.NewGuid().ToString("N"),
            PrettyRowA = Guid.NewGuid().ToString("N"),
            TypeA = "Type A",
            PropertyA = 250
        };

        FluentTestModelB modelB1 = new()
        {
            PrettyPartitionB = Guid.NewGuid().ToString("N"),
            PrettyRowB = Guid.NewGuid().ToString("N"),
            TypeB = "Type B1",
            PropertyB = true
        };

        FluentTestModelB modelB2 = new()
        {
            PrettyPartitionB = Guid.NewGuid().ToString("N"),
            PrettyRowB = Guid.NewGuid().ToString("N"),
            TypeB = "Type B2",
            PropertyB = false
        };

        FluentTestModelB modelB3 = new()
        {
            PrettyPartitionB = Guid.NewGuid().ToString("N"),
            PrettyRowB = Guid.NewGuid().ToString("N"),
            TypeB = "Type B3",
            PropertyB = true
        };

        await Context.FluentModels.UpsertEntityAsync(modelA);
        await Context.FluentModels.UpsertEntityAsync(modelB1);
        await Context.FluentModels.UpsertEntityAsync(modelB2);
        await Context.FluentModels.UpsertEntityAsync(modelB3);

        // Act
        var results = await Context.FluentModels
            .NotExistsIn((FluentTestModelB x) => x.TypeB, ["Type B1", "Type B4"])
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
        FluentTestModelA modelA1 = new()
        {
            PrettyPartitionA = Guid.NewGuid().ToString("N"),
            PrettyRowA = Guid.NewGuid().ToString("N"),
            TypeA = "Match",
            PropertyA = 150
        };

        FluentTestModelA modelA2 = new()
        {
            PrettyPartitionA = Guid.NewGuid().ToString("N"),
            PrettyRowA = Guid.NewGuid().ToString("N"),
            TypeA = "Match",
            PropertyA = 50
        };

        FluentTestModelA modelA3 = new()
        {
            PrettyPartitionA = Guid.NewGuid().ToString("N"),
            PrettyRowA = Guid.NewGuid().ToString("N"),
            TypeA = "NoMatch",
            PropertyA = 150
        };

        await Context.FluentModels.UpsertEntityAsync(modelA1);
        await Context.FluentModels.UpsertEntityAsync(modelA2);
        await Context.FluentModels.UpsertEntityAsync(modelA3);

        // Act
        var results = await Context.FluentModels
            .WhereFirstType()
            .Where(x => x.TypeA == "Match")
            .Where(x => x.PropertyA > 100)
            .ToListAsync();

        // Assert
        Assert.Single(results);
        Assert.Equal(modelA1.PrettyRowA, results[0].PrettyRowA);
        Assert.Equal("Match", results[0].TypeA);
        Assert.Equal(150, results[0].PropertyA);
    }

    [Fact]
    public async Task CombinedQuery_WithExistsInAndNotExistsIn_ShouldWork()
    {
        // Arrange
        FluentTestModelA modelA1 = new()
        {
            PrettyPartitionA = Guid.NewGuid().ToString("N"),
            PrettyRowA = Guid.NewGuid().ToString("N"),
            TypeA = "Type A1",
            PropertyA = 100
        };

        FluentTestModelA modelA2 = new()
        {
            PrettyPartitionA = Guid.NewGuid().ToString("N"),
            PrettyRowA = Guid.NewGuid().ToString("N"),
            TypeA = "Type A2",
            PropertyA = 200
        };

        FluentTestModelA modelA3 = new()
        {
            PrettyPartitionA = Guid.NewGuid().ToString("N"),
            PrettyRowA = Guid.NewGuid().ToString("N"),
            TypeA = "Type A3",
            PropertyA = 300
        };

        FluentTestModelB modelB = new()
        {
            PrettyPartitionB = Guid.NewGuid().ToString("N"),
            PrettyRowB = Guid.NewGuid().ToString("N"),
            TypeB = "Type B",
            PropertyB = false
        };

        await Context.FluentModels.UpsertEntityAsync(modelA1);
        await Context.FluentModels.UpsertEntityAsync(modelA2);
        await Context.FluentModels.UpsertEntityAsync(modelA3);
        await Context.FluentModels.UpsertEntityAsync(modelB);

        // Act
        var results = await Context.FluentModels
            .ExistsIn((FluentTestModelA x) => x.PropertyA, [100, 200, 300])
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
        FluentTestModelA modelA = new()
        {
            PrettyPartitionA = Guid.NewGuid().ToString("N"),
            PrettyRowA = Guid.NewGuid().ToString("N"),
            TypeA = "Type A",
            PropertyA = 175
        };

        FluentTestModelB modelB1 = new()
        {
            PrettyPartitionB = Guid.NewGuid().ToString("N"),
            PrettyRowB = Guid.NewGuid().ToString("N"),
            TypeB = "Type B1",
            PropertyB = true
        };

        FluentTestModelB modelB2 = new()
        {
            PrettyPartitionB = Guid.NewGuid().ToString("N"),
            PrettyRowB = Guid.NewGuid().ToString("N"),
            TypeB = "Type B2",
            PropertyB = false
        };

        await Context.FluentModels.UpsertEntityAsync(modelA);
        await Context.FluentModels.UpsertEntityAsync(modelB1);
        await Context.FluentModels.UpsertEntityAsync(modelB2);

        // Act
        var results = await Context.FluentModels
            .SelectFields((FluentTestModelB x) => x.PropertyB)
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
        FluentTestModelA modelA1 = new()
        {
            PrettyPartitionA = Guid.NewGuid().ToString("N"),
            PrettyRowA = Guid.NewGuid().ToString("N"),
            TypeA = "Type A1",
            PropertyA = 100
        };

        FluentTestModelA modelA2 = new()
        {
            PrettyPartitionA = Guid.NewGuid().ToString("N"),
            PrettyRowA = Guid.NewGuid().ToString("N"),
            TypeA = "Type A2",
            PropertyA = 200
        };

        FluentTestModelB modelB = new()
        {
            PrettyPartitionB = Guid.NewGuid().ToString("N"),
            PrettyRowB = Guid.NewGuid().ToString("N"),
            TypeB = "Type B",
            PropertyB = true
        };

        await Context.FluentModels.UpsertEntityAsync(modelA1);
        await Context.FluentModels.UpsertEntityAsync(modelA2);
        await Context.FluentModels.UpsertEntityAsync(modelB);

        // Act
        var results = new List<FluentTestModelA>();
        await foreach (var item in Context.FluentModels.WhereFirstType())
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
        FluentTestModelA modelA = new()
        {
            PrettyPartitionA = Guid.NewGuid().ToString("N"),
            PrettyRowA = Guid.NewGuid().ToString("N"),
            TypeA = "Type A",
            PropertyA = 125
        };

        FluentTestModelB modelB1 = new()
        {
            PrettyPartitionB = Guid.NewGuid().ToString("N"),
            PrettyRowB = Guid.NewGuid().ToString("N"),
            TypeB = "Type B1",
            PropertyB = true
        };

        FluentTestModelB modelB2 = new()
        {
            PrettyPartitionB = Guid.NewGuid().ToString("N"),
            PrettyRowB = Guid.NewGuid().ToString("N"),
            TypeB = "Type B2",
            PropertyB = false
        };

        await Context.FluentModels.UpsertEntityAsync(modelA);
        await Context.FluentModels.UpsertEntityAsync(modelB1);
        await Context.FluentModels.UpsertEntityAsync(modelB2);

        // Act
        var results = new List<FluentTestModelB>();
        await foreach (var item in Context.FluentModels.WhereSecondType())
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
        FluentTestModelA modelA = new()
        {
            PrettyPartitionA = Guid.NewGuid().ToString("N"),
            PrettyRowA = Guid.NewGuid().ToString("N"),
            TypeA = "Type A",
            PropertyA = 100
        };

        FluentTestModelB modelB = new()
        {
            PrettyPartitionB = Guid.NewGuid().ToString("N"),
            PrettyRowB = Guid.NewGuid().ToString("N"),
            TypeB = "Type B",
            PropertyB = false
        };

        await Context.FluentModels.UpsertEntityAsync(modelA);
        await Context.FluentModels.UpsertEntityAsync(modelB);

        // Act
        var result = await Context.FluentModels
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
        FluentTestModelA modelA = new()
        {
            PrettyPartitionA = Guid.NewGuid().ToString("N"),
            PrettyRowA = Guid.NewGuid().ToString("N"),
            TypeA = "Type A",
            PropertyA = 100
        };

        FluentTestModelB modelB = new()
        {
            PrettyPartitionB = Guid.NewGuid().ToString("N"),
            PrettyRowB = Guid.NewGuid().ToString("N"),
            TypeB = "Type B",
            PropertyB = true
        };

        await Context.FluentModels.UpsertEntityAsync(modelA);
        await Context.FluentModels.UpsertEntityAsync(modelB);

        // Act
        var result = await Context.FluentModels
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
        FluentTestModelA modelA = new()
        {
            PrettyPartitionA = Guid.NewGuid().ToString("N"),
            PrettyRowA = Guid.NewGuid().ToString("N"),
            TypeA = "Type A",
            PropertyA = 275
        };

        string uniquePartitionKey = Guid.NewGuid().ToString("N");
        string uniqueRowKey = Guid.NewGuid().ToString("N");
        FluentTestModelB modelB = new()
        {
            PrettyPartitionB = uniquePartitionKey,
            PrettyRowB = uniqueRowKey,
            TypeB = "Unique Type",
            PropertyB = true
        };

        await Context.FluentModels.UpsertEntityAsync(modelA);
        await Context.FluentModels.UpsertEntityAsync(modelB);

        // Act
        var result = await Context.FluentModels
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
        FluentTestModelA modelA = new()
        {
            PrettyPartitionA = Guid.NewGuid().ToString("N"),
            PrettyRowA = Guid.NewGuid().ToString("N"),
            TypeA = "Type A",
            PropertyA = 325
        };

        FluentTestModelB modelB = new()
        {
            PrettyPartitionB = Guid.NewGuid().ToString("N"),
            PrettyRowB = Guid.NewGuid().ToString("N"),
            TypeB = "Type B",
            PropertyB = false
        };

        await Context.FluentModels.UpsertEntityAsync(modelA);
        await Context.FluentModels.UpsertEntityAsync(modelB);

        // Act
        var result = await Context.FluentModels
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
        FluentTestModelA modelA1 = new()
        {
            PrettyPartitionA = Guid.NewGuid().ToString("N"),
            PrettyRowA = Guid.NewGuid().ToString("N"),
            TypeA = "Type A1",
            PropertyA = 100
        };

        FluentTestModelA modelA2 = new()
        {
            PrettyPartitionA = Guid.NewGuid().ToString("N"),
            PrettyRowA = Guid.NewGuid().ToString("N"),
            TypeA = "Type A2",
            PropertyA = 200
        };

        FluentTestModelB modelB = new()
        {
            PrettyPartitionB = Guid.NewGuid().ToString("N"),
            PrettyRowB = Guid.NewGuid().ToString("N"),
            TypeB = "Type B",
            PropertyB = true
        };

        await Context.FluentModels.UpsertEntityAsync(modelA1);
        await Context.FluentModels.UpsertEntityAsync(modelA2);
        await Context.FluentModels.UpsertEntityAsync(modelB);

        // Act
        var list = await Context.FluentModels.WhereFirstType().ToListAsync();
        int count = await Context.FluentModels.WhereFirstType().CountAsync();

        // Assert
        Assert.Equal(list.Count, count);
        Assert.True(count >= 2);
    }

    #endregion
}
