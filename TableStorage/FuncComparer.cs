namespace TableStorage;

public static class FuncComparer
{
    public static FuncComparer<T, TResult> Create<T, TResult>(Func<T, TResult> selector, IEqualityComparer<TResult>? equalityComparer = null)
    {
        return new FuncComparer<T, TResult>(selector, equalityComparer);
    }
}

public sealed class FuncComparer<T, TResult>(Func<T, TResult> selector, IEqualityComparer<TResult>? equalityComparer = null) : IEqualityComparer<T>
{
    private readonly Func<T, TResult> _selector = selector;
    private readonly IEqualityComparer<TResult> _equalityComparer = equalityComparer ?? EqualityComparer<TResult>.Default;

    public bool Equals(T? x, T? y)
    {
        if (x is null)
        {
            return y is null;
        }

        return y is not null && _equalityComparer.Equals(_selector(x), _selector(y));
    }

    public int GetHashCode(T obj)
    {
        TResult? selection = _selector(obj);
        return selection is null ? 0 : selection.GetHashCode();
    }
}