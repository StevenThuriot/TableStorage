using Microsoft.Extensions.DependencyInjection;

namespace TableStorage;
public static class TableStorageSetup
{
    public static IServiceCollection AddTableContext<T>(this IServiceCollection services, string connectionString)
        where T : TableContext, new()
    {
        return services.AddSingleton<TableStorageFactory>(_ => new(connectionString))
                       .AddSingleton(s =>
                       {
                           T context = new();
                           context.Init(s.GetRequiredService<TableStorageFactory>());
                           return context;
                       });
    }
}
