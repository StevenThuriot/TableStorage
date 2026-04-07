using System.Linq.Expressions;
using Azure.Data.Tables;
using TableStorage.Visitors;

namespace TableStorage.Tests;

public class PartitionKeySplitTests
{
    #region Splitting Scenarios

    [Fact]
    public void TrySplit_TwoPartitionKeys_Or_ReturnsTwoFilters()
    {
        // Arrange: PK == "1" || PK == "2"
        Expression<Func<TableEntity, bool>> filter = x => x.PartitionKey == "1" || x.PartitionKey == "2";

        // Act
        var result = PartitionKeySplitVisitor.TrySplit(filter);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Equal("""(x.PartitionKey == "1")""", result[0].Body.ToString());
        Assert.Equal("""(x.PartitionKey == "2")""", result[1].Body.ToString());
        AssertFilterMatchesEntities(result[0], [CreateEntity("1", "a")], [CreateEntity("2", "a")]);
        AssertFilterMatchesEntities(result[1], [CreateEntity("2", "a")], [CreateEntity("1", "a")]);
    }

    [Fact]
    public void TrySplit_ThreePartitionKeys_Or_ReturnsThreeFilters()
    {
        // Arrange: PK == "1" || PK == "2" || PK == "3"
        Expression<Func<TableEntity, bool>> filter = x => x.PartitionKey == "1" || x.PartitionKey == "2" || x.PartitionKey == "3";

        // Act
        var result = PartitionKeySplitVisitor.TrySplit(filter);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Count);
        Assert.Equal("""(x.PartitionKey == "1")""", result[0].Body.ToString());
        Assert.Equal("""(x.PartitionKey == "2")""", result[1].Body.ToString());
        Assert.Equal("""(x.PartitionKey == "3")""", result[2].Body.ToString());
        AssertFilterMatchesEntities(result[0], [CreateEntity("1", "a")], [CreateEntity("2", "a"), CreateEntity("3", "a")]);
        AssertFilterMatchesEntities(result[1], [CreateEntity("2", "a")], [CreateEntity("1", "a"), CreateEntity("3", "a")]);
        AssertFilterMatchesEntities(result[2], [CreateEntity("3", "a")], [CreateEntity("1", "a"), CreateEntity("2", "a")]);
    }

    [Fact]
    public void TrySplit_TwoPartitionKeys_WithRowKeyConditions_ReturnsTwoFilters()
    {
        // Arrange: (PK == "1" && RK == "a") || (PK == "2" && RK == "b")
        Expression<Func<TableEntity, bool>> filter = x =>
            (x.PartitionKey == "1" && x.RowKey == "a") ||
            (x.PartitionKey == "2" && x.RowKey == "b");

        // Act
        var result = PartitionKeySplitVisitor.TrySplit(filter);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Equal("""((x.PartitionKey == "1") AndAlso (x.RowKey == "a"))""", result[0].Body.ToString());
        Assert.Equal("""((x.PartitionKey == "2") AndAlso (x.RowKey == "b"))""", result[1].Body.ToString());
        AssertFilterMatchesEntities(result[0], [CreateEntity("1", "a")], [CreateEntity("1", "b"), CreateEntity("2", "b")]);
        AssertFilterMatchesEntities(result[1], [CreateEntity("2", "b")], [CreateEntity("2", "a"), CreateEntity("1", "a")]);
    }

    [Fact]
    public void TrySplit_SamePartitionKeyGrouped_ReturnsTwoGroups()
    {
        // Arrange: (PK == "1" && RK == "a") || (PK == "2" && RK == "b") || (PK == "1" && RK == "c")
        Expression<Func<TableEntity, bool>> filter = x =>
            (x.PartitionKey == "1" && x.RowKey == "a") ||
            (x.PartitionKey == "2" && x.RowKey == "b") ||
            (x.PartitionKey == "1" && x.RowKey == "c");

        // Act
        var result = PartitionKeySplitVisitor.TrySplit(filter);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Equal("""((x.PartitionKey == "1") AndAlso ((x.RowKey == "a") OrElse (x.RowKey == "c")))""", result[0].Body.ToString());
        Assert.Equal("""((x.PartitionKey == "2") AndAlso (x.RowKey == "b"))""", result[1].Body.ToString());

        // Group for PK=="1" should match both RK=="a" and RK=="c"
        AssertFilterMatchesEntities(result[0],
            [CreateEntity("1", "a"), CreateEntity("1", "c")],
            [CreateEntity("2", "b")]);

        // Group for PK=="2" should match RK=="b"
        AssertFilterMatchesEntities(result[1],
            [CreateEntity("2", "b")],
            [CreateEntity("1", "a"), CreateEntity("1", "c")]);
    }

    [Fact]
    public void TrySplit_AndDistribution_OrInsideAnd_ReturnsTwoFilters()
    {
        // Arrange: (PK == "1" || PK == "2") && RK == "a"
        Expression<Func<TableEntity, bool>> filter = x =>
            (x.PartitionKey == "1" || x.PartitionKey == "2") && x.RowKey == "a";

        // Act
        var result = PartitionKeySplitVisitor.TrySplit(filter);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Equal("""((x.PartitionKey == "1") AndAlso (x.RowKey == "a"))""", result[0].Body.ToString());
        Assert.Equal("""((x.PartitionKey == "2") AndAlso (x.RowKey == "a"))""", result[1].Body.ToString());
        AssertFilterMatchesEntities(result[0], [CreateEntity("1", "a")], [CreateEntity("1", "b"), CreateEntity("2", "a")]);
        AssertFilterMatchesEntities(result[1], [CreateEntity("2", "a")], [CreateEntity("2", "b"), CreateEntity("1", "a")]);
    }

    [Fact]
    public void TrySplit_AndDistribution_MultipleSharedTerms_ReturnsTwoFilters()
    {
        // Arrange: (PK == "1" || PK == "2") && RK == "a" && RK != "z"
        Expression<Func<TableEntity, bool>> filter = x =>
            (x.PartitionKey == "1" || x.PartitionKey == "2") && x.RowKey == "a" && x.RowKey != "z";

        // Act
        var result = PartitionKeySplitVisitor.TrySplit(filter);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Equal("""((x.PartitionKey == "1") AndAlso ((x.RowKey == "a") AndAlso (x.RowKey != "z")))""", result[0].Body.ToString());
        Assert.Equal("""((x.PartitionKey == "2") AndAlso ((x.RowKey == "a") AndAlso (x.RowKey != "z")))""", result[1].Body.ToString());
        AssertFilterMatchesEntities(result[0], [CreateEntity("1", "a")], [CreateEntity("1", "z"), CreateEntity("2", "a")]);
        AssertFilterMatchesEntities(result[1], [CreateEntity("2", "a")], [CreateEntity("2", "z"), CreateEntity("1", "a")]);
    }

    [Fact]
    public void TrySplit_CapturedVariables_ReturnsTwoFilters()
    {
        // Arrange: captured variables in closure
        Expression<Func<TableEntity, bool>> filter = CreateCapturedVariableFilter("partition-1", "partition-2");

        // Act
        var result = PartitionKeySplitVisitor.TrySplit(filter);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        AssertFilterMatchesEntities(result[0], [CreateEntity("partition-1", "a")], [CreateEntity("partition-2", "a")]);
        AssertFilterMatchesEntities(result[1], [CreateEntity("partition-2", "a")], [CreateEntity("partition-1", "a")]);
    }

    [Fact]
    public void TrySplit_ReversedOperands_ReturnsTwoFilters()
    {
        // Arrange: "1" == PK || "2" == PK (constant on left side)
        ParameterExpression param = Expression.Parameter(typeof(TableEntity), "x");
        Expression pkAccess = Expression.Property(param, nameof(ITableEntity.PartitionKey));

        Expression body = Expression.OrElse(
            Expression.Equal(Expression.Constant("1"), pkAccess),
            Expression.Equal(Expression.Constant("2"), pkAccess));

        var filter = Expression.Lambda<Func<TableEntity, bool>>(body, param);

        // Act
        var result = PartitionKeySplitVisitor.TrySplit(filter);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Equal("""("1" == x.PartitionKey)""", result[0].Body.ToString());
        Assert.Equal("""("2" == x.PartitionKey)""", result[1].Body.ToString());
        AssertFilterMatchesEntities(result[0], [CreateEntity("1", "a")], [CreateEntity("2", "a")]);
        AssertFilterMatchesEntities(result[1], [CreateEntity("2", "a")], [CreateEntity("1", "a")]);
    }

    [Fact]
    public void TrySplit_ProxyForm_ConvertExpression_ReturnsTwoFilters()
    {
        // Arrange: Simulates the WhereVisitor output: ((ITableEntity)x).PartitionKey == "1" || ((ITableEntity)x).PartitionKey == "2"
        ParameterExpression param = Expression.Parameter(typeof(TableEntity), "x");
        Expression pkAccess = Expression.Property(
            Expression.Convert(param, typeof(ITableEntity)),
            nameof(ITableEntity.PartitionKey));

        Expression body = Expression.OrElse(
            Expression.Equal(pkAccess, Expression.Constant("1")),
            Expression.Equal(pkAccess, Expression.Constant("2")));

        var filter = Expression.Lambda<Func<TableEntity, bool>>(body, param);

        // Act
        var result = PartitionKeySplitVisitor.TrySplit(filter);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Equal("""(Convert(x, ITableEntity).PartitionKey == "1")""", result[0].Body.ToString());
        Assert.Equal("""(Convert(x, ITableEntity).PartitionKey == "2")""", result[1].Body.ToString());
        AssertFilterMatchesEntities(result[0], [CreateEntity("1", "a")], [CreateEntity("2", "a")]);
        AssertFilterMatchesEntities(result[1], [CreateEntity("2", "a")], [CreateEntity("1", "a")]);
    }

    [Fact]
    public void TrySplit_AndDistribution_ThreePartitionKeys_ReturnsThreeFilters()
    {
        // Arrange: (PK == "1" || PK == "2" || PK == "3") && RK == "a"
        Expression<Func<TableEntity, bool>> filter = x =>
            (x.PartitionKey == "1" || x.PartitionKey == "2" || x.PartitionKey == "3") && x.RowKey == "a";

        // Act
        var result = PartitionKeySplitVisitor.TrySplit(filter);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Count);
        Assert.Equal("""((x.PartitionKey == "1") AndAlso (x.RowKey == "a"))""", result[0].Body.ToString());
        Assert.Equal("""((x.PartitionKey == "2") AndAlso (x.RowKey == "a"))""", result[1].Body.ToString());
        Assert.Equal("""((x.PartitionKey == "3") AndAlso (x.RowKey == "a"))""", result[2].Body.ToString());
        AssertFilterMatchesEntities(result[0], [CreateEntity("1", "a")], [CreateEntity("1", "b"), CreateEntity("2", "a")]);
        AssertFilterMatchesEntities(result[1], [CreateEntity("2", "a")], [CreateEntity("2", "b"), CreateEntity("1", "a")]);
        AssertFilterMatchesEntities(result[2], [CreateEntity("3", "a")], [CreateEntity("3", "b"), CreateEntity("1", "a")]);
    }

    [Fact]
    public void TrySplit_AndDistribution_WithGrouping_ReturnsTwoGroups()
    {
        // Arrange: ((PK == "1" && RK == "a") || (PK == "2" && RK == "b") || (PK == "1" && RK == "c")) && Timestamp != null
        Expression<Func<TableEntity, bool>> filter = x =>
            ((x.PartitionKey == "1" && x.RowKey == "a") ||
             (x.PartitionKey == "2" && x.RowKey == "b") ||
             (x.PartitionKey == "1" && x.RowKey == "c")) &&
            x.Timestamp != null;

        // Act
        var result = PartitionKeySplitVisitor.TrySplit(filter);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Equal("""(((x.PartitionKey == "1") AndAlso ((x.RowKey == "a") OrElse (x.RowKey == "c"))) AndAlso (x.Timestamp != null))""", result[0].Body.ToString());
        Assert.Equal("""(((x.PartitionKey == "2") AndAlso (x.RowKey == "b")) AndAlso (x.Timestamp != null))""", result[1].Body.ToString());

        // Group for PK=="1" should match (1,a) and (1,c) with Timestamp, but not (2,b) or entities without Timestamp
        AssertFilterMatchesEntities(result[0],
            [CreateEntityWithTimestamp("1", "a"), CreateEntityWithTimestamp("1", "c")],
            [CreateEntityWithTimestamp("2", "b"), CreateEntityWithTimestamp("1", "b"), CreateEntity("1", "a")]);

        // Group for PK=="2" should match (2,b) with Timestamp, but not (1,a)/(1,c) or entities without Timestamp
        AssertFilterMatchesEntities(result[1],
            [CreateEntityWithTimestamp("2", "b")],
            [CreateEntityWithTimestamp("1", "a"), CreateEntityWithTimestamp("1", "c"), CreateEntityWithTimestamp("2", "a"), CreateEntity("2", "b")]);
    }

    [Fact]
    public void TrySplit_FourPartitionKeys_TwoGroupingPairs_ReturnsTwoGroups()
    {
        // Arrange: (PK == "1" && RK == "a") || (PK == "2" && RK == "b") || (PK == "1" && RK == "c") || (PK == "2" && RK == "d")
        Expression<Func<TableEntity, bool>> filter = x =>
            (x.PartitionKey == "1" && x.RowKey == "a") ||
            (x.PartitionKey == "2" && x.RowKey == "b") ||
            (x.PartitionKey == "1" && x.RowKey == "c") ||
            (x.PartitionKey == "2" && x.RowKey == "d");

        // Act
        var result = PartitionKeySplitVisitor.TrySplit(filter);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Equal("""((x.PartitionKey == "1") AndAlso ((x.RowKey == "a") OrElse (x.RowKey == "c")))""", result[0].Body.ToString());
        Assert.Equal("""((x.PartitionKey == "2") AndAlso ((x.RowKey == "b") OrElse (x.RowKey == "d")))""", result[1].Body.ToString());

        AssertFilterMatchesEntities(result[0],
            [CreateEntity("1", "a"), CreateEntity("1", "c")],
            [CreateEntity("2", "b"), CreateEntity("2", "d"), CreateEntity("1", "x")]);

        AssertFilterMatchesEntities(result[1],
            [CreateEntity("2", "b"), CreateEntity("2", "d")],
            [CreateEntity("1", "a"), CreateEntity("1", "c"), CreateEntity("2", "x")]);
    }

    [Fact]
    public void TrySplit_GroupedDisjuncts_WithMultipleAndTerms_FactorsCorrectly()
    {
        // Arrange: (PK == "1" && RK == "a" && Timestamp != null) || (PK == "2" && RK == "b") || (PK == "1" && RK == "c" && Timestamp != null)
        Expression<Func<TableEntity, bool>> filter = x =>
            (x.PartitionKey == "1" && x.RowKey == "a" && x.Timestamp != null) ||
            (x.PartitionKey == "2" && x.RowKey == "b") ||
            (x.PartitionKey == "1" && x.RowKey == "c" && x.Timestamp != null);

        // Act
        var result = PartitionKeySplitVisitor.TrySplit(filter);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        // PK=="1" group: factored PK, remainders OR'd
        Assert.Equal("""((x.PartitionKey == "1") AndAlso (((x.RowKey == "a") AndAlso (x.Timestamp != null)) OrElse ((x.RowKey == "c") AndAlso (x.Timestamp != null))))""", result[0].Body.ToString());
        Assert.Equal("""((x.PartitionKey == "2") AndAlso (x.RowKey == "b"))""", result[1].Body.ToString());

        AssertFilterMatchesEntities(result[0],
            [CreateEntityWithTimestamp("1", "a"), CreateEntityWithTimestamp("1", "c")],
            [CreateEntity("1", "a"), CreateEntityWithTimestamp("1", "b"), CreateEntityWithTimestamp("2", "b")]);

        AssertFilterMatchesEntities(result[1],
            [CreateEntity("2", "b")],
            [CreateEntity("1", "a"), CreateEntity("2", "a")]);
    }

    [Fact]
    public void TrySplit_BarePkWithCompoundDisjunct_SimplifiesToPkOnly()
    {
        // Arrange: PK == "1" || (PK == "2" && RK == "b") || (PK == "1" && RK == "c")
        // Group for PK=="1" contains a bare PK equality, so it simplifies to just PK == "1"
        Expression<Func<TableEntity, bool>> filter = x =>
            x.PartitionKey == "1" ||
            (x.PartitionKey == "2" && x.RowKey == "b") ||
            (x.PartitionKey == "1" && x.RowKey == "c");

        // Act
        var result = PartitionKeySplitVisitor.TrySplit(filter);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        // Bare PK absorbs the compound: PK=="1" || (PK=="1" && RK=="c") simplifies to PK=="1"
        Assert.Equal("""(x.PartitionKey == "1")""", result[0].Body.ToString());
        Assert.Equal("""((x.PartitionKey == "2") AndAlso (x.RowKey == "b"))""", result[1].Body.ToString());

        // PK=="1" matches any row key
        AssertFilterMatchesEntities(result[0],
            [CreateEntity("1", "a"), CreateEntity("1", "c"), CreateEntity("1", "z")],
            [CreateEntity("2", "b")]);

        AssertFilterMatchesEntities(result[1],
            [CreateEntity("2", "b")],
            [CreateEntity("1", "a"), CreateEntity("2", "a")]);
    }

    [Fact]
    public void TrySplit_ThreeGroupsWithDifferentSizes_FactorsCorrectly()
    {
        // Arrange: Five disjuncts across three PKs: PK1 has 3, PK2 has 1, PK3 has 1
        Expression<Func<TableEntity, bool>> filter = x =>
            (x.PartitionKey == "1" && x.RowKey == "a") ||
            (x.PartitionKey == "2" && x.RowKey == "b") ||
            (x.PartitionKey == "1" && x.RowKey == "c") ||
            (x.PartitionKey == "3" && x.RowKey == "d") ||
            (x.PartitionKey == "1" && x.RowKey == "e");

        // Act
        var result = PartitionKeySplitVisitor.TrySplit(filter);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Count);
        Assert.Equal("""((x.PartitionKey == "1") AndAlso (((x.RowKey == "a") OrElse (x.RowKey == "c")) OrElse (x.RowKey == "e")))""", result[0].Body.ToString());
        Assert.Equal("""((x.PartitionKey == "2") AndAlso (x.RowKey == "b"))""", result[1].Body.ToString());
        Assert.Equal("""((x.PartitionKey == "3") AndAlso (x.RowKey == "d"))""", result[2].Body.ToString());

        AssertFilterMatchesEntities(result[0],
            [CreateEntity("1", "a"), CreateEntity("1", "c"), CreateEntity("1", "e")],
            [CreateEntity("1", "b"), CreateEntity("2", "b"), CreateEntity("3", "d")]);
    }

    [Fact]
    public void TrySplit_AndDistribution_FourPartitionKeys_TwoGroupingPairs_ReturnsTwoGroups()
    {
        // Arrange: ((PK=="1" && RK=="a") || (PK=="2" && RK=="b") || (PK=="1" && RK=="c") || (PK=="2" && RK=="d")) && Timestamp != null
        Expression<Func<TableEntity, bool>> filter = x =>
            ((x.PartitionKey == "1" && x.RowKey == "a") ||
             (x.PartitionKey == "2" && x.RowKey == "b") ||
             (x.PartitionKey == "1" && x.RowKey == "c") ||
             (x.PartitionKey == "2" && x.RowKey == "d")) &&
            x.Timestamp != null;

        // Act
        var result = PartitionKeySplitVisitor.TrySplit(filter);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Equal("""(((x.PartitionKey == "1") AndAlso ((x.RowKey == "a") OrElse (x.RowKey == "c"))) AndAlso (x.Timestamp != null))""", result[0].Body.ToString());
        Assert.Equal("""(((x.PartitionKey == "2") AndAlso ((x.RowKey == "b") OrElse (x.RowKey == "d"))) AndAlso (x.Timestamp != null))""", result[1].Body.ToString());

        AssertFilterMatchesEntities(result[0],
            [CreateEntityWithTimestamp("1", "a"), CreateEntityWithTimestamp("1", "c")],
            [CreateEntityWithTimestamp("1", "b"), CreateEntityWithTimestamp("2", "b"), CreateEntity("1", "a")]);

        AssertFilterMatchesEntities(result[1],
            [CreateEntityWithTimestamp("2", "b"), CreateEntityWithTimestamp("2", "d")],
            [CreateEntityWithTimestamp("2", "a"), CreateEntityWithTimestamp("1", "a"), CreateEntity("2", "b")]);
    }

    [Fact]
    public void TrySplit_DisjunctsWithTimestampAndRowKey_FactorsCorrectly()
    {
        // Arrange: (PK=="1" && RK=="a" && Timestamp != null) || (PK=="2" && RK=="b" && Timestamp != null) || (PK=="1" && RK=="c" && Timestamp != null)
        // Each disjunct has PK + RK + Timestamp — group for PK=="1" factors out PK, remainder keeps RK + Timestamp
        Expression<Func<TableEntity, bool>> filter = x =>
            (x.PartitionKey == "1" && x.RowKey == "a" && x.Timestamp != null) ||
            (x.PartitionKey == "2" && x.RowKey == "b" && x.Timestamp != null) ||
            (x.PartitionKey == "1" && x.RowKey == "c" && x.Timestamp != null);

        // Act
        var result = PartitionKeySplitVisitor.TrySplit(filter);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Equal("""((x.PartitionKey == "1") AndAlso (((x.RowKey == "a") AndAlso (x.Timestamp != null)) OrElse ((x.RowKey == "c") AndAlso (x.Timestamp != null))))""", result[0].Body.ToString());
        Assert.Equal("""(((x.PartitionKey == "2") AndAlso (x.RowKey == "b")) AndAlso (x.Timestamp != null))""", result[1].Body.ToString());

        AssertFilterMatchesEntities(result[0],
            [CreateEntityWithTimestamp("1", "a"), CreateEntityWithTimestamp("1", "c")],
            [CreateEntity("1", "a"), CreateEntity("1", "c"), CreateEntityWithTimestamp("1", "b"), CreateEntityWithTimestamp("2", "b")]);

        AssertFilterMatchesEntities(result[1],
            [CreateEntityWithTimestamp("2", "b")],
            [CreateEntity("2", "b"), CreateEntityWithTimestamp("2", "a")]);
    }

    [Fact]
    public void TrySplit_DisjunctsWithDifferentNonPkConditions_FactorsCorrectly()
    {
        // Arrange: disjuncts share PK but have heterogeneous remainders
        // (PK=="1" && RK=="a") || (PK=="2" && Timestamp != null) || (PK=="1" && Timestamp != null && RK != "z")
        Expression<Func<TableEntity, bool>> filter = x =>
            (x.PartitionKey == "1" && x.RowKey == "a") ||
            (x.PartitionKey == "2" && x.Timestamp != null) ||
            (x.PartitionKey == "1" && x.Timestamp != null && x.RowKey != "z");

        // Act
        var result = PartitionKeySplitVisitor.TrySplit(filter);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        // PK=="1" group: PK factored, two heterogeneous remainders OR'd
        Assert.Equal("""((x.PartitionKey == "1") AndAlso ((x.RowKey == "a") OrElse ((x.Timestamp != null) AndAlso (x.RowKey != "z"))))""", result[0].Body.ToString());
        Assert.Equal("""((x.PartitionKey == "2") AndAlso (x.Timestamp != null))""", result[1].Body.ToString());

        // PK=="1" matches (1,a) always, and (1,x) when Timestamp set and RK != "z"
        AssertFilterMatchesEntities(result[0],
            [CreateEntity("1", "a"), CreateEntityWithTimestamp("1", "b")],
            [CreateEntity("1", "b"), CreateEntityWithTimestamp("1", "z"), CreateEntityWithTimestamp("2", "a")]);

        AssertFilterMatchesEntities(result[1],
            [CreateEntityWithTimestamp("2", "a")],
            [CreateEntity("2", "a"), CreateEntityWithTimestamp("1", "a")]);
    }

    [Fact]
    public void TrySplit_AndDistribution_MultipleSharedFields_ReturnsCorrectFilters()
    {
        // Arrange: (PK=="1" || PK=="2") && RK != "z" && Timestamp != null
        // Two shared terms distributed to each PK branch
        Expression<Func<TableEntity, bool>> filter = x =>
            (x.PartitionKey == "1" || x.PartitionKey == "2") && x.RowKey != "z" && x.Timestamp != null;

        // Act
        var result = PartitionKeySplitVisitor.TrySplit(filter);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Equal("""((x.PartitionKey == "1") AndAlso ((x.RowKey != "z") AndAlso (x.Timestamp != null)))""", result[0].Body.ToString());
        Assert.Equal("""((x.PartitionKey == "2") AndAlso ((x.RowKey != "z") AndAlso (x.Timestamp != null)))""", result[1].Body.ToString());

        AssertFilterMatchesEntities(result[0],
            [CreateEntityWithTimestamp("1", "a"), CreateEntityWithTimestamp("1", "b")],
            [CreateEntity("1", "a"), CreateEntityWithTimestamp("1", "z"), CreateEntityWithTimestamp("2", "a")]);

        AssertFilterMatchesEntities(result[1],
            [CreateEntityWithTimestamp("2", "a")],
            [CreateEntity("2", "a"), CreateEntityWithTimestamp("2", "z"), CreateEntityWithTimestamp("1", "a")]);
    }

    [Fact]
    public void TrySplit_AndDistribution_GroupingWithMultipleSharedTerms_FactorsCorrectly()
    {
        // Arrange: ((PK=="1" && RK=="a") || (PK=="2" && RK=="b") || (PK=="1" && RK=="c")) && Timestamp != null && RK != "z"
        // Two shared terms AND'd with factored group
        Expression<Func<TableEntity, bool>> filter = x =>
            ((x.PartitionKey == "1" && x.RowKey == "a") ||
             (x.PartitionKey == "2" && x.RowKey == "b") ||
             (x.PartitionKey == "1" && x.RowKey == "c")) &&
            x.Timestamp != null && x.RowKey != "z";

        // Act
        var result = PartitionKeySplitVisitor.TrySplit(filter);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        // PK=="1" factored, with both shared terms appended
        Assert.Equal("""(((x.PartitionKey == "1") AndAlso ((x.RowKey == "a") OrElse (x.RowKey == "c"))) AndAlso ((x.Timestamp != null) AndAlso (x.RowKey != "z")))""", result[0].Body.ToString());
        Assert.Equal("""(((x.PartitionKey == "2") AndAlso (x.RowKey == "b")) AndAlso ((x.Timestamp != null) AndAlso (x.RowKey != "z")))""", result[1].Body.ToString());

        AssertFilterMatchesEntities(result[0],
            [CreateEntityWithTimestamp("1", "a"), CreateEntityWithTimestamp("1", "c")],
            [CreateEntity("1", "a"), CreateEntityWithTimestamp("1", "b"), CreateEntityWithTimestamp("2", "b")]);

        AssertFilterMatchesEntities(result[1],
            [CreateEntityWithTimestamp("2", "b")],
            [CreateEntity("2", "b"), CreateEntityWithTimestamp("2", "a"), CreateEntityWithTimestamp("1", "a")]);
    }

    [Fact]
    public void TrySplit_MultiFieldDisjuncts_UnionMatchesOriginal()
    {
        // Arrange: complex multi-field expression — verify semantic equivalence
        Expression<Func<TableEntity, bool>> filter = x =>
            (x.PartitionKey == "1" && x.RowKey == "a" && x.Timestamp != null) ||
            (x.PartitionKey == "2" && x.Timestamp != null) ||
            (x.PartitionKey == "1" && x.Timestamp != null && x.RowKey != "z");

        TableEntity[] testData =
        [
            CreateEntityWithTimestamp("1", "a"),
            CreateEntity("1", "a"),
            CreateEntityWithTimestamp("1", "b"),
            CreateEntityWithTimestamp("1", "z"),
            CreateEntity("1", "z"),
            CreateEntityWithTimestamp("2", "a"),
            CreateEntity("2", "a"),
            CreateEntityWithTimestamp("3", "a"),
        ];

        var originalMatches = testData.Where(filter.Compile()).ToList();

        // Act
        var result = PartitionKeySplitVisitor.TrySplit(filter);

        // Assert
        Assert.NotNull(result);

        var splitMatches = result
            .SelectMany(f => testData.Where(f.Compile()))
            .Distinct()
            .ToList();

        Assert.Equal(originalMatches.Count, splitMatches.Count);
        Assert.All(originalMatches, entity =>
            Assert.Contains(splitMatches, e =>
                e.PartitionKey == entity.PartitionKey && e.RowKey == entity.RowKey && e.Timestamp == entity.Timestamp));
    }

    [Fact]
    public void TrySplit_AndDistribution_MultipleSharedFields_UnionMatchesOriginal()
    {
        // Arrange: (PK=="1" || PK=="2") && RK != "z" && Timestamp != null
        Expression<Func<TableEntity, bool>> filter = x =>
            (x.PartitionKey == "1" || x.PartitionKey == "2") && x.RowKey != "z" && x.Timestamp != null;

        TableEntity[] testData =
        [
            CreateEntityWithTimestamp("1", "a"),
            CreateEntityWithTimestamp("1", "z"),
            CreateEntity("1", "a"),
            CreateEntityWithTimestamp("2", "b"),
            CreateEntityWithTimestamp("2", "z"),
            CreateEntity("2", "b"),
            CreateEntityWithTimestamp("3", "a"),
        ];

        var originalMatches = testData.Where(filter.Compile()).ToList();

        // Act
        var result = PartitionKeySplitVisitor.TrySplit(filter);

        // Assert
        Assert.NotNull(result);

        var splitMatches = result
            .SelectMany(f => testData.Where(f.Compile()))
            .ToList();

        Assert.Equal(originalMatches.Count, splitMatches.Count);
        Assert.All(originalMatches, entity =>
            Assert.Contains(splitMatches, e =>
                e.PartitionKey == entity.PartitionKey && e.RowKey == entity.RowKey));
    }

    #endregion Splitting Scenarios

    #region No-Split Scenarios

    [Fact]
    public void TrySplit_SinglePartitionKey_ReturnsNull()
    {
        // Arrange: PK == "1"
        Expression<Func<TableEntity, bool>> filter = x => x.PartitionKey == "1";

        // Act
        var result = PartitionKeySplitVisitor.TrySplit(filter);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void TrySplit_SinglePartitionKey_WithRowKey_ReturnsNull()
    {
        // Arrange: PK == "1" && RK == "a"
        Expression<Func<TableEntity, bool>> filter = x => x.PartitionKey == "1" && x.RowKey == "a";

        // Act
        var result = PartitionKeySplitVisitor.TrySplit(filter);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void TrySplit_SinglePartitionKey_OrOnRowKey_ReturnsNull()
    {
        // Arrange: PK == "1" && (RK == "a" || RK == "b")
        Expression<Func<TableEntity, bool>> filter = x =>
            x.PartitionKey == "1" && (x.RowKey == "a" || x.RowKey == "b");

        // Act
        var result = PartitionKeySplitVisitor.TrySplit(filter);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void TrySplit_AllSamePartitionKey_ReturnsNull()
    {
        // Arrange: (PK == "1" && RK == "a") || (PK == "1" && RK == "b")
        Expression<Func<TableEntity, bool>> filter = x =>
            (x.PartitionKey == "1" && x.RowKey == "a") ||
            (x.PartitionKey == "1" && x.RowKey == "b");

        // Act
        var result = PartitionKeySplitVisitor.TrySplit(filter);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void TrySplit_NoPartitionKeyComparisons_ReturnsNull()
    {
        // Arrange: RK == "1" || RK == "2"
        Expression<Func<TableEntity, bool>> filter = x => x.RowKey == "1" || x.RowKey == "2";

        // Act
        var result = PartitionKeySplitVisitor.TrySplit(filter);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void TrySplit_MixedDisjuncts_OneWithoutPartitionKey_ReturnsNull()
    {
        // Arrange: PK == "1" || RK == "2"
        Expression<Func<TableEntity, bool>> filter = x => x.PartitionKey == "1" || x.RowKey == "2";

        // Act
        var result = PartitionKeySplitVisitor.TrySplit(filter);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void TrySplit_NotEqualComparison_ReturnsNull()
    {
        // Arrange: PK != "1"
        Expression<Func<TableEntity, bool>> filter = x => x.PartitionKey != "1";

        // Act
        var result = PartitionKeySplitVisitor.TrySplit(filter);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void TrySplit_CompareToExpression_ReturnsNull()
    {
        // Arrange: PK.CompareTo("1") > 0
        Expression<Func<TableEntity, bool>> filter = x => x.PartitionKey.CompareTo("1") > 0;

        // Act
        var result = PartitionKeySplitVisitor.TrySplit(filter);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void TrySplit_GreaterThanComparison_ReturnsNull()
    {
        // Arrange: string.Compare(PK, "1") > 0 || string.Compare(PK, "2") > 0 — non-equality comparisons
        ParameterExpression param = Expression.Parameter(typeof(TableEntity), "x");
        Expression pkAccess = Expression.Property(param, nameof(ITableEntity.PartitionKey));

        System.Reflection.MethodInfo compareMethod = typeof(string).GetMethod(nameof(string.Compare), [typeof(string), typeof(string)])!;

        Expression body = Expression.OrElse(
            Expression.GreaterThan(Expression.Call(compareMethod, pkAccess, Expression.Constant("1")), Expression.Constant(0)),
            Expression.GreaterThan(Expression.Call(compareMethod, pkAccess, Expression.Constant("2")), Expression.Constant(0)));

        var filter = Expression.Lambda<Func<TableEntity, bool>>(body, param);

        // Act
        var result = PartitionKeySplitVisitor.TrySplit(filter);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void TrySplit_AndDistribution_SinglePartitionKey_ReturnsNull()
    {
        // Arrange: PK == "1" && RK == "a"  — single PK in AND, no OR
        Expression<Func<TableEntity, bool>> filter = x => x.PartitionKey == "1" && x.RowKey == "a";

        // Act
        var result = PartitionKeySplitVisitor.TrySplit(filter);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void TrySplit_AndDistribution_OrWithoutPartitionKey_ReturnsNull()
    {
        // Arrange: (RK == "a" || RK == "b") && PK == "1" — OR has no PK comparisons
        Expression<Func<TableEntity, bool>> filter = x =>
            (x.RowKey == "a" || x.RowKey == "b") && x.PartitionKey == "1";

        // Act
        var result = PartitionKeySplitVisitor.TrySplit(filter);

        // Assert
        Assert.Null(result);
    }

    #endregion No-Split Scenarios

    #region Correctness Verification

    [Fact]
    public void TrySplit_UnionOfSplitFilters_MatchesOriginalFilter()
    {
        // Arrange
        Expression<Func<TableEntity, bool>> filter = x =>
            (x.PartitionKey == "1" && x.RowKey == "a") ||
            (x.PartitionKey == "2" && x.RowKey == "b") ||
            (x.PartitionKey == "1" && x.RowKey == "c");

        TableEntity[] testData =
        [
            CreateEntity("1", "a"),
            CreateEntity("1", "b"),
            CreateEntity("1", "c"),
            CreateEntity("2", "a"),
            CreateEntity("2", "b"),
            CreateEntity("3", "a"),
        ];

        var originalMatches = testData.Where(filter.Compile()).ToList();

        // Act
        var result = PartitionKeySplitVisitor.TrySplit(filter);

        // Assert
        Assert.NotNull(result);

        var splitMatches = result
            .SelectMany(f => testData.Where(f.Compile()))
            .ToList();

        Assert.Equal(originalMatches.Count, splitMatches.Count);
        Assert.All(originalMatches, entity =>
            Assert.Contains(splitMatches, e =>
                e.PartitionKey == entity.PartitionKey && e.RowKey == entity.RowKey));
    }

    [Fact]
    public void TrySplit_AndDistribution_UnionMatchesOriginal()
    {
        // Arrange
        Expression<Func<TableEntity, bool>> filter = x =>
            (x.PartitionKey == "1" || x.PartitionKey == "2") && x.RowKey == "a";

        TableEntity[] testData =
        [
            CreateEntity("1", "a"),
            CreateEntity("1", "b"),
            CreateEntity("2", "a"),
            CreateEntity("2", "b"),
            CreateEntity("3", "a"),
        ];

        var originalMatches = testData.Where(filter.Compile()).ToList();

        // Act
        var result = PartitionKeySplitVisitor.TrySplit(filter);

        // Assert
        Assert.NotNull(result);

        var splitMatches = result
            .SelectMany(f => testData.Where(f.Compile()))
            .ToList();

        Assert.Equal(originalMatches.Count, splitMatches.Count);
        Assert.All(originalMatches, entity =>
            Assert.Contains(splitMatches, e =>
                e.PartitionKey == entity.PartitionKey && e.RowKey == entity.RowKey));
    }

    [Fact]
    public void TrySplit_SplitFiltersDoNotOverlap()
    {
        // Arrange: All different PKs — no duplicates possible
        Expression<Func<TableEntity, bool>> filter = x =>
            x.PartitionKey == "1" || x.PartitionKey == "2" || x.PartitionKey == "3";

        TableEntity[] testData =
        [
            CreateEntity("1", "a"),
            CreateEntity("2", "a"),
            CreateEntity("3", "a"),
            CreateEntity("4", "a"),
        ];

        // Act
        var result = PartitionKeySplitVisitor.TrySplit(filter);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Count);

        // Each filter should match exactly one entity
        foreach (var splitFilter in result)
        {
            var matches = testData.Where(splitFilter.Compile()).ToList();
            Assert.Single(matches);
        }
    }

    [Fact]
    public void TrySplit_AndDistribution_SplitFiltersPreserveSharedCondition()
    {
        // Arrange: (PK == "1" || PK == "2") && RK == "a"
        // Each split filter should include the RK == "a" condition
        Expression<Func<TableEntity, bool>> filter = x =>
            (x.PartitionKey == "1" || x.PartitionKey == "2") && x.RowKey == "a";

        // Act
        var result = PartitionKeySplitVisitor.TrySplit(filter);

        // Assert
        Assert.NotNull(result);

        // PK == "1" && RK == "a" should NOT match PK == "1" && RK == "b"
        AssertFilterMatchesEntities(result[0], [CreateEntity("1", "a")], [CreateEntity("1", "b")]);

        // PK == "2" && RK == "a" should NOT match PK == "2" && RK == "b"
        AssertFilterMatchesEntities(result[1], [CreateEntity("2", "a")], [CreateEntity("2", "b")]);
    }

    [Fact]
    public void TrySplit_GroupedFactoring_UnionMatchesOriginal()
    {
        // Arrange: four disjuncts with two PK groups
        Expression<Func<TableEntity, bool>> filter = x =>
            (x.PartitionKey == "1" && x.RowKey == "a") ||
            (x.PartitionKey == "2" && x.RowKey == "b") ||
            (x.PartitionKey == "1" && x.RowKey == "c") ||
            (x.PartitionKey == "2" && x.RowKey == "d");

        TableEntity[] testData =
        [
            CreateEntity("1", "a"),
            CreateEntity("1", "b"),
            CreateEntity("1", "c"),
            CreateEntity("2", "a"),
            CreateEntity("2", "b"),
            CreateEntity("2", "c"),
            CreateEntity("2", "d"),
            CreateEntity("3", "a"),
        ];

        var originalMatches = testData.Where(filter.Compile()).ToList();

        // Act
        var result = PartitionKeySplitVisitor.TrySplit(filter);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);

        var splitMatches = result
            .SelectMany(f => testData.Where(f.Compile()))
            .ToList();

        Assert.Equal(originalMatches.Count, splitMatches.Count);
        Assert.All(originalMatches, entity =>
            Assert.Contains(splitMatches, e =>
                e.PartitionKey == entity.PartitionKey && e.RowKey == entity.RowKey));
    }

    [Fact]
    public void TrySplit_BarePkAbsorption_UnionMatchesOriginal()
    {
        // Arrange: bare PK || compound same-PK — absorption should preserve semantics
        Expression<Func<TableEntity, bool>> filter = x =>
            x.PartitionKey == "1" ||
            (x.PartitionKey == "2" && x.RowKey == "b") ||
            (x.PartitionKey == "1" && x.RowKey == "c");

        TableEntity[] testData =
        [
            CreateEntity("1", "a"),
            CreateEntity("1", "c"),
            CreateEntity("1", "z"),
            CreateEntity("2", "a"),
            CreateEntity("2", "b"),
            CreateEntity("3", "a"),
        ];

        var originalMatches = testData.Where(filter.Compile()).ToList();

        // Act
        var result = PartitionKeySplitVisitor.TrySplit(filter);

        // Assert
        Assert.NotNull(result);

        var splitMatches = result
            .SelectMany(f => testData.Where(f.Compile()))
            .ToList();

        Assert.Equal(originalMatches.Count, splitMatches.Count);
        Assert.All(originalMatches, entity =>
            Assert.Contains(splitMatches, e =>
                e.PartitionKey == entity.PartitionKey && e.RowKey == entity.RowKey));
    }

    [Fact]
    public void TrySplit_AndDistribution_WithGrouping_UnionMatchesOriginal()
    {
        // Arrange: AND distribution with grouping
        Expression<Func<TableEntity, bool>> filter = x =>
            ((x.PartitionKey == "1" && x.RowKey == "a") ||
             (x.PartitionKey == "2" && x.RowKey == "b") ||
             (x.PartitionKey == "1" && x.RowKey == "c") ||
             (x.PartitionKey == "2" && x.RowKey == "d")) &&
            x.Timestamp != null;

        TableEntity[] testData =
        [
            CreateEntityWithTimestamp("1", "a"),
            CreateEntityWithTimestamp("1", "b"),
            CreateEntityWithTimestamp("1", "c"),
            CreateEntityWithTimestamp("2", "b"),
            CreateEntityWithTimestamp("2", "d"),
            CreateEntityWithTimestamp("2", "x"),
            CreateEntity("1", "a"),
            CreateEntity("2", "b"),
        ];

        var originalMatches = testData.Where(filter.Compile()).ToList();

        // Act
        var result = PartitionKeySplitVisitor.TrySplit(filter);

        // Assert
        Assert.NotNull(result);

        var splitMatches = result
            .SelectMany(f => testData.Where(f.Compile()))
            .ToList();

        Assert.Equal(originalMatches.Count, splitMatches.Count);
        Assert.All(originalMatches, entity =>
            Assert.Contains(splitMatches, e =>
                e.PartitionKey == entity.PartitionKey && e.RowKey == entity.RowKey));
    }

    #endregion Correctness Verification

    #region Helpers

    private static TableEntity CreateEntity(string partitionKey, string rowKey) =>
        new(partitionKey, rowKey);

    private static TableEntity CreateEntityWithTimestamp(string partitionKey, string rowKey) =>
        new(partitionKey, rowKey) { Timestamp = DateTimeOffset.UtcNow };

    private static Expression<Func<TableEntity, bool>> CreateCapturedVariableFilter(string pk1, string pk2) =>
        x => x.PartitionKey == pk1 || x.PartitionKey == pk2;

    private static void AssertFilterMatchesEntities(Expression<Func<TableEntity, bool>> filter, TableEntity[] expectedMatches, TableEntity[] expectedNonMatches)
    {
        Func<TableEntity, bool> compiled = filter.Compile();

        foreach (TableEntity entity in expectedMatches)
        {
            Assert.True(compiled(entity), $"Expected filter to match entity PK={entity.PartitionKey}, RK={entity.RowKey}");
        }

        foreach (TableEntity entity in expectedNonMatches)
        {
            Assert.False(compiled(entity), $"Expected filter NOT to match entity PK={entity.PartitionKey}, RK={entity.RowKey}");
        }
    }

    #endregion Helpers
}
