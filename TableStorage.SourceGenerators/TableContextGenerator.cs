﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Immutable;
using System.Text;

namespace TableStorage.SourceGenerators;

[Generator]
public class TableContextGenerator : IIncrementalGenerator
{
    private const string TableContextAttribute = Header.Value + @"using System;

namespace TableStorage
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public sealed class TableContextAttribute : Attribute
    {
    }
}";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(ctx => ctx.AddSource("TableContextAttribute.g.cs", SourceText.From(TableContextAttribute, Encoding.UTF8)));

        IncrementalValuesProvider<ClassDeclarationSyntax?> classDeclarations = context.SyntaxProvider.CreateSyntaxProvider(predicate: static (s, _) => IsSyntaxTargetForGeneration(s), // select class with attributes
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

                // Is the attribute the [TableContext] attribute?
                if (fullName is "TableStorage.TableContextAttribute")
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
        List<ContextClassToGenerate> classesToGenerate = GetTypesToGenerate(compilation, classes.Distinct(), context.CancellationToken);

        // If there were errors in the ClassDeclarationSyntax, we won't create an
        // ClassToGenerate for it, so make sure we have something to generate
        if (classesToGenerate.Count > 0)
        {
            // generate the source code and add it to the output
            foreach ((string name, string result) in GenerateTableContextClasses(classesToGenerate))
            {
                context.AddSource(name + ".g.cs", SourceText.From(result, Encoding.UTF8));
            }
        }
    }

    private static List<ContextClassToGenerate> GetTypesToGenerate(Compilation compilation, IEnumerable<ClassDeclarationSyntax> classes, CancellationToken ct)
    {
        // Create a list to hold our output
        var classesToGenerate = new List<ContextClassToGenerate>();

        // Get the semantic representation of our marker attribute 
        INamedTypeSymbol? contextAttribute = compilation.GetTypeByMetadataName("TableStorage.TableContextAttribute");
        if (contextAttribute is null)
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

            // Get all the members in the class
            ImmutableArray<ISymbol> classMembers = classSymbol.GetMembers();
            var members = new List<ContextMemberToGenerate>(classMembers.Length);

            // Get all the properties from the class, and add their name to the list
            foreach (ISymbol member in classMembers)
            {
                if (member is IPropertySymbol property)
                {
                    if (property.Type.Name is "TableSet" or "BlobSet")
                    {
                        ITypeSymbol tableSetType = ((INamedTypeSymbol)property.Type).TypeArguments[0];
                        members.Add(new(member.Name, tableSetType.ToDisplayString(), property.Type.TypeKind, property.Type.Name));
                    }
                }
            }

            // Create an ClassToGenerate for use in the generation phase
            classesToGenerate.Add(new(classSymbol.Name, classSymbol.ContainingNamespace.ToDisplayString(), members));
        }

        return classesToGenerate;
    }

    public static IEnumerable<(string name, string content)> GenerateTableContextClasses(List<ContextClassToGenerate> classesToGenerate)
    {
        StringBuilder contextBuilder = new();

        foreach (ContextClassToGenerate classToGenerate in classesToGenerate)
        {
            contextBuilder.Clear();
            contextBuilder.Append(Header.Value).Append(@"using Microsoft.Extensions.DependencyInjection;
using TableStorage;
using System;
");

            GenerateContext(contextBuilder, classToGenerate);

            yield return (classToGenerate.Namespace + "." + classToGenerate.Name, contextBuilder.ToString());
        }
    }

    private static void GenerateContext(StringBuilder sb, ContextClassToGenerate classToGenerate)
    {
        if (!string.IsNullOrEmpty(classToGenerate.Namespace))
        {
            sb.Append(@"
namespace ").Append(classToGenerate.Namespace).Append(@"
{");
        }

        sb.Append(@"
    public static class ").Append(classToGenerate.Name).Append(@"Extensions
    {
        public static IServiceCollection Add").Append(classToGenerate.Name).Append(@"(this IServiceCollection services, string connectionString, Action<TableStorage.TableOptions> configure = null");

        bool hasBlobSets = classToGenerate.Members.Any(x => x.SetType is "BlobSet");

        if (hasBlobSets)
        {
            sb.Append(", Action<TableStorage.BlobOptions> configureBlobs = null");
        }
        
        sb.Append(@")
        {
            ").Append(classToGenerate.Name).Append(@".Register(services, connectionString, configure");

        if (hasBlobSets)
        {
            sb.Append(", configureBlobs");
        }
        
        sb.Append(@");
            return services;
        }
    }

    partial class ").Append(classToGenerate.Name).Append(@"
    {
        private TableStorage.ICreator _creator { get; init; }");

        if (hasBlobSets)
        {

            sb.Append(@"
        private TableStorage.IBlobCreator _blobCreator { get; init; }

        public BlobSet<T> GetBlobSet<T>(string tableName)
            where T : class, TableStorage.IBlobEntity, new()
        {
            return _blobCreator.CreateSet<T>(tableName);
        }

        public BlobSet<T> GetBlobSet<T>(string tableName, string partitionKeyProxy = null, string rowKeyProxy = null)
            where T : class, TableStorage.IBlobEntity, new()
        {
            return _blobCreator.CreateSet<T>(tableName, partitionKeyProxy, rowKeyProxy);
        }");
        }

        sb.Append(@"

        public TableSet<T> GetTableSet<T>(string tableName)
            where T : class, Azure.Data.Tables.ITableEntity, new()
        {
            if (TableSet<T>.HasChangeTracking)
            {
                return ((dynamic) _creator).CreateSetWithChangeTracking<T>(tableName);
            }

            return _creator.CreateSet<T>(tableName);
        }

        public TableSet<T> GetTableSet<T>(string tableName, string partitionKeyProxy = null, string rowKeyProxy = null)
            where T : class, Azure.Data.Tables.ITableEntity, new()
        {
            if (TableSet<T>.HasChangeTracking)
            {
                return ((dynamic) _creator).CreateSetWithChangeTracking<T>(tableName);
            }

            return _creator.CreateSet<T>(tableName, partitionKeyProxy, rowKeyProxy);
        }

        private ").Append(classToGenerate.Name).Append(@"(TableStorage.ICreator creator");

        if (hasBlobSets)
        {
            sb.Append(@", TableStorage.IBlobCreator blobCreator");
        }

        sb.Append(@")
        {
            _creator = creator;");

        if (hasBlobSets)
        {
            sb.Append(@"
            _blobCreator = blobCreator;");
        }

        foreach (ContextMemberToGenerate item in classToGenerate.Members)
        {
            sb.Append(@"
            ").Append(item.Name).Append(" = ").Append(item.Type).Append(".Create").Append(item.SetType).Append("(");

            var name = item.Name;
            if (item.SetType is "BlobSet")
            {
                sb.Append("blobC");
                name = name.ToLowerInvariant();
            }
            else
            {
                sb.Append("c");
            }

            sb.Append("reator, \"").Append(name).Append("\");");
        }

        sb.Append(@"
        }

        public static void Register(IServiceCollection services, string connectionString, Action<TableStorage.TableOptions> configure");

        if (hasBlobSets)
        {
            sb.Append(", Action<TableStorage.BlobOptions> configureBlobs");
        }
        
        sb.Append(@")
        {
            services.AddSingleton(s =>
            {
                TableStorage.ICreator creator = TableStorage.TableStorageSetup.BuildCreator(connectionString, configure);");

        if (hasBlobSets)
        {
            sb.Append(@"
                TableStorage.IBlobCreator blobCreator = TableStorage.BlobStorageSetup.BuildCreator(connectionString, configureBlobs);");
        }

        sb.Append(@"
                return new ").Append(classToGenerate.Name).Append(@"(creator");
        
        if (hasBlobSets)
        {
            sb.Append(@", blobCreator");
        }

        sb.Append(@");
            });").Append(@"
        }
    }
");

        if (!string.IsNullOrEmpty(classToGenerate.Namespace))
        {
            sb.AppendLine("}");
        }
    }
}
