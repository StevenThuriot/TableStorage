using Microsoft.Extensions.DependencyInjection;
using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using TableStorage;
using TableStorage.Linq;
using TableStorage.Tests.Contexts;
using TableStorage.Tests.Models;

ServiceCollection services = new();
services.AddMyTableContext("UseDevelopmentStorage=true");
var provider = services.BuildServiceProvider();

var context = provider.GetRequiredService<MyTableContext>();

await context.Models1.Where(x => true).BatchDeleteAsync();
await context.Models2.Where(x => true).BatchDeleteAsync();
await context.Models3.Where(x => true).BatchDeleteAsync();
await context.Models4.Where(x => true).BatchDeleteAsync();
await context.Models5.Where(x => true).BatchDeleteAsync();

await context.Models1.UpsertEntityAsync(new()
{
    PrettyName = "root",
    PrettyRow = Guid.NewGuid().ToString("N"),
    MyProperty1 = 1,
    MyProperty2 = "hallo 1"
});

await context.Models1.UpsertEntityAsync(new()
{
    PrettyName = "root",
    PrettyRow = Guid.NewGuid().ToString("N"),
    MyProperty1 = 5,
    MyProperty2 = "hallo 5"
});

await context.Models2.UpsertEntityAsync(new()
{
    PartitionKey = "root",
    PrettyRow = Guid.NewGuid().ToString("N"),
    MyProperty1 = 1,
    MyProperty2 = "hallo 1",
    MyProperty3 = DateTimeOffset.UtcNow,
    MyProperty4 = Guid.NewGuid(),
    MyProperty5 = DateTimeOffset.UtcNow,
    MyProperty6 = Guid.NewGuid(),
    MyProperty7 = ModelEnum.Yes,
    MyProperty8 = ModelEnum.No
});

await context.Models2.UpsertEntityAsync(new()
{
    PartitionKey = "root",
    PrettyRow = Guid.NewGuid().ToString("N"),
    MyProperty1 = 5,
    MyProperty2 = "hallo 5",
    MyProperty3 = DateTimeOffset.UtcNow,
    MyProperty4 = Guid.NewGuid(),
    MyProperty5 = DateTimeOffset.UtcNow,
    MyProperty6 = Guid.NewGuid(),
    MyProperty7 = ModelEnum.Yes,
    MyProperty8 = ModelEnum.No
});

await context.Models2.UpsertEntityAsync(new()
{
    PartitionKey = "root",
    PrettyRow = "this is a test",
    MyProperty1 = 5,
    MyProperty2 = "hallo 5",
    MyProperty3 = DateTimeOffset.UtcNow,
    MyProperty4 = Guid.NewGuid(),
    MyProperty5 = DateTimeOffset.UtcNow,
    MyProperty6 = Guid.NewGuid(),
    MyProperty7 = ModelEnum.Yes,
    MyProperty8 = ModelEnum.No
});

var models2 = await context.Models2.ToListAsync(); //should just return all my big models
Debug.Assert(models2.Count > 0);

var enumFilters = await context.Models2.Where(x => x.MyProperty7 == ModelEnum.Yes && x.MyProperty8 == ModelEnum.No).ToListAsync(); //enum filtering should work
Debug.Assert(enumFilters.Count > 0);

var proxiedList = await context.Models1.SelectFields(x => x.PrettyName == "root" && x.PrettyRow != "").ToListAsync();
Debug.Assert(proxiedList?.Count > 0 && proxiedList.All(x => x.PrettyName == "root" && x.PrettyRow != ""));

var proxyWorksCount = await context.Models1.Where(x => x.PrettyName == "root" && x.PrettyRow != "").CountAsync();
Debug.Assert(proxyWorksCount == proxiedList.Count);

var proxySelectionWorks = await context.Models1.Select(x => new { x.PrettyName, x.PrettyRow }).ToListAsync();

var list1 = await context.Models1.Where(x => x.PrettyName == "root").Where(x => x.MyProperty1 > 2).Take(3).ToListAsync();
Debug.Assert(list1.Count <= 3 && list1.All(x => x.PrettyName != null && x.PrettyRow != null && x.MyProperty1 != 0 && x.MyProperty2 != null)); // Should not contain more than 3 items with all properties filled in

var list2 = await context.Models1.Where(x => x.PrettyName == "root").Where(x => x.MyProperty1 > 2).Take(3).Distinct(FuncComparer.Create((Model x) => x.MyProperty1)).ToListAsync();
Debug.Assert(list2.Count == 1 && list2.All(x => x.PrettyName != null && x.PrettyRow != null && x.MyProperty1 != 0 && x.MyProperty2 != null)); // Should contain 1 item with all properties filled in

var list3 = await context.Models1.Where(x => x.PrettyName == "root").Where(x => x.MyProperty1 > 2).Distinct(FuncComparer.Create((Model x) => x.MyProperty2, StringComparer.OrdinalIgnoreCase)).Take(3).ToListAsync();
Debug.Assert(list3.Count == 1 && list3.All(x => x.PrettyName != null && x.PrettyRow != null && x.MyProperty1 != 0 && x.MyProperty2 != null)); // Should contain 1 item with all properties filled in

var first1 = await context.Models1.Where(x => x.PrettyName == "root").Where(x => x.MyProperty1 > 2).SelectFields(x => new { x.MyProperty2, x.MyProperty1 }).FirstOrDefaultAsync();
Debug.Assert(first1 != null && first1.PrettyName == null && first1.PrettyRow == null && first1.MyProperty1 != 0 && first1.MyProperty2 != null); // Should only fill in the selected properties

var first2 = await context.Models1.Where(x => x.PrettyName == "root").Where(x => x.MyProperty1 > 2).SelectFields(x => x.MyProperty1).FirstOrDefaultAsync();
Debug.Assert(first2 != null && first2.PrettyName == null && first2.PrettyRow == null && first2.MyProperty1 != 0 && first2.MyProperty2 == null); // Should only fill in MyProperty1

var first3 = await context.Models1.Where(x => x.PrettyName == "root").Where(x => x.MyProperty1 > 2).SelectFields(x => new TestTransformAndSelect(x.MyProperty1, x.MyProperty2)).FirstOrDefaultAsync();
Debug.Assert(first3 != null && first3.PrettyName == null && first3.PrettyRow == null && first3.MyProperty1 != 0 && first3.MyProperty2 != null); // Should only fill in MyProperty1 and MyProperty2

var firstTransformed1 = await context.Models1.Where(x => x.PrettyName == "root").Where(x => x.MyProperty1 > 2).Select(x => new { x.MyProperty2, x.MyProperty1 }).FirstOrDefaultAsync();
Debug.Assert(firstTransformed1 != null && firstTransformed1.MyProperty1 != 0 && firstTransformed1.MyProperty2 != null); // Should return an anon type with only these two props

var firstTransformed2 = await context.Models1.Where(x => x.PrettyName == "root").Where(x => x.MyProperty1 > 2).Select(x => x.MyProperty1).FirstOrDefaultAsync();
Debug.Assert(firstTransformed2 != 0); // Should return an int

var firstTransformed3 = await context.Models1.Where(x => x.PrettyName == "root").Where(x => x.MyProperty1 > 2).Select(x => new TestTransformAndSelect(x.MyProperty1, x.MyProperty2)).FirstOrDefaultAsync();
Debug.Assert(firstTransformed3 != null && firstTransformed3.prop1 != 0 && firstTransformed3.prop2 != null); // Should return a record with only these two props

var firstTransformed4 = await context.Models1.Where(x => x.PrettyName == "root").Where(x => x.MyProperty1 > 2).Select(x => new TestTransformAndSelect(x.MyProperty1 + 1, x.MyProperty2 + "_test")).FirstOrDefaultAsync();
Debug.Assert(firstTransformed4 != null && firstTransformed4.prop1 != 0 && firstTransformed4.prop2 != null); // Should return a record with only these two props and included transformations

var firstTransformed5 = await context.Models1.Where(x => x.PrettyName == "root").Where(x => x.MyProperty1 > 2).Select(x => x.MyProperty1 + 1 + x.MyProperty2 + "_test").FirstOrDefaultAsync();
Debug.Assert(!string.IsNullOrEmpty(firstTransformed5)); // Should return a concatted string

var firstTransformed6 = await context.Models1.Where(x => x.PrettyName == "root").Where(x => x.MyProperty1 > 2).Select(x => TestTransformAndSelect.Map(x.MyProperty1, x.MyProperty2)).FirstOrDefaultAsync();
Debug.Assert(firstTransformed6 != null && firstTransformed6.prop1 != 0 && firstTransformed6.prop2 != null); // Should return a record with only these two props and included transformations

var firstTransformed7 = await context.Models1.Where(x => x.PrettyName == "root").Where(x => x.MyProperty1 > 2).Select(x => x.Map()).FirstOrDefaultAsync();
Debug.Assert(firstTransformed7 != null && firstTransformed7.prop1 != 0 && firstTransformed7.prop2 != null); // Should at least work but gets everything

var firstTransformed8 = await context.Models1.Where(x => x.PrettyName == "root").Where(x => x.MyProperty1 > 2).Select(x => new TestTransformAndSelectWithGuid(x.MyProperty1, x.MyProperty2, Guid.Parse(x.PrettyRow))).FirstOrDefaultAsync();
Debug.Assert(firstTransformed8 != null && firstTransformed8.prop1 != 0 && firstTransformed8.prop2 != null); // Should only get 3 props and transform

var firstTransformed9 = await context.Models1.Where(x => x.PrettyName == "root").Where(x => x.MyProperty1 > 2).Select(x => new TestTransformAndSelectWithGuid(x.MyProperty1, "test", Guid.NewGuid())).FirstOrDefaultAsync();
Debug.Assert(firstTransformed9 != null && firstTransformed9.prop1 != 0 && firstTransformed9.prop2 != null); // Should only get one prop and transform

var firstTransformed10 = await context.Models1.Where(x => x.PrettyName == "root").Where(x => x.MyProperty1 > 2).Select(x => new NestedTestTransformAndSelect(Guid.Parse(x.PrettyRow), new(x.MyProperty1 + 1 * 4, x.MyProperty2 + "_test"))).FirstOrDefaultAsync();
Debug.Assert(firstTransformed10 != null && firstTransformed10.id != Guid.Empty && firstTransformed10.test != null && firstTransformed10.test.prop1 != 0 && firstTransformed10.test.prop2 != null); // Should only get 3 props and transform into a nested object

var firstTransformed11 = await context.Models1.Where(x => x.PrettyName == "root").Where(x => x.MyProperty1 > 2).Select(x => new StringFormatted($"{x.PrettyRow} - {x.MyProperty1 + 1 * 4}, {x.MyProperty2}_test")).FirstOrDefaultAsync();
Debug.Assert(firstTransformed11?.value != null); // Should only get 3 props and transform into a string

var firstTransformed12 = await context.Models1.Select(x => new StringFormatted2($"{x.PrettyRow} - {x.MyProperty1 + 1 * 4}, {x.MyProperty2}_test", null, x.Timestamp.GetValueOrDefault())).ToListAsync();
Debug.Assert(firstTransformed12?.All(x => x.Value != null) == true); // Should only get 4 props and transform into a string

var firstTransformed13 = await context.Models1.Select(x => new StringFormatted2(string.Format("{0} - {1}, {2}_test {3}", x.PrettyRow, x.MyProperty1 + (1 * 4), x.MyProperty2, x.Timestamp.GetValueOrDefault()), null, x.Timestamp.GetValueOrDefault())).ToListAsync();
Debug.Assert(firstTransformed13?.All(x => x.Value != null && x.OtherValue == null && x.TimeStamp != default) == true); // Should only get 4 props and transform into a string

var unknown = context.GetTableSet<Model>("randomname");
Debug.Assert(unknown != null); // Gives a tableset that wasn't defined on the original DbContext

var exists = await context.Models1.ExistsIn(x => x.MyProperty1, [1, 2, 3, 4]).Where(x => x.PrettyName == "root").Where(x => x.MyProperty1 < 3).ToListAsync();
Debug.Assert(exists?.Count > 0); // Should return a list of existing models

try
{
    var single = await context.Models2.Where(x => x.PartitionKey == "root").Where(x => x.MyProperty1 > 2).SelectFields(x => x.MyProperty2).SingleAsync();
    Debug.Fail("Should throw");
}
catch (InvalidOperationException)
{
}

var fiveCount = await context.Models2.Where(x => x.MyProperty1 == 5).CountAsync();
var deleteCount = await context.Models2.Where(x => x.MyProperty1 == 5).BatchDeleteTransactionAsync();
Debug.Assert(deleteCount == fiveCount);

var newModels2 = await context.Models2.ToListAsync();
Debug.Assert(newModels2.Count == (models2.Count - deleteCount));

var updateCount = await context.Models2.Where(x => x.MyProperty1 == 1).BatchUpdateTransactionAsync(x => new() { MyProperty2 = "hallo 1 updated" });
var updatedModels = await context.Models2.Where(x => x.MyProperty2 == "hallo 1 updated").ToListAsync();
Debug.Assert(updateCount == updatedModels.Count);

var prettyItem = new { PrettyRow = "this is a test" };
var visitorWorks = await context.Models2.Where(x => x.PrettyRow == prettyItem.PrettyRow).ToListAsync();
Debug.Assert(visitorWorks.Count == 1);
visitorWorks = await context.Models2.Where(x => x.PrettyRow != prettyItem.PrettyRow).ToListAsync();
Debug.Assert(visitorWorks.Count > 1);

Model mergeTest = new()
{
    PrettyName = "root",
    PrettyRow = Guid.NewGuid().ToString("N"),
    MyProperty1 = 1,
    MyProperty2 = "hallo 1"
};
await context.Models1.UpsertEntityAsync(mergeTest);
await context.Models1.UpdateAsync(() => new()
{
    PrettyName = "root",
    PrettyRow = mergeTest.PrettyRow,
    MyProperty1 = 5
});
Debug.Assert((await context.Models1.Where(x => x.PrettyName == "root" && x.PrettyRow == mergeTest.PrettyRow).Select(x => x.MyProperty1).FirstAsync()) == 5);
var mergeCount = await context.Models1.Where(x => x.PrettyName == "root" && x.PrettyRow == mergeTest.PrettyRow).BatchUpdateAsync(x => new()
{
    MyProperty1 = x.MyProperty1 + 1
});
Debug.Assert(mergeCount == 1);
Debug.Assert((await context.Models1.Where(x => x.PrettyName == "root" && x.PrettyRow == mergeTest.PrettyRow).Select(x => x.MyProperty1).FirstAsync()) == 6);
mergeCount = await context.Models1.Where(x => x.PrettyName == "root" && x.PrettyRow == mergeTest.PrettyRow).BatchUpdateTransactionAsync(x => new()
{
    MyProperty1 = x.MyProperty1 - 1,
    MyProperty2 = Randoms.String(),
    MyProperty9 = Randoms.From(x.MyProperty3.ToString(), Randoms.String())

});
Debug.Assert(mergeCount == 1);
Debug.Assert((await context.Models1.Where(x => x.PrettyName == "root" && x.PrettyRow == mergeTest.PrettyRow).Select(x => x.MyProperty1).FirstAsync()) == 5);

await context.Models1.UpsertAsync(() => new()
{
    PrettyName = "root",
    PrettyRow = mergeTest.PrettyRow,
    MyProperty1 = 5,
    MyProperty6 = ModelEnum.No
});

#nullable disable
namespace TableStorage.Tests.Models
{
    public static class Randoms
    {
        public static string String() => Guid.NewGuid().ToString("N");
        public static string From(string value, string value2) => value + String() + value2;
    }

    [TableSet]
    [TableSetProperty(typeof(int), "MyProperty1")]
    [TableSetProperty(typeof(string), "MyProperty2")]
    [TableSetProperty(typeof(ModelEnum), "MyProperty3")]
    [TableSetProperty(typeof(ModelEnum?), "MyProperty4")]
    [TableSetProperty(typeof(Nullable<ModelEnum>), "MyProperty6")]
    [TableSetProperty(typeof(HttpStatusCode), "MyProperty7")]
    [TableSetProperty(typeof(HttpStatusCode?), "MyProperty8")]
    [TableSetProperty(typeof(string), "MyProperty9")]
    [PartitionKey("PrettyName")]
    [RowKey("PrettyRow")]
    public partial class Model
    {
        [System.Runtime.Serialization.IgnoreDataMember] public ModelEnum? MyProperty5 { get; set; }
    }

    [TableSet]
    [RowKey("PrettyRow")]
    public partial class Model2
    {
        public int MyProperty1 { get; set; }
        public string MyProperty2 { get; set; }
        public System.DateTimeOffset? MyProperty3 { get; set; }
        public System.Guid? MyProperty4 { get; set; }
        public System.DateTimeOffset MyProperty5 { get; set; }
        public System.Guid MyProperty6 { get; set; }
        public ModelEnum MyProperty7 { get; set; }
        public ModelEnum? MyProperty8 { get; set; }
        public Nullable<ModelEnum> MyProperty9 { get; set; }
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
}

namespace TableStorage.Tests.Contexts
{
    using TableStorage.Tests.Models;

    [TableContext]
    public partial class MyTableContext
    {
        public TableSet<Model> Models1 { get; set; }
        public TableSet<Model2> Models2 { get; private set; }
        public TableSet<Model> Models3 { get; }
        public TableSet<Model> Models4 { get; init; }
        public TableSet<Model3> Models5 { get; init; }
    }
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
    public static TestTransformAndSelect Map(this TableStorage.Tests.Models.Model model)
    {
        return new(model.MyProperty1, model.MyProperty2);
    }
}