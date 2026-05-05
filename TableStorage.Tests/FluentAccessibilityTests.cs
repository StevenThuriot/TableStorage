using TableStorage.Tests.Contexts;

namespace TableStorage.Tests;

/// <summary>
/// Verifies that generated FluentExtensions classes have the same accessibility as their originating table context.
/// </summary>
public class FluentAccessibilityTests
{
    [Fact]
    public void GeneratedFluentExtensionClass_ForPublicContext_IsPublic()
    {
        Assert.True(typeof(MyTableContextFluentExtensions).IsPublic);
    }

    [Fact]
    public void GeneratedFluentExtensionClass_ForInternalContext_IsInternal()
    {
        Assert.False(typeof(InternalTableContextFluentExtensions).IsPublic);
    }
}
