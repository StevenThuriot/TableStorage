using Microsoft.CodeAnalysis;

namespace TableStorage.SourceGenerators;

public readonly struct ClassToGenerate(string name, string @namespace, List<MemberToGenerate> members, List<PrettyMemberToGenerate> prettyMembers)
{
    public readonly string Name = name;
    public readonly string Namespace = @namespace;
    public readonly List<MemberToGenerate> Members = members;
    public readonly List<PrettyMemberToGenerate> PrettyMembers = prettyMembers;
}

public readonly struct MemberToGenerate(string name, string type, TypeKind typeKind, bool generateProperty, string paritionKeyProxy, string rowKeyProxy)
{
    public readonly string Name = name;
    public readonly string Type = type;
    public readonly TypeKind TypeKind = typeKind;
    public readonly bool GenerateProperty = generateProperty;
    public readonly string ParitionKeyProxy = paritionKeyProxy;
    public readonly string RowKeyProxy = rowKeyProxy;
}

public readonly struct PrettyMemberToGenerate(string name, string proxy)
{
    public readonly string Name = name;
    public readonly string Proxy = proxy;
}