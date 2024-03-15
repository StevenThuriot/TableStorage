using System.Runtime.CompilerServices;

namespace TableStorage;

internal class AsyncLazy<T>(Func<Task<T>> taskFactory) : Lazy<Task<T>>(() => taskFactory())
{
    public TaskAwaiter<T> GetAwaiter() => Value.GetAwaiter();
}
