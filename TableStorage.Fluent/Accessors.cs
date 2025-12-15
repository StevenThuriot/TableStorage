#if DEBUG
using System.Linq.Expressions;
using System.Reflection;
using TableStorage.Linq;

namespace TableStorage.Fluent;

internal static class Accessors
{
    private static readonly MethodInfo s_method = typeof(TableClient).GetMethod("Bind", BindingFlags.Static | BindingFlags.NonPublic, null, CallingConventions.Standard, [typeof(Expression)], []);

    public static string Bind(Expression expression)
    {
        return (string)s_method.Invoke(null, [expression])!;
    }

    public static string ToQueryString<T>(this ITableEnumerable<T> tableEnumerable)
    {
        if (tableEnumerable is not ITableSetQueryHelper helper)
        {
            throw new NotSupportedException();
        }

        Expression? filter = helper.GetFilter();

        if (filter is null)
        {
            return "";
        }

        return Bind(filter);
    }
}
#endif