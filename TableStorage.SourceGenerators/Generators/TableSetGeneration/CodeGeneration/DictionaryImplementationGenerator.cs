using Microsoft.CodeAnalysis;
using System.Text;
using TableStorage.SourceGenerators.Models;

namespace TableStorage.SourceGenerators.Generators.TableSetGeneration.CodeGeneration;

/// <summary>
/// Generates IDictionary implementation for table entities.
/// </summary>
internal static class DictionaryImplementationGenerator
{
    /// <summary>
    /// Generates the complete IDictionary implementation including collections and methods.
    /// </summary>
    /// <param name="sb">The StringBuilder to append to.</param>
    /// <param name="classToGenerate">The class configuration.</param>
    /// <param name="context">The model context.</param>
    public static void GenerateDictionaryImplementation(StringBuilder sb, ClassToGenerate classToGenerate, in ModelContext context)
    {
        string realPartitionKey = context.RealPartitionKey;
        string realRowKey = context.RealRowKey;
        List<MemberToGenerate> keysAndValuesToGenerate = [.. classToGenerate.Members.Where(x => x.Name != realPartitionKey && x.Name != realRowKey)];

        GenerateCollections(sb, context, keysAndValuesToGenerate);
        GenerateDictionaryMethods(sb, classToGenerate, context);
    }

    private static void GenerateCollections(StringBuilder sb, ModelContext context, List<MemberToGenerate> keysAndValuesToGenerate)
    {
        // Keys collection
        sb.Append(@"

        public global::System.Collections.Generic.ICollection<string> Keys => [ ""PartitionKey"", ""RowKey"", ""Timestamp"", ""odata.etag"", ");

        foreach (MemberToGenerate item in keysAndValuesToGenerate)
        {
            sb.Append('"').Append(item.Name).Append(@""", ");
        }

        // Values collection
        sb.Append(@" ];
        public global::System.Collections.Generic.ICollection<object> Values => [ ").Append(context.RealPartitionKey).Append(", ").Append(context.RealRowKey).Append(", Timestamp, ETag.ToString(), ");

        foreach (MemberToGenerate item in keysAndValuesToGenerate)
        {
            if (item.TypeKind == TypeKind.Enum)
            {
                sb.Append("(int");

                if (item.Type.EndsWith("?"))
                {
                    sb.Append('?');
                }

                sb.Append(") ");
            }

            sb.Append(item.Name).Append(", ");
        }

        // Count property
        sb.Append(@" ];
        public int Count => ").Append(4 + keysAndValuesToGenerate.Count).Append(@";
        public bool IsReadOnly => false;");
    }

    private static void GenerateDictionaryMethods(StringBuilder sb, ClassToGenerate classToGenerate, ModelContext context)
    {
        bool hasChangeTracking = context.HasChangeTracking;

        GenerateAddMethods(sb, hasChangeTracking);
        GenerateClearMethod(sb, classToGenerate, context);
        GenerateContainsMethods(sb, classToGenerate);
        GenerateCopyToMethod(sb);
        GenerateGetEnumeratorMethods(sb, classToGenerate, context);
        GenerateRemoveMethods(sb, hasChangeTracking);
        GenerateTryGetValueMethod(sb, classToGenerate, context);
    }

    private static void GenerateAddMethods(StringBuilder sb, bool hasChangeTracking)
    {
        sb.Append(@"

        public void Add(string key, object value)
        {
            this[key] = value;");

        if (hasChangeTracking)
        {
            sb.Append(@"
            SetChanged(key);");
        }

        sb.Append(@"
        }

        public void Add(global::System.Collections.Generic.KeyValuePair<string, object> item)
        {
            this[item.Key] = item.Value;");

        if (hasChangeTracking)
        {
            sb.Append(@"
            SetChanged(item.Key);");
        }

        sb.Append(@"
        }");
    }

    private static void GenerateClearMethod(StringBuilder sb, ClassToGenerate classToGenerate, ModelContext context)
    {
        sb.Append(@"

        public void Clear()
        {");

        foreach (MemberToGenerate item in classToGenerate.Members.Where(x => x.Name != context.RealPartitionKey && x.Name != context.RealRowKey))
        {
            sb.Append(@"
            ").Append(item.Name).Append(" = default(").Append(item.Type).Append(");");
        }

        sb.Append(@"
        }");
    }

    private static void GenerateContainsMethods(StringBuilder sb, ClassToGenerate classToGenerate)
    {
        sb.Append(@"

        public bool Contains(global::System.Collections.Generic.KeyValuePair<string, object> item)
        {
            if (TryGetValue(item.Key, out var value))
            {
                return value == item.Value;
            }

            return false;
        }

        public bool ContainsKey(string key)
        {
            switch (key)
            {
                case ""PartitionKey"":
                case ""RowKey"":
                case ""Timestamp"":
                case ""odata.etag"":");

        foreach (MemberToGenerate item in classToGenerate.Members)
        {
            sb.Append(@"
                case """).Append(item.Name).Append(@""": ");
        }

        sb.Append(@"
                    return true;
            
                default: return false;
            }
        }");
    }

    private static void GenerateCopyToMethod(StringBuilder sb)
    {
        sb.Append(@"

        public void CopyTo(global::System.Collections.Generic.KeyValuePair<string, object>[] array, int arrayIndex)
        {
            if (array == null)
            {
                throw new global::System.ArgumentNullException(""array"");
            }

            if ((uint)arrayIndex > (uint)array.Length)
            {
                throw new global::System.IndexOutOfRangeException();
            }

            if (array.Length - arrayIndex < Count)
            {
                throw new global::System.ArgumentException();
            }

            foreach (var item in this)
            {
                array[arrayIndex++] = item;
            }
        }");
    }

    private static void GenerateGetEnumeratorMethods(StringBuilder sb, ClassToGenerate classToGenerate, ModelContext context)
    {
        sb.Append(@"

        public global::System.Collections.Generic.IEnumerator<global::System.Collections.Generic.KeyValuePair<string, object>> GetEnumerator()
        {
            yield return new global::System.Collections.Generic.KeyValuePair<string, object>(""PartitionKey"", ").Append(context.RealPartitionKey).Append(@");
            yield return new global::System.Collections.Generic.KeyValuePair<string, object>(""RowKey"", ").Append(context.RealRowKey).Append(@");
            yield return new global::System.Collections.Generic.KeyValuePair<string, object>(""Timestamp"", Timestamp);
            yield return new global::System.Collections.Generic.KeyValuePair<string, object>(""odata.etag"", ETag.ToString());");

        foreach (MemberToGenerate item in classToGenerate.Members.Where(x => x.Name != context.RealPartitionKey && x.Name != context.RealRowKey))
        {
            sb.Append(@"
            yield return new global::System.Collections.Generic.KeyValuePair<string, object>(""").Append(item.Name).Append(@""", ");

            if (item.TypeKind == TypeKind.Enum)
            {
                sb.Append("(int");

                if (item.Type.EndsWith("?"))
                {
                    sb.Append('?');
                }

                sb.Append(')');
            }

            sb.Append(item.Name).Append(");");
        }

        sb.Append(@"
        }

        global::System.Collections.IEnumerator global::System.Collections.IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }");
    }

    private static void GenerateRemoveMethods(StringBuilder sb, bool hasChangeTracking)
    {
        sb.Append(@"

        public bool Remove(string key)
        {
            if (ContainsKey(key)) 
            {
                this[key] = null;");

        if (hasChangeTracking)
        {
            sb.Append(@"
                SetChanged(key);");
        }

        sb.Append(@"
                return true;
            }

            return false;
        }

        public bool Remove(global::System.Collections.Generic.KeyValuePair<string, object> item)
        {
            if (Contains(item)) 
            {
                this[item.Key] = null;");

        if (hasChangeTracking)
        {
            sb.Append(@"
                SetChanged(item.Key);");
        }

        sb.Append(@"
                return true;
            }

            return false;
        }");
    }

    private static void GenerateTryGetValueMethod(StringBuilder sb, ClassToGenerate classToGenerate, ModelContext context)
    {
        sb.Append(@"

        public bool TryGetValue(string key, out object value)
        {
            switch (key)
            {
                case ""PartitionKey"": value = ").Append(context.RealPartitionKey).Append(@"; return true;
                case ""RowKey"": value = ").Append(context.RealRowKey).Append(@"; return true;
                case ""Timestamp"": value = Timestamp; return true;
                case ""odata.etag"": value = ETag; return true;");

        foreach (MemberToGenerate item in classToGenerate.Members)
        {
            sb.Append(@"
                case """).Append(item.Name).Append(@""": value = ").Append(item.Name).Append("; return true;");
        }

        sb.Append(@"
                default: value = null; return false;
            }
        }");
    }
}