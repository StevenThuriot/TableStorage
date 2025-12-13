using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using TableStorage.SourceGenerators.Generators.TableSetGeneration.AttributeProcessing;
using TableStorage.SourceGenerators.Models;
using TableStorage.SourceGenerators.Utilities;

namespace TableStorage.SourceGenerators.Generators.TableSetGeneration;

/// <summary>
/// Processes class declarations to extract configuration and generate ClassToGenerate instances.
/// </summary>
internal static class ClassProcessor
{
    /// <summary>
    /// Processes a single class declaration to create a ClassToGenerate instance.
    /// </summary>
    /// <param name="compilation">The compilation context.</param>
    /// <param name="classDeclarationSyntax">The class declaration to process.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A ClassToGenerate instance, or null if processing failed.</returns>
    public static ClassToGenerate? ProcessClassDeclaration(
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

        var relevantAttributes = AttributeProcessor.GetRelevantAttributes(classDeclarationSyntax, semanticModel, ct);
        AttributeSyntax? tableSetAttributeSyntax = relevantAttributes
            .FirstOrDefault(attr => attr.fullName is "TableStorage.TableSetAttribute").attributeSyntax;

        if (tableSetAttributeSyntax is null)
        {
            // This should not happen if ForAttributeWithMetadataName is working correctly,
            // but as a safeguard or if the attribute is malformed.
            return null;
        }

        var (partitionKeyProxy, rowKeyProxy, prettyMembers) = AttributeProcessor.ProcessTableSetAttributeArguments(tableSetAttributeSyntax, classSymbol);

        bool withBlobSupport = AttributeProcessor.GetArgumentValue(tableSetAttributeSyntax, "SupportBlobs") is "true";
        bool withTablesSupport = AttributeProcessor.GetArgumentValue(tableSetAttributeSyntax, "DisableTables") is not "true";
        bool withChangeTracking = withTablesSupport && AttributeProcessor.GetArgumentValue(tableSetAttributeSyntax, "TrackChanges") is "true";

        List<MemberToGenerate> members = MemberProcessor.ProcessClassMembers(
            classSymbol,
            prettyMembers,
            withChangeTracking,
            partitionKeyProxy ?? "null",
            rowKeyProxy ?? "null",
            ct);

        AttributeProcessor.ProcessTableSetPropertyAttributes(
            relevantAttributes,
            semanticModel,
            members,
            withChangeTracking,
            partitionKeyProxy ?? "null",
            rowKeyProxy ?? "null",
            ct);

        return new ClassToGenerate(
            classSymbol.Name,
            classSymbol.ContainingNamespace.ToDisplayString(),
            new EquatableArray<MemberToGenerate>([.. members]),
            new EquatableArray<PrettyMemberToGenerate>([.. prettyMembers]),
            withBlobSupport,
            withTablesSupport);
    }
}