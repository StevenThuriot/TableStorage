using System.Text;
using TableStorage.SourceGenerators.Models;

namespace TableStorage.SourceGenerators.Generators.TableContextGeneration;

/// <summary>
/// Generates service registration methods for dependency injection.
/// </summary>
internal static class ServiceRegistrationGenerator
{
    /// <summary>
    /// Generates the static Register method for dependency injection setup.
    /// </summary>
    /// <param name="sb">The StringBuilder to append to.</param>
    /// <param name="classToGenerate">The class to generate.</param>
    /// <param name="hasTables">Whether table support is available.</param>
    /// <param name="hasBlobs">Whether blob support is available.</param>
    public static void GenerateRegistrationMethod(StringBuilder sb, ContextClassToGenerate classToGenerate, bool hasTables, bool hasBlobs)
    {
        GenerateRegistrationInternal(sb, true, classToGenerate, hasTables, hasBlobs);
        GenerateRegistrationInternal(sb, false, classToGenerate, hasTables, hasBlobs);
    }

    private static void GenerateRegistrationInternal(StringBuilder sb, bool withConnectionString, ContextClassToGenerate classToGenerate, bool hasTables, bool hasBlobs)
    {
        sb.Append(@"

        public static void Register(global::Microsoft.Extensions.DependencyInjection.IServiceCollection services");

        if (withConnectionString)
        {
            sb.Append(", string connectionString");
        }

        // Add configuration parameters
        if (hasTables)
        {
            sb.Append(", global::System.Action<global::TableStorage.TableOptions> configure");
        }

        if (hasBlobs)
        {
            sb.Append(", global::System.Action<global::TableStorage.BlobOptions> configureBlobs");
        }

        sb.Append(@")
        {
            global::Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddSingleton(services, s =>
            {");

        // Generate creator setup
        string paramter = withConnectionString ? "connectionString" : "s";
        GenerateCreatorSetup(sb, paramter, hasTables, hasBlobs);

        // Generate return statement
        sb.Append(@"
                return new global::").Append(classToGenerate.Namespace).Append('.').Append(classToGenerate.Name).Append('(');

        if (hasTables)
        {
            sb.Append("creator");
        }

        if (hasBlobs)
        {
            if (hasTables)
            {
                sb.Append(", ");
            }

            sb.Append("blobCreator");
        }

        sb.Append(@");
            });
        }");
    }

    private static void GenerateCreatorSetup(StringBuilder sb, string parameter, bool hasTables, bool hasBlobs)
    {
        if (hasTables)
        {
            sb.Append(@"
                global::TableStorage.ICreator creator = global::TableStorage.TableStorageSetup.BuildCreator(").Append(parameter).Append(", configure);");
        }

        if (hasBlobs)
        {
            sb.Append(@"
                global::TableStorage.IBlobCreator blobCreator = global::TableStorage.BlobStorageSetup.BuildCreator(").Append(parameter).Append(", configureBlobs);");
        }
    }
}
