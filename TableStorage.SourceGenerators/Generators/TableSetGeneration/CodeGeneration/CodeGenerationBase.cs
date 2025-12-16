using System.Text;
using TableStorage.SourceGenerators.Models;

namespace TableStorage.SourceGenerators.Generators.TableSetGeneration.CodeGeneration;

/// <summary>
/// Base class for code generation functionality.
/// Provides common methods and utilities for generating C# code.
/// </summary>
internal static class CodeGenerationBase
{
    /// <summary>
    /// Initializes the model context for code generation.
    /// </summary>
    /// <param name="classToGenerate">The class configuration to generate context for.</param>
    /// <returns>A ModelContext containing relevant generation settings.</returns>
    public static ModelContext InitializeModelContext(ClassToGenerate classToGenerate)
    {
        bool hasChangeTracking = classToGenerate.Members.Any(x => x.WithChangeTracking);
        bool hasPartitionKeyProxy = classToGenerate.TryGetPrettyMember("PartitionKey", out PrettyMemberToGenerate partitionKeyProxy);
        string realPartitionKey = hasPartitionKeyProxy ? partitionKeyProxy.Name : "PartitionKey";
        bool hasRowKeyProxy = classToGenerate.TryGetPrettyMember("RowKey", out PrettyMemberToGenerate rowKeyProxy);
        string realRowKey = hasRowKeyProxy ? rowKeyProxy.Name : "RowKey";

        return new ModelContext(
            hasChangeTracking,
            hasPartitionKeyProxy,
            hasRowKeyProxy,
            partitionKeyProxy,
            rowKeyProxy,
            realPartitionKey,
            realRowKey
        );
    }

    /// <summary>
    /// Generates the namespace opening declaration.
    /// </summary>
    /// <param name="sb">The StringBuilder to append to.</param>
    /// <param name="namespace">The namespace name, or null if no namespace.</param>
    public static void GenerateNamespaceStart(StringBuilder sb, string? @namespace)
    {
        if (!string.IsNullOrEmpty(@namespace))
        {
            sb.Append(@"
namespace ").Append(@namespace).Append(@"
{");
        }
    }

    /// <summary>
    /// Generates the namespace closing declaration.
    /// </summary>
    /// <param name="sb">The StringBuilder to append to.</param>
    /// <param name="namespace">The namespace name, or null if no namespace.</param>
    public static void GenerateNamespaceEnd(StringBuilder sb, string? @namespace)
    {
        if (!string.IsNullOrEmpty(@namespace))
        {
            sb.Append('}');
        }
    }

    /// <summary>
    /// Generates the class signature with appropriate interfaces and attributes.
    /// </summary>
    /// <param name="sb">The StringBuilder to append to.</param>
    /// <param name="classToGenerate">The class configuration.</param>
    /// <param name="context">The model context.</param>
    public static void GenerateClassSignature(StringBuilder sb, ClassToGenerate classToGenerate, in ModelContext context)
    {
        sb.Append(@"
    [global::System.Diagnostics.DebuggerDisplay(@""").Append(classToGenerate.Name).Append(@" \{ {").Append(context.RealPartitionKey).Append("}, {").Append(context.RealRowKey).Append(@"} \}"")]
    partial class ").Append(classToGenerate.Name);

        if (classToGenerate.WithTablesSupport || classToGenerate.WithBlobSupport)
        {
            sb.Append(" : ");
        }

        if (classToGenerate.WithTablesSupport)
        {
            sb.Append(@"global::System.Collections.Generic.IDictionary<string, object>, global::Azure.Data.Tables.ITableEntity");

            if (context.HasChangeTracking)
            {
                sb.Append(", global::TableStorage.IChangeTracking");
            }
        }

        if (classToGenerate.WithBlobSupport)
        {
            if (classToGenerate.WithTablesSupport)
            {
                sb.Append(", ");
            }

            sb.Append("global::TableStorage.IBlobEntity");
        }

        sb.Append(@"
    {
");
    }
}