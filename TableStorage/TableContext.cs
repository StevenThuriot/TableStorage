using System.Reflection;

namespace TableStorage;

public abstract class TableContext
{
    internal void Init(TableStorageFactory factory)
    {
        TableOptions options = new();

        Configure(options);

        var tableSetProperties = GetType().GetProperties()
                                 .Where(x => x.CanWrite && x.PropertyType.IsGenericType && x.PropertyType.GetGenericTypeDefinition() == typeof(TableSet<>));

        foreach (var property in tableSetProperties)
        {
            var tableSet = Activator.CreateInstance(property.PropertyType, BindingFlags.NonPublic | BindingFlags.Instance, null, new object[] { factory, property.Name, options }, System.Globalization.CultureInfo.CurrentCulture);
            property.SetValue(this, tableSet);
        }
    }

    protected virtual void Configure(TableOptions options)
    {
        options.AutoTimestamps = true;
        options.TableMode = TableUpdateMode.Merge;
    }
}