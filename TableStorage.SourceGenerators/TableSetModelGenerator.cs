using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Text;
using TableStorage.SourceGenerators.Extractors;
using TableStorage.SourceGenerators.Generators.TableSetGeneration;
using TableStorage.SourceGenerators.Generators.TableSetGeneration.CodeGeneration;
using TableStorage.SourceGenerators.Models;
using TableStorage.SourceGenerators.Utilities;

namespace TableStorage.SourceGenerators;

/// <summary>
/// Incremental source generator for creating TableSet model classes with attribute-driven configuration.
/// This generator processes classes marked with TableSetAttribute and generates the necessary
/// boilerplate code for table storage entities, blob support, and change tracking.
/// 
/// Follows the latest best practices for incremental source generators:
/// - Uses ForAttributeWithMetadataName for optimal performance
/// - Employs value-type data models for proper caching
/// - Avoids syntax nodes in the pipeline to enable incremental compilation
/// - Uses lightweight predicates and comprehensive transforms
/// - Separates configuration extraction for better caching
/// </summary>
[Generator]
public sealed class TableSetModelGenerator : IIncrementalGenerator
{
    /// <summary>
    /// The fully qualified name of the TableSet attribute to search for.
    /// </summary>
    private const string TableSetAttributeFullName = "TableStorage.TableSetAttribute";

    /// <summary>
    /// Pre-generated source code for TableStorage attributes.
    /// This includes TableSetAttribute, TableSetPropertyAttribute, and TagAttribute.
    /// </summary>
    private const string TableSetAttributesSource = Header.Value + @"using System;

namespace TableStorage
{
    /// <summary>
    /// Marks a class as a TableSet that should have source generation applied.
    /// This attribute configures partition key, row key, and various features.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public sealed class TableSetAttribute : Attribute
    {
        /// <summary>
        /// Gets or sets the partition key property name or expression.
        /// </summary>
        public string PartitionKey { get; set; }
        
        /// <summary>
        /// Gets or sets the row key property name or expression.
        /// </summary>
        public string RowKey { get; set; }
        
#if TABLESTORAGE
        /// <summary>
        /// Gets or sets a value indicating whether change tracking should be enabled.
        /// </summary>
        public bool TrackChanges { get; set; }
        
        /// <summary>
        /// Gets or sets a value indicating whether table storage should be disabled.
        /// </summary>
        public bool DisableTables { get; set; }
#endif

#if TABLESTORAGE_BLOBS
        /// <summary>
        /// Gets or sets a value indicating whether blob support should be enabled.
        /// </summary>
        public bool SupportBlobs { get; set; }
#endif
    }

    /// <summary>
    /// Configures additional properties for a TableSet class.
    /// Can be applied multiple times to configure different properties.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class TableSetPropertyAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the TableSetPropertyAttribute class.
        /// </summary>
        /// <param name=""type"">The type of the property.</param>
        /// <param name=""name"">The name of the property.</param>
        public TableSetPropertyAttribute(Type type, string name)
        {
        }

#if TABLESTORAGE_BLOBS
        /// <summary>
        /// Gets or sets a value indicating whether this property should be used as a blob tag.
        /// </summary>
        public bool Tag { get; set; }
#endif
    }

#if TABLESTORAGE_BLOBS
    /// <summary>
    /// Marks a property as a blob tag for blob storage operations.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public sealed class TagAttribute : Attribute
    {
    }
#endif
}";    /// <summary>
       /// Initializes the incremental generator by registering all the necessary providers and transformations.
       /// This follows the best practices for incremental generators:
       /// 1. Registers the attributes first in post-initialization
       /// 2. Uses ForAttributeWithMetadataName for optimal performance  
       /// 3. Separates configuration extraction for better caching
       /// 4. Uses value-type data models throughout the pipeline
       /// 5. Employs lightweight predicates with comprehensive transforms
       /// </summary>
       /// <param name="context">The generator initialization context.</param>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Register the attributes source first - this runs once during initialization
        context.RegisterPostInitializationOutput(static ctx =>
            ctx.AddSource("TableSetAttributes.g.cs", SourceText.From(TableSetAttributesSource, Encoding.UTF8)));

        // Extract generation options from analyzer configuration - runs once per config change
        IncrementalValueProvider<GenerationOptions> generationOptions = context.AnalyzerConfigOptionsProvider
            .Select(static (optionsProvider, _) => DataExtractor.ExtractGenerationOptions(optionsProvider))
            .WithTrackingName("TableSet.GenerationOptions");

        // Extract table set class information using ForAttributeWithMetadataName for optimal performance
        // This API was introduced in .NET 7 and provides massive performance improvements
        IncrementalValuesProvider<ClassToGenerate> tableSetClasses = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                TableSetAttributeFullName,
                // Enhanced predicate - checks for class with attribute lists to be more selective
                predicate: static (node, _) => node is ClassDeclarationSyntax classSyntax && classSyntax.AttributeLists.Count > 0,
                // Transform extracts all needed data to avoid syntax nodes in pipeline
                transform: static (context, ct) => ExtractTableSetClass(context, ct))
            .Where(static classInfo => classInfo.HasValue)
            .Select(static (classInfo, _) => classInfo!.Value)
            .WithTrackingName("TableSet.Classes");

        // Combine all providers for final generation efficiently
        // Collect transforms the values provider into a single value provider for batching
        IncrementalValueProvider<(EquatableArray<ClassToGenerate> Classes, GenerationOptions Options)> combinedData =
            tableSetClasses.Collect()
                .Select(static (classes, _) => new EquatableArray<ClassToGenerate>([.. classes]))
                .Combine(generationOptions)
                .WithTrackingName("TableSet.CombinedData");

        // Register the main source output - this only runs when combinedData changes
        context.RegisterSourceOutput(combinedData,
            static (spc, source) => ExecuteTableSetGeneration(
                source.Classes,
                source.Options,
                spc));
    }    /// <summary>
         /// Extracts table set class information from a GeneratorAttributeSyntaxContext.
         /// This method is designed to extract all necessary data in the transform stage to avoid
         /// keeping syntax nodes in the pipeline, which would break caching.
         /// 
         /// This bridges the new incremental generator approach with the existing extraction logic
         /// while ensuring proper data model extraction for caching.
         /// </summary>
         /// <param name="context">The generator attribute syntax context containing the decorated node.</param>
         /// <param name="cancellationToken">The cancellation token for operation cancellation.</param>
         /// <returns>The extracted class information, or null if extraction fails.</returns>
    private static ClassToGenerate? ExtractTableSetClass(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
    {
        // Validate input - only process class declarations
        if (context.TargetNode is not ClassDeclarationSyntax classDeclaration)
        {
            return null;
        }

        // Use the existing ClassProcessor logic but with proper validation
        // This processor extracts all needed information and returns a cacheable data model
        return ClassProcessor.ProcessClassDeclaration(context.SemanticModel.Compilation, classDeclaration, cancellationToken);
    }

    /// <summary>
    /// Main execution method that processes the extracted data and generates source code.
    /// This method only runs when the input data changes, thanks to the incremental generator caching.
    /// 
    /// The method generates source files for each valid table set class found.
    /// </summary>
    /// <param name="classes">The extracted table set class information.</param>
    /// <param name="options">The generation options from configuration.</param>
    /// <param name="context">The source production context for adding generated files.</param>
    private static void ExecuteTableSetGeneration(
        EquatableArray<ClassToGenerate> classes,
        GenerationOptions options,
        SourceProductionContext context)
    {
        // Early exit if no classes to process - avoids unnecessary work
        if (classes.IsEmpty)
        {
            return;
        }

        // Generate source code for each valid class using the existing generator logic
        // This maintains compatibility while benefiting from incremental generation
        foreach ((string name, string modelResult) in ModelGenerator.GenerateTableContextClasses(
            classes,
            options.PublishAot,
            options.TableStorageSerializerContext))
        {
            // Add each generated file with a consistent naming scheme
            context.AddSource($"{name}.g.cs", SourceText.From(modelResult, Encoding.UTF8));
        }

        // Generate the ModelInfoProvider class containing metadata for all models
        string modelInfoProvider = ModelInfoProviderGenerator.GenerateModelInfoProvider(classes);
        context.AddSource("TableStorage.ModelInfoProvider.g.cs", SourceText.From(modelInfoProvider, Encoding.UTF8));
    }
}