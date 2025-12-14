
namespace TableStorage.Fluent;

internal interface IFluentTableEntity : IDictionary<string, object>, ITableEntity
{
    public Type GetActualType();
    public FluentBackingType GetBackingType();
    public object GetValue();
    public object? GetValueOrDefault();
}