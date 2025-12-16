using System.Text;

namespace TableStorage.SourceGenerators.Generators.TableContextGeneration;

/// <summary>
/// Generates private fields for dependency injection.
/// </summary>
internal static class FieldGenerator
{
    /// <summary>
    /// Generates private fields for creators based on available capabilities.
    /// </summary>
    /// <param name="sb">The StringBuilder to append to.</param>
    /// <param name="hasTables">Whether table support is available.</param>
    /// <param name="hasBlobs">Whether blob support is available.</param>
    public static void GeneratePrivateFields(StringBuilder sb, bool hasTables, bool hasBlobs)
    {
        if (hasTables)
        {
            sb.Append(@"
        private global::TableStorage.ICreator _creator { get; init; }");
        }

        if (hasBlobs)
        {
            sb.Append(@"
        private global::TableStorage.IBlobCreator _blobCreator { get; init; }");
        }
    }
}