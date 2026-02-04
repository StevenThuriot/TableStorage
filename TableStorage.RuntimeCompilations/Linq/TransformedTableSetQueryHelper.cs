using System.Linq.Expressions;

namespace TableStorage.Linq;

internal sealed class TransformedTableSetQueryHelper<T, TResult>(ITableSetQueryHelper<T> tableSetQueryHelper, LazyExpression<T, TResult> transform) : ITableEnumerable<TResult>, ITableSetQueryHelper
    where T : class, ITableEntity, new()
{
    private readonly ITableSetQueryHelper<T> _helper = tableSetQueryHelper;
    private readonly LazyExpression<T, TResult> _transform = transform;

    public async Task<TResult> FirstAsync(CancellationToken token = default)
    {
        T result = await _helper.FirstAsync(token);
        return _transform.Invoke(result);
    }

    public async Task<TResult?> FirstOrDefaultAsync(CancellationToken token = default)
    {
        T? result = await _helper.FirstOrDefaultAsync(token);

        if (result is null)
        {
            return default;
        }

        return _transform.Invoke(result);
    }

    public async Task<TResult> SingleAsync(CancellationToken token = default)
    {
        T result = await _helper.SingleAsync(token);
        return _transform.Invoke(result);
    }

    public async Task<TResult?> SingleOrDefaultAsync(CancellationToken token = default)
    {
        T? result = await _helper.SingleOrDefaultAsync(token);

        if (result is null)
        {
            return default;
        }

        return _transform.Invoke(result);
    }

    public async IAsyncEnumerator<TResult> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        Func<T, TResult> invoker = _transform.Value;

        await foreach (T item in _helper)
        {
            yield return invoker(item);
        }
    }

    public Expression? GetFilter() => _helper.GetFilter();
    public bool HasFields() => _helper.HasFields();
    public Task UpdateAsync(ITableEntity entity, CancellationToken cancellationToken) => _helper.UpdateAsync(entity, cancellationToken);
    public Task SubmitTransactionAsync(IEnumerable<TableTransactionAction> transactionActions, TransactionSafety transactionSafety, CancellationToken cancellationToken = default) => _helper.SubmitTransactionAsync(transactionActions, transactionSafety, cancellationToken);
    public ModelInfo ModelInfo => _helper.ModelInfo;
    public ModelInfo GetModelInfo<TType>() => _helper.GetModelInfo<TType>();
    public ModelInfo GetModelInfo(Type type) => _helper.GetModelInfo(type);
}