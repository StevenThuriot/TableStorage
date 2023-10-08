// See https://aka.ms/new-console-template for more information
using Microsoft.Extensions.DependencyInjection;
using System;
using TableStorage.Linq;
using TableStorage.Tests.Contexts;
using TableStorage.Tests.Models;

ServiceCollection services = new();
services.AddMyTableContext("UseDevelopmentStorage=true");
var provider = services.BuildServiceProvider();

var context = provider.GetRequiredService<MyTableContext>();

await context.Models1.UpsertEntityAsync(new()
{
    PartitionKey = "root",
    RowKey = Guid.NewGuid().ToString("N"),
    MyProperty1 = 1,
    MyProperty2 = "hallo 1"
});

await context.Models1.UpsertEntityAsync(new()
{
    PartitionKey = "root",
    RowKey = Guid.NewGuid().ToString("N"),
    MyProperty1 = 5,
    MyProperty2 = "hallo 5"
});

await context.Models2.UpsertEntityAsync(new()
{
    PartitionKey = "root",
    RowKey = Guid.NewGuid().ToString("N"),
    MyProperty1 = 1,
    MyProperty2 = "hallo 1",
    MyProperty3 = DateTime.UtcNow,
    MyProperty4 = Guid.NewGuid(),
    MyProperty5 = DateTime.UtcNow,
    MyProperty6 = Guid.NewGuid(),
    MyProperty7 = ModelEnum.Yes,
    MyProperty8 = ModelEnum.No
});

await context.Models2.UpsertEntityAsync(new()
{
    PartitionKey = "root",
    RowKey = Guid.NewGuid().ToString("N"),
    MyProperty1 = 5,
    MyProperty2 = "hallo 5",
    MyProperty3 = DateTime.UtcNow,
    MyProperty4 = Guid.NewGuid(),
    MyProperty5 = DateTime.UtcNow,
    MyProperty6 = Guid.NewGuid(),
    MyProperty7 = ModelEnum.Yes,
    MyProperty8 = ModelEnum.No
});

var models2 = await context.Models2.ToListAsync(); //should just return all my big models
var enumFilters = await context.Models2.Where(x => x.MyProperty7 == ModelEnum.Yes && x.MyProperty8 == ModelEnum.No).ToListAsync(); //enum filtering should work

var list1 = await context.Models1.Where(x => x.PartitionKey == "root").Where(x => x.MyProperty1 > 2).Take(3).ToListAsync(); //Should not contain more than 3 items with all properties filled in
var list2 = await context.Models1.Where(x => x.PartitionKey == "root").Where(x => x.MyProperty1 > 2).Take(3).DistinctBy(x => x.MyProperty1).ToListAsync(); //Should contain 1 item with all properties filled in
var list3 = await context.Models1.Where(x => x.PartitionKey == "root").Where(x => x.MyProperty1 > 2).DistinctBy(x => x.MyProperty2, StringComparer.OrdinalIgnoreCase).Take(3).ToListAsync(); //Should contain 1 item with all properties filled in
var first1 = await context.Models1.Where(x => x.PartitionKey == "root").Where(x => x.MyProperty1 > 2).SelectFields(x => new { x.MyProperty2, x.MyProperty1 }).FirstOrDefaultAsync(); //Should only fill in the selected properties
var first2 = await context.Models1.Where(x => x.PartitionKey == "root").Where(x => x.MyProperty1 > 2).SelectFields(x => x.MyProperty1).FirstOrDefaultAsync(); //Should only fill in MyProperty1
var first3 = await context.Models1.Where(x => x.PartitionKey == "root").Where(x => x.MyProperty1 > 2).SelectFields(x => new TestTransformAndSelect(x.MyProperty1, x.MyProperty2)).FirstOrDefaultAsync(); //Should only fill in MyProperty1 and MyProperty2
var firstTransformed1 = await context.Models1.Where(x => x.PartitionKey == "root").Where(x => x.MyProperty1 > 2).Select(x => new { x.MyProperty2, x.MyProperty1 }).FirstOrDefaultAsync(); //Should return an anon type with only these two props
var firstTransformed2 = await context.Models1.Where(x => x.PartitionKey == "root").Where(x => x.MyProperty1 > 2).Select(x => x.MyProperty1).FirstOrDefaultAsync(); //Should return an int
var firstTransformed3 = await context.Models1.Where(x => x.PartitionKey == "root").Where(x => x.MyProperty1 > 2).Select(x => new TestTransformAndSelect(x.MyProperty1, x.MyProperty2)).FirstOrDefaultAsync(); //Should return a record with only these two props
var firstTransformed4 = await context.Models1.Where(x => x.PartitionKey == "root").Where(x => x.MyProperty1 > 2).Select(x => new TestTransformAndSelect(x.MyProperty1 + 1, x.MyProperty2 + "_test")).FirstOrDefaultAsync(); //Should return a record with only these two props and included transformations
var firstTransformed5 = await context.Models1.Where(x => x.PartitionKey == "root").Where(x => x.MyProperty1 > 2).Select(x => x.MyProperty1 + 1 + x.MyProperty2 + "_test").FirstOrDefaultAsync(); //Should return a concatted string
var firstTransformed6 = await context.Models1.Where(x => x.PartitionKey == "root").Where(x => x.MyProperty1 > 2).Select(x => TestTransformAndSelect.Map(x.MyProperty1, x.MyProperty2)).FirstOrDefaultAsync(); //Should return a record with only these two props and included transformations
var firstTransformed7 = await context.Models1.Where(x => x.PartitionKey == "root").Where(x => x.MyProperty1 > 2).Select(x => x.Map()).FirstOrDefaultAsync(); //Should at least work but gets everything
var firstTransformed8 = await context.Models1.Where(x => x.PartitionKey == "root").Where(x => x.MyProperty1 > 2).Select(x => new TestTransformAndSelectWithGuid(x.MyProperty1, x.MyProperty2, Guid.Parse(x.RowKey))).FirstOrDefaultAsync(); //Should only get 3 props and transform
var firstTransformed9 = await context.Models1.Where(x => x.PartitionKey == "root").Where(x => x.MyProperty1 > 2).Select(x => new TestTransformAndSelectWithGuid(x.MyProperty1, "test", Guid.NewGuid())).FirstOrDefaultAsync(); //Should only get one prop and transform
var firstTransformed10 = await context.Models1.Where(x => x.PartitionKey == "root").Where(x => x.MyProperty1 > 2).Select(x => new NestedTestTransformAndSelect(Guid.Parse(x.RowKey), new(x.MyProperty1 + 1 * 4, x.MyProperty2 + "_test"))).FirstOrDefaultAsync(); //Should only get 3 props and transform into a nested object
var firstTransformed11 = await context.Models1.Where(x => x.PartitionKey == "root").Where(x => x.MyProperty1 > 2).Select(x => new StringFormatted($"{x.RowKey} - {x.MyProperty1 + 1 * 4}, {x.MyProperty2}_test")).FirstOrDefaultAsync(); //Should only get 3 props and transform into a string
var firstTransformed12 = await context.Models1.Select(x => new StringFormatted2($"{x.RowKey} - {x.MyProperty1 + 1 * 4}, {x.MyProperty2}_test", null, x.Timestamp.GetValueOrDefault())).ToListAsync(); //Should only get 4 props and transform into a string
var firstTransformed13 = await context.Models1.Select(x => new StringFormatted2(string.Format("{0} - {1}, {2}_test {3}", x.RowKey, x.MyProperty1 + (1 * 4), x.MyProperty2, x.Timestamp.GetValueOrDefault()), null, x.Timestamp.GetValueOrDefault())).ToListAsync(); //Should only get 4 props and transform into a string
var unknown = context.GetTableSet<Model>("randomname") ?? throw new Exception("Should not be null"); //Gives a tableset that wasn't defined on the original DbContext
var exists = await context.Models1.ExistsIn(x => x.MyProperty1, new[] { 1, 2, 3, 4 }).Where(x => x.PartitionKey == "root").Where(x => x.MyProperty1 < 3).ToListAsync();
var single = await context.Models1.Where(x => x.PartitionKey == "root").Where(x => x.MyProperty1 > 2).SelectFields(x => x.MyProperty2).SingleAsync(); //Should throw

namespace TableStorage.Tests.Models
{
#nullable disable

    [TableSetModel]
    public partial class Model
    {
        public int MyProperty1 { get; set; }
        public string MyProperty2 { get; set; }
    }

    [TableSetModel]
    public partial class Model2
    {
        public int MyProperty1 { get; set; }
        public string MyProperty2 { get; set; }
        public System.DateTime? MyProperty3 { get; set; }
        public System.Guid? MyProperty4 { get; set; }
        public System.DateTime MyProperty5 { get; set; }
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
}

namespace TableStorage.Tests.Contexts
{
    using TableStorage.Tests.Models;

    [TableContext]
    public partial class MyTableContext
    {
        public TableSet<Model> Models1 { get; set; }
        public TableSet<Model2> Models2 { get; private set; }
        public TableSet<Model> Models3 { get; init; }
        public TableSet<Model> Models4 { get; init; }
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