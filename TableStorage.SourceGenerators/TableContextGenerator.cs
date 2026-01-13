using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Text;
using TableStorage.SourceGenerators.Extractors;
using TableStorage.SourceGenerators.Generators.TableContextGeneration;
using TableStorage.SourceGenerators.Models;
using TableStorage.SourceGenerators.Utilities;

namespace TableStorage.SourceGenerators;

/// <summary>
/// Incremental source generator for creating TableContext classes with dependency injection support.
/// This generator processes classes marked with TableContextAttribute and generates the necessary
/// boilerplate code for service registration, dependency injection, and table/blob set management.
/// 
/// Follows the latest best practices for incremental source generators:
/// - Uses ForAttributeWithMetadataName for optimal performance
/// - Employs value-type data models for proper caching
/// - Avoids syntax nodes in the pipeline to enable incremental compilation
/// - Uses lightweight predicates and comprehensive transforms
/// </summary>
[Generator]
public sealed class TableContextGenerator : IIncrementalGenerator
{
    /// <summary>
    /// The fully qualified name of the TableContext attribute to search for.
    /// </summary>
    private const string TableContextAttributeFullName = "TableStorage.TableContextAttribute";

    /// <summary>
    /// Pre-generated source code for TableContext attribute.
    /// This is registered during post-initialization to ensure the attribute is available.
    /// </summary>
    private const string TableContextAttributeSource = Header.Value + @"
namespace TableStorage
{
    /// <summary>
    /// Marks a class as a TableContext that should have source generation applied.
    /// This attribute triggers the generation of dependency injection setup code
    /// and table/blob set management functionality.
    /// </summary>
    [global::Microsoft.CodeAnalysis.EmbeddedAttribute]
    [global::System.AttributeUsage(global::System.AttributeTargets.Class, AllowMultiple = false)]
    public sealed class TableContextAttribute : global::System.Attribute
    {
    }
}";    /// <summary>
       /// Initializes the incremental generator by registering all the necessary providers and transformations.
       /// This follows the best practices for incremental generators:
       /// 1. Registers the attribute first in post-initialization
       /// 2. Uses ForAttributeWithMetadataName for optimal performance  
       /// 3. Separates compilation capabilities extraction for better caching
       /// 4. Uses value-type data models throughout the pipeline
       /// </summary>
       /// <param name="context">The generator initialization context.</param>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Register the attribute source first - this runs once during initialization
        context.RegisterPostInitializationOutput(static ctx =>
        {
            ctx.AddEmbeddedAttributeDefinition();
            ctx.AddSource("TableContextAttribute.g.cs", SourceText.From(TableContextAttributeSource, Encoding.UTF8));
        });

        // Extract compilation capabilities efficiently - runs once per compilation change
        IncrementalValueProvider<CompilationCapabilities> compilationCapabilities = context.CompilationProvider
            .Select(static (compilation, _) => DataExtractor.ExtractCompilationCapabilities(compilation))
            .WithTrackingName("TableContext.CompilationCapabilities");

        // Extract table context class information using ForAttributeWithMetadataName for optimal performance
        // This API was introduced in .NET 7 and provides massive performance improvements
        IncrementalValuesProvider<ContextClassToGenerate> tableContextClasses = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                TableContextAttributeFullName,
                // Lightweight predicate - runs for every decorated node but should be fast
                predicate: static (node, _) => node is ClassDeclarationSyntax,
                // Transform extracts all needed data to avoid syntax nodes in pipeline
                transform: static (ctx, ct) => ExtractTableContextClass(ctx, ct))
            .Where(static info => info.HasValue)
            .Select(static (info, _) => info!.Value)
            .WithTrackingName("TableContext.Classes");

        // Combine data efficiently - collect transforms the values provider into a single value provider
        // This enables batching all classes together for generation while maintaining caching benefits
        IncrementalValueProvider<(EquatableArray<ContextClassToGenerate> Classes, CompilationCapabilities Capabilities)> combinedData =
            tableContextClasses.Collect()
                .Select(static (classes, _) => new EquatableArray<ContextClassToGenerate>([.. classes]))
                .Combine(compilationCapabilities)
                .WithTrackingName("TableContext.CombinedData");

        // Register the main source output - this only runs when combinedData changes
        context.RegisterSourceOutput(combinedData,
            static (spc, source) => ExecuteTableContextGeneration(source.Classes, source.Capabilities, spc));

        // Register fluent extension methods generation - this only runs when tableContextClasses changes
        context.RegisterSourceOutput(
            tableContextClasses.Collect()
                .Select(static (classes, _) => new EquatableArray<ContextClassToGenerate>([.. classes]))
                .WithTrackingName("TableContext.FluentExtensions"),
            static (spc, classes) => ExecuteFluentExtensionsGeneration(classes, spc));
    }    /// <summary>
         /// Extracts table context class information from a GeneratorAttributeSyntaxContext.
         /// This method is designed to extract all necessary data in the transform stage to avoid
         /// keeping syntax nodes in the pipeline, which would break caching.
         /// 
         /// This bridges the new incremental generator approach with the existing extraction logic
         /// while ensuring proper data model extraction for caching.
         /// </summary>
         /// <param name="context">The generator attribute syntax context containing the decorated node.</param>
         /// <param name="cancellationToken">The cancellation token for operation cancellation.</param>
         /// <returns>The extracted context class information, or null if extraction fails.</returns>
    private static ContextClassToGenerate? ExtractTableContextClass(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
    {
        // Validate input - only process class declarations
        if (context.TargetNode is not ClassDeclarationSyntax classDeclaration)
        {
            return null;
        }

        // Use the existing TableContextClassProcessor logic to maintain compatibility
        // This processor extracts all needed information and returns a cacheable data model
        return TableContextClassProcessor.ProcessClassDeclaration(
            context.SemanticModel.Compilation,
            classDeclaration,
            cancellationToken);
    }

    /// <summary>
    /// Main execution method that processes the extracted data and generates source code.
    /// This method only runs when the input data changes, thanks to the incremental generator caching.
    /// 
    /// The method generates source files for each valid table context class found.
    /// </summary>
    /// <param name="classes">The extracted table context class information.</param>
    /// <param name="capabilities">The compilation capabilities (tables/blobs support).</param>
    /// <param name="context">The source production context for adding generated files.</param>
    private static void ExecuteTableContextGeneration(
        EquatableArray<ContextClassToGenerate> classes,
        CompilationCapabilities capabilities,
        SourceProductionContext context)
    {
        // Early exit if no classes to process - avoids unnecessary work
        if (classes.IsEmpty)
        {
            return;
        }

        // Generate source code for each valid class using the existing generator logic
        // This maintains compatibility while benefiting from incremental generation
        foreach ((string name, string result) in Generators.TableContextGeneration.TableContextGenerator.GenerateTableContextClasses(
            classes,
            capabilities.HasTables,
            capabilities.HasBlobs))
        {
            // Add each generated file with a consistent naming scheme
            context.AddSource($"{name}.g.cs", SourceText.From(result, Encoding.UTF8));
        }
    }

    /// <summary>
    /// Generates fluent extension methods for table context classes that have FluentTableEntity properties.
    /// This method creates extension methods like WhereMyEntityA() that delegate to WhereFirstType().
    /// </summary>
    /// <param name="classes">The extracted table context class information.</param>
    /// <param name="context">The source production context for adding generated files.</param>
    private static void ExecuteFluentExtensionsGeneration(
        EquatableArray<ContextClassToGenerate> classes,
        SourceProductionContext context)
    {
        // Early exit if no classes to process
        if (classes.IsEmpty)
        {
            return;
        }

        // Generate fluent extension methods for each class with fluent properties
        foreach ((string name, string result) in Generators.TableContextGeneration.FluentExtensionMethodsGenerator.GenerateFluentExtensions(classes))
        {
            context.AddSource($"{name}.g.cs", SourceText.From(result, Encoding.UTF8));
        }
    }
}