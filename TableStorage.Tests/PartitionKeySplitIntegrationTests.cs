using TableStorage.Tests.Contexts;
using TableStorage.Tests.Infrastructure;
using TableStorage.Tests.Models;

namespace TableStorage.Tests;

/// <summary>
/// Integration tests for partition key split optimization in QueryAsync.
/// Validates that queries with multiple partition key comparisons return correct results
/// when the filter is automatically split into per-partition-key sub-queries.
/// </summary>
public class PartitionKeySplitIntegrationTests(AzuriteFixture azuriteFixture) : AzuriteTestBase(azuriteFixture)
{
    #region Top-Level OR Splitting

    [Fact]
    public async Task QueryAsync_TwoPartitionKeys_Or_ReturnsMatchingEntities()
    {
        // Arrange
        const string pk1 = "split-pk1";
        const string pk2 = "split-pk2";
        const string pk3 = "split-other";

        await Context.Models1.UpsertEntityAsync(new() { PrettyName = pk1, PrettyRow = "r1", MyProperty1 = 1 }, TestContext.Current.CancellationToken);
        await Context.Models1.UpsertEntityAsync(new() { PrettyName = pk2, PrettyRow = "r2", MyProperty1 = 2 }, TestContext.Current.CancellationToken);
        await Context.Models1.UpsertEntityAsync(new() { PrettyName = pk3, PrettyRow = "r3", MyProperty1 = 3 }, TestContext.Current.CancellationToken);

        // Act
        List<Model> results = await Context.Models1
            .QueryAsync(x => x.PrettyName == pk1 || x.PrettyName == pk2, TestContext.Current.CancellationToken)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, results.Count);
        Assert.Contains(results, x => x.PrettyName == pk1);
        Assert.Contains(results, x => x.PrettyName == pk2);
        Assert.DoesNotContain(results, x => x.PrettyName == pk3);
    }

    [Fact]
    public async Task QueryAsync_ThreePartitionKeys_Or_ReturnsAllMatches()
    {
        // Arrange
        const string pk1 = "three-pk1";
        const string pk2 = "three-pk2";
        const string pk3 = "three-pk3";
        const string pk4 = "three-other";

        await Context.Models1.UpsertEntityAsync(new() { PrettyName = pk1, PrettyRow = "r1", MyProperty1 = 1 }, TestContext.Current.CancellationToken);
        await Context.Models1.UpsertEntityAsync(new() { PrettyName = pk2, PrettyRow = "r2", MyProperty1 = 2 }, TestContext.Current.CancellationToken);
        await Context.Models1.UpsertEntityAsync(new() { PrettyName = pk3, PrettyRow = "r3", MyProperty1 = 3 }, TestContext.Current.CancellationToken);
        await Context.Models1.UpsertEntityAsync(new() { PrettyName = pk4, PrettyRow = "r4", MyProperty1 = 4 }, TestContext.Current.CancellationToken);

        // Act
        List<Model> results = await Context.Models1
            .QueryAsync(x => x.PrettyName == pk1 || x.PrettyName == pk2 || x.PrettyName == pk3, TestContext.Current.CancellationToken)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(3, results.Count);
        Assert.DoesNotContain(results, x => x.PrettyName == pk4);
    }

    [Fact]
    public async Task QueryAsync_TwoPartitionKeys_WithRowKeyConditions_ReturnsCorrectEntities()
    {
        // Arrange
        const string pk1 = "rkfilter-pk1";
        const string pk2 = "rkfilter-pk2";

        await Context.Models1.UpsertEntityAsync(new() { PrettyName = pk1, PrettyRow = "match", MyProperty1 = 1 }, TestContext.Current.CancellationToken);
        await Context.Models1.UpsertEntityAsync(new() { PrettyName = pk1, PrettyRow = "nomatch", MyProperty1 = 2 }, TestContext.Current.CancellationToken);
        await Context.Models1.UpsertEntityAsync(new() { PrettyName = pk2, PrettyRow = "match2", MyProperty1 = 3 }, TestContext.Current.CancellationToken);

        // Act
        List<Model> results = await Context.Models1
            .QueryAsync(x =>
                (x.PrettyName == pk1 && x.PrettyRow == "match") ||
                (x.PrettyName == pk2 && x.PrettyRow == "match2"),
                TestContext.Current.CancellationToken)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, results.Count);
        Assert.Contains(results, x => x.PrettyName == pk1 && x.PrettyRow == "match");
        Assert.Contains(results, x => x.PrettyName == pk2 && x.PrettyRow == "match2");
        Assert.DoesNotContain(results, x => x.PrettyRow == "nomatch");
    }

    [Fact]
    public async Task QueryAsync_SamePartitionKeyGrouped_ReturnsCorrectEntities()
    {
        // Arrange
        const string pk1 = "group-pk1";
        const string pk2 = "group-pk2";

        await Context.Models1.UpsertEntityAsync(new() { PrettyName = pk1, PrettyRow = "a", MyProperty1 = 1 }, TestContext.Current.CancellationToken);
        await Context.Models1.UpsertEntityAsync(new() { PrettyName = pk1, PrettyRow = "c", MyProperty1 = 2 }, TestContext.Current.CancellationToken);
        await Context.Models1.UpsertEntityAsync(new() { PrettyName = pk1, PrettyRow = "x", MyProperty1 = 3 }, TestContext.Current.CancellationToken);
        await Context.Models1.UpsertEntityAsync(new() { PrettyName = pk2, PrettyRow = "b", MyProperty1 = 4 }, TestContext.Current.CancellationToken);

        // Act — PK=="1" appears twice, PK=="2" once
        List<Model> results = await Context.Models1
            .QueryAsync(x =>
                (x.PrettyName == pk1 && x.PrettyRow == "a") ||
                (x.PrettyName == pk2 && x.PrettyRow == "b") ||
                (x.PrettyName == pk1 && x.PrettyRow == "c"),
                TestContext.Current.CancellationToken)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(3, results.Count);
        Assert.Contains(results, x => x.PrettyName == pk1 && x.PrettyRow == "a");
        Assert.Contains(results, x => x.PrettyName == pk1 && x.PrettyRow == "c");
        Assert.Contains(results, x => x.PrettyName == pk2 && x.PrettyRow == "b");
        Assert.DoesNotContain(results, x => x.PrettyRow == "x");
    }

    [Fact]
    public async Task QueryAsync_FourDisjuncts_TwoGroupingPairs_ReturnsCorrectEntities()
    {
        // Arrange: (PK=="1" && RK=="a") || (PK=="2" && RK=="b") || (PK=="1" && RK=="c") || (PK=="2" && RK=="d")
        const string pk1 = "twopair-pk1";
        const string pk2 = "twopair-pk2";

        await Context.Models1.UpsertEntityAsync(new() { PrettyName = pk1, PrettyRow = "a", MyProperty1 = 1 }, TestContext.Current.CancellationToken);
        await Context.Models1.UpsertEntityAsync(new() { PrettyName = pk1, PrettyRow = "c", MyProperty1 = 2 }, TestContext.Current.CancellationToken);
        await Context.Models1.UpsertEntityAsync(new() { PrettyName = pk1, PrettyRow = "x", MyProperty1 = 3 }, TestContext.Current.CancellationToken);
        await Context.Models1.UpsertEntityAsync(new() { PrettyName = pk2, PrettyRow = "b", MyProperty1 = 4 }, TestContext.Current.CancellationToken);
        await Context.Models1.UpsertEntityAsync(new() { PrettyName = pk2, PrettyRow = "d", MyProperty1 = 5 }, TestContext.Current.CancellationToken);
        await Context.Models1.UpsertEntityAsync(new() { PrettyName = pk2, PrettyRow = "y", MyProperty1 = 6 }, TestContext.Current.CancellationToken);

        // Act
        List<Model> results = await Context.Models1
            .QueryAsync(x =>
                (x.PrettyName == pk1 && x.PrettyRow == "a") ||
                (x.PrettyName == pk2 && x.PrettyRow == "b") ||
                (x.PrettyName == pk1 && x.PrettyRow == "c") ||
                (x.PrettyName == pk2 && x.PrettyRow == "d"),
                TestContext.Current.CancellationToken)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(4, results.Count);
        Assert.Contains(results, x => x.PrettyName == pk1 && x.PrettyRow == "a");
        Assert.Contains(results, x => x.PrettyName == pk1 && x.PrettyRow == "c");
        Assert.Contains(results, x => x.PrettyName == pk2 && x.PrettyRow == "b");
        Assert.Contains(results, x => x.PrettyName == pk2 && x.PrettyRow == "d");
        Assert.DoesNotContain(results, x => x.PrettyRow == "x");
        Assert.DoesNotContain(results, x => x.PrettyRow == "y");
    }

    [Fact]
    public async Task QueryAsync_BarePkWithCompoundSamePk_ReturnsAllForThatPk()
    {
        // Arrange: PK == pk1 || (PK == pk2 && RK == "b") || (PK == pk1 && RK == "c")
        // Bare PK absorbs the compound — returns all entities for pk1
        const string pk1 = "bareabs-pk1";
        const string pk2 = "bareabs-pk2";

        await Context.Models1.UpsertEntityAsync(new() { PrettyName = pk1, PrettyRow = "a", MyProperty1 = 1 }, TestContext.Current.CancellationToken);
        await Context.Models1.UpsertEntityAsync(new() { PrettyName = pk1, PrettyRow = "c", MyProperty1 = 2 }, TestContext.Current.CancellationToken);
        await Context.Models1.UpsertEntityAsync(new() { PrettyName = pk1, PrettyRow = "z", MyProperty1 = 3 }, TestContext.Current.CancellationToken);
        await Context.Models1.UpsertEntityAsync(new() { PrettyName = pk2, PrettyRow = "b", MyProperty1 = 4 }, TestContext.Current.CancellationToken);
        await Context.Models1.UpsertEntityAsync(new() { PrettyName = pk2, PrettyRow = "other", MyProperty1 = 5 }, TestContext.Current.CancellationToken);

        // Act
        List<Model> results = await Context.Models1
            .QueryAsync(x =>
                x.PrettyName == pk1 ||
                (x.PrettyName == pk2 && x.PrettyRow == "b") ||
                (x.PrettyName == pk1 && x.PrettyRow == "c"),
                TestContext.Current.CancellationToken)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert — pk1 matches all 3 rows (bare PK), pk2 matches only "b"
        Assert.Equal(4, results.Count);
        Assert.Contains(results, x => x.PrettyName == pk1 && x.PrettyRow == "a");
        Assert.Contains(results, x => x.PrettyName == pk1 && x.PrettyRow == "c");
        Assert.Contains(results, x => x.PrettyName == pk1 && x.PrettyRow == "z");
        Assert.Contains(results, x => x.PrettyName == pk2 && x.PrettyRow == "b");
        Assert.DoesNotContain(results, x => x.PrettyName == pk2 && x.PrettyRow == "other");
    }

    [Fact]
    public async Task QueryAsync_GroupedDisjuncts_WithPropertyConditions_ReturnsCorrectEntities()
    {
        // Arrange: (PK=="1" && RK=="a" && MyProperty1 > 5) || (PK=="2" && RK=="b") || (PK=="1" && RK=="c" && MyProperty1 > 5)
        const string pk1 = "groupprop-pk1";
        const string pk2 = "groupprop-pk2";

        await Context.Models1.UpsertEntityAsync(new() { PrettyName = pk1, PrettyRow = "a", MyProperty1 = 10 }, TestContext.Current.CancellationToken);
        await Context.Models1.UpsertEntityAsync(new() { PrettyName = pk1, PrettyRow = "c", MyProperty1 = 20 }, TestContext.Current.CancellationToken);
        await Context.Models1.UpsertEntityAsync(new() { PrettyName = pk1, PrettyRow = "c2", MyProperty1 = 1 }, TestContext.Current.CancellationToken);
        await Context.Models1.UpsertEntityAsync(new() { PrettyName = pk2, PrettyRow = "b", MyProperty1 = 30 }, TestContext.Current.CancellationToken);

        // Act
        List<Model> results = await Context.Models1
            .QueryAsync(x =>
                (x.PrettyName == pk1 && x.PrettyRow == "a" && x.MyProperty1 > 5) ||
                (x.PrettyName == pk2 && x.PrettyRow == "b") ||
                (x.PrettyName == pk1 && x.PrettyRow == "c" && x.MyProperty1 > 5),
                TestContext.Current.CancellationToken)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(3, results.Count);
        Assert.Contains(results, x => x.PrettyName == pk1 && x.PrettyRow == "a");
        Assert.Contains(results, x => x.PrettyName == pk1 && x.PrettyRow == "c");
        Assert.Contains(results, x => x.PrettyName == pk2 && x.PrettyRow == "b");
        Assert.DoesNotContain(results, x => x.PrettyRow == "c2");
    }

    [Fact]
    public async Task QueryAsync_DisjunctsWithDifferentNonPkConditions_ReturnsCorrectEntities()
    {
        // Arrange: (PK=="1" && RK=="a") || (PK=="2" && MyProperty1 > 50) || (PK=="1" && MyProperty2 == "match" && RK != "z")
        // PK=="1" group has heterogeneous remainders
        const string pk1 = "hetero-pk1";
        const string pk2 = "hetero-pk2";

        await Context.Models1.UpsertEntityAsync(new() { PrettyName = pk1, PrettyRow = "a", MyProperty1 = 1, MyProperty2 = "nope" }, TestContext.Current.CancellationToken);
        await Context.Models1.UpsertEntityAsync(new() { PrettyName = pk1, PrettyRow = "b", MyProperty1 = 2, MyProperty2 = "match" }, TestContext.Current.CancellationToken);
        await Context.Models1.UpsertEntityAsync(new() { PrettyName = pk1, PrettyRow = "z", MyProperty1 = 3, MyProperty2 = "match" }, TestContext.Current.CancellationToken);
        await Context.Models1.UpsertEntityAsync(new() { PrettyName = pk2, PrettyRow = "r1", MyProperty1 = 100, MyProperty2 = "a" }, TestContext.Current.CancellationToken);
        await Context.Models1.UpsertEntityAsync(new() { PrettyName = pk2, PrettyRow = "r2", MyProperty1 = 10, MyProperty2 = "b" }, TestContext.Current.CancellationToken);

        // Act
        List<Model> results = await Context.Models1
            .QueryAsync(x =>
                (x.PrettyName == pk1 && x.PrettyRow == "a") ||
                (x.PrettyName == pk2 && x.MyProperty1 > 50) ||
                (x.PrettyName == pk1 && x.MyProperty2 == "match" && x.PrettyRow != "z"),
                TestContext.Current.CancellationToken)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert: pk1/a (by RK), pk1/b (MyProperty2 match, RK != z), pk2/r1 (MyProperty1 > 50)
        // pk1/z excluded (RK == "z"), pk2/r2 excluded (MyProperty1 <= 50)
        Assert.Equal(3, results.Count);
        Assert.Contains(results, x => x.PrettyName == pk1 && x.PrettyRow == "a");
        Assert.Contains(results, x => x.PrettyName == pk1 && x.PrettyRow == "b");
        Assert.Contains(results, x => x.PrettyName == pk2 && x.PrettyRow == "r1");
        Assert.DoesNotContain(results, x => x.PrettyRow == "z");
        Assert.DoesNotContain(results, x => x.PrettyRow == "r2");
    }

    [Fact]
    public async Task QueryAsync_GroupedDisjuncts_MultipleFieldsPerRemainder_ReturnsCorrectEntities()
    {
        // Arrange: (PK=="1" && RK=="a" && MyProperty1 > 5) || (PK=="2" && RK=="b" && MyProperty2 == "yes") || (PK=="1" && RK=="c" && MyProperty1 < 100)
        const string pk1 = "mfgroup-pk1";
        const string pk2 = "mfgroup-pk2";

        await Context.Models1.UpsertEntityAsync(new() { PrettyName = pk1, PrettyRow = "a", MyProperty1 = 10, MyProperty2 = "x" }, TestContext.Current.CancellationToken);
        await Context.Models1.UpsertEntityAsync(new() { PrettyName = pk1, PrettyRow = "a2", MyProperty1 = 2, MyProperty2 = "x" }, TestContext.Current.CancellationToken);
        await Context.Models1.UpsertEntityAsync(new() { PrettyName = pk1, PrettyRow = "c", MyProperty1 = 50, MyProperty2 = "x" }, TestContext.Current.CancellationToken);
        await Context.Models1.UpsertEntityAsync(new() { PrettyName = pk2, PrettyRow = "b", MyProperty1 = 1, MyProperty2 = "yes" }, TestContext.Current.CancellationToken);
        await Context.Models1.UpsertEntityAsync(new() { PrettyName = pk2, PrettyRow = "b2", MyProperty1 = 1, MyProperty2 = "no" }, TestContext.Current.CancellationToken);

        // Act
        List<Model> results = await Context.Models1
            .QueryAsync(x =>
                (x.PrettyName == pk1 && x.PrettyRow == "a" && x.MyProperty1 > 5) ||
                (x.PrettyName == pk2 && x.PrettyRow == "b" && x.MyProperty2 == "yes") ||
                (x.PrettyName == pk1 && x.PrettyRow == "c" && x.MyProperty1 < 100),
                TestContext.Current.CancellationToken)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert: pk1/a (prop1>5), pk1/c (prop1<100), pk2/b (prop2=="yes")
        Assert.Equal(3, results.Count);
        Assert.Contains(results, x => x.PrettyName == pk1 && x.PrettyRow == "a");
        Assert.Contains(results, x => x.PrettyName == pk1 && x.PrettyRow == "c");
        Assert.Contains(results, x => x.PrettyName == pk2 && x.PrettyRow == "b");
        Assert.DoesNotContain(results, x => x.PrettyRow == "a2");
        Assert.DoesNotContain(results, x => x.PrettyRow == "b2");
    }

    #endregion

    #region AND Distribution

    [Fact]
    public async Task QueryAsync_AndDistribution_OrInsideAnd_ReturnsCorrectEntities()
    {
        // Arrange: (PK == "1" || PK == "2") && RK == "a"
        const string pk1 = "anddist-pk1";
        const string pk2 = "anddist-pk2";

        await Context.Models1.UpsertEntityAsync(new() { PrettyName = pk1, PrettyRow = "target", MyProperty1 = 1 }, TestContext.Current.CancellationToken);
        await Context.Models1.UpsertEntityAsync(new() { PrettyName = pk1, PrettyRow = "other", MyProperty1 = 2 }, TestContext.Current.CancellationToken);
        await Context.Models1.UpsertEntityAsync(new() { PrettyName = pk2, PrettyRow = "target", MyProperty1 = 3 }, TestContext.Current.CancellationToken);
        await Context.Models1.UpsertEntityAsync(new() { PrettyName = pk2, PrettyRow = "other", MyProperty1 = 4 }, TestContext.Current.CancellationToken);

        // Act
        List<Model> results = await Context.Models1
            .QueryAsync(x =>
                (x.PrettyName == pk1 || x.PrettyName == pk2) && x.PrettyRow == "target",
                TestContext.Current.CancellationToken)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, results.Count);
        Assert.All(results, x => Assert.Equal("target", x.PrettyRow));
        Assert.Contains(results, x => x.PrettyName == pk1);
        Assert.Contains(results, x => x.PrettyName == pk2);
    }

    [Fact]
    public async Task QueryAsync_AndDistribution_MultipleSharedConditions_ReturnsCorrectEntities()
    {
        // Arrange: (PK == "1" || PK == "2") && MyProperty1 > 5 && MyProperty2 == "match"
        const string pk1 = "multishare-pk1";
        const string pk2 = "multishare-pk2";

        await Context.Models1.UpsertEntityAsync(new() { PrettyName = pk1, PrettyRow = "r1", MyProperty1 = 10, MyProperty2 = "match" }, TestContext.Current.CancellationToken);
        await Context.Models1.UpsertEntityAsync(new() { PrettyName = pk1, PrettyRow = "r2", MyProperty1 = 3, MyProperty2 = "match" }, TestContext.Current.CancellationToken);
        await Context.Models1.UpsertEntityAsync(new() { PrettyName = pk2, PrettyRow = "r3", MyProperty1 = 20, MyProperty2 = "match" }, TestContext.Current.CancellationToken);
        await Context.Models1.UpsertEntityAsync(new() { PrettyName = pk2, PrettyRow = "r4", MyProperty1 = 20, MyProperty2 = "nomatch" }, TestContext.Current.CancellationToken);

        // Act
        List<Model> results = await Context.Models1
            .QueryAsync(x =>
                (x.PrettyName == pk1 || x.PrettyName == pk2) && x.MyProperty1 > 5 && x.MyProperty2 == "match",
                TestContext.Current.CancellationToken)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, results.Count);
        Assert.Contains(results, x => x.PrettyName == pk1 && x.PrettyRow == "r1");
        Assert.Contains(results, x => x.PrettyName == pk2 && x.PrettyRow == "r3");
    }

    [Fact]
    public async Task QueryAsync_AndDistribution_ThreeSharedFields_ReturnsCorrectEntities()
    {
        // Arrange: (PK == "1" || PK == "2") && RK != "z" && MyProperty1 > 5 && MyProperty2 == "ok"
        // Three shared terms combined with two PK branches
        const string pk1 = "threeshare-pk1";
        const string pk2 = "threeshare-pk2";

        await Context.Models1.UpsertEntityAsync(new() { PrettyName = pk1, PrettyRow = "r1", MyProperty1 = 10, MyProperty2 = "ok" }, TestContext.Current.CancellationToken);
        await Context.Models1.UpsertEntityAsync(new() { PrettyName = pk1, PrettyRow = "z", MyProperty1 = 10, MyProperty2 = "ok" }, TestContext.Current.CancellationToken);
        await Context.Models1.UpsertEntityAsync(new() { PrettyName = pk1, PrettyRow = "r2", MyProperty1 = 2, MyProperty2 = "ok" }, TestContext.Current.CancellationToken);
        await Context.Models1.UpsertEntityAsync(new() { PrettyName = pk2, PrettyRow = "r3", MyProperty1 = 20, MyProperty2 = "ok" }, TestContext.Current.CancellationToken);
        await Context.Models1.UpsertEntityAsync(new() { PrettyName = pk2, PrettyRow = "r4", MyProperty1 = 20, MyProperty2 = "bad" }, TestContext.Current.CancellationToken);

        // Act
        List<Model> results = await Context.Models1
            .QueryAsync(x =>
                (x.PrettyName == pk1 || x.PrettyName == pk2) && x.PrettyRow != "z" && x.MyProperty1 > 5 && x.MyProperty2 == "ok",
                TestContext.Current.CancellationToken)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert: pk1/r1 passes all, pk2/r3 passes all — others fail one condition each
        Assert.Equal(2, results.Count);
        Assert.Contains(results, x => x.PrettyName == pk1 && x.PrettyRow == "r1");
        Assert.Contains(results, x => x.PrettyName == pk2 && x.PrettyRow == "r3");
    }

    [Fact]
    public async Task QueryAsync_AndDistribution_GroupingWithMultipleSharedFields_ReturnsCorrectEntities()
    {
        // Arrange: ((PK=="1" && RK=="a") || (PK=="2" && RK=="b") || (PK=="1" && RK=="c")) && MyProperty1 > 0 && MyProperty2 == "active"
        const string pk1 = "grpshare-pk1";
        const string pk2 = "grpshare-pk2";

        await Context.Models1.UpsertEntityAsync(new() { PrettyName = pk1, PrettyRow = "a", MyProperty1 = 10, MyProperty2 = "active" }, TestContext.Current.CancellationToken);
        await Context.Models1.UpsertEntityAsync(new() { PrettyName = pk1, PrettyRow = "c", MyProperty1 = 20, MyProperty2 = "active" }, TestContext.Current.CancellationToken);
        await Context.Models1.UpsertEntityAsync(new() { PrettyName = pk1, PrettyRow = "c2", MyProperty1 = 30, MyProperty2 = "inactive" }, TestContext.Current.CancellationToken);
        await Context.Models1.UpsertEntityAsync(new() { PrettyName = pk2, PrettyRow = "b", MyProperty1 = 40, MyProperty2 = "active" }, TestContext.Current.CancellationToken);
        await Context.Models1.UpsertEntityAsync(new() { PrettyName = pk2, PrettyRow = "b2", MyProperty1 = 50, MyProperty2 = "active" }, TestContext.Current.CancellationToken);

        // Act
        List<Model> results = await Context.Models1
            .QueryAsync(x =>
                ((x.PrettyName == pk1 && x.PrettyRow == "a") ||
                 (x.PrettyName == pk2 && x.PrettyRow == "b") ||
                 (x.PrettyName == pk1 && x.PrettyRow == "c")) &&
                x.MyProperty1 > 0 && x.MyProperty2 == "active",
                TestContext.Current.CancellationToken)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert: pk1/a, pk1/c, pk2/b match — pk1/c2 wrong MyProperty2, pk2/b2 wrong RK
        Assert.Equal(3, results.Count);
        Assert.Contains(results, x => x.PrettyName == pk1 && x.PrettyRow == "a");
        Assert.Contains(results, x => x.PrettyName == pk1 && x.PrettyRow == "c");
        Assert.Contains(results, x => x.PrettyName == pk2 && x.PrettyRow == "b");
        Assert.DoesNotContain(results, x => x.PrettyRow == "c2");
        Assert.DoesNotContain(results, x => x.PrettyRow == "b2");
    }

    [Fact]
    public async Task QueryAsync_AndDistribution_WithGrouping_ReturnsCorrectEntities()
    {
        // Arrange: ((PK=="1" && RK=="a") || (PK=="2" && RK=="b") || (PK=="1" && RK=="c")) && MyProperty1 > 0
        const string pk1 = "anddistgrp-pk1";
        const string pk2 = "anddistgrp-pk2";

        await Context.Models1.UpsertEntityAsync(new() { PrettyName = pk1, PrettyRow = "a", MyProperty1 = 10 }, TestContext.Current.CancellationToken);
        await Context.Models1.UpsertEntityAsync(new() { PrettyName = pk1, PrettyRow = "c", MyProperty1 = 20 }, TestContext.Current.CancellationToken);
        await Context.Models1.UpsertEntityAsync(new() { PrettyName = pk1, PrettyRow = "x", MyProperty1 = 30 }, TestContext.Current.CancellationToken);
        await Context.Models1.UpsertEntityAsync(new() { PrettyName = pk2, PrettyRow = "b", MyProperty1 = 40 }, TestContext.Current.CancellationToken);
        await Context.Models1.UpsertEntityAsync(new() { PrettyName = pk2, PrettyRow = "y", MyProperty1 = 50 }, TestContext.Current.CancellationToken);

        // Act
        List<Model> results = await Context.Models1
            .QueryAsync(x =>
                ((x.PrettyName == pk1 && x.PrettyRow == "a") ||
                 (x.PrettyName == pk2 && x.PrettyRow == "b") ||
                 (x.PrettyName == pk1 && x.PrettyRow == "c")) &&
                x.MyProperty1 > 0,
                TestContext.Current.CancellationToken)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(3, results.Count);
        Assert.Contains(results, x => x.PrettyName == pk1 && x.PrettyRow == "a");
        Assert.Contains(results, x => x.PrettyName == pk1 && x.PrettyRow == "c");
        Assert.Contains(results, x => x.PrettyName == pk2 && x.PrettyRow == "b");
        Assert.DoesNotContain(results, x => x.PrettyRow == "x");
        Assert.DoesNotContain(results, x => x.PrettyRow == "y");
    }

    [Fact]
    public async Task QueryAsync_AndDistribution_GroupingWithChainedWhere_ReturnsCorrectEntities()
    {
        // Arrange: ((PK=="1" && RK=="a") || (PK=="2" && RK=="b") || (PK=="1" && RK=="c")).Where(MyProperty1 > 5)
        const string pk1 = "chaingrp-pk1";
        const string pk2 = "chaingrp-pk2";

        await Context.Models1.UpsertEntityAsync(new() { PrettyName = pk1, PrettyRow = "a", MyProperty1 = 10 }, TestContext.Current.CancellationToken);
        await Context.Models1.UpsertEntityAsync(new() { PrettyName = pk1, PrettyRow = "c", MyProperty1 = 2 }, TestContext.Current.CancellationToken);
        await Context.Models1.UpsertEntityAsync(new() { PrettyName = pk2, PrettyRow = "b", MyProperty1 = 20 }, TestContext.Current.CancellationToken);

        // Act
        List<Model> results = await Context.Models1
            .Where(x =>
                (x.PrettyName == pk1 && x.PrettyRow == "a") ||
                (x.PrettyName == pk2 && x.PrettyRow == "b") ||
                (x.PrettyName == pk1 && x.PrettyRow == "c"))
            .Where(x => x.MyProperty1 > 5)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert — pk1/c has MyProperty1==2, so it's excluded
        Assert.Equal(2, results.Count);
        Assert.Contains(results, x => x.PrettyName == pk1 && x.PrettyRow == "a");
        Assert.Contains(results, x => x.PrettyName == pk2 && x.PrettyRow == "b");
    }

    #endregion

    #region LINQ Pipeline (Where chaining)

    [Fact]
    public async Task Where_TwoPartitionKeys_Or_ReturnsMatchingEntities()
    {
        // Arrange
        const string pk1 = "linq-pk1";
        const string pk2 = "linq-pk2";
        const string pk3 = "linq-other";

        await Context.Models1.UpsertEntityAsync(new() { PrettyName = pk1, PrettyRow = "r1", MyProperty1 = 1 }, TestContext.Current.CancellationToken);
        await Context.Models1.UpsertEntityAsync(new() { PrettyName = pk2, PrettyRow = "r2", MyProperty1 = 2 }, TestContext.Current.CancellationToken);
        await Context.Models1.UpsertEntityAsync(new() { PrettyName = pk3, PrettyRow = "r3", MyProperty1 = 3 }, TestContext.Current.CancellationToken);

        // Act
        List<Model> results = await Context.Models1
            .Where(x => x.PrettyName == pk1 || x.PrettyName == pk2)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, results.Count);
        Assert.Contains(results, x => x.PrettyName == pk1);
        Assert.Contains(results, x => x.PrettyName == pk2);
    }

    [Fact]
    public async Task Where_Chained_AndDistribution_ReturnsCorrectEntities()
    {
        // Arrange: .Where(PK == "1" || PK == "2").Where(MyProperty1 > 5)
        // Combined filter: (PK == "1" || PK == "2") && MyProperty1 > 5
        const string pk1 = "chain-pk1";
        const string pk2 = "chain-pk2";

        await Context.Models1.UpsertEntityAsync(new() { PrettyName = pk1, PrettyRow = "r1", MyProperty1 = 10 }, TestContext.Current.CancellationToken);
        await Context.Models1.UpsertEntityAsync(new() { PrettyName = pk1, PrettyRow = "r2", MyProperty1 = 3 }, TestContext.Current.CancellationToken);
        await Context.Models1.UpsertEntityAsync(new() { PrettyName = pk2, PrettyRow = "r3", MyProperty1 = 20 }, TestContext.Current.CancellationToken);
        await Context.Models1.UpsertEntityAsync(new() { PrettyName = pk2, PrettyRow = "r4", MyProperty1 = 1 }, TestContext.Current.CancellationToken);

        // Act
        List<Model> results = await Context.Models1
            .Where(x => x.PrettyName == pk1 || x.PrettyName == pk2)
            .Where(x => x.MyProperty1 > 5)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, results.Count);
        Assert.Contains(results, x => x.PrettyName == pk1 && x.MyProperty1 == 10);
        Assert.Contains(results, x => x.PrettyName == pk2 && x.MyProperty1 == 20);
    }

    [Fact]
    public async Task Where_Chained_WithSelect_ReturnsProjectedValues()
    {
        // Arrange
        const string pk1 = "selectchain-pk1";
        const string pk2 = "selectchain-pk2";

        await Context.Models1.UpsertEntityAsync(new() { PrettyName = pk1, PrettyRow = "r1", MyProperty1 = 10, MyProperty2 = "hello" }, TestContext.Current.CancellationToken);
        await Context.Models1.UpsertEntityAsync(new() { PrettyName = pk2, PrettyRow = "r2", MyProperty1 = 20, MyProperty2 = "world" }, TestContext.Current.CancellationToken);

        // Act
        List<int> results = await Context.Models1
            .Where(x => x.PrettyName == pk1 || x.PrettyName == pk2)
            .Select(x => x.MyProperty1)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, results.Count);
        Assert.Contains(10, results);
        Assert.Contains(20, results);
    }

    [Fact]
    public async Task Where_Chained_WithTake_ReturnsLimitedResults()
    {
        // Arrange
        const string pk1 = "takechain-pk1";
        const string pk2 = "takechain-pk2";

        await Context.Models1.UpsertEntityAsync(new() { PrettyName = pk1, PrettyRow = "r1", MyProperty1 = 1 }, TestContext.Current.CancellationToken);
        await Context.Models1.UpsertEntityAsync(new() { PrettyName = pk1, PrettyRow = "r2", MyProperty1 = 2 }, TestContext.Current.CancellationToken);
        await Context.Models1.UpsertEntityAsync(new() { PrettyName = pk2, PrettyRow = "r3", MyProperty1 = 3 }, TestContext.Current.CancellationToken);
        await Context.Models1.UpsertEntityAsync(new() { PrettyName = pk2, PrettyRow = "r4", MyProperty1 = 4 }, TestContext.Current.CancellationToken);

        // Act
        List<Model> results = await Context.Models1
            .Where(x => x.PrettyName == pk1 || x.PrettyName == pk2)
            .Take(2)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.True(results.Count <= 2);
    }

    #endregion

    #region No-Split Fallback Scenarios

    [Fact]
    public async Task QueryAsync_SinglePartitionKey_StillWorks()
    {
        // Arrange
        const string pk = "single-pk";

        await Context.Models1.UpsertEntityAsync(new() { PrettyName = pk, PrettyRow = "r1", MyProperty1 = 1 }, TestContext.Current.CancellationToken);
        await Context.Models1.UpsertEntityAsync(new() { PrettyName = pk, PrettyRow = "r2", MyProperty1 = 2 }, TestContext.Current.CancellationToken);
        await Context.Models1.UpsertEntityAsync(new() { PrettyName = "other-pk", PrettyRow = "r3", MyProperty1 = 3 }, TestContext.Current.CancellationToken);

        // Act
        List<Model> results = await Context.Models1
            .QueryAsync(x => x.PrettyName == pk, TestContext.Current.CancellationToken)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, results.Count);
        Assert.All(results, x => Assert.Equal(pk, x.PrettyName));
    }

    [Fact]
    public async Task QueryAsync_SinglePartitionKey_OrOnRowKey_StillWorks()
    {
        // Arrange: PK == "x" && (RK == "a" || RK == "b") — single PK, no split
        const string pk = "rkor-pk";

        await Context.Models1.UpsertEntityAsync(new() { PrettyName = pk, PrettyRow = "a", MyProperty1 = 1 }, TestContext.Current.CancellationToken);
        await Context.Models1.UpsertEntityAsync(new() { PrettyName = pk, PrettyRow = "b", MyProperty1 = 2 }, TestContext.Current.CancellationToken);
        await Context.Models1.UpsertEntityAsync(new() { PrettyName = pk, PrettyRow = "c", MyProperty1 = 3 }, TestContext.Current.CancellationToken);

        // Act
        List<Model> results = await Context.Models1
            .QueryAsync(x => x.PrettyName == pk && (x.PrettyRow == "a" || x.PrettyRow == "b"), TestContext.Current.CancellationToken)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, results.Count);
        Assert.Contains(results, x => x.PrettyRow == "a");
        Assert.Contains(results, x => x.PrettyRow == "b");
    }

    [Fact]
    public async Task QueryAsync_AllSamePartitionKey_StillWorks()
    {
        // Arrange: (PK == "x" && RK == "a") || (PK == "x" && RK == "b") — all same PK, no split
        const string pk = "allsame-pk";

        await Context.Models1.UpsertEntityAsync(new() { PrettyName = pk, PrettyRow = "a", MyProperty1 = 1 }, TestContext.Current.CancellationToken);
        await Context.Models1.UpsertEntityAsync(new() { PrettyName = pk, PrettyRow = "b", MyProperty1 = 2 }, TestContext.Current.CancellationToken);
        await Context.Models1.UpsertEntityAsync(new() { PrettyName = pk, PrettyRow = "c", MyProperty1 = 3 }, TestContext.Current.CancellationToken);

        // Act
        List<Model> results = await Context.Models1
            .QueryAsync(x =>
                (x.PrettyName == pk && x.PrettyRow == "a") ||
                (x.PrettyName == pk && x.PrettyRow == "b"),
                TestContext.Current.CancellationToken)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, results.Count);
    }

    #endregion

    #region Non-Proxy Model (Model2 has no PartitionKey proxy)

    [Fact]
    public async Task QueryAsync_NonProxyModel_TwoPartitionKeys_ReturnsMatchingEntities()
    {
        // Arrange — Model2 has default PartitionKey (no proxy rename)
        const string pk1 = "noproxy-pk1";
        const string pk2 = "noproxy-pk2";

        await Context.Models2.UpsertEntityAsync(new() { PartitionKey = pk1, PrettyRow = "r1", MyProperty1 = 1, MyProperty2 = "a" }, TestContext.Current.CancellationToken);
        await Context.Models2.UpsertEntityAsync(new() { PartitionKey = pk2, PrettyRow = "r2", MyProperty1 = 2, MyProperty2 = "b" }, TestContext.Current.CancellationToken);
        await Context.Models2.UpsertEntityAsync(new() { PartitionKey = "other", PrettyRow = "r3", MyProperty1 = 3, MyProperty2 = "c" }, TestContext.Current.CancellationToken);

        // Act
        List<Model2> results = await Context.Models2
            .QueryAsync(x => x.PartitionKey == pk1 || x.PartitionKey == pk2, TestContext.Current.CancellationToken)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, results.Count);
        Assert.Contains(results, x => x.PartitionKey == pk1);
        Assert.Contains(results, x => x.PartitionKey == pk2);
    }

    [Fact]
    public async Task Where_NonProxyModel_AndDistribution_ReturnsCorrectEntities()
    {
        // Arrange — .Where(PK == "1" || PK == "2").Where(MyProperty1 > 5)
        const string pk1 = "noproxy-chain-pk1";
        const string pk2 = "noproxy-chain-pk2";

        await Context.Models2.UpsertEntityAsync(new() { PartitionKey = pk1, PrettyRow = "r1", MyProperty1 = 10, MyProperty2 = "a" }, TestContext.Current.CancellationToken);
        await Context.Models2.UpsertEntityAsync(new() { PartitionKey = pk1, PrettyRow = "r2", MyProperty1 = 3, MyProperty2 = "b" }, TestContext.Current.CancellationToken);
        await Context.Models2.UpsertEntityAsync(new() { PartitionKey = pk2, PrettyRow = "r3", MyProperty1 = 20, MyProperty2 = "c" }, TestContext.Current.CancellationToken);

        // Act
        List<Model2> results = await Context.Models2
            .Where(x => x.PartitionKey == pk1 || x.PartitionKey == pk2)
            .Where(x => x.MyProperty1 > 5)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, results.Count);
        Assert.Contains(results, x => x.PartitionKey == pk1 && x.MyProperty1 == 10);
        Assert.Contains(results, x => x.PartitionKey == pk2 && x.MyProperty1 == 20);
    }

    #endregion

    #region Captured Variables

    [Fact]
    public async Task QueryAsync_CapturedVariables_ReturnsMatchingEntities()
    {
        // Arrange
        await Context.Models1.UpsertEntityAsync(new() { PrettyName = "captured-pk1", PrettyRow = "r1", MyProperty1 = 1 }, TestContext.Current.CancellationToken);
        await Context.Models1.UpsertEntityAsync(new() { PrettyName = "captured-pk2", PrettyRow = "r2", MyProperty1 = 2 }, TestContext.Current.CancellationToken);
        await Context.Models1.UpsertEntityAsync(new() { PrettyName = "captured-other", PrettyRow = "r3", MyProperty1 = 3 }, TestContext.Current.CancellationToken);

        // Act — variables captured in closure
        List<Model> results = await QueryWithCapturedVariables(Context, "captured-pk1", "captured-pk2");

        // Assert
        Assert.Equal(2, results.Count);
        Assert.Contains(results, x => x.PrettyName == "captured-pk1");
        Assert.Contains(results, x => x.PrettyName == "captured-pk2");
    }

    private static async Task<List<Model>> QueryWithCapturedVariables(MyTableContext context, string pk1, string pk2)
    {
        return await context.Models1
            .QueryAsync(x => x.PrettyName == pk1 || x.PrettyName == pk2, TestContext.Current.CancellationToken)
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    #endregion

    #region ExistsIn (generates the OR chain pattern)

    [Fact]
    public async Task Where_ExistsIn_MultiplePartitionKeys_ReturnsMatchingEntities()
    {
        // Arrange — ExistsIn generates PK == "1" || PK == "2" || PK == "3"
        const string pk1 = "existsin-pk1";
        const string pk2 = "existsin-pk2";
        const string pk3 = "existsin-pk3";

        await Context.Models1.UpsertEntityAsync(new() { PrettyName = pk1, PrettyRow = "r1", MyProperty1 = 1 }, TestContext.Current.CancellationToken);
        await Context.Models1.UpsertEntityAsync(new() { PrettyName = pk2, PrettyRow = "r2", MyProperty1 = 2 }, TestContext.Current.CancellationToken);
        await Context.Models1.UpsertEntityAsync(new() { PrettyName = pk3, PrettyRow = "r3", MyProperty1 = 3 }, TestContext.Current.CancellationToken);
        await Context.Models1.UpsertEntityAsync(new() { PrettyName = "existsin-other", PrettyRow = "r4", MyProperty1 = 4 }, TestContext.Current.CancellationToken);

        string[] partitionKeys = [pk1, pk2, pk3];

        // Act
        List<Model> results = await Context.Models1
            .ExistsIn(x => x.PrettyName, partitionKeys)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(3, results.Count);
        Assert.Contains(results, x => x.PrettyName == pk1);
        Assert.Contains(results, x => x.PrettyName == pk2);
        Assert.Contains(results, x => x.PrettyName == pk3);
    }

    [Fact]
    public async Task Where_ExistsIn_WithAdditionalFilter_ReturnsFilteredResults()
    {
        // Arrange — ExistsIn + .Where produces AND distribution pattern
        const string pk1 = "existsinfilter-pk1";
        const string pk2 = "existsinfilter-pk2";

        await Context.Models1.UpsertEntityAsync(new() { PrettyName = pk1, PrettyRow = "r1", MyProperty1 = 10 }, TestContext.Current.CancellationToken);
        await Context.Models1.UpsertEntityAsync(new() { PrettyName = pk1, PrettyRow = "r2", MyProperty1 = 3 }, TestContext.Current.CancellationToken);
        await Context.Models1.UpsertEntityAsync(new() { PrettyName = pk2, PrettyRow = "r3", MyProperty1 = 20 }, TestContext.Current.CancellationToken);
        await Context.Models1.UpsertEntityAsync(new() { PrettyName = pk2, PrettyRow = "r4", MyProperty1 = 1 }, TestContext.Current.CancellationToken);

        string[] partitionKeys = [pk1, pk2];

        // Act
        List<Model> results = await Context.Models1
            .ExistsIn(x => x.PrettyName, partitionKeys)
            .Where(x => x.MyProperty1 > 5)
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, results.Count);
        Assert.Contains(results, x => x.PrettyName == pk1 && x.MyProperty1 == 10);
        Assert.Contains(results, x => x.PrettyName == pk2 && x.MyProperty1 == 20);
    }

    #endregion
}
