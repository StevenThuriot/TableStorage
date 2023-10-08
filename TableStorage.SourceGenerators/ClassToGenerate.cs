using Microsoft.CodeAnalysis;

namespace TableStorage.SourceGenerators;

public readonly struct ClassToGenerate
{
    public readonly string Name;
    public readonly string Namespace;
    public readonly List<MemberToGenerate> Members;

    public ClassToGenerate(string name, string @namespace, List<MemberToGenerate> members)
    {
        Name = name;
        Namespace = @namespace;
        Members = members;
    }
}

public readonly struct MemberToGenerate
{
    public readonly string Name;
    public readonly string Type;
    public readonly TypeKind TypeKind;

    public MemberToGenerate(string name, string type, TypeKind typeKind)
    {
        Name = name;
        Type = type;
        TypeKind = typeKind;
    }
}