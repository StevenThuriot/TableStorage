using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Immutable;
using System.Text;

namespace TableStorage.SourceGenerators;

[Generator]
public class TableSetModelGenerator : IIncrementalGenerator
{
    private const string TableAttributes = @"
using System;

namespace TableStorage
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public sealed class TableSetAttribute : Attribute
    {
        public string PartitionKey { get; set; }
        public string RowKey { get; set; }
        public bool TrackChanges { get; set; }
    }


    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class TableSetPropertyAttribute : Attribute
    {
        public TableSetPropertyAttribute(Type type, string name)
        {
        }
    }
}";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(ctx => ctx.AddSource("TableSetAttributes.g.cs", SourceText.From(TableAttributes, Encoding.UTF8)));

        var classDeclarations = context.SyntaxProvider.CreateSyntaxProvider(predicate: static (s, _) => IsSyntaxTargetForGeneration(s), // select class with attributes
                                                                            transform: static (ctx, _) => GetSemanticTargetForGeneration(ctx)) // select the class with the [TableContext] attribute
                                                      .Where(static m => m is not null) // filter out attributed classes that we don't care about
                                                      ;

        IncrementalValueProvider<(Compilation, ImmutableArray<ClassDeclarationSyntax>)> compilationAndClasses = context.CompilationProvider.Combine(classDeclarations.Collect())!;
        context.RegisterSourceOutput(compilationAndClasses, static (spc, source) => Execute(source.Item1, source.Item2, spc));

    }

    private static bool IsSyntaxTargetForGeneration(SyntaxNode node) => node is ClassDeclarationSyntax m && m.AttributeLists.Count > 0;

    private static ClassDeclarationSyntax? GetSemanticTargetForGeneration(GeneratorSyntaxContext context)
    {
        // we know the node is a ClassDeclarationSyntax thanks to IsSyntaxTargetForGeneration
        var classDeclarationSyntax = (ClassDeclarationSyntax)context.Node;

        // loop through all the attributes on the method
        foreach (AttributeListSyntax attributeListSyntax in classDeclarationSyntax.AttributeLists)
        {
            foreach (AttributeSyntax attributeSyntax in attributeListSyntax.Attributes)
            {
                if (context.SemanticModel.GetSymbolInfo(attributeSyntax).Symbol is not IMethodSymbol attributeSymbol)
                {
                    // weird, we couldn't get the symbol, ignore it
                    continue;
                }

                INamedTypeSymbol attributeContainingTypeSymbol = attributeSymbol.ContainingType;
                string fullName = attributeContainingTypeSymbol.ToDisplayString();

                // Is the attribute the [TableSetAttribute] attribute?
                if (fullName is "TableStorage.TableSetAttribute")
                {
                    // return the class
                    return classDeclarationSyntax;
                }
            }
        }

        // we didn't find the attribute we were looking for
        return null;
    }

    private static void Execute(Compilation compilation, ImmutableArray<ClassDeclarationSyntax> classes, SourceProductionContext context)
    {
        if (classes.IsDefaultOrEmpty)
        {
            // nothing to do yet
            return;
        }

        // Convert each ClassDeclarationSyntax to a ClassToGenerate
        List<ClassToGenerate> classesToGenerate = GetTypesToGenerate(compilation, classes.Distinct(), context.CancellationToken);

        // If there were errors in the ClassDeclarationSyntax, we won't create an
        // ClassToGenerate for it, so make sure we have something to generate
        if (classesToGenerate.Count > 0)
        {
            // generate the source code and add it to the output
            foreach ((string name, string modelResult) in GenerateTableContextClasses(classesToGenerate))
            {
                context.AddSource(name + ".g.cs", SourceText.From(modelResult, Encoding.UTF8));
            }
        }
    }

    private static List<ClassToGenerate> GetTypesToGenerate(Compilation compilation, IEnumerable<ClassDeclarationSyntax> classes, CancellationToken ct)
    {
        // Create a list to hold our output
        var classesToGenerate = new List<ClassToGenerate>();

        // Get the semantic representation of our marker attribute 
        INamedTypeSymbol? modelAttribute = compilation.GetTypeByMetadataName("TableStorage.TableSetAttribute");
        if (modelAttribute is null)
        {
            return classesToGenerate;
        }

        foreach (ClassDeclarationSyntax classDeclarationSyntax in classes)
        {
            // stop if we're asked to
            ct.ThrowIfCancellationRequested();

            // Get the semantic representation of the class syntax
            SemanticModel semanticModel = compilation.GetSemanticModel(classDeclarationSyntax.SyntaxTree);
            if (semanticModel.GetDeclaredSymbol(classDeclarationSyntax) is not INamedTypeSymbol classSymbol)
            {
                // something went wrong, bail out
                continue;
            }

            List<(string fullName, AttributeSyntax attributeSyntax)> relevantSymbols = [];
            foreach (AttributeListSyntax attributeListSyntax in classDeclarationSyntax.AttributeLists)
            {
                foreach (AttributeSyntax attributeSyntax in attributeListSyntax.Attributes)
                {
                    if (semanticModel.GetSymbolInfo(attributeSyntax).Symbol is not IMethodSymbol attributeSymbol)
                    {
                        // weird, we couldn't get the symbol, ignore it
                        continue;
                    }

                    INamedTypeSymbol attributeContainingTypeSymbol = attributeSymbol.ContainingType;
                    string fullName = attributeContainingTypeSymbol.ToDisplayString();

                    if (fullName.StartsWith("TableStorage."))
                    {
                        relevantSymbols.Add((fullName, attributeSyntax));
                    }
                }
            }

            // Get all the members in the class
            var classMembers = classSymbol.GetMembers();

            var members = new List<MemberToGenerate>(classMembers.Length);
            var prettyMembers = new List<PrettyMemberToGenerate>(2);

            var tablesetAttribute = relevantSymbols.First(x => x.fullName == "TableStorage.TableSetAttribute").attributeSyntax;
            bool withChangeTracking = GetArgumentValue(tablesetAttribute, "TrackChanges") == "true";

            // Get all the properties from the class, and add their name to the list
            foreach (ISymbol member in classMembers)
            {
                if (member is IPropertySymbol property)
                {
                    switch (property.Name)
                    {
                        case "PartitionKey":
                        case "RowKey":
                        case "Timestamp":
                        case "ETag":
                        case "this[]":
                        case "Keys":
                        case "Values":
                        case "Count":
                        case "IsReadOnly":
                            break;

                        default:
                            ITypeSymbol type = property.Type;
                            TypeKind typeKind = GetTypeKind(type);
                            members.Add(new(member.Name, type.ToDisplayString(), typeKind, false, "null", "null", withChangeTracking));
                            break;
                    }
                }
            }

            string? partitionKeyProxy = GetArgumentValue(tablesetAttribute, "PartitionKey");
            if (partitionKeyProxy is not null)
            {
                prettyMembers.Add(new(partitionKeyProxy.Trim('"'), "PartitionKey"));
            }
            else
            {
                partitionKeyProxy = "null";
            }

            string? rowKeyProxy = GetArgumentValue(tablesetAttribute, "RowKey");
            if (rowKeyProxy is not null)
            {
                prettyMembers.Add(new(rowKeyProxy.Trim('"'), "RowKey"));
            }
            else
            {
                rowKeyProxy = "null";
            }

            foreach (var (_, tableSetPropertyAttribute) in relevantSymbols.Where(x => x.fullName == "TableStorage.TableSetPropertyAttribute"))
            {
                // Generate additional fields
                var nameSyntax = (LiteralExpressionSyntax)tableSetPropertyAttribute.ArgumentList!.Arguments[1].Expression;
                var name = nameSyntax.Token.ValueText;

                var typeOfSyntax = (TypeOfExpressionSyntax)tableSetPropertyAttribute.ArgumentList!.Arguments[0].Expression;
                var typeSyntax = typeOfSyntax.Type;

                TypeInfo typeInfo = semanticModel.GetTypeInfo(typeSyntax);

                string type = typeInfo.Type?.ToDisplayString() ?? typeSyntax.ToFullString();
                TypeKind typeKind = GetTypeKind(typeInfo.Type);

                members.Add(new(name, type, typeKind, true, partitionKeyProxy, rowKeyProxy, withChangeTracking));
            }

            // Create an ClassToGenerate for use in the generation phase
            classesToGenerate.Add(new(classSymbol.Name, classSymbol.ContainingNamespace.ToDisplayString(), members, prettyMembers));
        }

        return classesToGenerate;
    }

    private static string? GetArgumentValue(AttributeSyntax tablesetAttribute, string name)
    {
        return tablesetAttribute.ArgumentList?.Arguments.Where(x => x.NameEquals?.Name.NormalizeWhitespace().ToFullString() == name)
                                                                                       .Select(x => x.Expression.NormalizeWhitespace().ToFullString())
                                                                                       .FirstOrDefault();
    }

    private static TypeKind GetTypeKind(ITypeSymbol? type) => type switch
    {
        null => TypeKind.Unknown,
        INamedTypeSymbol namedTypeSymbol when type.NullableAnnotation == NullableAnnotation.Annotated || //Sometimes it's nullable yet not annoted
                                              namedTypeSymbol.ConstructedFrom.ToDisplayString() == "System.Nullable<T>" => namedTypeSymbol.TypeArguments[0].TypeKind,
        _ => type.TypeKind,
    };

    public static IEnumerable<(string name, string result)> GenerateTableContextClasses(List<ClassToGenerate> classesToGenerate)
    {
        StringBuilder modelBuilder = new();

        foreach (var classToGenerate in classesToGenerate)
        {
            modelBuilder.Clear();
            modelBuilder.Append(@"using Microsoft.Extensions.DependencyInjection;
using TableStorage;
using System.Collections.Generic;
using System;
");

            if (classToGenerate.Members.Any(m => m.WithChangeTracking))
            {
                modelBuilder.AppendLine("using System.Linq;");
            }

            modelBuilder.Append(@"

#nullable disable
");

            GenerateModel(modelBuilder, classToGenerate);

            yield return (classToGenerate.Namespace + "." + classToGenerate.Name, modelBuilder.ToString());
        }
    }

    private static void GenerateModel(StringBuilder sb, ClassToGenerate classToGenerate)
    {
        var hasChangeTracking = classToGenerate.Members.Any(x => x.WithChangeTracking);
        var hasPartitionKeyProxy = classToGenerate.TryGetPrettyMember("PartitionKey", out var partitionKeyProxy);
        var realParitionKey = hasPartitionKeyProxy ? partitionKeyProxy.Name : "PartitionKey";
        var hasRowKeyProxy = classToGenerate.TryGetPrettyMember("RowKey", out var rowKeyProxy);
        var realRowKey = hasRowKeyProxy ? rowKeyProxy.Name : "RowKey";

        if (!string.IsNullOrEmpty(classToGenerate.Namespace))
        {
            sb.Append(@"
namespace ").Append(classToGenerate.Namespace).Append(@"
{");
        }

        sb.Append(@"
    [System.Diagnostics.DebuggerDisplay(@""").Append(classToGenerate.Name).Append(@" \{ {").Append(realParitionKey).Append("}, {").Append(realRowKey).Append(@"} \}"")]
    partial class ").Append(classToGenerate.Name).Append(@" : IDictionary<string, object>, Azure.Data.Tables.ITableEntity");

        if (hasChangeTracking)
        {
            sb.Append(", TableStorage.IChangeTracking");
        }

        sb.Append(@"
    {
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public static TableSet<").Append(classToGenerate.Name).Append(@"> CreateSet(TableStorage.ICreator creator, string name)
        {
            return creator.CreateSet");

        if (hasChangeTracking)
        {
            sb.Append("WithChangeTracking");
        }

        sb.Append('<').Append(classToGenerate.Name).Append(">(name, ");

        if (hasPartitionKeyProxy)
        {
            sb.Append('"').Append(partitionKeyProxy.Name).Append('"');
        }
        else
        {
            sb.Append("null");
        }

        sb.Append(", ");

        if (hasRowKeyProxy)
        {
            sb.Append('"').Append(rowKeyProxy.Name).Append('"');
        }
        else
        {
            sb.Append("null");
        }

        sb.Append(@");
        }
");

        if (hasChangeTracking)
        {
            sb.Append(@"
        private readonly HashSet<string> _changes = [];

        public void AcceptChanges()
        {
            _changes.Clear();
        }

        public bool IsChanged()
        {
            return _changes.Count is not 0;
        }

        public bool IsChanged(string field)
        {
            return _changes.Contains(field);
        }

        public Azure.Data.Tables.ITableEntity GetEntity()
        {
            var result = new Azure.Data.Tables.TableEntity(").Append(realParitionKey).Append(", ").Append(realRowKey).Append(@");

            foreach (var key in _changes)
            {
                result[key] = this[key];
            }

            return result;
        }

        public void SetChanged(string field)
        {
            _changes.Add(field);
        }

        public void SetChanged()
        {");

            foreach (MemberToGenerate member in classToGenerate.Members)
            {
                sb.AppendLine().Append("            _changes.Add(\"" + member.Name + "\");");
            }

            sb.Append(@"
        }
");
        }

        sb.Append(@"
        ");

        if (hasPartitionKeyProxy)
        {
            sb.Append("string Azure.Data.Tables.ITableEntity.PartitionKey { get => ").Append(partitionKeyProxy.Name).Append("; set => ").Append(partitionKeyProxy.Name).Append(" = value; }");
        }
        else
        {
            sb.Append("public string PartitionKey { get; set; }");
        }

        sb.Append(@"
        ");

        if (hasRowKeyProxy)
        {
            sb.Append("string Azure.Data.Tables.ITableEntity.RowKey { get => ").Append(rowKeyProxy.Name).Append("; set => ").Append(rowKeyProxy.Name).Append(" = value; }");
        }
        else
        {
            sb.Append("public string RowKey { get; set; }");
        }

        sb.Append(@"
        public DateTimeOffset? Timestamp { get; set; }
        public Azure.ETag ETag { get; set; }");

        foreach (var item in classToGenerate.Members.Where(x => x.GenerateProperty))
        {
            sb.Append(@"
        [System.Runtime.Serialization.IgnoreDataMember] public ").Append(item.Type).Append(" ").Append(item.Name);

            if (item.WithChangeTracking)
            {
                sb.Append(@"
        { 
            get
            {
                return _").Append(item.Name).Append(@";
            }
            set
            {
                _").Append(item.Name).Append(@" = value;
                _changes.Add(""").Append(item.Name).Append(@""");
            }
        }
        private ").Append(item.Type).Append(" _").Append(item.Name).Append(";");
            }
            else
            {
                sb.Append(" { get; set; }");
            }
        }

        foreach (var item in classToGenerate.PrettyMembers)
        {
            if (item.Proxy is "PartitionKey" or "RowKey")
            {
                sb.Append(@"
        [System.Runtime.Serialization.IgnoreDataMember] public string ").Append(item.Name).Append(" { get; set; }");
            }
            else
            {
                sb.Append(@"
        [System.Runtime.Serialization.IgnoreDataMember] public string ").Append(item.Name).Append(" { get => ").Append(item.Proxy).Append("; set => ").Append(item.Proxy).Append(" = value; }");
            }
        }

        sb.Append(@"

        public object this[string key]
        {
            get
            {
                switch (key)
                {
                    case ""PartitionKey"": return ").Append(realParitionKey).Append(@";
                    case ""RowKey"": return ").Append(realRowKey).Append(@";
                    case ""Timestamp"": return Timestamp;
                    case ""odata.etag"": return ETag.ToString();");

        foreach (var item in classToGenerate.Members)
        {
            sb.Append(@"
                    case """).Append(item.Name).Append(@""": return ").Append(item.Name).Append(";");
        }

        sb.Append(@"
                    default: return null;
                }
            }

            set
            {
                switch (key)
                {
                    case ""PartitionKey"": ").Append(realParitionKey).Append(@" = value?.ToString(); break;
                    case ""RowKey"": ").Append(realRowKey).Append(@" = value?.ToString(); break;
                    case ""Timestamp"": Timestamp = (System.DateTimeOffset?)value; break;
                    case ""odata.etag"": ETag = new Azure.ETag(value?.ToString()); break;");

        foreach (var item in classToGenerate.Members)
        {
            sb.Append(@"
                    case """).Append(item.Name).Append(@""": ");

            if (item.WithChangeTracking)
            {
                sb.Append('_');
            }

            sb.Append(item.Name).Append(" = (");

            if (item.Type == typeof(DateTime).FullName)
            {
                sb.Append("(DateTimeOffset)value).DateTime");
            }
            else if (item.Type == typeof(DateTime).FullName + "?")
            {
                sb.Append("value as DateTimeOffset?)?.DateTime");
            }
            else if (item.TypeKind == TypeKind.Enum)
            {
                sb.Append("value is int _").Append(item.Name).Append("Integer ? (").Append(item.Type).Append(") _").Append(item.Name).Append("Integer : ")
                  .Append("Enum.TryParse(value?.ToString(), out ")
                  .Append(item.Type.TrimEnd('?'))
                  .Append(" _")
                  .Append(item.Name)
                  .Append("ParseResult) ? _")
                  .Append(item.Name)
                  .Append("ParseResult : default(")
                  .Append(item.Type)
                  .Append("))");
            }
            else
            {
                sb.Append(item.Type).Append(") value");
            }

            sb.Append("; break;");
        }

        sb.Append(@"
                }
            }
        }

        public ICollection<string> Keys => [ ""PartitionKey"", ""RowKey"", ""Timestamp"", ""odata.etag"", ");

        IReadOnlyList<MemberToGenerate> keysAndValuesToGenerate = classToGenerate.Members;

        //if (hasChangeTracking)
        //{
        //    keysAndValuesToGenerate = keysAndValuesToGenerate.Where(x => !x.WithChangeTracking).ToList();
        //}

        foreach (var item in keysAndValuesToGenerate)
        {
            sb.Append(@"""").Append(item.Name).Append(@""", ");
        }

        //if (hasChangeTracking)
        //{
        //    sb.Append(" .._changes");
        //}

        sb.Append(@" ];
        public ICollection<object> Values => [ ").Append(realParitionKey).Append(", ").Append(realRowKey).Append(", Timestamp, ETag.ToString(), ");

        foreach (var item in keysAndValuesToGenerate)
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

//        if (hasChangeTracking)
//        {
//            sb.Append(@" .._changes.Select<string, object>(x => x switch
//        {
//");

//            foreach (var item in classToGenerate.Members.Where(x => x.WithChangeTracking))
//            {
//                sb.Append("            \"").Append(item.Name).Append("\" => ");

//                if (item.TypeKind == TypeKind.Enum)
//                {
//                    sb.Append("(int");

//                    if (item.Type.EndsWith("?"))
//                    {
//                        sb.Append('?');
//                    }

//                    sb.Append(") ");
//                }

//                sb.Append(item.Name).AppendLine(", ");
//            }

//            sb.Append(@"            _ => throw new System.ArgumentException()
//        })");
//        }

        sb.Append(@" ];
        public int Count => ").Append(4 + keysAndValuesToGenerate.Count);

        //if (hasChangeTracking)
        //{
        //    sb.Append(" + _changes.Count");
        //}

        sb.Append(@";
        public bool IsReadOnly => false;

        public void Add(string key, object value)
        {
            this[key] = value;");

        if (hasChangeTracking)
        {
            sb.Append(@"
            _changes.Add(key);");
        }

        sb.Append(@"
        }

        public void Add(KeyValuePair<string, object> item)
        {
            this[item.Key] = item.Value;");

        if (hasChangeTracking)
        {
            sb.Append(@"
            _changes.Add(item.Key);");
        }

        sb.Append(@"
        }

        public void Clear()
        {");

        foreach (var item in classToGenerate.Members)
        {
            sb.Append(@"
            ").Append(item.Name).Append(" = default(").Append(item.Type).Append(");");
        }

        sb.Append(@"
        }

        public bool Contains(KeyValuePair<string, object> item)
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

        foreach (var item in classToGenerate.Members)
        {
            sb.Append(@"
                case """).Append(item.Name).Append(@""": ");
        }

        sb.Append(@"
                    return true;
            
                default: return false;
            }
        }

        public void CopyTo(KeyValuePair<string, object>[] array, int arrayIndex)
        {
            if (array == null)
            {
                throw new System.ArgumentNullException(""array"");
            }

            if ((uint)arrayIndex > (uint)array.Length)
            {
                throw new System.IndexOutOfRangeException();
            }

            if (array.Length - arrayIndex < Count)
            {
                throw new System.ArgumentException();
            }

            foreach (var item in this)
            {
                array[arrayIndex++] = item;
            }
        }

        public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
        {
            yield return new KeyValuePair<string, object>(""PartitionKey"", ").Append(realParitionKey).Append(@");
            yield return new KeyValuePair<string, object>(""RowKey"", ").Append(realRowKey).Append(@");
            yield return new KeyValuePair<string, object>(""Timestamp"", Timestamp);
            yield return new KeyValuePair<string, object>(""odata.etag"", ETag.ToString());");

        foreach (var item in classToGenerate.Members)
        {
            sb.Append(@"
            yield return new KeyValuePair<string, object>(""").Append(item.Name).Append(@""", ");

            if (item.TypeKind == TypeKind.Enum)
            {
                sb.Append("(int");

                if (item.Type.EndsWith("?"))
                {
                    sb.Append('?');
                }

                sb.Append(")");
            }

            sb.Append(item.Name).Append(");");
        }

        sb.Append(@"
        }

        public bool Remove(string key)
        {
            if (ContainsKey(key)) 
            {
                this[key] = null;");

        if (hasChangeTracking)
        {
            sb.Append(@"
                _changes.Add(key);");
        }

        sb.Append(@"
                return true;
            }

            return false;
        }

        public bool Remove(KeyValuePair<string, object> item)
        {
            if (Contains(item)) 
            {
                this[item.Key] = null;");

        if (hasChangeTracking)
        {
            sb.Append(@"
                _changes.Add(item.Key);");
        }

        sb.Append(@"
                return true;
            }

            return false;
        }

        public bool TryGetValue(string key, out object value)
        {
            switch (key)
            {
                case ""PartitionKey"": value = ").Append(realParitionKey).Append(@"; return true;
                case ""RowKey"": value = ").Append(realRowKey).Append(@"; return true;
                case ""Timestamp"": value = Timestamp; return true;
                case ""odata.etag"": value = ETag; return true;");

        foreach (var item in classToGenerate.Members)
        {
            sb.Append(@"
                case """).Append(item.Name).Append(@""": value = ").Append(item.Name).Append("; return true;");
        }

        sb.Append(@"
                default: value = null; return false;
            }
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }
    }
");

        if (!string.IsNullOrEmpty(classToGenerate.Namespace))
        {
            sb.Append("}");
        }
    }
}
