#nullable disable

using ProtoBuf;
using System.Net;
using System.Text.Json;

namespace TableStorage.Tests.Models;

public static class Randoms
{
    public static string String() => Guid.NewGuid().ToString("N");
    public static string From(string value, string value2) => value + String() + value2;
}

public abstract class BaseModel
{
    public virtual string PrettyRow { get; set; }
    public string CommonProperty { get; set; }
    public virtual int BaseId { get; set; }
    public virtual int BaseId2 { get; set; }
}

[TableSet(TrackChanges = true, PartitionKey = "PrettyName", RowKey = "PrettyRow", SupportBlobs = true)]
[TableSetProperty(typeof(int), "MyProperty1")]
[TableSetProperty(typeof(string), "MyProperty2")]
[TableSetProperty(typeof(ModelEnum), "MyProperty3")]
[TableSetProperty(typeof(ModelEnum?), "MyProperty4")]
[TableSetProperty(typeof(Nullable<ModelEnum>), "MyProperty6")]
[TableSetProperty(typeof(HttpStatusCode), "MyProperty7")]
[TableSetProperty(typeof(HttpStatusCode?), "MyProperty8")]
[TableSetProperty(typeof(string), "MyProperty9")]
public partial class Model : BaseModel
{
    // Define as partial if you want to have changetracking
    public partial ModelEnum? MyProperty5 { get; set; }
}

[TableSet(RowKey = "PrettyRow", SupportBlobs = true)]
public partial class Model2 : BaseModel
{
    public static string RandomHelpingString { get; } = "Test"; // Should not be generated as a property

    public partial int MyProperty1 { get; set; }
    public string MyProperty2 { get; set; }
    public System.DateTimeOffset? MyProperty3 { get; set; }
    public System.Guid? MyProperty4 { get; set; }
    public System.DateTimeOffset MyProperty5 { get; set; }
    public System.Guid MyProperty6 { get; set; }
    public ModelEnum MyProperty7 { get; set; }
    public ModelEnum? MyProperty8 { get; set; }
    public Nullable<ModelEnum> MyProperty9 { get; set; }

    public override int BaseId2 { get; set; } = 10; // Override a base property to test if it works
}

public enum ModelEnum
{
    Yes,
    No
}

[TableSet]
public partial class Model3
{
}

#nullable enable
[TableSet(PartitionKey = nameof(PrettyPartition), RowKey = nameof(PrettyRow), SupportBlobs = true, TrackChanges = true)]
[ProtoContract(IgnoreListHandling = true)] // Important to ignore list handling because we are generating an IDictionary implementation that is not supported by protobuf
public partial class Model4
{
    [ProtoMember(1)]
    public partial string PrettyPartition { get; set; } // We can partial the PK and RowKey to enable custom serialization attributes

    [ProtoMember(2)]
    public partial string PrettyRow { get; set; }

    [ProtoMember(3)]
    public partial int MyProperty1 { get; set; }

    [ProtoMember(4)]
    public partial string MyProperty2 { get; set; }

    [ProtoMember(5)]
    public partial string? MyNullableProperty2 { get; set; }
}
#nullable disable

[TableSet(PartitionKey = "Id", RowKey = "ContinuationToken", SupportBlobs = true, DisableTables = true)]
public partial class Model5
{
    public partial string Id { get; set; }
    public partial string ContinuationToken { get; set; }
    public partial Model5Entry[] Entries { get; set; }
}

public sealed class Model5Entry
{
    public DateTimeOffset Creation { get; set; }
    public long? Duration { get; set; }
}

[TableSet(PartitionKey = nameof(PrettyPartition), RowKey = nameof(PrettyRow))]
public partial class TestModel
{
    public partial string PrettyPartition { get; set; }
    public partial string PrettyRow { get; set; }
    public partial int MyProperty1 { get; set; }
    public partial string MyProperty2 { get; set; }
    public partial string MyNullableProperty2 { get; set; }
}

public record TestTransformAndSelectWithGuid(int prop1, string prop2, Guid id);

public record NestedTestTransformAndSelect(Guid id, TestTransformAndSelect test);

public record StringFormatted(string value);

public record StringFormatted2(string Value, string OtherValue, DateTimeOffset TimeStamp);

public record TestTransformAndSelect(int prop1, string prop2)
{
    public static TestTransformAndSelect Map(int prop1, string prop2)
    {
        return new(prop1, prop2);
    }
}

public static class Mapper
{
    public static TestTransformAndSelect Map(this Model model)
    {
        return new(model.MyProperty1, model.MyProperty2);
    }

    public static TestTransformAndSelect Map(this FluentTestModelA model)
    {
        return new(model.PropertyA, model.TypeA);
    }
}

// Base class tests for property inheritance
public abstract class BaseClassWithBothKeys
{
    public string PrettyPartition { get; set; }
    public string PrettyRow { get; set; }
}

public abstract class BaseClassWithOnlyPartitionKey
{
    public string PrettyPartition { get; set; }
}

public abstract class BaseClassWithOnlyRowKey
{
    public string PrettyRow { get; set; }
}

public abstract class BaseClassWithNoKeys
{
    public string SomeOtherProperty { get; set; }
}

public abstract class BaseClassWithBothKeysPartial
{
    public string PrettyPartition { get; set; }
    public string PrettyRow { get; set; }
}

public abstract class BaseClassWithVirtualKeys
{
    public virtual string PrettyPartition { get; set; }
    public virtual string PrettyRow { get; set; }
}

// Test models - both keys in base class
[TableSet(PartitionKey = nameof(PrettyPartition), RowKey = nameof(PrettyRow))]
public partial class ModelBothKeysInBase : BaseClassWithBothKeys
{
    public partial string MyProperty { get; set; }
}

// Test models - only partition key in base class
[TableSet(PartitionKey = nameof(PrettyPartition), RowKey = nameof(PrettyRow))]
public partial class ModelPartitionKeyInBase : BaseClassWithOnlyPartitionKey
{
    public partial string MyProperty { get; set; }
}

// Test models - only row key in base class
[TableSet(PartitionKey = nameof(PrettyPartition), RowKey = nameof(PrettyRow))]
public partial class ModelRowKeyInBase : BaseClassWithOnlyRowKey
{
    public partial string MyProperty { get; set; }
}

// Test models - no keys in base class
[TableSet(PartitionKey = nameof(PrettyPartition), RowKey = nameof(PrettyRow))]
public partial class ModelNoKeysInBase : BaseClassWithNoKeys
{
    public partial string MyProperty { get; set; }
}

// Test models - both keys in base class with partial override using 'new'
[TableSet(PartitionKey = nameof(PrettyPartition), RowKey = nameof(PrettyRow))]
public partial class ModelBothKeysInBaseWithPartial : BaseClassWithBothKeysPartial
{
    public new partial string PrettyPartition { get; set; }
    public new partial string PrettyRow { get; set; }
    public partial string MyProperty { get; set; }
}

// Test models - both keys in base class with virtual override
[TableSet(PartitionKey = nameof(PrettyPartition), RowKey = nameof(PrettyRow))]
public partial class ModelBothKeysInBaseWithOverride : BaseClassWithVirtualKeys
{
    public override string PrettyPartition { get; set; }
    public override string PrettyRow { get; set; }
    public partial string MyProperty { get; set; }
}

[TableSet(PartitionKey = nameof(PrettyPartitionA), RowKey = nameof(PrettyRowA))]
public partial class FluentTestModelA
{
    public partial string PrettyPartitionA { get; set; }
    public partial string PrettyRowA { get; set; }
    public partial string TypeA { get; set; }
    public partial int PropertyA { get; set; }
}

[TableSet(PartitionKey = nameof(PrettyPartitionB), RowKey = nameof(PrettyRowB))]
public partial class FluentTestModelB
{
    public partial string PrettyPartitionB { get; set; }
    public partial string PrettyRowB { get; set; }
    public partial string TypeB { get; set; }
    public partial bool PropertyB { get; set; }
}

// Dedicated models for InternalTableContext accessibility tests
[TableSet(PartitionKey = nameof(PrettyPartition), RowKey = nameof(PrettyRow))]
internal sealed partial class InternalFluentModelA
{
    public partial string PrettyPartition { get; set; }
    public partial string PrettyRow { get; set; }
}

[TableSet(PartitionKey = nameof(PrettyPartition), RowKey = nameof(PrettyRow))]
internal sealed partial class InternalFluentModelB
{
    public partial string PrettyPartition { get; set; }
    public partial string PrettyRow { get; set; }
}