using Microsoft.CodeAnalysis;
using System.Text;
using TableStorage.SourceGenerators.Models;

namespace TableStorage.SourceGenerators.Generators.TableSetGeneration.CodeGeneration;

/// <summary>
/// Generates change tracking functionality for entities.
/// </summary>
internal static class ChangeTrackingGenerator
{
    /// <summary>
    /// Generates complete change tracking support including fields, methods, and GetEntity implementation.
    /// </summary>
    /// <param name="sb">The StringBuilder to append to.</param>
    /// <param name="classToGenerate">The class configuration.</param>
    /// <param name="context">The model context.</param>
    public static void GenerateChangeTrackingSupport(StringBuilder sb, ClassToGenerate classToGenerate, in ModelContext context)
    {
        string realPartitionKey = context.RealPartitionKey;
        string realRowKey = context.RealRowKey;

        sb.Append(@"
        private readonly global::System.Collections.Generic.HashSet<string> _changes = new global::System.Collections.Generic.HashSet<string>();

        public void AcceptChanges()
        {
            _changes.Clear();
        }

        public bool IsChanged()
        {
            return _changes.Count != 0;
        }

        public bool IsChanged(string field)
        {
            return _changes.Contains(field);
        }");

        GenerateGetEntityMethod(sb, classToGenerate, context, realPartitionKey, realRowKey);
        GenerateSetChangedMethods(sb, classToGenerate, realPartitionKey, realRowKey);
    }

    private static void GenerateGetEntityMethod(StringBuilder sb, ClassToGenerate classToGenerate, ModelContext context, string realPartitionKey, string realRowKey)
    {
        sb.Append(@"

        public global::Azure.Data.Tables.ITableEntity GetEntity()
        {
            var entityDictionary = new global::System.Collections.Generic.Dictionary<string, object>(").Append(4 + classToGenerate.Members.Count(x => x.Name != realPartitionKey && x.Name != realRowKey && !x.WithChangeTracking)).Append(@" + _changes.Count)
            {
                [""PartitionKey""] = ").Append(context.RealPartitionKey).Append(@",
                [""RowKey""] = ").Append(context.RealRowKey).Append(@",
                [""Timestamp""] = Timestamp,
                [""ETag""] = ETag.ToString(),");

        foreach (MemberToGenerate item in classToGenerate.Members.Where(x => x.Name != realPartitionKey && x.Name != realRowKey && !x.WithChangeTracking))
        {
            sb.AppendLine().Append("                [\"").Append(item.Name).Append("\"] = ");

            if (item.TypeKind == TypeKind.Enum)
            {
                sb.Append("(int");

                if (item.Type.EndsWith("?"))
                {
                    sb.Append('?');
                }

                sb.Append(") ");
            }

            sb.Append(item.Name).Append(',');
        }

        sb.Append(@"
            };

            foreach (var key in _changes)
            {
                entityDictionary[key] = key switch
                {
");

        foreach (MemberToGenerate item in classToGenerate.Members.Where(x => x.Name != realPartitionKey && x.Name != realRowKey && x.WithChangeTracking))
        {
            sb.Append("                    \"").Append(item.Name).Append("\" => ");

            if (item.TypeKind == TypeKind.Enum)
            {
                sb.Append("(int");

                if (item.Type.EndsWith("?"))
                {
                    sb.Append('?');
                }

                sb.Append(") ");
            }

            sb.Append(item.Name).AppendLine(", ");
        }

        sb.Append(@"                    _ => throw new global::System.ArgumentException()
                };");

        sb.Append(@"
            }

            return new global::Azure.Data.Tables.TableEntity(entityDictionary);
        }");
    }

    private static void GenerateSetChangedMethods(StringBuilder sb, ClassToGenerate classToGenerate, string realPartitionKey, string realRowKey)
    {
        sb.Append(@"

        public void SetChanged(string field)
        {
            _changes.Add(field);
        }

        public void SetChanged()
        {");

        foreach (MemberToGenerate member in classToGenerate.Members.Where(x => x.Name != realPartitionKey && x.Name != realRowKey && x.GenerateProperty))
        {
            sb.AppendLine().Append("            SetChanged(\"" + member.Name + "\");");
        }

        sb.Append(@"
        }
");
    }
}