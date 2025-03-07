using Microsoft.CodeAnalysis;

namespace TableStorage.SourceGenerators;

public readonly struct ContextClassToGenerate(string name, string @namespace, List<ContextMemberToGenerate> members)
{
    public readonly string Name = name;
    public readonly string Namespace = @namespace;
    public readonly List<ContextMemberToGenerate> Members = members;
}

public readonly struct ContextMemberToGenerate(string name, string type, TypeKind typeKind, string setType)
{
    public readonly string Name = name;
    public readonly string Type = type;
    public readonly TypeKind TypeKind = typeKind;
    public readonly string SetType = setType;
}

public readonly struct ClassToGenerate(string name, string @namespace, List<MemberToGenerate> members, List<PrettyMemberToGenerate> prettyMembers, bool withBlobSupport)
{
    public readonly string Name = name;
    public readonly string Namespace = @namespace;
    public readonly List<MemberToGenerate> Members = members;
    public readonly List<PrettyMemberToGenerate> PrettyMembers = prettyMembers;
    public readonly bool WithBlobSupport = withBlobSupport;

    public bool TryGetPrettyMember(string proxy, out PrettyMemberToGenerate prettyMemberToGenerate)
    {
        foreach (PrettyMemberToGenerate member in PrettyMembers)
        {
            if (member.Proxy == proxy)
            {
                prettyMemberToGenerate = member;
                return true;
            }
        }

        prettyMemberToGenerate = default;
        return false;
    }
}

public readonly struct MemberToGenerate(string name, string type, TypeKind typeKind, bool generateProperty, string partitionKeyProxy, string rowKeyProxy, bool withChangeTracking, bool isPartial, bool tagBlob)
{
    public readonly string Name = name;
    public readonly string Type = type;
    public readonly TypeKind TypeKind = typeKind;
    public readonly bool GenerateProperty = generateProperty;
    public readonly string PartitionKeyProxy = partitionKeyProxy;
    public readonly string RowKeyProxy = rowKeyProxy;
    public readonly bool WithChangeTracking = generateProperty && withChangeTracking;
    public readonly bool IsPartial = isPartial;
    public readonly bool TagBlob = tagBlob;
}

public readonly struct PrettyMemberToGenerate(string name, string proxy)
{
    public readonly string Name = name;
    public readonly string Proxy = proxy;
}