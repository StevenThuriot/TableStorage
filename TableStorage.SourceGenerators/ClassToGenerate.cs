using Microsoft.CodeAnalysis;

namespace TableStorage.SourceGenerators;

public readonly struct ClassToGenerate
{
    public readonly string Name;
    public readonly string Namespace;
    public readonly List<MemberToGenerate> Members;
    public readonly List<PrettyMemberToGenerate> PrettyMembers;

    public ClassToGenerate(string name, string @namespace, List<MemberToGenerate> members, List<PrettyMemberToGenerate> prettyMembers)
    {
        Name = name;
        Namespace = @namespace;
        Members = members;
        PrettyMembers = prettyMembers;
    }
}

public readonly struct MemberToGenerate
{
    public readonly string Name;
    public readonly string Type;
    public readonly TypeKind TypeKind;
    public readonly bool GenerateProperty;

    public MemberToGenerate(string name, string type, TypeKind typeKind, bool generateProperty)
    {
        Name = name;
        Type = type;
        TypeKind = typeKind;
        GenerateProperty = generateProperty;
    }
}

public readonly struct PrettyMemberToGenerate
{
    public readonly string Name;
    public readonly string Proxy;

    public PrettyMemberToGenerate(string name, string proxy)
    {
        Name = name;
        Proxy = proxy;
    }
}