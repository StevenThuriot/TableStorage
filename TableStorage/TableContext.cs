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

        var contextType = GetType();
        var tableSetProperties = contextType.GetProperties()
                                          .Where(x => x.PropertyType.IsGenericType &&
                                                      x.PropertyType.GetGenericTypeDefinition() == typeof(TableSet<>));

        foreach (var property in tableSetProperties)
        {
            var tableSet = Activator.CreateInstance(property.PropertyType, BindingFlags.NonPublic | BindingFlags.Instance, null, new object[] { factory, property.Name, Options }, System.Globalization.CultureInfo.CurrentCulture);

            if (property.CanWrite)
            {
                property.SetValue(this, tableSet);
            }
            else
            {
                var backingField = contextType.GetField($"<{property.Name}>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);

                if (backingField is null)
                {
                    throw new Exception($"Unable to set value for {property.Name} due to missing setter");
                }

                backingField.SetValue(this, tableSet);
            }
        }
    }

    protected virtual void Configure(TableOptions options)
    {
        options.AutoTimestamps = true;
        options.TableMode = TableUpdateMode.Merge;
    }
}