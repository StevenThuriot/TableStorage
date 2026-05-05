using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using TableStorage.SourceGenerators.Models;
using TableStorage.SourceGenerators.Utilities;

namespace TableStorage.SourceGenerators.Generators.TableContextGeneration;

/// <summary>
/// Processes table context class declarations to extract configuration and generate ContextClassToGenerate instances.
/// </summary>
internal static class TableContextClassProcessor
{
    /// <summary>
    /// Processes a single table context class declaration to create a ContextClassToGenerate instance.
    /// </summary>
    /// <param name="compilation">The compilation context.</param>
    /// <param name="classDeclarationSyntax">The class declaration to process.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A ContextClassToGenerate instance, or null if processing failed.</returns>
    public static ContextClassToGenerate? ProcessClassDeclaration(
        Compilation compilation,
        ClassDeclarationSyntax classDeclarationSyntax,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        SemanticModel semanticModel = compilation.GetSemanticModel(classDeclarationSyntax.SyntaxTree);
        if (semanticModel.GetDeclaredSymbol(classDeclarationSyntax, cancellationToken: ct) is not INamedTypeSymbol classSymbol)
        {
            return null; // something went wrong, bail out
        }

        ImmutableArray<ISymbol> classMembers = classSymbol.GetMembers();
        var members = new List<ContextMemberToGenerate>(classMembers.Length);

        // Get all the properties from the class that are TableSet, BlobSet, or AppendBlobSet
        foreach (ISymbol member in classMembers)
        {
            if (member is IPropertySymbol property)
            {
                if (!property.IsStatic && property.Type.Name is "TableSet" or "BlobSet" or "AppendBlobSet")
                {
                    ITypeSymbol tableSetType = ((INamedTypeSymbol)property.Type).TypeArguments[0];
                    members.Add(new(
                        member.Name,
                        tableSetType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                        property.Type.TypeKind,
                        property.Type.Name));
                }
            }
        }

        string accessibility = classSymbol.DeclaredAccessibility == Accessibility.Public ? "public" : "internal";

        return new ContextClassToGenerate(
            classSymbol.Name,
            classSymbol.ContainingNamespace.ToDisplayString(),
            new EquatableArray<ContextMemberToGenerate>([.. members]),
            accessibility);
    }
}