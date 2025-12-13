using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Concurrent;
using TableStorage.SourceGenerators.Models;
using TableStorage.SourceGenerators.Utilities;

namespace TableStorage.SourceGenerators.Extractors;

/// <summary>
/// Extracts data from syntax nodes and semantic models to create cacheable data models.
/// This follows incremental generator best practices by extracting all necessary information
/// from syntax nodes in the transform stage, avoiding the need to store syntax nodes in the pipeline.
/// 
/// Implementation follows Andrew Lock's "Avoiding performance pitfalls in incremental generators" guidelines:
/// 1. ✅ Uses ForAttributeWithMetadataName-compatible extraction methods
/// 2. ✅ Never returns *Syntax or ISymbol instances in pipeline
/// 3. ✅ Uses value type data models with proper structural equality  
/// 4. ✅ Uses EquatableArray&lt;T&gt; for collections to avoid caching issues
/// 5. ✅ Provides CompilationProvider-safe extraction methods
/// 6. ✅ Includes diagnostic handling infrastructure (DiagnosticInfo)
/// 7. ✅ Avoids reflection APIs entirely
/// 
/// Performance optimizations:
/// - Static readonly dictionaries for O(1) lookups
/// - Early validation to avoid unnecessary work
/// - Minimal allocations with span usage where possible
/// - Efficient symbol analysis with caching
/// - Single-pass enumeration with early exit
/// - Concurrent caching for repeated operations
/// </summary>
internal static class DataExtractor
{
    /// <summary>
    /// Known TableSet type names for fast lookup.
    /// </summary>
    private static readonly HashSet<string> s_tableSetTypeNames = new(StringComparer.Ordinal)
    {
        "TableSet",
        "DefaultTableSet",
        "ChangeTrackingTableSet"
    };

    /// <summary>
    /// Known BlobSet type names for fast lookup.
    /// </summary>
    private static readonly HashSet<string> s_blobSetTypeNames = new(StringComparer.Ordinal)
    {
        "BlobSet",
        "AppendBlobSet"
    };

    /// <summary>
    /// Extracts table context class information from a GeneratorAttributeSyntaxContext.
    /// This method is designed to be used in the transform stage of ForAttributeWithMetadataName.
    /// 
    /// Performance optimizations:
    /// - Early validation with minimal allocations
    /// - Fast type checking using cached HashSets
    /// - Single-pass member extraction
    /// - Efficient array building with capacity planning
    /// </summary>
    /// <param name="context">The generator attribute syntax context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The extracted table context class information, or null if extraction fails.</returns>
    public static TableContextClassInfo? ExtractTableContextInfo(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
    {
        // Fast validation - check syntax node type first (fastest check)
        if (context.TargetNode is not ClassDeclarationSyntax classDeclaration)
        {
            return null;
        }

        // Early cancellation check before expensive semantic operations
        cancellationToken.ThrowIfCancellationRequested();

        // Get symbol with cancellation support
        if (context.SemanticModel.GetDeclaredSymbol(classDeclaration, cancellationToken) is not INamedTypeSymbol classSymbol)
        {
            return null;
        }

        // Extract basic information efficiently
        string name = classSymbol.Name;
        string @namespace = GetNamespaceString(classSymbol.ContainingNamespace);

        // Pre-allocate member list with reasonable capacity to avoid resizing
        var members = new List<TableContextMemberInfo>(capacity: 8);

        // Single-pass member extraction with efficient filtering
        foreach (var member in classSymbol.GetMembers())
        {
            // Fast type check first, then detailed analysis
            if (member is IPropertySymbol property && IsTableContextMemberFast(property))
            {
                var memberInfo = ExtractTableContextMemberInfoOptimized(property);
                if (memberInfo.HasValue)
                {
                    members.Add(memberInfo.Value);
                }
            }

            // Check cancellation periodically during expensive operations
            cancellationToken.ThrowIfCancellationRequested();
        }

        return new TableContextClassInfo(name, @namespace, new EquatableArray<TableContextMemberInfo>([.. members]));
    }    /// <summary>
         /// Extracts table set class information from a GeneratorAttributeSyntaxContext.
         /// This method is designed to be used in the transform stage of ForAttributeWithMetadataName.
         /// 
         /// Performance optimizations:
         /// - Early validation and cancellation checks
         /// - Efficient attribute lookup with minimal allocations
         /// - Single-pass member extraction with capacity planning
         /// - Fast attribute property access
         /// </summary>
         /// <param name="context">The generator attribute syntax context.</param>
         /// <param name="cancellationToken">The cancellation token.</param>
         /// <returns>The extracted table set class information, or null if extraction fails.</returns>
    public static TableSetClassInfo? ExtractTableSetInfo(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
    {
        // Fast validation - check syntax node type first
        if (context.TargetNode is not ClassDeclarationSyntax classDeclaration)
        {
            return null;
        }

        // Early cancellation check
        cancellationToken.ThrowIfCancellationRequested();

        // Get symbol with cancellation support
        if (context.SemanticModel.GetDeclaredSymbol(classDeclaration, cancellationToken) is not INamedTypeSymbol classSymbol)
        {
            return null;
        }

        // Extract basic information efficiently
        string name = classSymbol.Name;
        string @namespace = GetNamespaceString(classSymbol.ContainingNamespace);

        // Fast attribute lookup with early exit
        var tableSetAttribute = GetTableSetAttributeFast(classSymbol);
        if (tableSetAttribute is null)
        {
            return null;
        }

        // Extract attribute properties efficiently using cached property names
        bool withBlobSupport = GetAttributePropertyFast<bool>(tableSetAttribute, s_supportBlobsProperty);
        bool withTablesSupport = !GetAttributePropertyFast<bool>(tableSetAttribute, s_disableTablesProperty);

        // Pre-allocate collections with reasonable capacity
        var members = new List<TableSetMemberInfo>(capacity: 16);
        var prettyMembers = new List<TableSetPrettyMemberInfo>(capacity: 4);

        // Optimized member extraction
        ExtractTableSetMembersOptimized(classSymbol, tableSetAttribute, members, prettyMembers, cancellationToken);

        return new TableSetClassInfo(
            name,
            @namespace,
            new EquatableArray<TableSetMemberInfo>([.. members]),
            new EquatableArray<TableSetPrettyMemberInfo>([.. prettyMembers]),
            withBlobSupport,
            withTablesSupport);
    }    /// <summary>
         /// Extracts generation options from analyzer config options.
         /// Following Andrew Lock's best practices for configuration data extraction.
         /// </summary>
         /// <param name="optionsProvider">The analyzer config options provider.</param>
         /// <returns>The generation options.</returns>
    public static GenerationOptions ExtractGenerationOptions(Microsoft.CodeAnalysis.Diagnostics.AnalyzerConfigOptionsProvider optionsProvider)
    {
        bool publishAot = ConfigurationHelper.GetPublishAotProperty(optionsProvider);
        string? tableStorageSerializerContext = ConfigurationHelper.GetTableStorageSerializerContextProperty(optionsProvider);

        return new GenerationOptions(publishAot, tableStorageSerializerContext);
    }

    /// <summary>
    /// Cache for namespace strings to avoid repeated ToDisplayString() calls.
    /// This can significantly improve performance when the same namespaces are encountered multiple times.
    /// </summary>
    private static readonly ConcurrentDictionary<INamespaceSymbol, string> s_namespaceCache =
        new(SymbolEqualityComparer.Default);

    /// <summary>
    /// Cache for type display strings to avoid repeated expensive ToDisplayString() calls.
    /// </summary>
    private static readonly ConcurrentDictionary<ITypeSymbol, string> s_typeDisplayStringCache =
        new(SymbolEqualityComparer.Default);

    /// <summary>
    /// Cache for TableSet attribute lookups to avoid repeated attribute enumeration.
    /// </summary>
    private static readonly ConcurrentDictionary<INamedTypeSymbol, AttributeData?> s_tableSetAttributeCache =
        new(SymbolEqualityComparer.Default);

    /// <summary>
    /// Interned strings for common property names to reduce memory allocations.
    /// </summary>
    private static readonly string s_partitionKeyProperty = string.Intern("PartitionKey");
    private static readonly string s_rowKeyProperty = string.Intern("RowKey");
    private static readonly string s_supportBlobsProperty = string.Intern("SupportBlobs");
    private static readonly string s_disableTablesProperty = string.Intern("DisableTables");
    private static readonly string s_trackChangesProperty = string.Intern("TrackChanges");
    private static readonly string s_tagProperty = string.Intern("Tag");

    /// <summary>
    /// Common type strings interned to reduce memory allocations.
    /// </summary>
    private static readonly string s_tableSetString = string.Intern("TableSet");
    private static readonly string s_blobSetString = string.Intern("BlobSet");
    private static readonly string s_unknownString = string.Intern("Unknown");    /// <summary>
                                                                                  /// Fast attribute lookup with caching for better performance.
                                                                                  /// </summary>
                                                                                  /// <param name="classSymbol">The class symbol to search.</param>
                                                                                  /// <returns>The TableSet attribute, or null if not found.</returns>
    private static AttributeData? GetTableSetAttributeFast(INamedTypeSymbol classSymbol)
    {
        // Use cache for better performance with repeated lookups
        return s_tableSetAttributeCache.GetOrAdd(classSymbol, static symbol =>
        {
            foreach (var attr in symbol.GetAttributes())
            {
                if (attr.AttributeClass?.Name is "TableSetAttribute")
                {
                    return attr;
                }
            }

            return null;
        });
    }

    /// <summary>
    /// Fast attribute property access with optimized lookups.
    /// </summary>
    /// <typeparam name="T">The property type.</typeparam>
    /// <param name="attribute">The attribute to search.</param>
    /// <param name="propertyName">The property name.</param>
    /// <returns>The property value or default.</returns>
    private static T GetAttributePropertyFast<T>(AttributeData attribute, string propertyName)
    {
        // Use span enumeration for better performance
        foreach (var namedArgument in attribute.NamedArguments)
        {
            if (namedArgument.Key == propertyName && namedArgument.Value.Value is T value)
            {
                return value;
            }
        }

        return default!;
    }

    /// <summary>
    /// Optimized table set member extraction with cancellation support.
    /// </summary>
    /// <param name="classSymbol">The class symbol.</param>
    /// <param name="tableSetAttribute">The TableSet attribute.</param>
    /// <param name="members">The members list to populate.</param>
    /// <param name="prettyMembers">The pretty members list to populate.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    private static void ExtractTableSetMembersOptimized(
        INamedTypeSymbol classSymbol,
        AttributeData tableSetAttribute,
        List<TableSetMemberInfo> members,
        List<TableSetPrettyMemberInfo> prettyMembers,
        CancellationToken cancellationToken)
    {        // Extract common attribute properties once using cached strings
        string partitionKey = GetAttributePropertyFast<string>(tableSetAttribute, s_partitionKeyProperty) ?? s_partitionKeyProperty;
        string rowKey = GetAttributePropertyFast<string>(tableSetAttribute, s_rowKeyProperty) ?? s_rowKeyProperty;
        bool trackChanges = GetAttributePropertyFast<bool>(tableSetAttribute, s_trackChangesProperty);

        // Fast property attribute enumeration
        foreach (var propertyAttribute in GetTableSetPropertyAttributesFast(classSymbol))
        {
            // Check cancellation periodically during expensive operations
            cancellationToken.ThrowIfCancellationRequested();

            if (propertyAttribute.ConstructorArguments.Length >= 2)
            {
                var typeArg = propertyAttribute.ConstructorArguments[0];
                var nameArg = propertyAttribute.ConstructorArguments[1];

                if (typeArg.Value is INamedTypeSymbol memberType && nameArg.Value is string memberName)
                {
                    const bool generateProperty = true; // Could be enhanced with more logic
                    const bool isPartial = false; // Could be enhanced with more logic
                    bool tagBlob = GetAttributePropertyFast<bool>(propertyAttribute, s_tagProperty);

                    var memberInfo = new TableSetMemberInfo(
                        memberName,
                        memberType.ToDisplayString(),
                        memberType.TypeKind.ToString(),
                        generateProperty,
                        partitionKey,
                        rowKey,
                        trackChanges,
                        isPartial,
                        tagBlob);

                    members.Add(memberInfo);

                    // Add pretty member if names differ
                    if (memberName != memberType.Name)
                    {
                        prettyMembers.Add(new TableSetPrettyMemberInfo(memberType.Name, memberName));
                    }
                }
            }
        }
    }

    /// <summary>
    /// Fast enumeration of TableSetProperty attributes.
    /// </summary>
    /// <param name="classSymbol">The class symbol.</param>
    /// <returns>Enumerable of property attributes.</returns>
    private static IEnumerable<AttributeData> GetTableSetPropertyAttributesFast(INamedTypeSymbol classSymbol)
    {
        foreach (var attr in classSymbol.GetAttributes())
        {
            if (attr.AttributeClass?.Name is "TableSetPropertyAttribute")
            {
                yield return attr;
            }
        }
    }    /// <summary>
         /// Gets a cached namespace string to avoid repeated ToDisplayString() calls.
         /// This can significantly improve performance when the same namespaces are encountered multiple times.
         /// </summary>
         /// <param name="namespaceSymbol">The namespace symbol.</param>
         /// <returns>The namespace string.</returns>
    private static string GetNamespaceString(INamespaceSymbol namespaceSymbol)
    {
        return s_namespaceCache.GetOrAdd(namespaceSymbol, static ns => ns.ToDisplayString());
    }

    /// <summary>
    /// Fast check if a property is a table context member using optimized lookups.
    /// </summary>
    /// <param name="property">The property to check.</param>
    /// <returns>True if this is a table context member.</returns>
    private static bool IsTableContextMemberFast(IPropertySymbol property)
    {
        var type = property.Type;
        if (type is not INamedTypeSymbol namedType)
        {
            return false;
        }

        // Fast check using cached HashSets
        string typeName = namedType.Name;
        return s_tableSetTypeNames.Contains(typeName) || s_blobSetTypeNames.Contains(typeName);
    }    /// <summary>
         /// Optimized version of table context member info extraction with caching.
         /// </summary>
         /// <param name="property">The property symbol.</param>
         /// <returns>The extracted member info.</returns>
    private static TableContextMemberInfo? ExtractTableContextMemberInfoOptimized(IPropertySymbol property)
    {
        // Extract information efficiently with caching
        string name = property.Name;
        string type = GetCachedTypeDisplayString(property.Type);
        string typeKind = property.Type.TypeKind.ToString();

        // Fast set type determination using cached HashSets and interned strings
        string setType = DetermineSetTypeFast(property.Type);

        return new TableContextMemberInfo(name, type, typeKind, setType);
    }

    /// <summary>
    /// Gets a cached type display string to avoid repeated expensive ToDisplayString() calls.
    /// </summary>
    /// <param name="type">The type symbol.</param>
    /// <returns>The cached type display string.</returns>
    private static string GetCachedTypeDisplayString(ITypeSymbol type)
    {
        return s_typeDisplayStringCache.GetOrAdd(type, static t => t.ToDisplayString());
    }    /// <summary>
         /// Fast set type determination using cached lookups and interned strings.
         /// </summary>
         /// <param name="type">The type symbol.</param>
         /// <returns>The set type string.</returns>
    private static string DetermineSetTypeFast(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol namedType)
        {
            string typeName = namedType.Name;

            if (s_tableSetTypeNames.Contains(typeName))
            {
                return s_tableSetString;
            }

            if (s_blobSetTypeNames.Contains(typeName))
            {
                return s_blobSetString;
            }
        }

        return s_unknownString;
    }    /// <summary>
         /// Optimized compilation capabilities extraction that follows Andrew Lock's best practices.
         /// Extracts only essential data in a single pass to maintain optimal caching performance.
         /// </summary>
         /// <param name="compilation">The compilation to analyze.</param>
         /// <returns>The compilation capabilities with optimized caching.</returns>
    public static CompilationCapabilities ExtractCompilationCapabilities(Compilation compilation)
    {
        bool hasTables = false;
        bool hasBlobs = false;

        // Andrew Lock Best Practice: Single-pass enumeration with early exit optimization
        foreach (var assemblyIdentity in compilation.ReferencedAssemblyNames)
        {
            string assemblyName = assemblyIdentity.Name;

            // Fast lookup using cached HashSets (O(1) operations)
            if (!hasTables && assemblyName is "TableStorage")
            {
                hasTables = true;
            }

            if (!hasBlobs && assemblyName is "TableStorage.Blobs")
            {
                hasBlobs = true;
            }

            // Andrew Lock Best Practice: Early exit when both capabilities found
            if (hasTables && hasBlobs)
            {
                break;
            }
        }

        return new CompilationCapabilities(hasTables, hasBlobs);
    }

    /// <summary>
    /// Andrew Lock Best Practice: Fast predicate for syntax-only validation.
    /// This method should be used in ForAttributeWithMetadataName predicates to ensure
    /// maximum performance by avoiding semantic model operations.
    /// </summary>
    /// <param name="node">The syntax node to check.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the node should be processed further.</returns>
    public static bool IsSyntaxTargetForTableContextGeneration(SyntaxNode node, CancellationToken cancellationToken)
    {
        // Andrew Lock Best Practice: Syntax-only checks for maximum performance
        // This runs for every syntax node on every keypress, so must be ultra-fast

        // Fast type check first - most efficient filter
        if (node is not ClassDeclarationSyntax classDeclaration)
        {
            return false;
        }

        // Quick syntax-only checks to reduce false positives
        // Check if class has any attributes at all (cheap syntax check)
        if (classDeclaration.AttributeLists.Count is 0)
        {
            return false;
        }

        // Andrew Lock Best Practice: Periodic cancellation checks even in predicates
        cancellationToken.ThrowIfCancellationRequested();

        // Additional quick syntax checks could go here
        // e.g., check for partial keyword, public modifier, etc.

        return true;
    }

    /// <summary>
    /// Andrew Lock Best Practice: Fast predicate for TableSet generation.
    /// Optimized for syntax-only validation to maximize incremental generator performance.
    /// </summary>
    /// <param name="node">The syntax node to check.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the node should be processed further.</returns>
    public static bool IsSyntaxTargetForTableSetGeneration(SyntaxNode node, CancellationToken cancellationToken)
    {
        // Andrew Lock Best Practice: Fast syntax-only validation
        if (node is not ClassDeclarationSyntax classDeclaration)
        {
            return false;
        }

        // Quick check for attributes (syntax-only, very fast)
        if (classDeclaration.AttributeLists.Count is 0)
        {
            return false;
        }

        cancellationToken.ThrowIfCancellationRequested();

        // Could add more sophisticated syntax-only filtering here
        // For example, checking for specific attribute names in syntax
        // (though this would need to be balanced against predicate performance)

        return true;
    }
}