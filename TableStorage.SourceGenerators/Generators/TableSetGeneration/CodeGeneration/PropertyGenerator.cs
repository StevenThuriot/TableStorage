using System.Text;
using TableStorage.SourceGenerators.Models;

namespace TableStorage.SourceGenerators.Generators.TableSetGeneration.CodeGeneration;

/// <summary>
/// Generates class properties for both generated and pretty members.
/// </summary>
internal static class PropertyGenerator
{
    /// <summary>
    /// Generates all class properties including custom properties and pretty member properties.
    /// </summary>
    /// <param name="sb">The StringBuilder to append to.</param>
    /// <param name="classToGenerate">The class configuration.</param>
    public static void GenerateClassProperties(StringBuilder sb, ClassToGenerate classToGenerate)
    {
        GenerateCustomProperties(sb, classToGenerate);
        GeneratePrettyMemberProperties(sb, classToGenerate);
    }

    private static void GenerateCustomProperties(StringBuilder sb, ClassToGenerate classToGenerate)
    {
        // Generate custom properties
        foreach (MemberToGenerate item in classToGenerate.Members.Where(x => x.GenerateProperty))
        {
            if (classToGenerate.WithTablesSupport)
            {
                sb.Append(@"
        [global::System.Runtime.Serialization.IgnoreDataMember]");
            }

            sb.Append(@"
        public ");

            if (item.IsPartial)
            {
                sb.Append("partial ");
            }
            else if (item.IsOverride)
            {
                sb.Append("override ");
            }

            sb.Append(item.Type).Append(' ').Append(item.Name);

            if (item.IsPartial || item.IsOverride || item.WithChangeTracking)
            {
                sb.Append(@"
        { 
            get
            {
                return ");

                if (item.IsOverride)
                {
                    sb.Append("base.");
                }
                else
                {
                    sb.Append('_');
                }

                sb.Append(item.Name).Append(@";
            }
            set
            {
                ");

                if (item.IsOverride)
                {
                    sb.Append("base.");
                }
                else
                {
                    sb.Append('_');
                }

                sb.Append(item.Name).Append(@" = value;");

                if (item.WithChangeTracking)
                {
                    sb.Append(@"
                SetChanged(""").Append(item.Name).Append(@""");");
                }

                sb.Append(@"
            }
        }");
                if (!item.IsOverride)
                {
                    sb.Append(@"
        private ").Append(item.Type).Append(" _").Append(item.Name).Append(';');
                }
            }
            else
            {
                sb.Append(" { get; set; }");
            }
        }
    }

    private static void GeneratePrettyMemberProperties(StringBuilder sb, ClassToGenerate classToGenerate)
    {
        // Generate pretty member properties
        foreach (PrettyMemberToGenerate item in classToGenerate.PrettyMembers)
        {
            bool partial = classToGenerate.Members.Any(x => x.IsPartial && x.Name == item.Name);
            bool @virtual = classToGenerate.Members.Any(x => x.IsOverride && x.Name == item.Name);

            // Skip generation if the property exists in base class and is not being overridden with partial
            if (item.ExistsInBaseClass && !partial)
            {
                continue;
            }

            if (item.Proxy is "PartitionKey" or "RowKey")
            {
                GenerateKeyProxyProperty(sb, classToGenerate, item, partial, @virtual);
            }
            else
            {
                GenerateRegularProxyProperty(sb, classToGenerate, item, partial, @virtual);
            }
        }
    }

    private static void GenerateKeyProxyProperty(StringBuilder sb, ClassToGenerate classToGenerate, PrettyMemberToGenerate item, bool partial, bool @virtual)
    {
        if (classToGenerate.WithTablesSupport)
        {
            sb.Append(@"
        [global::System.Runtime.Serialization.IgnoreDataMember]");
        }

        sb.Append(@"
        public ");

        // Check if the partial property is marked with 'new' modifier
        bool isNew = classToGenerate.Members.Any(x => x.IsPartial && x.Name == item.Name && x.IsNew);

        if (isNew)
        {
            sb.Append("new ");
        }

        if (partial)
        {
            sb.Append("partial ");
        }
        else if (@virtual)
        {
            sb.Append("override ");
        }

        sb.Append("string ").Append(item.Name);

        if (partial)
        {
            sb.Append(" { get => _").Append(item.Name).Append("; set => _").Append(item.Name).AppendLine(" = value; }")
              .Append("        private string _").Append(item.Name).Append(';');
        }
        else if (@virtual)
        {
            sb.Append(" { get => base.").Append(item.Name).Append("; set => base.").Append(item.Name).Append(" = value; }");
        }
        else
        {
            sb.Append(" { get; set; }");
        }
    }

    private static void GenerateRegularProxyProperty(StringBuilder sb, ClassToGenerate classToGenerate, PrettyMemberToGenerate item, bool partial, bool @virtual)
    {
        if (classToGenerate.WithTablesSupport)
        {
            sb.Append(@"
        [global::System.Runtime.Serialization.IgnoreDataMember]");
        }

        sb.Append(@"
        public ");

        if (partial)
        {
            sb.Append("partial ");
        }
        else if (@virtual)
        {
            sb.Append("override ");
        }

        sb.Append("string ").Append(item.Name).Append(" { get => ").Append(item.Proxy).Append("; set => ").Append(item.Proxy).Append(" = value; }");
    }
}