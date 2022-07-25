using System.Reflection;

namespace TableStorage;

public abstract class TableContext
{
    internal TableStorageFactory? Factory { get; private set; }
    internal TableOptions Options { get; } = new();

    internal void Init(TableStorageFactory factory)
    {
        Factory = factory;
        Configure(Options);

        var tableSetProperties = GetType().GetProperties()
                                          .Where(x => x.PropertyType.IsGenericType &&
                                                      x.PropertyType.GetGenericTypeDefinition() == typeof(TableSet<>));

        foreach (var property in tableSetProperties)
        {
            if (!property.CanWrite)
            {
                throw new Exception($"Unable to set value for {property.Name} due to missing setter");
            }

            var tableSet = Activator.CreateInstance(property.PropertyType, BindingFlags.NonPublic | BindingFlags.Instance, null, new object[] { factory, property.Name, Options }, System.Globalization.CultureInfo.CurrentCulture);
            property.SetValue(this, tableSet);
        }
    }

    protected virtual void Configure(TableOptions options)
    {
        options.AutoTimestamps = true;
        options.TableMode = TableUpdateMode.Merge;
    }
}