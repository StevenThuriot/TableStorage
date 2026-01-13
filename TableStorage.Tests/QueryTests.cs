using TableStorage.Tests.Contexts;
using TableStorage.Tests.Infrastructure;
using TableStorage.Tests.Models;

namespace TableStorage.Tests;

/// <summary>
/// Tests for basic query operations including filtering, counting, and finding entities
/// Covers both table storage and blob storage query operations
/// </summary>
public class QueryTests(AzuriteFixture azuriteFixture) : AzuriteTestBase(azuriteFixture)
{
    #region Table Storage Query Tests

    [Fact]
    public async Task Table_ToListAsync_ShouldReturnAllModels()
    {
        // Arrange
        await Context.Models2.UpsertEntityAsync(new()
        {
            PartitionKey = "root",
            PrettyRow = Guid.NewGuid().ToString("N"),
            MyProperty1 = 1,
            MyProperty2 = "test 1"
        });

        await Context.Models2.UpsertEntityAsync(new()
        {
            PartitionKey = "root",
            PrettyRow = Guid.NewGuid().ToString("N"),
            MyProperty1 = 2,
            MyProperty2 = "test 2"
        });

        // Act
        List<Model2> models = await Context.Models2.ToListAsync();

        // Assert
        Assert.NotEmpty(models);
        Assert.True(models.Count >= 2);
    }

    [Fact]
    public async Task Table_FindAsync_ShouldReturnRequestedEntities()
    {
        // Arrange
        Model2 model1 = new()
        {
            PartitionKey = "root",
            PrettyRow = Guid.NewGuid().ToString("N"),
            MyProperty1 = 1,
            MyProperty2 = "test 1"
        };
        Model2 model2 = new()
        {
            PartitionKey = "root",
            PrettyRow = Guid.NewGuid().ToString("N"),
            MyProperty1 = 2,
            MyProperty2 = "test 2"
        };

        await Context.Models2.UpsertEntityAsync(model1);
        await Context.Models2.UpsertEntityAsync(model2);

        // Act
        List<Model2> findResults = await Context.Models2
            .FindAsync((model1.PartitionKey, model1.PrettyRow), (model2.PartitionKey, model2.PrettyRow))
            .ToListAsync();

        // Assert
        Assert.Equal(2, findResults.Count);
    }

    [Fact]
    public async Task Table_Where_WithEnumFilters_ShouldFilterCorrectly()
    {
        // Arrange
        await Context.Models2.UpsertEntityAsync(new()
        {
            PartitionKey = "root",
            PrettyRow = Guid.NewGuid().ToString("N"),
            MyProperty1 = 1,
            MyProperty2 = "test",
            MyProperty7 = ModelEnum.Yes,
            MyProperty8 = ModelEnum.No
        });

        await Context.Models2.UpsertEntityAsync(new()
        {
            PartitionKey = "root",
            PrettyRow = Guid.NewGuid().ToString("N"),
            MyProperty1 = 2,
            MyProperty2 = "test2",
            MyProperty7 = ModelEnum.No,
            MyProperty8 = ModelEnum.Yes
        });

        // Act
        List<Model2> enumFilters = await Context.Models2
            .Where(x => x.MyProperty7 == ModelEnum.Yes && x.MyProperty8 == ModelEnum.No)
            .ToListAsync();

        // Assert
        Assert.NotEmpty(enumFilters);
        Assert.All(enumFilters, x =>
        {
            Assert.Equal(ModelEnum.Yes, x.MyProperty7);
            Assert.Equal(ModelEnum.No, x.MyProperty8);
        });
    }

    [Fact]
    public async Task Table_CountAsync_ShouldMatchListCount()
    {
        // Arrange
        await Context.Models1.UpsertEntityAsync(new()
        {
            PrettyName = "root",
            PrettyRow = Guid.NewGuid().ToString("N"),
            MyProperty1 = 1,
            MyProperty2 = "test"
        });

        await Context.Models1.UpsertEntityAsync(new()
        {
            PrettyName = "root",
            PrettyRow = Guid.NewGuid().ToString("N"),
            MyProperty1 = 2,
            MyProperty2 = "test2"
        });

        // Act
        List<Model> proxiedList = await Context.Models1
            .Where(x => x.PrettyName == "root" && x.PrettyRow != "")
            .ToListAsync();
        int proxyWorksCount = await Context.Models1
            .Where(x => x.PrettyName == "root" && x.PrettyRow != "")
            .CountAsync();

        // Assert
        Assert.Equal(proxiedList.Count, proxyWorksCount);
    }

    [Fact]
    public async Task Table_Where_WithVariableValue_ShouldWork()
    {
        // Arrange
        var prettyItem = new { PrettyRow = "test-value" };
        await Context.Models2.UpsertEntityAsync(new()
        {
            PartitionKey = "root",
            PrettyRow = prettyItem.PrettyRow,
            MyProperty1 = 1,
            MyProperty2 = "test"
        });

        // Act
        List<Model2> visitorWorks = await Context.Models2
            .Where(x => x.PrettyRow == prettyItem.PrettyRow)
            .ToListAsync();

        // Assert
        Assert.NotEmpty(visitorWorks);
        Assert.Equal(prettyItem.PrettyRow, visitorWorks[0].PrettyRow);
    }

    [Fact]
    public async Task Table_Where_WithNotEquals_ShouldWork()
    {
        // Arrange
        var prettyItem = new { PrettyRow = "test-value" };
        await Context.Models2.UpsertEntityAsync(new()
        {
            PartitionKey = "root",
            PrettyRow = prettyItem.PrettyRow,
            MyProperty1 = 1,
            MyProperty2 = "test"
        });

        await Context.Models2.UpsertEntityAsync(new()
        {
            PartitionKey = "root",
            PrettyRow = "different",
            MyProperty1 = 2,
            MyProperty2 = "test2"
        });

        // Act
        List<Model2> visitorWorks = await Context.Models2
            .Where(x => x.PrettyRow != prettyItem.PrettyRow)
            .ToListAsync();

        // Assert
        Assert.NotEmpty(visitorWorks);
        Assert.NotEqual(prettyItem.PrettyRow, visitorWorks[0].PrettyRow);
    }

    [Fact]
    public async Task Table_SingleAsync_WithMultipleResults_ShouldThrowInvalidOperationException()
    {
        // Arrange
        await Context.Models2.UpsertEntityAsync(new()
        {
            PartitionKey = "root",
            PrettyRow = Guid.NewGuid().ToString("N"),
            MyProperty1 = 5,
            MyProperty2 = "test"
        });

        await Context.Models2.UpsertEntityAsync(new()
        {
            PartitionKey = "root",
            PrettyRow = Guid.NewGuid().ToString("N"),
            MyProperty1 = 5,
            MyProperty2 = "test2"
        });

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await Context.Models2
                .Where(x => x.PartitionKey == "root")
                .Where(x => x.MyProperty1 > 2)
                .SelectFields(x => x.MyProperty2)
                .SingleAsync();
        });
    }

    #endregion

    #region FluentTableEntity Tests

    [Fact]
    public async Task FluentEntity_AddAndRetrieveModelA_ShouldStoreAndRetrieveCorrectly()
    {
        // Arrange
        FluentTestModelA modelA = new()
        {
            PrettyPartitionA = "fluent-test",
            PrettyRowA = Guid.NewGuid().ToString("N"),
            TypeA = "TypeA Model",
            PropertyA = 42
        };

        // Act
        await Context.FluentModels.UpsertEntityAsync(modelA);
        var retrieved = await Context.FluentModels.FindAsync(modelA.PrettyPartitionA, modelA.PrettyRowA);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal("FluentTestModelA", retrieved["$type"]);
        Assert.Equal("TypeA Model", retrieved["TypeA"]);
        Assert.Equal(42, retrieved["PropertyA"]);
    }

    [Fact]
    public async Task FluentEntity_AddAndRetrieveModelB_ShouldStoreAndRetrieveCorrectly()
    {
        // Arrange
        FluentTestModelB modelB = new()
        {
            PrettyPartitionB = "fluent-test",
            PrettyRowB = Guid.NewGuid().ToString("N"),
            TypeB = "TypeB Model",
            PropertyB = true
        };

        // Act
        await Context.FluentModels.UpsertEntityAsync(modelB);
        var retrieved = await Context.FluentModels.FindAsync(modelB.PrettyPartitionB, modelB.PrettyRowB);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal("FluentTestModelB", retrieved["$type"]);
        Assert.Equal("TypeB Model", retrieved["TypeB"]);
        Assert.True((bool)retrieved["PropertyB"]);
    }

    [Fact]
    public async Task FluentEntity_StoreMultipleTypes_ShouldPreserveTypeInformation()
    {
        // Arrange
        const string partitionKey = "fluent-partition";
        string modelAId = Guid.NewGuid().ToString("N");
        string modelBId = Guid.NewGuid().ToString("N");

        FluentTestModelA modelA = new()
        {
            PrettyPartitionA = partitionKey,
            PrettyRowA = modelAId,
            TypeA = "First Type A",
            PropertyA = 100
        };

        FluentTestModelB modelB = new()
        {
            PrettyPartitionB = partitionKey,
            PrettyRowB = modelBId,
            TypeB = "First Type B",
            PropertyB = false
        };

        // Act
        await Context.FluentModels.UpsertEntityAsync(modelA);
        await Context.FluentModels.UpsertEntityAsync(modelB);

        var retrievedA = await Context.FluentModels.FindAsync(partitionKey, modelAId);
        var retrievedB = await Context.FluentModels.FindAsync(partitionKey, modelBId);

        // Assert
        Assert.NotNull(retrievedA);
        Assert.NotNull(retrievedB);
        Assert.Equal("FluentTestModelA", retrievedA["$type"]);
        Assert.Equal("FluentTestModelB", retrievedB["$type"]);
    }

    [Fact]
    public async Task FluentEntity_RetrieveAsModelA_ShouldCastCorrectly()
    {
        // Arrange
        var originalA = new FluentTestModelA
        {
            PrettyPartitionA = "fluent-test",
            PrettyRowA = Guid.NewGuid().ToString("N"),
            TypeA = "Casting Test A",
            PropertyA = 777
        };

        // Act
        await Context.FluentModels.UpsertEntityAsync(originalA);
        var retrieved = await Context.FluentModels.FindAsync(originalA.PrettyPartitionA, originalA.PrettyRowA);

        // Cast back to ModelA
        FluentTestModelA castedA = (FluentTestModelA)retrieved;

        // Assert
        Assert.NotNull(castedA);
        Assert.Equal("Casting Test A", castedA.TypeA);
        Assert.Equal(777, castedA.PropertyA);
    }

    [Fact]
    public async Task FluentEntity_UpdateModelA_ShouldPersistChanges()
    {
        // Arrange
        FluentTestModelA modelA = new()
        {
            PrettyPartitionA = "fluent-test",
            PrettyRowA = Guid.NewGuid().ToString("N"),
            TypeA = "Original Value",
            PropertyA = 10
        };

        await Context.FluentModels.UpsertEntityAsync(modelA);

        // Act - Update the entity
        var retrieved = await Context.FluentModels.FindAsync(modelA.PrettyPartitionA, modelA.PrettyRowA);

        Assert.NotNull(retrieved);
        retrieved["TypeA"] = "Updated Value";
        retrieved["PropertyA"] = 20;

        await Context.FluentModels.UpsertEntityAsync(retrieved);

        var updatedRetrieved = await Context.FluentModels.FindAsync(modelA.PrettyPartitionA, modelA.PrettyRowA);

        // Assert
        Assert.NotNull(updatedRetrieved);
        Assert.Equal("Updated Value", updatedRetrieved["TypeA"]);
        Assert.Equal(20, updatedRetrieved["PropertyA"]);
    }

    [Fact]
    public async Task FluentEntity_ToListAsync_ShouldReturnAllStoredEntities()
    {
        // Arrange
        const string partitionKey = "fluent-list-test";
        var modelA1 = new FluentTestModelA
        {
            PrettyPartitionA = partitionKey,
            PrettyRowA = Guid.NewGuid().ToString("N"),
            TypeA = "List A 1",
            PropertyA = 1
        };

        var modelA2 = new FluentTestModelA
        {
            PrettyPartitionA = partitionKey,
            PrettyRowA = Guid.NewGuid().ToString("N"),
            TypeA = "List A 2",
            PropertyA = 2
        };

        FluentTestModelB modelB = new()
        {
            PrettyPartitionB = partitionKey,
            PrettyRowB = Guid.NewGuid().ToString("N"),
            TypeB = "List B 1",
            PropertyB = true
        };

        // Act
        await Context.FluentModels.UpsertEntityAsync(modelA1);
        await Context.FluentModels.UpsertEntityAsync(modelA2);
        await Context.FluentModels.UpsertEntityAsync(modelB);

        var allEntities = await Context.FluentModels.ToListAsync();

        // Assert
        Assert.True(allEntities.Count >= 3);
    }

    [Fact]
    public async Task FluentEntity_FindAsync_WithMultipleIds_ShouldReturnRequestedEntities()
    {
        // Arrange
        FluentTestModelA modelA = new()
        {
            PrettyPartitionA = "fluent-find",
            PrettyRowA = Guid.NewGuid().ToString("N"),
            TypeA = "Find A",
            PropertyA = 50
        };

        FluentTestModelB modelB = new()
        {
            PrettyPartitionB = "fluent-find",
            PrettyRowB = Guid.NewGuid().ToString("N"),
            TypeB = "Find B",
            PropertyB = false
        };

        // Act
        await Context.FluentModels.UpsertEntityAsync(modelA);
        await Context.FluentModels.UpsertEntityAsync(modelB);

        var findResults = await Context.FluentModels
            .FindAsync(
                ("fluent-find", modelA.PrettyRowA),
                ("fluent-find", modelB.PrettyRowB)
            )
            .ToListAsync();

        // Assert
        Assert.Equal(2, findResults.Count);
    }

    [Fact]
    public async Task FluentEntity_Where_ShouldFilterByPartitionKey()
    {
        // Arrange
        FluentTestModelA modelA = new()
        {
            PrettyPartitionA = "partition-a",
            PrettyRowA = Guid.NewGuid().ToString("N"),
            TypeA = "Partition A",
            PropertyA = 11
        };

        FluentTestModelB modelB = new()
        {
            PrettyPartitionB = "partition-b",
            PrettyRowB = Guid.NewGuid().ToString("N"),
            TypeB = "Partition B",
            PropertyB = true
        };

        // Act
        await Context.FluentModels.UpsertEntityAsync(modelA);
        await Context.FluentModels.UpsertEntityAsync(modelB);

        var filterResults = await Context.FluentModels
            .Where(x => x.PartitionKey == "partition-a")
            .ToListAsync();

        // Assert
        Assert.NotEmpty(filterResults);
        Assert.All(filterResults, x => Assert.Equal("partition-a", x.PartitionKey));
    }

    [Fact]
    public async Task FluentEntity_ImplicitCasting_FromModelAToFluentEntity_ShouldWork()
    {
        // Arrange
        FluentTestModelA modelA = new()
        {
            PrettyPartitionA = "cast-test",
            PrettyRowA = Guid.NewGuid().ToString("N"),
            TypeA = "Implicit Cast Test",
            PropertyA = 99
        };

        // Act - Implicit cast
        await Context.FluentModels.UpsertEntityAsync(modelA);
        var retrieved = await Context.FluentModels.FindAsync(modelA.PrettyPartitionA, modelA.PrettyRowA);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal("Implicit Cast Test", retrieved["TypeA"]);
        Assert.Equal(99, retrieved["PropertyA"]);
    }

    [Fact]
    public async Task FluentEntity_ImplicitCasting_FromFluentEntityToModelA_ShouldWork()
    {
        // Arrange
        var originalA = new FluentTestModelA
        {
            PrettyPartitionA = "reverse-cast",
            PrettyRowA = Guid.NewGuid().ToString("N"),
            TypeA = "Reverse Cast Test",
            PropertyA = 88
        };

        await Context.FluentModels.UpsertEntityAsync(originalA);

        // Act - Retrieve and implicitly cast back
        var retrieved = await Context.FluentModels.FindAsync(originalA.PrettyPartitionA, originalA.PrettyRowA);
        FluentTestModelA castedBack = retrieved;

        // Assert
        Assert.NotNull(castedBack);
        Assert.Equal("Reverse Cast Test", castedBack.TypeA);
        Assert.Equal(88, castedBack.PropertyA);
    }

    [Fact]
    public async Task FluentEntity_CountAsync_ShouldReturnCorrectCount()
    {
        // Arrange
        const string partitionKey = "count-test";
        for (int i = 0; i < 3; i++)
        {
            FluentTestModelA modelA = new()
            {
                PrettyPartitionA = partitionKey,
                PrettyRowA = Guid.NewGuid().ToString("N"),
                TypeA = $"Item {i}",
                PropertyA = i
            };
            await Context.FluentModels.UpsertEntityAsync(modelA);
        }

        // Act
        int count = await Context.FluentModels
            .Where(x => x.PartitionKey == partitionKey)
            .CountAsync();

        // Assert
        Assert.Equal(3, count);
    }

    [Fact]
    public async Task FluentEntity_DeleteEntity_ShouldRemoveFromStorage()
    {
        // Arrange
        FluentTestModelA modelA = new()
        {
            PrettyPartitionA = "delete-test",
            PrettyRowA = Guid.NewGuid().ToString("N"),
            TypeA = "To Delete",
            PropertyA = 42
        };
        await Context.FluentModels.UpsertEntityAsync(modelA);

        // Act
        await Context.FluentModels.DeleteEntityAsync(modelA.PrettyPartitionA, modelA.PrettyRowA);
        var retrieved = await Context.FluentModels.FindAsync(modelA.PrettyPartitionA, modelA.PrettyRowA);
        // Assert
        Assert.Null(retrieved);
    }

    #endregion

    #region FluentPartitionTableEntity Tests

    [Fact]
    public async Task FluentPartitionEntity_AddAndRetrieveModelA_ShouldStoreAndRetrieveCorrectly()
    {
        // Arrange
        FluentTestModelA modelA = new()
        {
            PrettyPartitionA = "ignored-partition", // Should be ignored as PK is type name
            PrettyRowA = Guid.NewGuid().ToString("N"),
            TypeA = "TypeA Model",
            PropertyA = 42
        };

        // Act
        await Context.FluentPartitionModels.UpsertEntityAsync(modelA);

        // Retrieve using Type Name as PartitionKey
        var retrieved = await Context.FluentPartitionModels.FindAsync("FluentTestModelA", modelA.PrettyRowA);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal("FluentTestModelA", retrieved.PartitionKey);
        Assert.Equal(modelA.PrettyRowA, retrieved.RowKey);
        Assert.Equal("TypeA Model", retrieved["TypeA"]);
        Assert.Equal(42, retrieved["PropertyA"]);
    }

    [Fact]
    public async Task FluentPartitionEntity_StoreMultipleTypes_ShouldSeparateByPartition()
    {
        // Arrange
        string rowKey = Guid.NewGuid().ToString("N");

        FluentTestModelA modelA = new()
        {
            PrettyPartitionA = "ignored",
            PrettyRowA = rowKey,
            TypeA = "Model A",
            PropertyA = 100
        };

        FluentTestModelB modelB = new()
        {
            PrettyPartitionB = "ignored",
            PrettyRowB = rowKey, // Same RowKey, but different types so different partitions
            TypeB = "Model B",
            PropertyB = false
        };

        // Act
        await Context.FluentPartitionModels.UpsertEntityAsync(modelA);
        await Context.FluentPartitionModels.UpsertEntityAsync(modelB);

        var retrievedA = await Context.FluentPartitionModels.FindAsync("FluentTestModelA", rowKey);
        var retrievedB = await Context.FluentPartitionModels.FindAsync("FluentTestModelB", rowKey);

        // Assert
        Assert.NotNull(retrievedA);
        Assert.NotNull(retrievedB);
        Assert.Equal("FluentTestModelA", retrievedA.PartitionKey);
        Assert.Equal("FluentTestModelB", retrievedB.PartitionKey);
    }

    #endregion

    #region FluentRowTypeTableEntity Tests

    [Fact]
    public async Task FluentRowTypeEntity_AddAndRetrieveModelA_ShouldStoreAndRetrieveCorrectly()
    {
        // Arrange
        FluentTestModelA modelA = new()
        {
            PrettyPartitionA = "rowtype-test",
            PrettyRowA = "ignored-row", // Should be ignored as RK is type name
            TypeA = "TypeA Model",
            PropertyA = 42
        };

        // Act
        await Context.FluentRowKeyModels.UpsertEntityAsync(modelA);

        // Retrieve using Type Name as RowKey
        var retrieved = await Context.FluentRowKeyModels.FindAsync(modelA.PrettyPartitionA, "FluentTestModelA");

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(modelA.PrettyPartitionA, retrieved.PartitionKey);
        Assert.Equal("FluentTestModelA", retrieved.RowKey);
        Assert.Equal("TypeA Model", retrieved["TypeA"]);
        Assert.Equal(42, retrieved["PropertyA"]);
    }

    [Fact]
    public async Task FluentRowTypeEntity_StoreMultipleTypes_ShouldSeparateByRowKey()
    {
        // Arrange
        const string partitionKey = "rowtype-partition";

        FluentTestModelA modelA = new()
        {
            PrettyPartitionA = partitionKey,
            PrettyRowA = "ignored",
            TypeA = "Model A",
            PropertyA = 100
        };

        FluentTestModelB modelB = new()
        {
            PrettyPartitionB = partitionKey,
            PrettyRowB = "ignored",
            TypeB = "Model B",
            PropertyB = false
        };

        // Act
        await Context.FluentRowKeyModels.UpsertEntityAsync(modelA);
        await Context.FluentRowKeyModels.UpsertEntityAsync(modelB);

        var retrievedA = await Context.FluentRowKeyModels.FindAsync(partitionKey, "FluentTestModelA");
        var retrievedB = await Context.FluentRowKeyModels.FindAsync(partitionKey, "FluentTestModelB");

        // Assert
        Assert.NotNull(retrievedA);
        Assert.NotNull(retrievedB);
        Assert.Equal("FluentTestModelA", retrievedA.RowKey);
        Assert.Equal("FluentTestModelB", retrievedB.RowKey);
    }

    #endregion

    #region Blob Storage Query Tests

    [Fact]
    public async Task Blob_FindAsync_WithMultipleKeys_ShouldReturnAllEntities()
    {
        // Arrange
        await Context.Models4Blob.DeleteAllEntitiesAsync("root");
        string blobId1 = Guid.NewGuid().ToString("N");
        string blobId2 = Guid.NewGuid().ToString("N");

        await Context.Models4Blob.AddEntityAsync(new()
        {
            PrettyPartition = "root",
            PrettyRow = blobId1,
            MyProperty1 = 1,
            MyProperty2 = "test 1"
        });

        await Context.Models4Blob.AddEntityAsync(new()
        {
            PrettyPartition = "root",
            PrettyRow = blobId2,
            MyProperty1 = 2,
            MyProperty2 = "test 2"
        });

        // Act
        List<Model4> findBlobResults = await Context.Models4Blob
            .FindAsync(("root", blobId1), ("root", blobId2))
            .ToListAsync();

        // Assert
        Assert.Equal(2, findBlobResults.Count);
    }

    [Fact]
    public async Task Blob_FindAsync_WithSingleKey_ShouldReturnEntity()
    {
        // Arrange
        await Context.Models4Blob.DeleteAllEntitiesAsync("root");
        string blobId1 = Guid.NewGuid().ToString("N");

        await Context.Models4Blob.AddEntityAsync(new()
        {
            PrettyPartition = "root",
            PrettyRow = blobId1,
            MyProperty1 = 1,
            MyProperty2 = "test 1"
        });

        // Act
        Model4? findSingleBlobResult = await Context.Models4Blob.FindAsync("root", blobId1);

        // Assert
        Assert.NotNull(findSingleBlobResult);
    }

    [Fact]
    public async Task Blob_GetEntityOrDefaultAsync_WithExistingEntity_ShouldReturnEntity()
    {
        // Arrange
        await Context.Models4Blob.DeleteAllEntitiesAsync("root");
        string blobId = Guid.NewGuid().ToString("N");
        await Context.Models4Blob.AddEntityAsync(new()
        {
            PrettyPartition = "root",
            PrettyRow = blobId,
            MyProperty1 = 1,
            MyProperty2 = "test 1"
        });

        // Act
        Model4? blob = await Context.Models4Blob.GetEntityOrDefaultAsync("root", blobId);

        // Assert
        Assert.NotNull(blob);
        Assert.Equal(1, blob.MyProperty1);
        Assert.Equal("test 1", blob.MyProperty2);
    }

    [Fact]
    public async Task Blob_GetEntityOrDefaultAsync_WithNonExistingEntity_ShouldReturnNull()
    {
        // Arrange
        await Context.Models4Blob.DeleteAllEntitiesAsync("root");

        // Act
        Model4? blob = await Context.Models4Blob.GetEntityOrDefaultAsync("root", Guid.NewGuid().ToString("N"));

        // Assert
        Assert.Null(blob);
    }

    [Fact]
    public async Task Blob_Where_WithComplexFilter_ShouldFilterByTagsAndProperties()
    {
        // Arrange
        await Context.Models4Blob.DeleteAllEntitiesAsync("root");
        string blobId = Guid.NewGuid().ToString("N");
        await Context.Models4Blob.AddEntityAsync(new()
        {
            PrettyPartition = "root",
            PrettyRow = blobId,
            MyProperty1 = 2,
            MyProperty2 = "test value"
        });

        // Act
        List<Model4> blobResult = await Context.Models4Blob
            .Where(x => x.PrettyPartition == "root")
            .Where(x => x.PrettyRow == blobId)
            .Where(x => x.MyProperty1 == 2)
            .Where(x => x.MyProperty2 == "test value")
            .ToListAsync();

        // Assert
        Assert.Single(blobResult);
        Assert.Equal(2, blobResult[0].MyProperty1);
        Assert.Equal("test value", blobResult[0].MyProperty2);
    }

    [Fact]
    public async Task Blob_Where_WithTagsOnly_ShouldFilterByTags()
    {
        // Arrange
        await Context.Models4Blob.DeleteAllEntitiesAsync("root");
        string blobId = Guid.NewGuid().ToString("N");
        await Context.Models4Blob.AddEntityAsync(new()
        {
            PrettyPartition = "root",
            PrettyRow = blobId,
            MyProperty1 = 2,
            MyProperty2 = "test value"
        });

        // Act
        List<Model4> blobResult = await Context.Models4Blob
            .Where(x => x.PrettyPartition == "root")
            .Where(x => x.PrettyRow == blobId)
            .Where(x => x.MyProperty1 == 2)
            .ToListAsync();

        // Assert
        Assert.Single(blobResult);
        Assert.Equal(2, blobResult[0].MyProperty1);
        Assert.Equal("test value", blobResult[0].MyProperty2);
    }

    [Fact]
    public async Task Blob_Where_WithPartitionAndRowKey_ShouldReturnEntity()
    {
        // Arrange
        await Context.Models4Blob.DeleteAllEntitiesAsync("root");
        string blobId = Guid.NewGuid().ToString("N");
        await Context.Models4Blob.AddEntityAsync(new()
        {
            PrettyPartition = "root",
            PrettyRow = blobId,
            MyProperty1 = 2,
            MyProperty2 = "test value"
        });

        // Act
        List<Model4> blobResult = await Context.Models4Blob
            .Where(x => x.PrettyPartition == "root" && x.PrettyRow == blobId)
            .ToListAsync();

        // Assert
        Assert.Single(blobResult);
        Assert.Equal(2, blobResult[0].MyProperty1);
        Assert.Equal("test value", blobResult[0].MyProperty2);
    }

    #endregion

    #region Fluent Extension Method Tests

    [Fact]
    public async Task FluentExtension_WhereFluentTestModelA_ShouldFilterToModelAOnly()
    {
        // Arrange
        const string partitionKey = "fluent-extension-test";
        var modelA1 = new FluentTestModelA
        {
            PrettyPartitionA = partitionKey,
            PrettyRowA = Guid.NewGuid().ToString("N"),
            TypeA = "Extension A 1",
            PropertyA = 100
        };

        var modelA2 = new FluentTestModelA
        {
            PrettyPartitionA = partitionKey,
            PrettyRowA = Guid.NewGuid().ToString("N"),
            TypeA = "Extension A 2",
            PropertyA = 200
        };

        var modelB = new FluentTestModelB
        {
            PrettyPartitionB = partitionKey,
            PrettyRowB = Guid.NewGuid().ToString("N"),
            TypeB = "Extension B",
            PropertyB = true
        };

        await Context.FluentModels.UpsertEntityAsync(modelA1);
        await Context.FluentModels.UpsertEntityAsync(modelA2);
        await Context.FluentModels.UpsertEntityAsync(modelB);

        // Act - Using generated extension method
        var results = await Context.FluentModels.WhereFluentTestModelA().ToListAsync();

        // Assert
        Assert.NotEmpty(results);
        var matchingResults = results.Where(x => 
            x.PrettyPartitionA == partitionKey && 
            (x.PrettyRowA == modelA1.PrettyRowA || x.PrettyRowA == modelA2.PrettyRowA)).ToList();
        Assert.Equal(2, matchingResults.Count);
        Assert.All(matchingResults, x => Assert.NotNull(x.TypeA));
    }

    [Fact]
    public async Task FluentExtension_WhereFluentTestModelB_ShouldFilterToModelBOnly()
    {
        // Arrange
        const string partitionKey = "fluent-extension-b-test";
        var modelA = new FluentTestModelA
        {
            PrettyPartitionA = partitionKey,
            PrettyRowA = Guid.NewGuid().ToString("N"),
            TypeA = "Extension A",
            PropertyA = 300
        };

        var modelB1 = new FluentTestModelB
        {
            PrettyPartitionB = partitionKey,
            PrettyRowB = Guid.NewGuid().ToString("N"),
            TypeB = "Extension B 1",
            PropertyB = true
        };

        var modelB2 = new FluentTestModelB
        {
            PrettyPartitionB = partitionKey,
            PrettyRowB = Guid.NewGuid().ToString("N"),
            TypeB = "Extension B 2",
            PropertyB = false
        };

        await Context.FluentModels.UpsertEntityAsync(modelA);
        await Context.FluentModels.UpsertEntityAsync(modelB1);
        await Context.FluentModels.UpsertEntityAsync(modelB2);

        // Act - Using generated extension method
        var results = await Context.FluentModels.WhereFluentTestModelB().ToListAsync();

        // Assert
        Assert.NotEmpty(results);
        var matchingResults = results.Where(x => 
            x.PrettyPartitionB == partitionKey && 
            (x.PrettyRowB == modelB1.PrettyRowB || x.PrettyRowB == modelB2.PrettyRowB)).ToList();
        Assert.Equal(2, matchingResults.Count);
        Assert.All(matchingResults, x => Assert.NotNull(x.TypeB));
    }

    [Fact]
    public async Task FluentExtension_WhereFluentTestModelA_WithAdditionalFilter_ShouldCombineFilters()
    {
        // Arrange
        const string partitionKey = "fluent-combined-filter";
        var modelA1 = new FluentTestModelA
        {
            PrettyPartitionA = partitionKey,
            PrettyRowA = Guid.NewGuid().ToString("N"),
            TypeA = "Match",
            PropertyA = 100
        };

        var modelA2 = new FluentTestModelA
        {
            PrettyPartitionA = partitionKey,
            PrettyRowA = Guid.NewGuid().ToString("N"),
            TypeA = "NoMatch",
            PropertyA = 200
        };

        var modelB = new FluentTestModelB
        {
            PrettyPartitionB = partitionKey,
            PrettyRowB = Guid.NewGuid().ToString("N"),
            TypeB = "Type B",
            PropertyB = true
        };

        await Context.FluentModels.UpsertEntityAsync(modelA1);
        await Context.FluentModels.UpsertEntityAsync(modelA2);
        await Context.FluentModels.UpsertEntityAsync(modelB);

        // Act - Using generated extension method with additional filter
        var results = await Context.FluentModels.WhereFluentTestModelA()
            .Where(x => x.TypeA == "Match")
            .ToListAsync();

        // Assert
        var matchingResults = results.Where(x => x.PrettyPartitionA == partitionKey).ToList();
        Assert.Single(matchingResults);
        Assert.Equal("Match", matchingResults[0].TypeA);
        Assert.Equal(100, matchingResults[0].PropertyA);
    }

    [Fact]
    public async Task FluentExtension_WhereFluentTestModelA_SelectFields_ShouldReturnOnlySelectedFields()
    {
        // Arrange
        const string partitionKey = "fluent-select-test";
        var modelA = new FluentTestModelA
        {
            PrettyPartitionA = partitionKey,
            PrettyRowA = Guid.NewGuid().ToString("N"),
            TypeA = "Select Test",
            PropertyA = 999
        };

        await Context.FluentModels.UpsertEntityAsync(modelA);

        // Act - Using generated extension method with SelectFields
        var results = await Context.FluentModels.WhereFluentTestModelA()
            .Where(x => x.PrettyPartitionA == partitionKey && x.PrettyRowA == modelA.PrettyRowA)
            .SelectFields(x => new { x.TypeA })
            .ToListAsync();

        // Assert
        Assert.Single(results);
        Assert.Equal("Select Test", results[0].TypeA);
    }

    [Fact]
    public async Task FluentExtension_WhereFluentTestModelB_Take_ShouldLimitResults()
    {
        // Arrange
        const string partitionKey = "fluent-take-test";
        for (int i = 0; i < 5; i++)
        {
            await Context.FluentModels.UpsertEntityAsync(new FluentTestModelB
            {
                PrettyPartitionB = partitionKey,
                PrettyRowB = Guid.NewGuid().ToString("N"),
                TypeB = $"Take Test {i}",
                PropertyB = i % 2 == 0
            });
        }

        // Act - Using generated extension method with Take
        var results = await Context.FluentModels.WhereFluentTestModelB()
            .Where(x => x.PrettyPartitionB == partitionKey)
            .Take(3)
            .ToListAsync();

        // Assert
        Assert.Equal(3, results.Count);
    }

    [Fact]
    public async Task FluentExtension_MultipleFluentProperties_ShouldUseCorrectProperty()
    {
        // Arrange - FluentPartitionModels uses partition key as discriminator
        const string partitionKey = "FluentTestModelA"; // Partition key matches type name
        var modelA = new FluentTestModelA
        {
            PrettyPartitionA = partitionKey,
            PrettyRowA = Guid.NewGuid().ToString("N"),
            TypeA = "Partition Test",
            PropertyA = 777
        };

        await Context.FluentPartitionModels.UpsertEntityAsync(modelA);

        // Act - Extension method should work with FluentPartitionModels too
        var results = await Context.FluentPartitionModels.WhereFluentTestModelA().ToListAsync();

        // Assert - Should find at least one result (could be from either property)
        Assert.NotEmpty(results);
    }

    [Fact]
    public async Task FluentExtension_FindFluentTestModelAAsync_OnFluentPartitionModels_ShouldWork()
    {
        // Arrange - FluentPartitionModels uses partition key as discriminator
        const string partitionKey = "FluentTestModelA"; // Partition key matches type name
        string rowKey = Guid.NewGuid().ToString("N");
        var modelA = new FluentTestModelA
        {
            PrettyPartitionA = partitionKey,
            PrettyRowA = rowKey,
            TypeA = "Partition Find Test",
            PropertyA = 777
        };

        await Context.FluentPartitionModels.UpsertEntityAsync(modelA);

        // Act - FluentPartitionTableEntity FindAsync takes only rowKey parameter
        var found = await Context.FluentPartitionModels.FindFluentTestModelAAsync(rowKey);

        // Assert
        Assert.NotNull(found);
        Assert.Equal("Partition Find Test", found.TypeA);
        Assert.Equal(777, found.PropertyA);
    }

    [Fact]
    public async Task FluentExtension_FindFluentTestModelAAsync_WithCancellationToken_ShouldRespectCancellation()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync(); // Cancel asynchronously

        // Act & Assert - FluentPartitionTableEntity FindAsync takes only rowKey parameter
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await Context.FluentPartitionModels.FindFluentTestModelAAsync("any-row", cts.Token);
        });
    }

    [Fact]
    public async Task FluentExtension_WhereFluentTestModelA_OnFluentRowKeyModels_ShouldWork()
    {
        // Arrange - FluentRowKeyModels uses row key as discriminator
        const string rowKey = "FluentTestModelA"; // Row key matches type name
        var modelA = new FluentTestModelA
        {
            PrettyPartitionA = Guid.NewGuid().ToString("N"),
            PrettyRowA = rowKey,
            TypeA = "RowKey Where Test",
            PropertyA = 999
        };

        var modelB = new FluentTestModelB
        {
            PrettyPartitionB = Guid.NewGuid().ToString("N"),
            PrettyRowB = "FluentTestModelB",
            TypeB = "Other Type",
            PropertyB = true
        };

        await Context.FluentRowKeyModels.UpsertEntityAsync(modelA);
        await Context.FluentRowKeyModels.UpsertEntityAsync(modelB);

        // Act
        var results = await Context.FluentRowKeyModels.WhereFluentTestModelA().ToListAsync();

        // Assert
        Assert.Single(results);
        Assert.Equal("RowKey Where Test", results[0].TypeA);
        Assert.Equal(999, results[0].PropertyA);
    }

    [Fact]
    public async Task FluentExtension_FindFluentTestModelAAsync_OnFluentRowKeyModels_ShouldWork()
    {
        // Arrange - FluentRowKeyModels uses row key as discriminator
        const string rowKey = "FluentTestModelA"; // Row key matches type name
        string partitionKey = Guid.NewGuid().ToString("N");
        var modelA = new FluentTestModelA
        {
            PrettyPartitionA = partitionKey,
            PrettyRowA = rowKey,
            TypeA = "RowKey Find Test",
            PropertyA = 888
        };

        await Context.FluentRowKeyModels.UpsertEntityAsync(modelA);

        // Act - FluentRowTableEntity FindAsync takes only partitionKey parameter
        var found = await Context.FluentRowKeyModels.FindFluentTestModelAAsync(partitionKey);

        // Assert
        Assert.NotNull(found);
        Assert.Equal("RowKey Find Test", found.TypeA);
        Assert.Equal(888, found.PropertyA);
    }

    [Fact]
    public async Task FluentExtension_FindFluentTestModelBAsync_OnFluentRowKeyModels_ShouldWork()
    {
        // Arrange - FluentRowKeyModels uses row key as discriminator
        const string rowKey = "FluentTestModelB"; // Row key matches type name
        string partitionKey = Guid.NewGuid().ToString("N");
        var modelB = new FluentTestModelB
        {
            PrettyPartitionB = partitionKey,
            PrettyRowB = rowKey,
            TypeB = "RowKey Find B Test",
            PropertyB = false
        };

        await Context.FluentRowKeyModels.UpsertEntityAsync(modelB);

        // Act - FluentRowTableEntity FindAsync takes only partitionKey parameter
        var found = await Context.FluentRowKeyModels.FindFluentTestModelBAsync(partitionKey);

        // Assert
        Assert.NotNull(found);
        Assert.Equal("RowKey Find B Test", found.TypeB);
        Assert.False(found.PropertyB);
    }

    [Fact]
    public async Task FluentExtension_FindFluentTestModelAAsync_OnFluentRowKeyModels_WithWrongKey_ShouldReturnNull()
    {
        // Arrange
        const string rowKey = "FluentTestModelA";
        var modelA = new FluentTestModelA
        {
            PrettyPartitionA = Guid.NewGuid().ToString("N"),
            PrettyRowA = rowKey,
            TypeA = "Test",
            PropertyA = 123
        };

        await Context.FluentRowKeyModels.UpsertEntityAsync(modelA);

        // Act
        var notFound = await Context.FluentRowKeyModels.FindFluentTestModelAAsync("wrong-partition-key");

        // Assert
        Assert.Null(notFound);
    }

    #endregion
}
