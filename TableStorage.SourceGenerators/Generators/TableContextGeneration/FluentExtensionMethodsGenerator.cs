using System.Text;
using TableStorage.SourceGenerators.Models;
using TableStorage.SourceGenerators.Utilities;

namespace TableStorage.SourceGenerators.Generators.TableContextGeneration;

/// <summary>
/// Generates extension methods for FluentTableEntity properties in data contexts.
/// Creates methods like WhereMyEntityA() that delegate to WhereFirstType(), WhereSecondType(), etc.
/// </summary>
internal static class FluentExtensionMethodsGenerator
{
    private static readonly string[] s_ordinalNames =
    [
        "First", "Second", "Third", "Fourth", "Fifth", "Sixth", "Seventh", "Eighth",
        "Ninth", "Tenth", "Eleventh", "Twelfth", "Thirteenth", "Fourteenth", "Fifteenth", "Sixteenth"
    ];

    /// <summary>
    /// Generates fluent extension methods for table context classes that have FluentTableEntity properties.
    /// </summary>
    /// <param name="classesToGenerate">The classes to generate extensions for.</param>
    /// <returns>An enumerable of (name, code) pairs for each generated extension class.</returns>
    public static IEnumerable<(string name, string content)> GenerateFluentExtensions(
        EquatableArray<ContextClassToGenerate> classesToGenerate)
    {
        foreach (ContextClassToGenerate classToGenerate in classesToGenerate)
        {
            // Filter to only members that are fluent types
            var fluentMembers = classToGenerate.Members.Where(m => m.IsFluentType).ToList();

            if (fluentMembers.Count == 0)
            {
                continue;
            }

            StringBuilder sb = new();

            // Generate file header
            sb.Append(Header.Value);

            // Add necessary using directives
            sb.Append(@"
using System.Linq.Expressions;
using TableStorage.Linq;
");

            // Generate namespace
            sb.Append(@"

namespace ").Append(classToGenerate.Namespace).Append(@"
{");

            // Generate extension class
            GenerateExtensionClass(sb, classToGenerate, fluentMembers);

            // Close namespace
            sb.Append(@"
}
");

            yield return ($"{classToGenerate.Namespace}.{classToGenerate.Name}.FluentExtensions", sb.ToString());
        }
    }

    private static void GenerateExtensionClass(
        StringBuilder sb,
        ContextClassToGenerate classToGenerate,
        List<ContextMemberToGenerate> fluentMembers)
    {
        sb.Append(@"
    /// <summary>
    /// Extension methods for fluent type properties in ").Append(classToGenerate.Name).Append(@".
    /// </summary>
    public static partial class ").Append(classToGenerate.Name).Append(@"FluentExtensions
    {");

        // Generate extension methods for each fluent member
        foreach (ContextMemberToGenerate member in fluentMembers)
        {
            string[] genericArgs = member.GetFluentGenericArguments();
            if (genericArgs.Length == 0)
            {
                continue;
            }

            // Generate extension methods for each generic type in this TableSet
            for (int i = 0; i < genericArgs.Length && i < s_ordinalNames.Length; i++)
            {
                string fullTypeName = genericArgs[i];
                string simpleTypeName = ContextMemberToGenerate.GetSimpleTypeName(fullTypeName);
                string ordinalName = s_ordinalNames[i];

                GenerateWhereExtensionMethod(sb, member.Type, simpleTypeName, fullTypeName, ordinalName);
                GenerateFindAsyncExtensionMethod(sb, member.Type, simpleTypeName, fullTypeName, ordinalName, member.FluentTypeVariant!);
            }
        }

        // Close extension class
        sb.Append(@"
    }");
    }

    private static void GenerateWhereExtensionMethod(
        StringBuilder sb,
        string tableSetType,
        string simpleTypeName,
        string fullTypeName,
        string ordinalName)
    {
        sb.Append(@"

        /// <summary>
        /// Filters the table set to only return entities of type ").Append(simpleTypeName).Append(@".
        /// </summary>
        /// <param name=""table"">The table set.</param>
        /// <returns>A queryable filtered to ").Append(simpleTypeName).Append(@" entities.</returns>
        public static global::TableStorage.Linq.IFilteredTableQueryable<").Append(fullTypeName).Append(@"> Where").Append(simpleTypeName).Append(@"(this global::TableStorage.TableSet<").Append(tableSetType).Append(@"> table)
        {
            return table.Where").Append(ordinalName).Append(@"Type();
        }

        /// <summary>
        /// Filters the ").Append(ordinalName.ToLowerInvariant()).Append(@" type entities by the specified predicate.
        /// </summary>
        /// <param name=""predicate"">The filter predicate.</param>
        /// <returns>A filtered table queryable for <typeparamref name=""").Append(fullTypeName).Append(@"""/>.</returns>
        public static global::TableStorage.Linq.IFilteredTableQueryable<").Append(fullTypeName).Append(@"> Where").Append(simpleTypeName).Append(@"(this global::TableStorage.TableSet<").Append(tableSetType).Append(@"> table, global::System.Linq.Expressions.Expression<global::System.Func<").Append(fullTypeName).Append(@", bool>> predicate)
        {
            return table.Where").Append(ordinalName).Append(@"Type().Where(predicate);
        }

        /// <summary>
        /// Selects specific fields from the ").Append(ordinalName.ToLowerInvariant()).Append(@" type entities.
        /// </summary>
        /// <typeparam name=""TResult"">The result type of the selector.</typeparam>
        /// <param name=""selector"">The field selector expression.</param>
        /// <returns>A selected table queryable for <typeparamref name=""").Append(fullTypeName).Append(@"""/>.</returns>
        public static global::TableStorage.Linq.ISelectedTableQueryable<").Append(fullTypeName).Append(@"> Select").Append(simpleTypeName).Append(@"Fields<TResult>(this global::TableStorage.TableSet<").Append(tableSetType).Append(@"> table, global::System.Linq.Expressions.Expression<global::System.Func<").Append(fullTypeName).Append(@", TResult>> selector)
        {
            return table.Where").Append(ordinalName).Append(@"Type().SelectFields(selector);
        }

        /// <summary>
        /// Filters ").Append(ordinalName.ToLowerInvariant()).Append(@" type entities where the predicate value exists in the provided elements.
        /// </summary>
        /// <typeparam name=""TElement"">The element type.</typeparam>
        /// <param name=""predicate"">The predicate to evaluate.</param>
        /// <param name=""elements"">The elements to check existence in.</param>
        /// <returns>A filtered table queryable for <typeparamref name=""").Append(fullTypeName).Append(@"""/>.</returns>
        public static global::TableStorage.Linq.IFilteredTableQueryable<").Append(fullTypeName).Append(@"> ExistsIn").Append(simpleTypeName).Append(@"<TElement>(this global::TableStorage.TableSet<").Append(tableSetType).Append(@"> table, global::System.Linq.Expressions.Expression<global::System.Func<").Append(fullTypeName).Append(@", TElement>> predicate, global::System.Collections.Generic.IEnumerable<TElement> elements)
        {
            return table.Where").Append(ordinalName).Append(@"Type().ExistsIn(predicate, elements);
        }

        /// <summary>
        /// Filters ").Append(ordinalName.ToLowerInvariant()).Append(@" type entities where the predicate value does not exist in the provided elements.
        /// </summary>
        /// <typeparam name=""TElement"">The element type.</typeparam>
        /// <param name=""predicate"">The predicate to evaluate.</param>
        /// <param name=""elements"">The elements to check non-existence in.</param>
        /// <returns>A filtered table queryable for <typeparamref name=""").Append(fullTypeName).Append(@"""/>.</returns>
        public static global::TableStorage.Linq.IFilteredTableQueryable<").Append(fullTypeName).Append(@"> NotExistsIn").Append(simpleTypeName).Append(@"<TElement>(this global::TableStorage.TableSet<").Append(tableSetType).Append(@"> table, global::System.Linq.Expressions.Expression<global::System.Func<").Append(fullTypeName).Append(@", TElement>> predicate, global::System.Collections.Generic.IEnumerable<TElement> elements)
        {
            return table.Where").Append(ordinalName).Append(@"Type().NotExistsIn(predicate, elements);
        }
");
    }

    private static void GenerateFindAsyncExtensionMethod(
        StringBuilder sb,
        string tableSetType,
        string simpleTypeName,
        string fullTypeName,
        string ordinalName,
        string variant)
    {
        if (variant is "FluentPartitionTableEntity" or "FluentRowTableEntity")
        {
            (string parameterName, string paramDescription) = variant switch
            {
                "FluentPartitionTableEntity" => ("RowKey", "The row key of the entity."),
                "FluentRowTableEntity" => ("PartitionKey", "The partition key of the entity."),
                _ => throw new InvalidOperationException($"Unsupported fluent type variant: {variant}")
            };

            sb.Append(@"

        /// <summary>
        /// Finds a single entity of type ").Append(simpleTypeName).Append(@" by key.
        /// </summary>
        /// <param name=""table"">The table set.</param>
        /// <param name=""").Append(parameterName.ToLowerInvariant()).Append(@""">").Append(paramDescription).Append(@"</param>
        /// <param name=""cancellationToken"">Cancellation token.</param>
        /// <returns>The entity if found, null otherwise.</returns>
        public static global::System.Threading.Tasks.Task<").Append(fullTypeName).Append(@"?> Find").Append(simpleTypeName).Append(@"Async(this global::TableStorage.TableSet<").Append(tableSetType).Append(@"> table, string ").Append(parameterName.ToLowerInvariant()).Append(@", global::System.Threading.CancellationToken cancellationToken = default)
        {
            return table.Where").Append(ordinalName).Append(@"Type().Where(x => ((global::Azure.Data.Tables.ITableEntity) x).").Append(parameterName).Append(" == ").Append(parameterName.ToLowerInvariant()).Append(@").FirstOrDefaultAsync(cancellationToken);
        }

        /// <summary>
        /// Finds a the entities of type ").Append(simpleTypeName).Append(@" by keys.
        /// </summary>
        /// <param name=""table"">The table set.</param>
        /// <param name=""").Append(parameterName.ToLowerInvariant()).Append(@"s"">").Append(paramDescription).Append(@"</param>
        /// <returns>The entities if found, empty list otherwise.</returns>
        public static global::TableStorage.Linq.IFilteredTableQueryable<").Append(fullTypeName).Append(@"> Find").Append(simpleTypeName).Append(@"Async(this global::TableStorage.TableSet<").Append(tableSetType).Append(@"> table, params global::System.Collections.Generic.IReadOnlyList<string> ").Append(parameterName.ToLowerInvariant()).Append(@"s)
        {
            return table.Where").Append(ordinalName).Append(@"Type().ExistsIn(x => ((global::Azure.Data.Tables.ITableEntity) x).").Append(parameterName).Append(", ").Append(parameterName.ToLowerInvariant()).Append(@"s);
        }");
        }
    }
}