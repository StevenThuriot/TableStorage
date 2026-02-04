#if !PublishAot
using TableStorage.Tests.Contexts;
using TableStorage.Tests.Infrastructure;
using TableStorage.Tests.Models;

namespace TableStorage.Tests;

/// <summary>
/// Tests for runtime compilation features with FluentRowKeyModels that require Select/expression compilation
/// These tests are skipped when PublishAot is enabled
/// </summary>
public class FluentRowKeyRuntimeCompilationTests(AzuriteFixture azuriteFixture) : AzuriteTestBase(azuriteFixture)
{
    [Fact]
    public async Task Select_WithAnonymousType_ShouldProjectProperties()
    {
        // Arrange
        await Context.FluentRowKeyModels.UpsertEntityAsync(new FluentTestModelA()
        {
            PrettyPartitionA = Guid.NewGuid().ToString("N"),
            PrettyRowA = "ignored",
            TypeA = "test",
            PropertyA = 5
        });

        // Act
        var proxySelectionWorks = await Context.FluentRowKeyModels
            .WhereFluentTestModelA((x) => x.PropertyA > 0)
            .Select(x => new { x.TypeA, x.PropertyA })
            .ToListAsync();

        // Assert
        Assert.NotEmpty(proxySelectionWorks);
        Assert.All(proxySelectionWorks, x =>
        {
            Assert.NotNull(x.TypeA);
            Assert.NotEqual(0, x.PropertyA);
        });
    }

    [Fact]
    public async Task Select_WithAnonymousTypeAndFilter_ShouldProjectSpecificProperties()
    {
        // Arrange
        await Context.FluentRowKeyModels.UpsertEntityAsync(new FluentTestModelA()
        {
            PrettyPartitionA = Guid.NewGuid().ToString("N"),
            PrettyRowA = "ignored",
            TypeA = "test type",
            PropertyA = 5
        });

        // Act
        var firstTransformed1 = await Context.FluentRowKeyModels
            .WhereFluentTestModelA((x) => x.PropertyA > 2)
            .Select(x => new { x.PropertyA, x.TypeA })
            .FirstOrDefaultAsync();

        // Assert
        Assert.NotNull(firstTransformed1);
        Assert.NotEqual(0, firstTransformed1.PropertyA);
        Assert.NotNull(firstTransformed1.TypeA);
    }

    [Fact]
    public async Task Select_WithSingleProperty_ShouldReturnPrimitiveType()
    {
        // Arrange
        await Context.FluentRowKeyModels.UpsertEntityAsync(new FluentTestModelA()
        {
            PrettyPartitionA = Guid.NewGuid().ToString("N"),
            PrettyRowA = "ignored",
            TypeA = "test type",
            PropertyA = 5
        });

        // Act
        int firstTransformed2 = await Context.FluentRowKeyModels
            .WhereFluentTestModelA((x) => x.PropertyA > 2)
            .Select(x => x.PropertyA)
            .FirstOrDefaultAsync();

        // Assert
        Assert.NotEqual(0, firstTransformed2);
    }

    [Fact]
    public async Task Select_WithCustomRecord_ShouldTransformToRecord()
    {
        // Arrange
        await Context.FluentRowKeyModels.UpsertEntityAsync(new FluentTestModelA()
        {
            PrettyPartitionA = Guid.NewGuid().ToString("N"),
            PrettyRowA = "ignored",
            TypeA = "test type",
            PropertyA = 5
        });

        // Act
        TestTransformAndSelect? firstTransformed3 = await Context.FluentRowKeyModels
            .WhereFluentTestModelA((x) => x.PropertyA > 2)
            .Select(x => new TestTransformAndSelect(x.PropertyA, x.TypeA))
            .FirstOrDefaultAsync();

        // Assert
        Assert.NotNull(firstTransformed3);
        Assert.NotEqual(0, firstTransformed3.prop1);
        Assert.NotNull(firstTransformed3.prop2);
    }

    [Fact]
    public async Task Select_WithTransformations_ShouldApplyTransformations()
    {
        // Arrange
        await Context.FluentRowKeyModels.UpsertEntityAsync(new FluentTestModelA()
        {
            PrettyPartitionA = Guid.NewGuid().ToString("N"),
            PrettyRowA = "ignored",
            TypeA = "test",
            PropertyA = 5
        });

        // Act
        TestTransformAndSelect? firstTransformed4 = await Context.FluentRowKeyModels
            .WhereFluentTestModelA((x) => x.PropertyA > 2)
            .Select(x => new TestTransformAndSelect(x.PropertyA + 1, x.TypeA + "_test"))
            .FirstOrDefaultAsync();

        // Assert
        Assert.NotNull(firstTransformed4);
        Assert.Equal(6, firstTransformed4.prop1);
        Assert.Equal("test_test", firstTransformed4.prop2);
    }

    [Fact]
    public async Task Select_WithStringConcatenation_ShouldReturnConcatenatedString()
    {
        // Arrange
        await Context.FluentRowKeyModels.UpsertEntityAsync(new FluentTestModelA()
        {
            PrettyPartitionA = Guid.NewGuid().ToString("N"),
            PrettyRowA = "ignored",
            TypeA = "test",
            PropertyA = 5
        });

        // Act
        string? firstTransformed5 = await Context.FluentRowKeyModels
            .WhereFluentTestModelA((x) => x.PropertyA > 2)
            .Select(x => x.PropertyA + 1 + x.TypeA + "_test")
            .FirstOrDefaultAsync();

        // Assert
        Assert.False(string.IsNullOrEmpty(firstTransformed5));
        Assert.Contains("6", firstTransformed5);
        Assert.Contains("test_test", firstTransformed5);
    }

    [Fact]
    public async Task Select_WithStaticMethod_ShouldCallMethod()
    {
        // Arrange
        await Context.FluentRowKeyModels.UpsertEntityAsync(new FluentTestModelA()
        {
            PrettyPartitionA = Guid.NewGuid().ToString("N"),
            PrettyRowA = "ignored",
            TypeA = "test type",
            PropertyA = 5
        });

        // Act
        TestTransformAndSelect? firstTransformed6 = await Context.FluentRowKeyModels
            .WhereFluentTestModelA((x) => x.PropertyA > 2)
            .Select(x => TestTransformAndSelect.Map(x.PropertyA, x.TypeA))
            .FirstOrDefaultAsync();

        // Assert
        Assert.NotNull(firstTransformed6);
        Assert.NotEqual(0, firstTransformed6.prop1);
        Assert.NotNull(firstTransformed6.prop2);
    }

    [Fact]
    public async Task Select_WithExtensionMethod_ShouldCallExtension()
    {
        // Arrange
        await Context.FluentRowKeyModels.UpsertEntityAsync(new FluentTestModelA()
        {
            PrettyPartitionA = Guid.NewGuid().ToString("N"),
            PrettyRowA = "ignored",
            TypeA = "test type",
            PropertyA = 5
        });

        // Act
        TestTransformAndSelect? firstTransformed7 = await Context.FluentRowKeyModels
            .WhereFluentTestModelA((x) => x.PropertyA > 2)
            .Select(x => x.Map())
            .FirstOrDefaultAsync();

        // Assert
        Assert.NotNull(firstTransformed7);
        Assert.NotEqual(0, firstTransformed7.prop1);
        Assert.NotNull(firstTransformed7.prop2);
    }

    [Fact]
    public async Task Select_WithGuidParsing_ShouldParseGuid()
    {
        // Arrange
        string partitionKey = Guid.NewGuid().ToString("N");
        await Context.FluentRowKeyModels.UpsertEntityAsync(new FluentTestModelA()
        {
            PrettyPartitionA = partitionKey,
            PrettyRowA = "ignored",
            TypeA = "test type",
            PropertyA = 5
        });

        // Act
        TestTransformAndSelectWithGuid? firstTransformed8 = await Context.FluentRowKeyModels
            .WhereFluentTestModelA((x) => x.PropertyA > 2)
            .Select(x => new TestTransformAndSelectWithGuid(x.PropertyA, x.TypeA, Guid.Parse(x.PrettyPartitionA)))
            .FirstOrDefaultAsync();

        // Assert
        Assert.NotNull(firstTransformed8);
        Assert.NotEqual(0, firstTransformed8.prop1);
        Assert.NotNull(firstTransformed8.prop2);
        Assert.Equal(Guid.Parse(partitionKey), firstTransformed8.id);
    }

    [Fact]
    public async Task Select_WithConstantValues_ShouldUseConstants()
    {
        // Arrange
        await Context.FluentRowKeyModels.UpsertEntityAsync(new FluentTestModelA()
        {
            PrettyPartitionA = Guid.NewGuid().ToString("N"),
            PrettyRowA = "ignored",
            TypeA = "test type",
            PropertyA = 5
        });

        // Act
        TestTransformAndSelectWithGuid? firstTransformed9 = await Context.FluentRowKeyModels
            .WhereFluentTestModelA((x) => x.PropertyA > 2)
            .Select(x => new TestTransformAndSelectWithGuid(x.PropertyA, "test", Guid.NewGuid()))
            .FirstOrDefaultAsync();

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
        string partitionKey = Guid.NewGuid().ToString("N");
        await Context.FluentRowKeyModels.UpsertEntityAsync(new FluentTestModelA()
        {
            PrettyPartitionA = partitionKey,
            PrettyRowA = "ignored",
            TypeA = "test",
            PropertyA = 5
        });

        // Act
        NestedTestTransformAndSelect? firstTransformed10 = await Context.FluentRowKeyModels
            .WhereFluentTestModelA((x) => x.PropertyA > 2)
            .Select(x => new NestedTestTransformAndSelect(Guid.Parse(x.PrettyPartitionA), new(x.PropertyA + (1 * 4), x.TypeA + "_test")))
            .FirstOrDefaultAsync();

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
        string partitionKey = Guid.NewGuid().ToString("N");
        await Context.FluentRowKeyModels.UpsertEntityAsync(new FluentTestModelA()
        {
            PrettyPartitionA = partitionKey,
            PrettyRowA = "ignored",
            TypeA = "test",
            PropertyA = 5
        });

        // Act
        StringFormatted? firstTransformed11 = await Context.FluentRowKeyModels
            .WhereFluentTestModelA((x) => x.PropertyA > 2)
            .Select(x => new StringFormatted($"{x.PrettyPartitionA} - {x.PropertyA + (1 * 4)}, {x.TypeA}_test"))
            .FirstOrDefaultAsync();

        // Assert
        Assert.NotNull(firstTransformed11);
        Assert.NotNull(firstTransformed11.value);
        Assert.Contains(partitionKey, firstTransformed11.value);
        Assert.Contains("9", firstTransformed11.value);
        Assert.Contains("test_test", firstTransformed11.value);
    }

    [Fact]
    public async Task Select_WithStringFormatAndTimestamp_ShouldIncludeAllProperties()
    {
        // Arrange
        await Context.FluentRowKeyModels.UpsertEntityAsync(new FluentTestModelA()
        {
            PrettyPartitionA = Guid.NewGuid().ToString("N"),
            PrettyRowA = "ignored",
            TypeA = "test",
            PropertyA = 5
        });

        // Act
        List<StringFormatted2> firstTransformed12 = await Context.FluentRowKeyModels
            .WhereFluentTestModelA((x) => x.PropertyA > 0)
            .Select(x => new StringFormatted2($"{x.PrettyPartitionA} - {x.PropertyA + (1 * 4)}, {x.TypeA}_test", null, x.Timestamp.GetValueOrDefault()))
            .ToListAsync();

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
        await Context.FluentRowKeyModels.UpsertEntityAsync(new FluentTestModelA()
        {
            PrettyPartitionA = Guid.NewGuid().ToString("N"),
            PrettyRowA = "ignored",
            TypeA = "test",
            PropertyA = 5
        });

        // Act
        List<StringFormatted2> firstTransformed13 = await Context.FluentRowKeyModels
            .WhereFluentTestModelA((x) => x.PropertyA > 0)
            .Select(x => new StringFormatted2(string.Format("{0} - {1}, {2}_test {3}", new object[] { x.PrettyPartitionA, x.PropertyA + (1 * 4), x.TypeA, x.Timestamp.GetValueOrDefault() }), null, x.Timestamp.GetValueOrDefault()))
            .ToListAsync();

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
        var fluentModel = new FluentTestModelA
        {
            PrettyPartitionA = Guid.NewGuid().ToString("N"),
            PrettyRowA = "ignored",
            TypeA = "test",
            PropertyA = 5
        };
        await Context.FluentRowKeyModels.UpsertEntityAsync(fluentModel);

        // Act
        int mergeCount = await Context.FluentRowKeyModels
            .WhereFluentTestModelA((x) => x.PrettyPartitionA == fluentModel.PrettyPartitionA)
            .BatchUpdateAsync(x => new()
            {
                PropertyA = x.PropertyA + 1
            });

        int result = await Context.FluentRowKeyModels
            .WhereFluentTestModelA((x) => x.PrettyPartitionA == fluentModel.PrettyPartitionA)
            .Select(x => x.PropertyA)
            .FirstAsync();

        // Assert
        Assert.Equal(1, mergeCount);
        Assert.Equal(6, result);
    }

    [Fact]
    public async Task UpdateAsync_ShouldUpdateEntity()
    {
        // Arrange
        var model = new FluentTestModelA
        {
            PrettyPartitionA = Guid.NewGuid().ToString("N"),
            TypeA = "before",
            PropertyA = 1
        };
        await Context.FluentRowKeyModels.UpsertEntityAsync(model);

        // Act
        await Context.FluentRowKeyModels.UpdateAsync(() => new FluentTestModelA
        {
            PrettyPartitionA = model.PrettyPartitionA,
            TypeA = "after",
            PropertyA = 2
        });

        // Assert
        var updated = await Context.FluentRowKeyModels
            .WhereFluentTestModelA(x => x.PrettyPartitionA == model.PrettyPartitionA)
            .FirstOrDefaultAsync();
        Assert.NotNull(updated);
        Assert.Equal("after", updated.TypeA);
        Assert.Equal(2, updated.PropertyA);
    }

    [Fact]
    public async Task UpsertAsync_ShouldInsertOrUpdateEntity()
    {
        // Arrange
        var model = new FluentTestModelA
        {
            PrettyPartitionA = Guid.NewGuid().ToString("N"),
            TypeA = "inserted",
            PropertyA = 10
        };

        // Act
        await Context.FluentRowKeyModels.UpsertAsync(() => new FluentTestModelA
        {
            PrettyPartitionA = model.PrettyPartitionA,
            TypeA = model.TypeA,
            PropertyA = model.PropertyA
        });

        // Assert
        var upserted = await Context.FluentRowKeyModels
            .WhereFluentTestModelA(x => x.PrettyPartitionA == model.PrettyPartitionA)
            .FirstOrDefaultAsync();
        Assert.NotNull(upserted);
        Assert.Equal("inserted", upserted.TypeA);
        Assert.Equal(10, upserted.PropertyA);

        // Act - update
        await Context.FluentRowKeyModels.UpsertAsync(() => new FluentTestModelA
        {
            PrettyPartitionA = model.PrettyPartitionA,
            TypeA = "updated",
            PropertyA = 20
        });

        // Assert update
        var updated = await Context.FluentRowKeyModels
            .WhereFluentTestModelA(x => x.PrettyPartitionA == model.PrettyPartitionA)
            .FirstOrDefaultAsync();
        Assert.NotNull(updated);
        Assert.Equal("updated", updated.TypeA);
        Assert.Equal(20, updated.PropertyA);
    }
}
#endif