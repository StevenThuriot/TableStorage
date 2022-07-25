// See https://aka.ms/new-console-template for more information
using Azure;
using Azure.Data.Tables;
using Microsoft.Extensions.DependencyInjection;
using TableStorage;
using TableStorage.Linq;

ServiceCollection services = new();
services.AddTableContext<MyTableContext>("UseDevelopmentStorage=true");
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

var list1 = await context.Models1.Where(x => x.PartitionKey == "root").Where(x => x.MyProperty1 > 2).Take(3).ToListAsync(); //Should not contain more than 3 items with all properties filled in
var list2 = await context.Models1.Where(x => x.PartitionKey == "root").Where(x => x.MyProperty1 > 2).Take(3).DistinctBy(x => x.MyProperty1).ToListAsync(); //Should contain 1 item with all properties filled in
var list3 = await context.Models1.Where(x => x.PartitionKey == "root").Where(x => x.MyProperty1 > 2).DistinctBy(x => x.MyProperty2, StringComparer.OrdinalIgnoreCase).Take(3).ToListAsync(); //Should contain 1 item with all properties filled in
var first1 = await context.Models1.Where(x => x.PartitionKey == "root").Where(x => x.MyProperty1 > 2).Select(x => new { x.MyProperty2, x.MyProperty1 }).FirstOrDefaultAsync(); //Should only fill in the selected properties
var first2 = await context.Models1.Where(x => x.PartitionKey == "root").Where(x => x.MyProperty1 > 2).Select(x => x.MyProperty1).FirstOrDefaultAsync(); //Should only fill in MyProperty1
var unknown = context.GetTableSet<Model>("randomname") ?? throw new Exception("Should not be null"); //Gives a tableset that wasn't defined on the original DbContext
var single = await context.Models1.Where(x => x.PartitionKey == "root").Where(x => x.MyProperty1 > 2).Select(x => x.MyProperty2).SingleAsync(); //Should throw

#nullable disable

public class Model : ITableEntity
{
    public string PartitionKey { get; set; }
    public string RowKey { get; set; }
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public int MyProperty1 { get; set; }
    public string MyProperty2 { get; set; }
}

public class MyTableContext : TableContext
{
    public TableSet<Model> Models1 { get; set; }
    public TableSet<Model> Models2 { get; private set; }
    //public TableSet<Model> Models3 { get; } -- This throws because we're unable to set it
    public TableSet<Model> Models3 { get; init; } // This is fine, though.
}