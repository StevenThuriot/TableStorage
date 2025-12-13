using Microsoft.CodeAnalysis;
using TableStorage.SourceGenerators.Models;

namespace TableStorage.SourceGenerators.Utilities;

/// <summary>
/// Utility class for validating compilation requirements and dependencies.
/// Follows clean code principles with clear separation of concerns and proper error handling.
/// </summary>
internal static class ValidationHelper
{    /// <summary>
     /// Diagnostic descriptor for missing TableStorage assembly references.
     /// </summary>
    private static readonly DiagnosticDescriptor s_missingTableStorageReference = new(
        id: "TSG001",
        title: "Missing TableStorage Reference",
        messageFormat: "The TableStorage or TableStorage.Blobs assembly reference is required for source generation",
        category: "TableStorage.SourceGenerators",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "At least one of the TableStorage assemblies must be referenced to use TableStorage source generators.",
        helpLinkUri: "https://github.com/LieselThuriot/TableStorage");

    /// <summary>
    /// Validates that the required assemblies are referenced in the compilation.
    /// Uses functional programming patterns for clean error handling.
    /// </summary>
    /// <param name="compilation">The compilation to check.</param>
    /// <returns>A result indicating success or failure with diagnostic information.</returns>
    public static Result<bool> ValidateRequiredAssemblies(Compilation compilation)
    {
        bool hasTableStorage = compilation.ReferencedAssemblyNames.Any(asm => asm.Name is "TableStorage");
        bool hasTableStorageBlobs = compilation.ReferencedAssemblyNames.Any(asm => asm.Name is "TableStorage.Blobs");

        if (!hasTableStorage && !hasTableStorageBlobs)
        {
            var diagnostic = new DiagnosticInfo(s_missingTableStorageReference, (LocationInfo?)null);
            return Result<bool>.Failure(diagnostic);
        }

        return Result<bool>.Success(true);
    }

    /// <summary>
    /// Legacy method for backward compatibility.
    /// Validates that the required assemblies are referenced in the compilation.
    /// </summary>
    /// <param name="compilation">The compilation to check.</param>
    /// <param name="context">The source production context for reporting diagnostics.</param>
    /// <returns>True if all required assemblies are referenced, false otherwise.</returns>
    public static bool AreRequiredAssembliesReferenced(Compilation compilation, SourceProductionContext context)
    {
        var result = ValidateRequiredAssemblies(compilation);

        if (result.HasDiagnostics)
        {
            foreach (var diagnosticInfo in result.Diagnostics)
            {
                context.ReportDiagnostic(diagnosticInfo.CreateDiagnostic());
            }
        }

        return result.IsSuccess;
    }
}