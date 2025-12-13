using System.Text;
using TableStorage.SourceGenerators.Models;
using TableStorage.SourceGenerators.Utilities;

namespace TableStorage.SourceGenerators.Generators.TableSetGeneration.CodeGeneration;

/// <summary>
/// Main orchestrator for generating complete table set model classes.
/// Coordinates all specialized generators to produce the final code.
/// </summary>
internal static class ModelGenerator
{
    /// <summary>
    /// Generates a complete table set class for a single ClassToGenerate configuration.
    /// </summary>
    /// <param name="classToGenerate">The class configuration to generate.</param>
    /// <param name="publishAot">Whether AOT publishing is enabled.</param>
    /// <param name="tableStorageSerializerContext">The serializer context for AOT.</param>
    /// <returns>The generated C# code as a string.</returns>
    public static string GenerateSingleTableSetClassString(ClassToGenerate classToGenerate, bool publishAot, string? tableStorageSerializerContext)
    {
        StringBuilder modelBuilder = new();

        // Add file header and using statements
        GenerateFileHeader(modelBuilder, classToGenerate);

        // Generate the complete model
        GenerateModel(modelBuilder, classToGenerate, publishAot, tableStorageSerializerContext);

        return modelBuilder.ToString();
    }

    /// <summary>
    /// Generates table context classes for multiple ClassToGenerate configurations.
    /// </summary>
    /// <param name="classesToGenerate">The classes to generate.</param>
    /// <param name="publishAot">Whether AOT publishing is enabled.</param>
    /// <param name="tableStorageSerializerContext">The serializer context for AOT.</param>
    /// <returns>An enumerable of (name, code) pairs for each generated class.</returns>
    public static IEnumerable<(string name, string result)> GenerateTableContextClasses(
        EquatableArray<ClassToGenerate> classesToGenerate,
        bool publishAot,
        string? tableStorageSerializerContext)
    {
        foreach (ClassToGenerate classToGenerate in classesToGenerate)
        {
            string modelResult = GenerateSingleTableSetClassString(classToGenerate, publishAot, tableStorageSerializerContext);
            string fileName = string.IsNullOrEmpty(classToGenerate.Namespace) || classToGenerate.Namespace is "<global namespace>"
                ? classToGenerate.Name
                : classToGenerate.Namespace + "." + classToGenerate.Name;
            yield return (fileName, modelResult);
        }
    }

    private static void GenerateFileHeader(StringBuilder sb, ClassToGenerate classToGenerate)
    {
        sb.Append(Header.Value).Append(@"using Microsoft.Extensions.DependencyInjection;
using TableStorage;
using System.Collections.Generic;
using System;
");

        if (classToGenerate.Members.Any(m => m.WithChangeTracking))
        {
            sb.AppendLine("using System.Linq;");
        }

        if (classToGenerate.WithBlobSupport)
        {
            sb.AppendLine("using System.Text.Json;");
        }
    }

    private static void GenerateModel(StringBuilder sb, ClassToGenerate classToGenerate, bool publishAot, string? tableStorageSerializerContext)
    {
        var modelContext = CodeGenerationBase.InitializeModelContext(classToGenerate);

        // Generate namespace and class structure
        CodeGenerationBase.GenerateNamespaceStart(sb, classToGenerate.Namespace);
        CodeGenerationBase.GenerateClassSignature(sb, classToGenerate, modelContext);

        // Generate factory methods and support features
        if (classToGenerate.WithTablesSupport)
        {
            FactoryGenerator.GenerateTableSetFactoryMethod(sb, classToGenerate, modelContext);
        }

        if (classToGenerate.WithBlobSupport)
        {
            FactoryGenerator.GenerateBlobSupportMembers(sb, classToGenerate, modelContext);
        }

        // Generate change tracking if enabled
        if (classToGenerate.WithTablesSupport && modelContext.HasChangeTracking)
        {
            ChangeTrackingGenerator.GenerateChangeTrackingSupport(sb, classToGenerate, modelContext);
        }

        // Generate table entity members
        if (classToGenerate.WithTablesSupport)
        {
            TableEntityGenerator.GenerateTableEntityMembers(sb, modelContext);
        }

        // Generate properties and indexer
        PropertyGenerator.GenerateClassProperties(sb, classToGenerate);
        IndexerGenerator.GenerateIndexerImplementation(sb, classToGenerate, modelContext, publishAot, tableStorageSerializerContext);

        // Generate dictionary implementation for table support
        if (classToGenerate.WithTablesSupport)
        {
            DictionaryImplementationGenerator.GenerateDictionaryImplementation(sb, classToGenerate, modelContext);
        }

        // Close class and namespace
        sb.Append(@"
    }
");
        CodeGenerationBase.GenerateNamespaceEnd(sb, classToGenerate.Namespace);
    }
}