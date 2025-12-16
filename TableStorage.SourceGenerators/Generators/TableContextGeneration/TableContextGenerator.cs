using System.Text;
using TableStorage.SourceGenerators.Generators.TableSetGeneration.CodeGeneration;
using TableStorage.SourceGenerators.Models;
using TableStorage.SourceGenerators.Utilities;

namespace TableStorage.SourceGenerators.Generators.TableContextGeneration;

/// <summary>
/// Main orchestrator for generating complete table context classes.
/// Coordinates all specialized generators to produce the final code.
/// </summary>
internal static class TableContextGenerator
{
    /// <summary>
    /// Generates table context classes for multiple ContextClassToGenerate configurations.
    /// </summary>
    /// <param name="classesToGenerate">The classes to generate.</param>
    /// <param name="hasTables">Whether table support is available.</param>
    /// <param name="hasBlobs">Whether blob support is available.</param>
    /// <returns>An enumerable of (name, code) pairs for each generated class.</returns>
    public static IEnumerable<(string name, string content)> GenerateTableContextClasses(
        EquatableArray<ContextClassToGenerate> classesToGenerate,
        bool hasTables,
        bool hasBlobs)
    {
        StringBuilder contextBuilder = new();

        foreach (ContextClassToGenerate classToGenerate in classesToGenerate)
        {
            contextBuilder.Clear();

            // Generate file header
            GenerateFileHeader(contextBuilder);

            // Generate the complete context
            GenerateCompleteContext(contextBuilder, classToGenerate, hasTables, hasBlobs);

            yield return (classToGenerate.Namespace + "." + classToGenerate.Name, contextBuilder.ToString());
        }
    }

    private static void GenerateFileHeader(StringBuilder sb)
    {
        sb.Append(Header.Value);
    }

    private static void GenerateCompleteContext(
        StringBuilder sb,
        ContextClassToGenerate classToGenerate,
        bool hasTables,
        bool hasBlobs)
    {
        // Generate namespace and extension class
        CodeGenerationBase.GenerateNamespaceStart(sb, classToGenerate.Namespace);
        ServiceExtensionGenerator.GenerateServiceExtensions(sb, classToGenerate, hasTables, hasBlobs);

        // Generate the main context class
        GenerateContextClass(sb, classToGenerate, hasTables, hasBlobs);

        // Close namespace
        CodeGenerationBase.GenerateNamespaceEnd(sb, classToGenerate.Namespace);
    }

    private static void GenerateContextClass(
        StringBuilder sb,
        ContextClassToGenerate classToGenerate,
        bool hasTables,
        bool hasBlobs)
    {
        // Start partial class
        sb.Append(@"

    partial class ").Append(classToGenerate.Name).Append(@"
    {");

        // Generate private fields
        FieldGenerator.GeneratePrivateFields(sb, hasTables, hasBlobs);

        // Generate helper methods
        HelperMethodGenerator.GenerateHelperMethods(sb, hasTables, hasBlobs);

        // Generate constructor
        ConstructorGenerator.GenerateConstructor(sb, classToGenerate, hasTables, hasBlobs);

        // Generate service registration
        ServiceRegistrationGenerator.GenerateRegistrationMethod(sb, classToGenerate.Name, hasTables, hasBlobs);

        // Close class
        sb.Append(@"
    }");
    }
}