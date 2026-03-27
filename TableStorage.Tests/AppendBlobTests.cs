using Azure;
using TableStorage.Tests.Infrastructure;
using TableStorage.Tests.Models;

namespace TableStorage.Tests;

/// <summary>
/// Tests for append blob storage operations
/// </summary>
public class AppendBlobTests(AzuriteFixture azuriteFixture) : AzuriteTestBase(azuriteFixture)
{
    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        await Context.Models5Blob.DeleteAllEntitiesAsync("root", TestContext.Current.CancellationToken);
        await Context.Models5BlobInJson.DeleteAllEntitiesAsync("root", TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task UpsertEntityAsync_ShouldCreateAppendBlob()
    {
        // Arrange
        var appendModel = new Model5
        {
            Id = "root",
            ContinuationToken = "test",
            Entries =
            [
                new Model5Entry
                {
                    Creation = DateTimeOffset.UtcNow,
                    Duration = Random.Shared.Next(500, 2000)
                }
            ]
        };

        // Act
        await Context.Models5Blob.UpsertEntityAsync(appendModel, TestContext.Current.CancellationToken);
        var result = await Context.Models5Blob
            .Where(x => x.Id == "root" && x.ContinuationToken == "test")
            .FirstAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Entries);
        Assert.Single(result.Entries);
        Assert.Equal(appendModel.Entries[0].Creation.ToUnixTimeSeconds(), result.Entries[0].Creation.ToUnixTimeSeconds());
        Assert.Equal(appendModel.Entries[0].Duration, result.Entries[0].Duration);
    }

    [Fact]
    public async Task AppendAsync_WhenBlobDoesNotExist_ShouldThrowException()
    {
        // Arrange
        await using Stream stream = BinaryData.FromString($"|{DateTimeOffset.UtcNow.AddSeconds(2).ToUnixTimeSeconds()};{Random.Shared.Next(500, 2000)}").ToStream();

        // Act & Assert
        await Assert.ThrowsAsync<RequestFailedException>(async () =>
        {
            await Context.Models5Blob.AppendAsync("root", "nonexistent", stream, TestContext.Current.CancellationToken);
        });
    }

    [Fact]
    public async Task AppendAsync_WhenBlobExists_ShouldAppendData()
    {
        // Arrange
        var appendModel = new Model5
        {
            Id = "root",
            ContinuationToken = "test",
            Entries =
            [
                new Model5Entry
                {
                    Creation = DateTimeOffset.UtcNow,
                    Duration = Random.Shared.Next(500, 2000)
                }
            ]
        };
        await Context.Models5Blob.UpsertEntityAsync(appendModel, TestContext.Current.CancellationToken);

        // Act
        await using Stream stream = BinaryData.FromString($"|{DateTimeOffset.UtcNow.AddSeconds(2).ToUnixTimeSeconds()};{Random.Shared.Next(500, 2000)}").ToStream();
        await Context.Models5Blob.AppendAsync("root", "test", stream, TestContext.Current.CancellationToken);

        var appendedBlob = await Context.Models5Blob
            .Where(x => x.Id == "root" && x.ContinuationToken == "test")
            .FirstAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(appendedBlob);
        Assert.NotNull(appendedBlob.Entries);
        Assert.Equal(2, appendedBlob.Entries.Length);
        Assert.Equal(appendModel.Entries[0].Creation.ToUnixTimeSeconds(), appendedBlob.Entries[0].Creation.ToUnixTimeSeconds());
        Assert.Equal(appendModel.Entries[0].Duration, appendedBlob.Entries[0].Duration);
        Assert.True(appendedBlob.Entries[1].Creation.ToUnixTimeSeconds() > appendedBlob.Entries[0].Creation.ToUnixTimeSeconds());
        Assert.True(appendedBlob.Entries[1].Duration.HasValue);
    }

    [Fact]
    public async Task AppendBlobWithJson_ShouldSerializeAndDeserializeCorrectly()
    {
        // Arrange
        var appendModel = new Model5
        {
            Id = "root",
            ContinuationToken = "test",
            Entries =
            [
                new Model5Entry
                {
                    Creation = DateTimeOffset.UtcNow,
                    Duration = Random.Shared.Next(500, 2000)
                }
            ]
        };

        // Act
        await Context.Models5BlobInJson.AddEntityAsync(appendModel, TestContext.Current.CancellationToken);
        var jsonBlob = await Context.Models5BlobInJson
            .Where(x => x.Id == "root" && x.ContinuationToken == "test")
            .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(jsonBlob);
        Assert.Equal("root", jsonBlob.Id);
        Assert.Equal("test", jsonBlob.ContinuationToken);
        Assert.NotNull(jsonBlob.Entries);
        Assert.Single(jsonBlob.Entries);
        Assert.Equal(appendModel.Entries[0].Creation, jsonBlob.Entries[0].Creation);
        Assert.Equal(appendModel.Entries[0].Duration, jsonBlob.Entries[0].Duration);
    }

    [Fact]
    public async Task FindAsync_WithMultipleAppendBlobs_ShouldReturnAll()
    {
        // Arrange
        await Context.Models5BlobInJson.AddEntityAsync(new()
        {
            Id = "root",
            ContinuationToken = "test",
            Entries =
            [
                new Model5Entry
                {
                    Creation = DateTimeOffset.UtcNow,
                    Duration = Random.Shared.Next(500, 2000)
                }
            ]
        }, TestContext.Current.CancellationToken);

        await Context.Models5BlobInJson.AddEntityAsync(new()
        {
            Id = "root",
            ContinuationToken = "test2",
            Entries =
            [
                new Model5Entry
                {
                    Creation = DateTimeOffset.UtcNow,
                    Duration = Random.Shared.Next(500, 2000)
                }
            ]
        }, TestContext.Current.CancellationToken);

        // Act
        var findAppendBlobResults = await Context.Models5BlobInJson
            .FindAsync(("root", "test"), ("root", "test2"))
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, findAppendBlobResults.Count);
    }

    [Fact]
    public async Task DeleteAllEntitiesAsync_ShouldRemoveAllAppendBlobs()
    {
        // Arrange
        await Context.Models5Blob.UpsertEntityAsync(new()
        {
            Id = "root",
            ContinuationToken = "test1",
            Entries =
            [
                new Model5Entry
                {
                    Creation = DateTimeOffset.UtcNow,
                    Duration = Random.Shared.Next(500, 2000)
                }
            ]
        }, TestContext.Current.CancellationToken);

        await Context.Models5Blob.UpsertEntityAsync(new()
        {
            Id = "root",
            ContinuationToken = "test2",
            Entries =
            [
                new Model5Entry
                {
                    Creation = DateTimeOffset.UtcNow,
                    Duration = Random.Shared.Next(500, 2000)
                }
            ]
        }, TestContext.Current.CancellationToken);

        // Act
        await Context.Models5Blob.DeleteAllEntitiesAsync("root", TestContext.Current.CancellationToken);
        var result = await Context.Models5Blob
            .Where(x => x.Id == "root")
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task MultipleAppends_ShouldAccumulateEntries()
    {
        // Arrange
        var appendModel = new Model5
        {
            Id = "root",
            ContinuationToken = "test",
            Entries =
            [
                new Model5Entry
                {
                    Creation = DateTimeOffset.UtcNow,
                    Duration = Random.Shared.Next(500, 2000)
                }
            ]
        };
        await Context.Models5Blob.UpsertEntityAsync(appendModel, TestContext.Current.CancellationToken);

        // Act - Append multiple times
        for (int i = 0; i < 3; i++)
        {
            await using Stream stream = BinaryData.FromString($"|{DateTimeOffset.UtcNow.AddSeconds(i + 2).ToUnixTimeSeconds()};{Random.Shared.Next(500, 2000)}").ToStream();
            await Context.Models5Blob.AppendAsync("root", "test", stream, TestContext.Current.CancellationToken);
        }

        var result = await Context.Models5Blob
            .Where(x => x.Id == "root" && x.ContinuationToken == "test")
            .FirstAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Entries);
        Assert.Equal(4, result.Entries.Length); // 1 original + 3 appends
    }
}
