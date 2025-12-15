using System.Text;
using TableStorage.SourceGenerators.Models;

namespace TableStorage.SourceGenerators.Generators.TableContextGeneration;

/// <summary>
/// Generates constructor logic for table context classes.
/// </summary>
internal static class ConstructorGenerator
{
    /// <summary>
    /// Generates the private constructor for the table context class.
    /// </summary>
    /// <param name="sb">The StringBuilder to append to.</param>
    /// <param name="classToGenerate">The class configuration.</param>
    /// <param name="hasTables">Whether table support is available.</param>
    /// <param name="hasBlobs">Whether blob support is available.</param>
    public static void GenerateConstructor(
        StringBuilder sb,
        ContextClassToGenerate classToGenerate,
        bool hasTables,
        bool hasBlobs)
    {
        // Generate constructor signature
        sb.Append(@"
        private ").Append(classToGenerate.Name).Append('(');

        GenerateConstructorParameters(sb, hasTables, hasBlobs);

        sb.Append(@")
        {");

        // Generate constructor body
        GenerateConstructorBody(sb, classToGenerate, hasTables, hasBlobs);

        sb.Append(@"
        }");
    }

    private static void GenerateConstructorParameters(StringBuilder sb, bool hasTables, bool hasBlobs)
    {
        if (hasTables)
        {
            sb.Append("TableStorage.ICreator creator");
        }

        if (hasBlobs)
        {
            if (hasTables)
            {
                sb.Append(", ");
            }

            sb.Append("TableStorage.IBlobCreator blobCreator");
        }
    }

    private static void GenerateConstructorBody(
        StringBuilder sb,
        ContextClassToGenerate classToGenerate,
        bool hasTables,
        bool hasBlobs)
    {
        // Assign injected dependencies
        if (hasTables)
        {
            sb.Append(@"
            _creator = creator;");
        }

        if (hasBlobs)
        {
            sb.Append(@"
            _blobCreator = blobCreator;");
        }

        // Initialize all the set properties
        foreach (ContextMemberToGenerate item in classToGenerate.Members)
        {
            sb.Append(@"
            ").Append(item.Name).Append(" = ").Append(item.Type).Append(".Create").Append(item.SetType).Append('(');

            string name = item.Name;
            if (item.SetType is "BlobSet" or "AppendBlobSet")
            {
                sb.Append("blobC");
                name = name.ToLowerInvariant();
            }
            else
            {
                sb.Append('c');
            }

            sb.Append("reator, \"").Append(name).Append("\", TableStorage.ModelInfoProvider.GetInfo);");
        }
    }
}