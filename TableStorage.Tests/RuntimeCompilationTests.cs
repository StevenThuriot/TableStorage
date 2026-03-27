#if !PublishAot
using TableStorage.Tests.Infrastructure;
using TableStorage.Tests.Models;

namespace TableStorage.Tests;

/// <summary>
/// Tests for runtime compilation features that require Select/expression compilation
/// These tests are skipped when PublishAot is enabled
/// </summary>
public class RuntimeCompilationTests(AzuriteFixture azuriteFixture) : AzuriteTestBase(azuriteFixture)
{
    [Fact]
    public async Task Select_WithAnonymousType_ShouldProjectProperties()
    {
        // Arrange
        await Context.Models1.UpsertEntityAsync(new()
        {
            PrettyName = "root",
            PrettyRow = Guid.NewGuid().ToString("N"),
            MyProperty1 = 5,
            MyProperty2 = "test value"
        }, TestContext.Current.CancellationToken);

        // Act
        var proxySelectionWorks = await Context.Models1
            .Select(x => new { x.PrettyName, x.PrettyRow })
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.NotEmpty(proxySelectionWorks);
        Assert.All(proxySelectionWorks, x =>
        {
            Assert.NotNull(x.PrettyName);
            Assert.NotNull(x.PrettyRow);
        });
    }

    [Fact]
    public async Task Select_WithAnonymousTypeAndFilter_ShouldProjectSpecificProperties()
    {
        // Arrange
        await Context.Models1.UpsertEntityAsync(new()
        {
            PrettyName = "root",
            PrettyRow = Guid.NewGuid().ToString("N"),
            MyProperty1 = 5,
            MyProperty2 = "test value"
        }, TestContext.Current.CancellationToken);

        // Act
        var firstTransformed1 = await Context.Models1
            .Where(x => x.PrettyName == "root")
            .Where(x => x.MyProperty1 > 2)
            .Select(x => new { x.MyProperty2, x.MyProperty1 })
            .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(firstTransformed1);
        Assert.NotEqual(0, firstTransformed1.MyProperty1);
        Assert.NotNull(firstTransformed1.MyProperty2);
    }

    [Fact]
    public async Task Select_WithSingleProperty_ShouldReturnPrimitiveType()
    {
        // Arrange
        await Context.Models1.UpsertEntityAsync(new()
        {
            PrettyName = "root",
            PrettyRow = Guid.NewGuid().ToString("N"),
            MyProperty1 = 5,
            MyProperty2 = "test value"
        }, TestContext.Current.CancellationToken);

        // Act
        int firstTransformed2 = await Context.Models1
            .Where(x => x.PrettyName == "root")
            .Where(x => x.MyProperty1 > 2)
            .Select(x => x.MyProperty1)
            .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.NotEqual(0, firstTransformed2);
    }

    [Fact]
    public async Task Select_WithCustomRecord_ShouldTransformToRecord()
    {
        // Arrange
        await Context.Models1.UpsertEntityAsync(new()
        {
            PrettyName = "root",
            PrettyRow = Guid.NewGuid().ToString("N"),
            MyProperty1 = 5,
            MyProperty2 = "test value"
        }, TestContext.Current.CancellationToken);

        // Act
        TestTransformAndSelect? firstTransformed3 = await Context.Models1
            .Where(x => x.PrettyName == "root")
            .Where(x => x.MyProperty1 > 2)
            .Select(x => new TestTransformAndSelect(x.MyProperty1, x.MyProperty2))
            .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(firstTransformed3);
        Assert.NotEqual(0, firstTransformed3.prop1);
        Assert.NotNull(firstTransformed3.prop2);
    }

    [Fact]
    public async Task Select_WithTransformations_ShouldApplyTransformations()
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
        TestTransformAndSelect? firstTransformed4 = await Context.Models1
            .Where(x => x.PrettyName == "root")
            .Where(x => x.MyProperty1 > 2)
            .Select(x => new TestTransformAndSelect(x.MyProperty1 + 1, x.MyProperty2 + "_test"))
            .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(firstTransformed4);
        Assert.Equal(6, firstTransformed4.prop1);
        Assert.Equal("test_test", firstTransformed4.prop2);
    }

    [Fact]
    public async Task Select_WithStringConcatenation_ShouldReturnConcatenatedString()
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
        string? firstTransformed5 = await Context.Models1
            .Where(x => x.PrettyName == "root")
            .Where(x => x.MyProperty1 > 2)
            .Select(x => x.MyProperty1 + 1 + x.MyProperty2 + "_test")
            .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.False(string.IsNullOrEmpty(firstTransformed5));
        Assert.Contains("6", firstTransformed5);
        Assert.Contains("test_test", firstTransformed5);
    }

    [Fact]
    public async Task Select_WithStaticMethod_ShouldCallMethod()
    {
        // Arrange
        await Context.Models1.UpsertEntityAsync(new()
        {
            PrettyName = "root",
            PrettyRow = Guid.NewGuid().ToString("N"),
            MyProperty1 = 5,
            MyProperty2 = "test value"
        }, TestContext.Current.CancellationToken);

        // Act
        TestTransformAndSelect? firstTransformed6 = await Context.Models1
            .Where(x => x.PrettyName == "root")
            .Where(x => x.MyProperty1 > 2)
            .Select(x => TestTransformAndSelect.Map(x.MyProperty1, x.MyProperty2))
            .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(firstTransformed6);
        Assert.NotEqual(0, firstTransformed6.prop1);
        Assert.NotNull(firstTransformed6.prop2);
    }

    [Fact]
    public async Task Select_WithExtensionMethod_ShouldCallExtension()
    {
        // Arrange
        await Context.Models1.UpsertEntityAsync(new()
        {
            PrettyName = "root",
            PrettyRow = Guid.NewGuid().ToString("N"),
            MyProperty1 = 5,
            MyProperty2 = "test value"
        }, TestContext.Current.CancellationToken);

        // Act
        TestTransformAndSelect? firstTransformed7 = await Context.Models1
            .Where(x => x.PrettyName == "root")
            .Where(x => x.MyProperty1 > 2)
            .Select(x => x.Map())
            .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(firstTransformed7);
        Assert.NotEqual(0, firstTransformed7.prop1);
        Assert.NotNull(firstTransformed7.prop2);
    }

    [Fact]
    public async Task Select_WithGuidParsing_ShouldParseGuid()
    {
        // Arrange
        string rowKey = Guid.NewGuid().ToString("N");
        await Context.Models1.UpsertEntityAsync(new()
        {
            PrettyName = "root",
            PrettyRow = rowKey,
            MyProperty1 = 5,
            MyProperty2 = "test value"
        }, TestContext.Current.CancellationToken);

        // Act
        TestTransformAndSelectWithGuid? firstTransformed8 = await Context.Models1
            .Where(x => x.PrettyName == "root")
            .Where(x => x.MyProperty1 > 2)
            .Select(x => new TestTransformAndSelectWithGuid(x.MyProperty1, x.MyProperty2, Guid.Parse(x.PrettyRow)))
            .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(firstTransformed8);
        Assert.NotEqual(0, firstTransformed8.prop1);
        Assert.NotNull(firstTransformed8.prop2);
        Assert.Equal(Guid.Parse(rowKey), firstTransformed8.id);
    }

    [Fact]
    public async Task Select_WithConstantValues_ShouldUseConstants()
    {
        // Arrange
        await Context.Models1.UpsertEntityAsync(new()
        {
            PrettyName = "root",
            PrettyRow = Guid.NewGuid().ToString("N"),
            MyProperty1 = 5,
            MyProperty2 = "test value"
        }, TestContext.Current.CancellationToken);

        // Act
        TestTransformAndSelectWithGuid? firstTransformed9 = await Context.Models1
            .Where(x => x.PrettyName == "root")
            .Where(x => x.MyProperty1 > 2)
            .Select(x => new TestTransformAndSelectWithGuid(x.MyProperty1, "test", Guid.NewGuid()))
            .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(firstTransformed9);
        Assert.NotEqual(0, firstTransformed9.prop1);
        Assert.Equal("test", firstTransformed9.prop2);
        Assert.NotEqual(Guid.Empty, firstTransformed9.id);
    }

    [Fact]
    public async Task Select_WithNestedRecord_ShouldCreateNestedStructure()
    {
        // Arrange
        string rowKey = Guid.NewGuid().ToString("N");
        await Context.Models1.UpsertEntityAsync(new()
        {
            PrettyName = "root",
            PrettyRow = rowKey,
            MyProperty1 = 5,
            MyProperty2 = "test"
        }, TestContext.Current.CancellationToken);

        // Act
        NestedTestTransformAndSelect? firstTransformed10 = await Context.Models1
            .Where(x => x.PrettyName == "root")
            .Where(x => x.MyProperty1 > 2)
            .Select(x => new NestedTestTransformAndSelect(Guid.Parse(x.PrettyRow), new(x.MyProperty1 + (1 * 4), x.MyProperty2 + "_test")))
            .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(firstTransformed10);
        Assert.NotEqual(Guid.Empty, firstTransformed10.id);
        Assert.NotNull(firstTransformed10.test);
        Assert.Equal(9, firstTransformed10.test.prop1);
        Assert.Equal("test_test", firstTransformed10.test.prop2);
    }

    [Fact]
    public async Task Select_WithInterpolatedString_ShouldFormatString()
    {
        // Arrange
        string rowKey = Guid.NewGuid().ToString("N");
        await Context.Models1.UpsertEntityAsync(new()
        {
            PrettyName = "root",
            PrettyRow = rowKey,
            MyProperty1 = 5,
            MyProperty2 = "test"
        }, TestContext.Current.CancellationToken);

        // Act
        StringFormatted? firstTransformed11 = await Context.Models1
            .Where(x => x.PrettyName == "root")
            .Where(x => x.MyProperty1 > 2)
            .Select(x => new StringFormatted($"{x.PrettyRow} - {x.MyProperty1 + (1 * 4)}, {x.MyProperty2}_test"))
            .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(firstTransformed11);
        Assert.NotNull(firstTransformed11.value);
        Assert.Contains(rowKey, firstTransformed11.value);
        Assert.Contains("9", firstTransformed11.value);
        Assert.Contains("test_test", firstTransformed11.value);
    }

    [Fact]
    public async Task Select_WithStringFormatAndTimestamp_ShouldIncludeAllProperties()
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
        List<StringFormatted2> firstTransformed12 = await Context.Models1
            .Select(x => new StringFormatted2($"{x.PrettyRow} - {x.MyProperty1 + (1 * 4)}, {x.MyProperty2}_test", null, x.Timestamp.GetValueOrDefault()))
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.NotEmpty(firstTransformed12);
        Assert.All(firstTransformed12, x =>
        {
            Assert.NotNull(x.Value);
            Assert.Null(x.OtherValue);
            Assert.NotEqual(default, x.TimeStamp);
        });
    }

    [Fact]
    public async Task Select_WithStringFormat_ShouldFormatCorrectly()
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
        List<StringFormatted2> firstTransformed13 = await Context.Models1
            .Select(x => new StringFormatted2(string.Format("{0} - {1}, {2}_test {3}", new object[] { x.PrettyRow, x.MyProperty1 + (1 * 4), x.MyProperty2, x.Timestamp.GetValueOrDefault() }), null, x.Timestamp.GetValueOrDefault()))
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.NotEmpty(firstTransformed13);
        Assert.All(firstTransformed13, x =>
        {
            Assert.NotNull(x.Value);
            Assert.Null(x.OtherValue);
            Assert.NotEqual(default, x.TimeStamp);
        });
    }

    [Fact]
    public async Task BatchUpdateAsync_WithExpressionIncrement_ShouldIncrementValue()
    {
        // Arrange
        var mergeTest = new Model
        {
            PrettyName = "root",
            PrettyRow = Guid.NewGuid().ToString("N"),
            MyProperty1 = 5,
            MyProperty2 = "test"
        };
        await Context.Models1.UpsertEntityAsync(mergeTest, TestContext.Current.CancellationToken);

        // Act
        int mergeCount = await Context.Models1
            .Where(x => x.PrettyName == "root" && x.PrettyRow == mergeTest.PrettyRow)
            .BatchUpdateAsync(x => new()
            {
                MyProperty1 = x.MyProperty1 + 1
            }, TestContext.Current.CancellationToken);

        int result = await Context.Models1
            .Where(x => x.PrettyName == "root" && x.PrettyRow == mergeTest.PrettyRow)
            .Select(x => x.MyProperty1)
            .FirstAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, mergeCount);
        Assert.Equal(6, result);
    }

    [Fact]
    public async Task BatchUpdateTransactionAsync_WithExpressionAndRandoms_ShouldUpdateMultipleProperties()
    {
        // Arrange
        var mergeTest = new Model
        {
            PrettyName = "root",
            PrettyRow = Guid.NewGuid().ToString("N"),
            MyProperty1 = 6,
            MyProperty2 = "test",
            MyProperty3 = ModelEnum.Yes
        };
        await Context.Models1.UpsertEntityAsync(mergeTest, TestContext.Current.CancellationToken);

        // Act
        int mergeCount = await Context.Models1
            .Where(x => x.PrettyName == "root" && x.PrettyRow == mergeTest.PrettyRow)
            .BatchUpdateTransactionAsync(x => new()
            {
                MyProperty1 = x.MyProperty1 - 1,
                MyProperty2 = Randoms.String(),
                MyProperty9 = Randoms.From(x.MyProperty3.ToString(), Randoms.String())
            }, TestContext.Current.CancellationToken);

        Model? result = await Context.Models1
            .Where(x => x.PrettyName == "root" && x.PrettyRow == mergeTest.PrettyRow)
            .FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, mergeCount);
        Assert.NotNull(result);
        Assert.Equal(5, result.MyProperty1);
        Assert.NotEqual("test", result.MyProperty2);
        Assert.NotNull(result.MyProperty9);
    }
}
#endif
