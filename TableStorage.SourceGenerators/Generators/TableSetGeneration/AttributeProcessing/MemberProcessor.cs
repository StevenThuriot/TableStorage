using Microsoft.CodeAnalysis;
using TableStorage.SourceGenerators.Models;
using TableStorage.SourceGenerators.Utilities;

namespace TableStorage.SourceGenerators.Generators.TableSetGeneration.AttributeProcessing;

/// <summary>
/// Processes class members to determine which properties should be generated and how.
/// </summary>
internal static class MemberProcessor
{    /// <summary>
     /// Reserved property names that should not be processed as regular members.
     /// </summary>
    private static readonly HashSet<string> s_reservedPropertyNames =
    [
        "PartitionKey",
        "RowKey",
        "Timestamp",
        "ETag",
        "this[]", // Indexer
        "Keys",   // From IDictionary
        "Values", // From IDictionary
        "Count",  // From IDictionary
        "IsReadOnly" // From IDictionary
    ];

    /// <summary>
    /// Processes all members of a class symbol to generate appropriate member configurations.
    /// Includes support for inherited properties from base classes.
    /// </summary>
    /// <param name="classSymbol">The class symbol to process.</param>
    /// <param name="prettyMembers">List of pretty members (proxies) already configured.</param>
    /// <param name="withChangeTracking">Whether change tracking should be enabled.</param>
    /// <param name="partitionKeyForNewMembers">Partition key proxy for new members.</param>
    /// <param name="rowKeyForNewMembers">Row key proxy for new members.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of members to generate.</returns>
    public static List<MemberToGenerate> ProcessClassMembers(
        INamedTypeSymbol classSymbol,
        List<PrettyMemberToGenerate> prettyMembers,
        bool withChangeTracking,
        string partitionKeyForNewMembers,
        string rowKeyForNewMembers,
        CancellationToken ct)
    {
        // Get all properties including inherited ones
        var allProperties = GetAllProperties(classSymbol);
        List<MemberToGenerate> members = new(allProperties.Count);

        foreach (IPropertySymbol property in allProperties)
        {
            ct.ThrowIfCancellationRequested();

            if (property.IsStatic)
            {
                continue;
            }

            if (s_reservedPropertyNames.Contains(property.Name))
            {
                continue;
            }

            ITypeSymbol type = property.Type;
            TypeKind typeKind = TypeHelper.GetTypeKind(type);
            bool tagBlob = property.GetAttributes().Any(x => x.AttributeClass?.ToDisplayString() is "TableStorage.TagAttribute");

            // Check if this is a partial property definition or a virtual property that should be overridden
            bool isPartial = property.IsPartialDefinition;
            bool isVirtualFromBase = property.IsVirtual && !SymbolEqualityComparer.Default.Equals(property.ContainingType, classSymbol);
            bool generate = (isPartial || isVirtualFromBase) && !prettyMembers.Any(x => x.Name == property.Name);

            // Check if the property hides a base member (has 'new' modifier)
            bool isNew = IsPropertyMarkedAsNew(property);

            members.Add(new MemberToGenerate(
                name: property.Name,
                type: type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                typeKind: typeKind,
                generateProperty: generate,
                partitionKeyProxy: partitionKeyForNewMembers, // Proxies are for TableSetPropertyAttribute, not existing properties
                rowKeyProxy: rowKeyForNewMembers,       // Proxies are for TableSetPropertyAttribute, not existing properties
                withChangeTracking: withChangeTracking,
                isPartial: isPartial,
                isOverride: isVirtualFromBase,
                isNew: isNew,
                tagBlob: tagBlob
            ));
        }

        return members;
    }

    /// <summary>
    /// Gets all properties from the class and its base classes, excluding system types.
    /// </summary>
    /// <param name="classSymbol">The class symbol to analyze.</param>
    /// <returns>A list of all properties including inherited ones.</returns>
    private static List<IPropertySymbol> GetAllProperties(INamedTypeSymbol classSymbol)
    {
        List<IPropertySymbol> properties = [];
        HashSet<string> processedPropertyNames = [];

        INamedTypeSymbol? currentType = classSymbol;
        while (currentType is not null && currentType.SpecialType is SpecialType.None)
        {
            foreach (IPropertySymbol property in currentType.GetMembers().OfType<IPropertySymbol>())
            {
                if (processedPropertyNames.Add(property.Name))
                {
                    properties.Add(property);
                }
            }

            currentType = currentType.BaseType;
        }

        properties.Sort((x, y) => string.Compare(x.Name, y.Name, StringComparison.Ordinal));
        return properties;
    }

    /// <summary>
    /// Checks if a property is marked with the 'new' modifier by examining its syntax.
    /// </summary>
    /// <param name="property">The property symbol to check.</param>
    /// <returns>True if the property has the 'new' modifier, false otherwise.</returns>
    private static bool IsPropertyMarkedAsNew(IPropertySymbol property)
    {
        foreach (var syntaxRef in property.DeclaringSyntaxReferences)
        {
            if (syntaxRef.GetSyntax() is Microsoft.CodeAnalysis.CSharp.Syntax.PropertyDeclarationSyntax propertySyntax)
            {
                foreach (var modifier in propertySyntax.Modifiers)
                {
                    if (modifier.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.NewKeyword))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }
}