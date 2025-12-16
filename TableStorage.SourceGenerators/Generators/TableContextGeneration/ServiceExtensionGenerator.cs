using System.Text;
using TableStorage.SourceGenerators.Models;

namespace TableStorage.SourceGenerators.Generators.TableContextGeneration;

/// <summary>
/// Generates extension methods for service collection registration.
/// </summary>
internal static class ServiceExtensionGenerator
{
    /// <summary>
    /// Generates the extension class and method for adding the table context to service collection.
    /// </summary>
    /// <param name="sb">The StringBuilder to append to.</param>
    /// <param name="classToGenerate">The class configuration.</param>
    /// <param name="hasTables">Whether table support is available.</param>
    /// <param name="hasBlobs">Whether blob support is available.</param>
    public static void GenerateServiceExtensions(
        StringBuilder sb,
        ContextClassToGenerate classToGenerate,
        bool hasTables,
        bool hasBlobs)
    {
        sb.Append(@"
    public static class ").Append(classToGenerate.Name).Append(@"Extensions
    {
        public static global::Microsoft.Extensions.DependencyInjection.IServiceCollection Add").Append(classToGenerate.Name).Append(@"(this global::Microsoft.Extensions.DependencyInjection.IServiceCollection services, string connectionString");

        // Add optional configuration parameters based on capabilities
        if (hasTables)
        {
            sb.Append(", global::System.Action<global::TableStorage.TableOptions> configure = null");
        }

        if (hasBlobs)
        {
            sb.Append(", global::System.Action<global::TableStorage.BlobOptions> configureBlobs = null");
        }

        sb.Append(@")
        {
            global::").Append(classToGenerate.Namespace).Append('.').Append(classToGenerate.Name).Append(@".Register(services, connectionString");

        // Pass configuration parameters
        if (hasTables)
        {
            sb.Append(", configure");
        }

        if (hasBlobs)
        {
            sb.Append(", configureBlobs");
        }

        sb.Append(@");
            return services;
        }
    }");
    }
}