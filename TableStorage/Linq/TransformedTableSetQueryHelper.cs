using FastExpressionCompiler;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace TableStorage.Linq;

internal class TransformedTableSetQueryHelper<T, TResult> : ITableEnumerable<TResult>
    where T : class, ITableEntity, new()
{
    private readonly TableSetQueryHelper<T> _helper;
    private readonly Func<T, TResult> _transform;

    public TransformedTableSetQueryHelper(TableSetQueryHelper<T> tableSetQueryHelper, Expression<Func<T, TResult>> transform)
    {
        _helper = tableSetQueryHelper;
        _transform = transform.CompileFast();
    }

    public async Task<TResult> FirstAsync(CancellationToken token = default)
    {
        var result = await _helper.FirstAsync(token);
        return _transform(result);
    }

    public async Task<TResult?> FirstOrDefaultAsync(CancellationToken token = default)
    {
        var result = await _helper.FirstOrDefaultAsync(token);

        if (result is null)
        {
            return default;
        }

        return _transform(result);
    }

    public async Task<TResult> SingleAsync(CancellationToken token = default)
    {
        var result = await _helper.SingleAsync(token);
        return _transform(result);
    }

    public async Task<TResult?> SingleOrDefaultAsync(CancellationToken token = default)
    {
        var result = await _helper.SingleOrDefaultAsync(token);

        if (result is null)
        {
            return default;
        }

        return _transform(result);
    }

    public async IAsyncEnumerable<TResult> ToAsyncEnumerableAsync([EnumeratorCancellation] CancellationToken token = default)
    {
        await foreach (var item in _helper.ToAsyncEnumerableAsync(token))
        {
            yield return _transform(item);
        }
    }

    public async Task<List<TResult>> ToListAsync(CancellationToken token = default)
    {
        List<TResult> result = _helper.Amount.HasValue ? new(_helper.Amount.GetValueOrDefault()) : new();

        await foreach (var item in ToAsyncEnumerableAsync(token))
        {
            result.Add(item);
        }

        return result;
    }
}
