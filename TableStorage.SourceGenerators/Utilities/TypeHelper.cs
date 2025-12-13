using Microsoft.CodeAnalysis;

namespace TableStorage.SourceGenerators.Utilities;

/// <summary>
/// Utility class for type-related operations in source generation.
/// </summary>
internal static class TypeHelper
{
    /// <summary>
    /// Determines the TypeKind for a given type symbol, handling nullable types appropriately.
    /// </summary>
    /// <param name="type">The type symbol to analyze.</param>
    /// <returns>The TypeKind of the underlying type.</returns>
    public static TypeKind GetTypeKind(ITypeSymbol? type) => type switch
    {
        null => TypeKind.Unknown,
        INamedTypeSymbol namedTypeSymbol when type.NullableAnnotation is NullableAnnotation.Annotated ||
                                              namedTypeSymbol.ConstructedFrom.ToDisplayString() is "System.Nullable<T>" =>
            namedTypeSymbol.TypeArguments.Length is not 0
                ? namedTypeSymbol.TypeArguments[0].TypeKind
                : namedTypeSymbol.ConstructedFrom.TypeKind,
        _ => type.TypeKind,
    };
}