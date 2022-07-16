using System.Runtime.CompilerServices;

namespace TableStorage;

internal class AsyncLazy<T> : Lazy<Task<T>>
{
    public AsyncLazy(Func<T> valueFactory)
        : base(() => Task.FromResult(valueFactory()))
    { }

    public AsyncLazy(Func<Task<T>> taskFactory)
        : base(() => taskFactory())
    { }

    public TaskAwaiter<T> GetAwaiter() { return Value.GetAwaiter(); }
}
