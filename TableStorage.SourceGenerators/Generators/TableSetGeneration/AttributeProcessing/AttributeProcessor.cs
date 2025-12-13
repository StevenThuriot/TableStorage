using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.RegularExpressions;
using TableStorage.SourceGenerators.Models;
using TableStorage.SourceGenerators.Utilities;

namespace TableStorage.SourceGenerators.Generators.TableSetGeneration.AttributeProcessing;

/// <summary>
/// Handles processing of TableStorage attributes on class declarations.
/// </summary>
internal static class AttributeProcessor
{
    /// <summary>
    /// Extracts relevant TableStorage attributes from a class declaration.
    /// </summary>
    /// <param name="classDeclarationSyntax">The class declaration to analyze.</param>
    /// <param name="semanticModel">The semantic model for symbol resolution.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of relevant attributes with their full names.</returns>
    public static List<(string fullName, AttributeSyntax attributeSyntax)> GetRelevantAttributes(
        ClassDeclarationSyntax classDeclarationSyntax,
        SemanticModel semanticModel,
        CancellationToken ct)
    {
        List<(string fullName, AttributeSyntax attributeSyntax)> relevantSymbols = [];

        foreach (AttributeListSyntax attributeListSyntax in classDeclarationSyntax.AttributeLists)
        {
            foreach (AttributeSyntax attributeSyntax in attributeListSyntax.Attributes)
            {
                ct.ThrowIfCancellationRequested();

                if (semanticModel.GetSymbolInfo(attributeSyntax, cancellationToken: ct).Symbol is not IMethodSymbol attributeSymbol)
                {
                    continue; // weird, we couldn't get the symbol, ignore it
                }

                INamedTypeSymbol attributeContainingTypeSymbol = attributeSymbol.ContainingType;
                string fullName = attributeContainingTypeSymbol.ToDisplayString();

                if (fullName.StartsWith("TableStorage."))
                {
                    relevantSymbols.Add((fullName, attributeSyntax));
                }
            }
        }

        return relevantSymbols;
    }

    /// <summary>
    /// Processes TableSetAttribute arguments to extract partition key, row key, and pretty members.
    /// </summary>
    /// <param name="tablesetAttribute">The TableSetAttribute syntax.</param>
    /// <param name="classSymbol">The class symbol to check for base class properties.</param>
    /// <returns>Tuple containing partition key proxy, row key proxy, and pretty members.</returns>
    public static (string? PartitionKeyProxy, string? RowKeyProxy, List<PrettyMemberToGenerate> PrettyMembers) ProcessTableSetAttributeArguments(
        AttributeSyntax tablesetAttribute,
        INamedTypeSymbol classSymbol)
    {
        List<PrettyMemberToGenerate> prettyMembers = new(2);

        string? partitionKeyProxy = GetArgumentValue(tablesetAttribute, "PartitionKey");
        if (partitionKeyProxy != null)
        {
            bool existsInBase = PropertyExistsInBaseClass(classSymbol, partitionKeyProxy);
            prettyMembers.Add(new(partitionKeyProxy, "PartitionKey", existsInBase));
        }

        string? rowKeyProxy = GetArgumentValue(tablesetAttribute, "RowKey");
        if (rowKeyProxy != null)
        {
            bool existsInBase = PropertyExistsInBaseClass(classSymbol, rowKeyProxy);
            prettyMembers.Add(new(rowKeyProxy, "RowKey", existsInBase));
        }

        return (partitionKeyProxy, rowKeyProxy, prettyMembers);
    }

    /// <summary>
    /// Checks if a property with the given name exists in any base class (not in the current class).
    /// </summary>
    /// <param name="classSymbol">The current class symbol.</param>
    /// <param name="propertyName">The property name to search for.</param>
    /// <returns>True if the property exists in a base class, false otherwise.</returns>
    private static bool PropertyExistsInBaseClass(INamedTypeSymbol classSymbol, string propertyName)
    {
        INamedTypeSymbol? currentBase = classSymbol.BaseType;

        while (currentBase is not null && currentBase.SpecialType is SpecialType.None)
        {
            var property = currentBase.GetMembers(propertyName).OfType<IPropertySymbol>().FirstOrDefault();
            if (property is not null)
            {
                return true;
            }

            currentBase = currentBase.BaseType;
        }

        return false;
    }

    /// <summary>
    /// Processes TableSetPropertyAttribute instances to add generated members.
    /// </summary>
    /// <param name="relevantAttributes">All relevant attributes found on the class.</param>
    /// <param name="semanticModel">The semantic model for type resolution.</param>
    /// <param name="members">The list of members to add to.</param>
    /// <param name="withChangeTracking">Whether change tracking is enabled.</param>
    /// <param name="partitionKeyProxy">The partition key proxy value.</param>
    /// <param name="rowKeyProxy">The row key proxy value.</param>
    /// <param name="ct">Cancellation token.</param>
    public static void ProcessTableSetPropertyAttributes(
        List<(string fullName, AttributeSyntax attributeSyntax)> relevantAttributes,
        SemanticModel semanticModel,
        List<MemberToGenerate> members,
        bool withChangeTracking,
        string partitionKeyProxy,
        string rowKeyProxy,
        CancellationToken ct)
    {
        foreach ((string attrFullName, AttributeSyntax tableSetPropertyAttribute) in relevantAttributes.Where(x => x.fullName is "TableStorage.TableSetPropertyAttribute"))
        {
            ct.ThrowIfCancellationRequested();

            if (tableSetPropertyAttribute.ArgumentList is null || tableSetPropertyAttribute.ArgumentList.Arguments.Count < 2)
            {
                continue;
            }

            var nameSyntax = tableSetPropertyAttribute.ArgumentList.Arguments[1].Expression as LiteralExpressionSyntax;
            string name = nameSyntax?.Token.ValueText ?? string.Empty;
            if (string.IsNullOrEmpty(name))
            {
                continue;
            }

            if (tableSetPropertyAttribute.ArgumentList.Arguments[0].Expression is not TypeOfExpressionSyntax typeOfSyntax)
            {
                continue;
            }

            TypeSyntax typeSyntax = typeOfSyntax.Type;
            TypeInfo typeInfo = semanticModel.GetTypeInfo(typeSyntax, cancellationToken: ct);
            string type = typeInfo.Type?.ToDisplayString() ?? typeSyntax.ToFullString();
            TypeKind typeKind = TypeHelper.GetTypeKind(typeInfo.Type);
            bool tagBlob = GetArgumentValue(tableSetPropertyAttribute, "Tag") is "true";

            members.Add(new MemberToGenerate(
                name: name,
                type: type,
                typeKind: typeKind,
                generateProperty: true, // These are always generated
                partitionKeyProxy: partitionKeyProxy,
                rowKeyProxy: rowKeyProxy,
                withChangeTracking: withChangeTracking,
                isPartial: false, // These are not pre-existing partial properties
                isOverride: false, // These are not overrides, they are new properties
                isNew: false, // These are not hiding base members
                tagBlob: tagBlob
            ));
        }
    }

    /// <summary>
    /// Extracts the value of a named argument from an attribute.
    /// </summary>
    /// <param name="tablesetAttribute">The attribute syntax to search.</param>
    /// <param name="name">The name of the argument to find.</param>
    /// <returns>The argument value, or null if not found.</returns>
    public static string? GetArgumentValue(AttributeSyntax tablesetAttribute, string name)
    {
        string? result = tablesetAttribute.ArgumentList?.Arguments
            .Where(x => x.NameEquals?.Name.NormalizeWhitespace().ToFullString() == name)
            .Select(x => x.Expression.NormalizeWhitespace().ToFullString())
            .FirstOrDefault();

        if (!string.IsNullOrEmpty(result))
        {
            result = Regex.Replace(result, @"nameof\s*\(\s*(?:\w+\.)*([^\s)]+)\s*\)", "$1").Trim('"');
        }

        return result;
    }
}