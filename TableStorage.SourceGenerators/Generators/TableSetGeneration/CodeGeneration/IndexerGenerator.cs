using System.Text;
using TableStorage.SourceGenerators.Models;

namespace TableStorage.SourceGenerators.Generators.TableSetGeneration.CodeGeneration;

/// <summary>
/// Generates indexer implementation for dictionary access to entity properties.
/// </summary>
internal static class IndexerGenerator
{
    /// <summary>
    /// Generates the complete indexer implementation including getter and setter.
    /// </summary>
    /// <param name="sb">The StringBuilder to append to.</param>
    /// <param name="classToGenerate">The class configuration.</param>
    /// <param name="context">The model context.</param>
    /// <param name="publishAot">Whether AOT publishing is enabled.</param>
    /// <param name="tableStorageSerializerContext">The serializer context for AOT.</param>
    public static void GenerateIndexerImplementation(
        StringBuilder sb,
        ClassToGenerate classToGenerate,
        in ModelContext context,
        bool publishAot,
        string? tableStorageSerializerContext)
    {
        GenerateIndexerGetter(sb, classToGenerate, context);

        if (classToGenerate.WithTablesSupport)
        {
            GenerateIndexerSetter(sb, classToGenerate, context, publishAot, tableStorageSerializerContext);
        }

        sb.AppendLine().Append("        }");
    }

    private static void GenerateIndexerGetter(StringBuilder sb, ClassToGenerate classToGenerate, in ModelContext context)
    {
        sb.Append(@"

        public object this[string key]
        {
            get
            {
                switch (key)
                {
                    case ""PartitionKey"": return ").Append(context.RealPartitionKey).Append(@";
                    case ""RowKey"": return ").Append(context.RealRowKey).Append(';');

        if (classToGenerate.WithTablesSupport)
        {
            sb.Append(@"
                    case ""Timestamp"": return Timestamp;
                    case ""odata.etag"": return ETag.ToString();");
        }

        foreach (MemberToGenerate item in classToGenerate.Members)
        {
            sb.Append(@"
                    case """).Append(item.Name).Append(@""": return ").Append(item.Name).Append(';');
        }

        sb.Append(@"
                    default: return null;
                }
            }");
    }

    private static void GenerateIndexerSetter(
        StringBuilder sb,
        ClassToGenerate classToGenerate,
        in ModelContext context,
        bool publishAot,
        string? tableStorageSerializerContext)
    {
        sb.Append(@"

            set
            {
                switch (key)
                {
                    case ""PartitionKey"": ").Append(context.RealPartitionKey).Append(@" = value?.ToString(); break;
                    case ""RowKey"": ").Append(context.RealRowKey).Append(@" = value?.ToString(); break;
                    case ""Timestamp"": Timestamp = ");

        if (classToGenerate.WithBlobSupport)
        {
            sb.Append("(value is global::System.Text.Json.JsonElement _TimestampJsonElement ? _TimestampJsonElement.GetDateTimeOffset() : (global::System.DateTimeOffset?)value)");
        }
        else
        {
            sb.Append("(global::System.DateTimeOffset?)value");
        }

        sb.Append(@"; break;
                    case ""odata.etag"": ETag = new global::Azure.ETag(value?.ToString()); break;");

        foreach (MemberToGenerate item in classToGenerate.Members)
        {
            sb.Append(@"
                    case """).Append(item.Name).Append(@""": ");

            if (item.WithChangeTracking)
            {
                if (item.IsOverride)
                {
                    sb.Append("base.");
                }
                else
                {
                    sb.Append('_');
                }
            }

            sb.Append(item.Name).Append(" = ");

            ValueConversionGenerator.GenerateValueConversion(sb, item, classToGenerate.WithBlobSupport, publishAot, tableStorageSerializerContext);

            sb.Append("; break;");
        }

        sb.Append(@"
                }
            }");
    }
}