using System.Text;
using TableStorage.SourceGenerators.Models;
using TableStorage.SourceGenerators.Utilities;

namespace TableStorage.SourceGenerators.Generators.TableSetGeneration.CodeGeneration;

/// <summary>
/// Generates a static ModelInfoProvider class that provides metadata about generated models.
/// This includes partition key and row key proxy information for each model.
/// </summary>
internal static class ModelInfoProviderGenerator
{
    /// <summary>
    /// Generates the ModelInfoProvider class containing metadata for all generated models.
    /// </summary>
    /// <param name="classesToGenerate">The collection of classes being generated.</param>
    /// <returns>The generated C# code as a string.</returns>
    public static string GenerateModelInfoProvider(EquatableArray<ClassToGenerate> classesToGenerate)
    {
        StringBuilder sb = new();

        // Add file header
        sb.Append(Header.Value).Append(@"
namespace TableStorage
{
    /// <summary>
    /// Provides metadata information about generated TableSet models.
    /// </summary>
    public static class ModelInfoProvider
    {
        private static readonly global::System.Collections.Generic.Dictionary<global::System.Type, global::TableStorage.ModelInfo> s_info = new global::System.Collections.Generic.Dictionary<global::System.Type, global::TableStorage.ModelInfo>(").Append(classesToGenerate.Length).Append(@")
        {
");

        // Generate dictionary entries for each model
        bool first = true;
        foreach (ClassToGenerate classToGenerate in classesToGenerate)
        {
            if (!first)
            {
                sb.AppendLine(",");
            }

            first = false;

            // Get partition key and row key proxy names
            string? partitionKey = GetPartitionKeyProxy(classToGenerate);
            string? rowKey = GetRowKeyProxy(classToGenerate);

            // Build the fully qualified type name
            string fullTypeName = string.IsNullOrEmpty(classToGenerate.Namespace) || classToGenerate.Namespace == "<global namespace>"
                ? classToGenerate.Name
                : $"global::{classToGenerate.Namespace}.{classToGenerate.Name}";

            // Format the key values with proper null handling
            string partitionKeyValue = partitionKey != null ? $"\"{partitionKey}\"" : "null";
            string rowKeyValue = rowKey != null ? $"\"{rowKey}\"" : "null";

            sb.Append($"            {{ typeof({fullTypeName}), new global::TableStorage.ModelInfo({partitionKeyValue}, {rowKeyValue}, typeof({fullTypeName})) }}");
        }

        // Close the dictionary and class
        sb.Append(@"
        };

        /// <summary>
        /// Gets the model information for the specified type.
        /// </summary>
        /// <typeparam name=""T"">The model type.</typeparam>
        /// <returns>The model information if found; otherwise, null.</returns>
        public static global::TableStorage.ModelInfo GetInfo<T>() => GetInfo(typeof(T));

        /// <summary>
        /// Gets the model information for the specified type.
        /// </summary>
        /// <param name=""type"">The model type.</param>
        /// <returns>The model information if found; otherwise, null.</returns>
        public static global::TableStorage.ModelInfo GetInfo(global::System.Type type)
        {
            if (!s_info.TryGetValue(type, out global::TableStorage.ModelInfo info))
            {
                s_info[type] = info = new(null, null, type);
            }

            return info;
        }
    }
}
");

        return sb.ToString();
    }

    /// <summary>
    /// Extracts the partition key proxy name from a class configuration.
    /// </summary>
    /// <param name="classToGenerate">The class configuration.</param>
    /// <returns>The partition key proxy name, or null if not specified.</returns>
    private static string? GetPartitionKeyProxy(ClassToGenerate classToGenerate)
    {
        // Check if there's a pretty member that maps to PartitionKey
        if (classToGenerate.TryGetPrettyMember("PartitionKey", out PrettyMemberToGenerate prettyMember))
        {
            return prettyMember.Name;
        }

        return null;
    }

    /// <summary>
    /// Extracts the row key proxy name from a class configuration.
    /// </summary>
    /// <param name="classToGenerate">The class configuration.</param>
    /// <returns>The row key proxy name, or null if not specified.</returns>
    private static string? GetRowKeyProxy(ClassToGenerate classToGenerate)
    {
        // Check if there's a pretty member that maps to RowKey
        if (classToGenerate.TryGetPrettyMember("RowKey", out PrettyMemberToGenerate prettyMember))
        {
            return prettyMember.Name;
        }

        return null;
    }
}