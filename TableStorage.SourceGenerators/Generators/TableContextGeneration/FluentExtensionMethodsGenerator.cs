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
    private static readonly string[] OrdinalNames = 
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
            for (int i = 0; i < genericArgs.Length && i < OrdinalNames.Length; i++)
            {
                string fullTypeName = genericArgs[i];
                string simpleTypeName = ContextMemberToGenerate.GetSimpleTypeName(fullTypeName);
                string ordinalName = OrdinalNames[i];

                GenerateExtensionMethod(sb, member.Type, simpleTypeName, fullTypeName, ordinalName);
            }
        }

        // Close extension class
        sb.Append(@"
    }");
    }

    private static void GenerateExtensionMethod(
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
        }");
    }
}
