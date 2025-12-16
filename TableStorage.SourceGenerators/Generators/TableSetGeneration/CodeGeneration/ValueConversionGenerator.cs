using Microsoft.CodeAnalysis;
using System.Text;
using TableStorage.SourceGenerators.Models;

namespace TableStorage.SourceGenerators.Generators.TableSetGeneration.CodeGeneration;

/// <summary>
/// Generates value conversion logic for different data types.
/// </summary>
internal static class ValueConversionGenerator
{
    /// <summary>
    /// Generates appropriate value conversion code based on the member type.
    /// </summary>
    /// <param name="sb">The StringBuilder to append to.</param>
    /// <param name="item">The member to generate conversion for.</param>
    /// <param name="withBlobSupport">Whether blob support is enabled.</param>
    /// <param name="publishAot">Whether AOT publishing is enabled.</param>
    /// <param name="tableStorageSerializerContext">The serializer context for AOT.</param>
    public static void GenerateValueConversion(
        StringBuilder sb,
        MemberToGenerate item,
        bool withBlobSupport,
        bool publishAot,
        string? tableStorageSerializerContext)
    {
        // Begin cast
        sb.Append('(');

        if (item.Type == typeof(DateTime).FullName)
        {
            GenerateDateTimeConversion(sb, item, withBlobSupport, false);
        }
        else if (item.Type == typeof(DateTime).FullName + "?")
        {
            GenerateDateTimeConversion(sb, item, withBlobSupport, true);
        }
        else if (item.TypeKind == TypeKind.Enum)
        {
            GenerateEnumConversion(sb, item, withBlobSupport);
        }
        else
        {
            if (withBlobSupport)
            {
                GenerateBlobJsonConversion(sb, item, publishAot, tableStorageSerializerContext);
            }
            else
            {
                sb.Append(item.Type).Append(") value");
            }
        }
    }

    private static void GenerateDateTimeConversion(StringBuilder sb, MemberToGenerate item, bool withBlobSupport, bool isNullable)
    {
        if (withBlobSupport)
        {
            sb.Append("value is global::System.Text.Json.JsonElement _").Append(item.Name).Append("JsonElement ? _").Append(item.Name).Append("JsonElement.GetDateTimeOffset() : ");

            if (isNullable)
            {
                sb.Append("value as global::System.DateTimeOffset?)?.DateTime");
            }
            else
            {
                sb.Append("(global::System.DateTimeOffset)value).DateTime");
            }
        }
        else
        {
            if (isNullable)
            {
                sb.Append("value as global::System.DateTimeOffset?)?.DateTime");
            }
            else
            {
                sb.Append("(global::System.DateTimeOffset)value).DateTime");
            }
        }
    }

    private static void GenerateEnumConversion(StringBuilder sb, MemberToGenerate item, bool withBlobSupport)
    {
        if (withBlobSupport)
        {
            sb.Append("value is global::System.Text.Json.JsonElement _")
                .Append(item.Name)
                .Append("JsonElement ? (global::System.Enum.TryParse(_")
                .Append(item.Name)
                .Append("JsonElement.ToString(), out ")
                .Append(item.Type.TrimEnd('?'))
                .Append(" _")
                .Append(item.Name)
                .Append("JsonElementParseResult) ? _")
                .Append(item.Name)
                .Append("JsonElementParseResult : default(")
                .Append(item.Type)
                .Append(")) : (");
        }

        sb.Append("value is int _").Append(item.Name).Append("Integer ? (").Append(item.Type).Append(") _").Append(item.Name).Append("Integer : ")
            .Append("global::System.Enum.TryParse(value?.ToString(), out ")
            .Append(item.Type.TrimEnd('?'))
            .Append(" _")
            .Append(item.Name)
            .Append("ParseResult) ? _")
            .Append(item.Name)
            .Append("ParseResult : default(")
            .Append(item.Type)
            .Append("))");

        if (withBlobSupport)
        {
            sb.Append(')');
        }
    }

    private static void GenerateBlobJsonConversion(StringBuilder sb, MemberToGenerate item, bool publishAot, string? tableStorageSerializerContext)
    {
        string? deserializing = GetJsonDeserializationMethod(item.Type);

        if (deserializing is null)
        {
            GenerateComplexTypeConversion(sb, item, publishAot, tableStorageSerializerContext);
        }
        else
        {
            GenerateSimpleTypeConversion(sb, item, deserializing);
        }
    }

    private static string? GetJsonDeserializationMethod(string type)
    {
        return type.ToLowerInvariant().TrimEnd('?') switch
        {
            "string" or "system.string" => "GetString(",
            "int" or "system.int32" => "GetInt32(",
            "long" or "system.int64" => "GetInt64(",
            "double" or "system.double" => "GetDouble(",
            "float" or "system.single" => "GetSingle(",
            "decimal" or "system.decimal" => "GetDecimal(",
            "bool" or "system.boolean" => "GetBoolean(",
            "system.guid" => "GetGuid(",
            "system.datetime" => "GetDateTime(",
            "system.datetimeoffset" => "GetDateTimeOffset(",
            "system.timespan" => "GetTimeSpan(",
            _ => null
        };
    }

    private static void GenerateComplexTypeConversion(StringBuilder sb, MemberToGenerate item, bool publishAot, string? tableStorageSerializerContext)
    {
        sb.Append(item.Type).Append(") ");

        if (!publishAot && string.IsNullOrEmpty(tableStorageSerializerContext))
        {
            sb.Append("( value is global::System.Text.Json.JsonElement _")
                .Append(item.Name)
                .Append("JsonElement ? _")
                .Append(item.Name)
                .Append("JsonElement.Deserialize<")
                .Append(item.Type)
                .Append(">() : ");
        }
        else if (!string.IsNullOrEmpty(tableStorageSerializerContext))
        {
            sb.Append("( value is global::System.Text.Json.JsonElement _")
                .Append(item.Name)
                .Append($"JsonElement ? (")
                .Append(item.Type)
                .Append(") global::System.Text.Json.JsonSerializer.Deserialize(_")
                .Append(item.Name)
                .Append("JsonElement, ")
                .Append(tableStorageSerializerContext)
                .Append(".Default.GetTypeInfo(typeof(")
                .Append(item.Type)
                .Append("))) : ");
        }

        sb.Append(" value");

        if (!string.IsNullOrEmpty(tableStorageSerializerContext) || (!publishAot && string.IsNullOrEmpty(tableStorageSerializerContext)))
        {
            sb.Append(')');
        }
    }

    private static void GenerateSimpleTypeConversion(StringBuilder sb, MemberToGenerate item, string deserializing)
    {
        sb.Append("value is global::System.Text.Json.JsonElement _")
            .Append(item.Name)
            .Append("JsonElement ? _")
            .Append(item.Name)
            .Append("JsonElement.")
            .Append(deserializing)
            .Append(") : (")
            .Append(item.Type)
            .Append(") value)");
    }
}