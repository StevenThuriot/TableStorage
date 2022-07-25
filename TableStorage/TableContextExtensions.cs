using System.Collections.Concurrent;

namespace TableStorage;

public static class TableContextExtensions
{
    private static class TypedTableSetCache<T>
        where T : class, ITableEntity, new()
    {
        private static readonly ConcurrentDictionary<string, TableSet<T>> _sets = new(StringComparer.OrdinalIgnoreCase);

        public static TableSet<T> GetOrAdd(TableStorageFactory factory, string tableName, TableOptions options)
        {
            return _sets.GetOrAdd(tableName, name => new TableSet<T>(factory, name, options));
        }
    }

    public static TableSet<T> GetTableSet<T>(this TableContext context, string tableName)
        where T : class, ITableEntity, new()
    {
        var factory = context.Factory ?? throw new Exception("The TableContext was not properly initialized");
        return TypedTableSetCache<T>.GetOrAdd(factory, tableName, context.Options);
    }
}