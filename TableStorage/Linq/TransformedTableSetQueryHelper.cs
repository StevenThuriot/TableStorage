using FastExpressionCompiler;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace TableStorage.Linq;

internal class TransformedTableSetQueryHelper<T, TResult>(TableSetQueryHelper<T> tableSetQueryHelper, Expression<Func<T, TResult>> transform) : ITableEnumerable<TResult>
    where T : class, ITableEntity, new()
{
    private readonly TableSetQueryHelper<T> _helper = tableSetQueryHelper;
    private readonly Func<T, TResult> _transform = transform.CompileFast();

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

    public async IAsyncEnumerator<TResult> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        await foreach (var item in _helper)
        {
            yield return _transform(item);
        }
    }
}
