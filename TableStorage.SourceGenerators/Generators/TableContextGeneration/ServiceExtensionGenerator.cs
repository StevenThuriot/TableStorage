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
    public static void GenerateServiceExtensions(StringBuilder sb, ContextClassToGenerate classToGenerate, bool hasTables, bool hasBlobs)
    {
        sb.Append(@"
    /// <summary>
    /// Extension methods for registering <see cref=""global::").Append(classToGenerate.Namespace).Append('.').Append(classToGenerate.Name).Append(@"""/> and its dependencies in the dependency injection container.
    /// </summary>
    public static class ").Append(classToGenerate.Name).Append(@"Extensions
    {");

        GenerateServiceExtensionsWithInit(sb, classToGenerate, hasTables, hasBlobs);
        GenerateServiceExtensionsWithoutInit(sb, classToGenerate, hasTables, hasBlobs);

        sb.Append("    }");
    }

    private static void GenerateServiceExtensionsWithInit(StringBuilder sb, ContextClassToGenerate classToGenerate, bool hasTables, bool hasBlobs)
    {
        sb.Append(@"
        /// <summary>
        /// Registers <see cref=""global::").Append(classToGenerate.Namespace).Append('.').Append(classToGenerate.Name).Append(@"""/> and initializes the required Azure Table Storage and Blob Storage clients
        /// using the provided connection string.
        /// </summary>
        /// <param name=""services"">
        /// The service collection to register services with.
        /// </param>
        /// <param name=""connectionString"">
        /// The storage account connection string used to create the underlying clients.
        /// </param>
        ");

        if (hasTables)
        {
            sb.Append(@"/// <param name=""configure"">
        /// Optional configuration callback for <see cref=""global::TableStorage.TableOptions""/>.
        /// </param>
        ");
        }

        if (hasBlobs)
        {
            sb.Append(@"/// <param name=""configureBlobs"">
        /// Optional configuration callback for <see cref=""global::TableStorage.BlobOptions""/>.
        /// </param>
        ");
        }

        sb.Append(@"/// <returns>
        /// The same <see cref=""global::Microsoft.Extensions.DependencyInjection.IServiceCollection""/> instance
        /// so that additional registrations can be chained.
        /// </returns>
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
");
    }

    private static void GenerateServiceExtensionsWithoutInit(StringBuilder sb, ContextClassToGenerate classToGenerate, bool hasTables, bool hasBlobs)
    {
        sb.Append(@"
        /// <summary>
        /// Registers <see cref=""global::").Append(classToGenerate.Namespace).Append('.').Append(classToGenerate.Name).Append(@"""/> using existing Azure Table Storage and Blob Storage clients
        /// that have already been registered in the dependency injection container.
        /// </summary>
        /// <param name=""services"">
        /// The service collection to register services with.
        /// </param>
        ");

        if (hasTables)
        {
            sb.Append(@"/// <param name=""configure"">
        /// Optional configuration callback for <see cref=""global::TableStorage.TableOptions""/>.
        /// </param>
        ");
        }

        if (hasBlobs)
        {
            sb.Append(@"/// <param name=""configureBlobs"">
        /// Optional configuration callback for <see cref=""global::TableStorage.BlobOptions""/>.
        /// </param>
        ");
        }

        sb.Append(@"/// <returns>
        /// The same <see cref=""global::Microsoft.Extensions.DependencyInjection.IServiceCollection""/> instance
        /// so that additional registrations can be chained.
        /// </returns>
        /// <remarks>
        /// This overload does not create or register storage clients. It assumes the required
        /// Table Storage and Blob Storage clients are already available in the service collection.
        /// </remarks>
        public static global::Microsoft.Extensions.DependencyInjection.IServiceCollection Add").Append(classToGenerate.Name).Append(@"(this global::Microsoft.Extensions.DependencyInjection.IServiceCollection services");

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
            global::").Append(classToGenerate.Namespace).Append('.').Append(classToGenerate.Name).Append(@".Register(services");

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
");
    }
}