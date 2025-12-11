using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using TableStorage.Fluent;

namespace TableStorage.Linq;

public static class FluentTableQueryHelper
{
    internal static Expression<Func<FluentPartitionTableEntity<T1, T2>, bool>> FakeExpression<T1, T2>(LambdaExpression predicate)
        where T1 : class, IDictionary<string, object>, ITableEntity, new()
        where T2 : class, IDictionary<string, object>, ITableEntity, new()
    {
        return FakeExpression<T1, T2, bool>(predicate);
    }
    
    internal static Expression<Func<FluentPartitionTableEntity<T1, T2>, TResult>> FakeExpression<T1, T2, TResult>(LambdaExpression predicate)
        where T1 : class, IDictionary<string, object>, ITableEntity, new()
        where T2 : class, IDictionary<string, object>, ITableEntity, new()
    {
        ParameterExpression param = Expression.Parameter(typeof(FluentPartitionTableEntity<T1, T2>), "e");
        return Expression.Lambda<Func<FluentPartitionTableEntity<T1, T2>, TResult>>(predicate.Body, param);
    }
    
    extension<T1, T2>(TableSet<FluentPartitionTableEntity<T1, T2>> table)
        where T1 : class, IDictionary<string, object>, ITableEntity, new()
        where T2 : class, IDictionary<string, object>, ITableEntity, new()
    {
        public Task<FluentPartitionTableEntity<T1, T2>?> FindAsync(string rowKey, CancellationToken token = default)
        {
            Expression<Func<FluentPartitionTableEntity<T1, T2>, bool>> predicate = Helpers.CreateFindPredicate<FluentPartitionTableEntity<T1, T2>>(FluentPartitionTableEntity<T1, T2>.Discriminator, rowKey, table.PartitionKeyProxy, table.RowKeyProxy);
            return table.Where(predicate).FirstOrDefaultAsync(token);
        }

        public IFilteredTableQueryable<FluentPartitionTableEntity<T1, T2>> FindAsync(params IReadOnlyList<string> rowKeys)
        {
            var keys = rowKeys.Select(rk => (partitionKey: FluentPartitionTableEntity<T1, T2>.Discriminator, rowKey: rk)).ToList();
            Expression<Func<FluentPartitionTableEntity<T1, T2>, bool>> predicate = Helpers.CreateFindPredicate<FluentPartitionTableEntity<T1, T2>>(keys, table.PartitionKeyProxy, table.RowKeyProxy);
            return table.Where(predicate);
        }

        public IFilteredTableQueryable<FluentPartitionTableEntity<T1, T2>> Where(Expression<Func<T1, bool>> predicate)
        {
            return TableSetQueryHelper.From(table).AddFilter(FakeExpression<T1, T2>(predicate));
        }

        public IFilteredTableQueryable<FluentPartitionTableEntity<T1, T2>> Where(Expression<Func<T2, bool>> predicate)
        {
            return TableSetQueryHelper.From(table).AddFilter(FakeExpression<T1, T2>(predicate));
        }

        public ISelectedTableQueryable<FluentPartitionTableEntity<T1, T2>> SelectFields<TResult>(Expression<Func<T1, TResult>> selector)
        {
            var fakeSelector = FakeExpression<T1, T2>(selector);
            return TableSetQueryHelper.From(table).SetFields(fakeSelector);
        }

        public ISelectedTableQueryable<FluentPartitionTableEntity<T1, T2>> SelectFields<TResult>(Expression<Func<T2, TResult>> selector)
        {
            var fakeSelector = FakeExpression<T1, T2>(selector);
            return TableSetQueryHelper.From(table).SetFields(fakeSelector);
        }

        public IFilteredTableQueryable<FluentPartitionTableEntity<T1, T2>> ExistsIn<TElement>(Expression<Func<T1, TElement>> predicate, IEnumerable<TElement> elements)
        {
            return TableSetQueryHelper.From(table).AddExistsInFilter(FakeExpression<T1, T2, TElement>(predicate), elements);
        }

        public IFilteredTableQueryable<FluentPartitionTableEntity<T1, T2>> ExistsIn<TElement>(Expression<Func<T2, TElement>> predicate, IEnumerable<TElement> elements)
        {
            return TableSetQueryHelper.From(table).AddExistsInFilter(FakeExpression<T1, T2, TElement>(predicate), elements);
        }

        public IFilteredTableQueryable<FluentPartitionTableEntity<T1, T2>> NotExistsIn<TElement>(Expression<Func<T1, TElement>> predicate, IEnumerable<TElement> elements)
        {
            return TableSetQueryHelper.From(table).AddNotExistsInFilter(FakeExpression<T1, T2, TElement>(predicate), elements);
        }

        public IFilteredTableQueryable<FluentPartitionTableEntity<T1, T2>> NotExistsIn<TElement>(Expression<Func<T2, TElement>> predicate, IEnumerable<TElement> elements)
        {
            return TableSetQueryHelper.From(table).AddNotExistsInFilter(FakeExpression<T1, T2, TElement>(predicate), elements);
        }
        
        public IFilteredTableQueryable<T1> WhereFirstType()
        {
            Expression<Func<T1, bool>> filter = x => x.PartitionKey == typeof(T1).Name;
            TableSetQueryHelper<FluentPartitionTableEntity<T1, T2>> helper = TableSetQueryHelper.From(table).AddFilter(FakeExpression<T1, T2>(filter));
            return new FluentTableSetQueryHelper<T1, T1, T2>(helper);
        }
        
        public IFilteredTableQueryable<T2> WhereSecondType()
        {
            Expression<Func<T2, bool>> filter = x => x.PartitionKey == typeof(T2).Name;
            TableSetQueryHelper<FluentPartitionTableEntity<T1, T2>> helper = TableSetQueryHelper.From(table).AddFilter(FakeExpression<T1, T2>(filter));
            return new FluentTableSetQueryHelper<T2, T1, T2>(helper);
        }
    }
}
    
internal sealed class FluentTableSetQueryHelper<T, T1, T2>(TableSetQueryHelper<FluentPartitionTableEntity<T1, T2>> helper) :
    IAsyncEnumerable<T>,
    ISelectedTableQueryable<T>,
    ITakenTableQueryable<T>,
    IFilteredTableQueryable<T>,
    ISelectedTakenTableQueryable<T>,
    ITableSetQueryHelper
    where T : class, IDictionary<string, object>, ITableEntity, new() // T is either T1 or T2. We've already filtered by Type before this helper is created
    where T1 : class, IDictionary<string, object>, ITableEntity, new()
    where T2 : class, IDictionary<string, object>, ITableEntity, new()
{
    private readonly TableSetQueryHelper<FluentPartitionTableEntity<T1, T2>> _typedHelper = helper;
    private ITableSetQueryHelper Helper => _typedHelper;
    
    public async IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken)
    {
        await foreach (FluentPartitionTableEntity<T1, T2> entity in _typedHelper.WithCancellation(cancellationToken))
        {
            yield return Unsafe.As<T>(entity.GetRawValue());
        }
    }

    public Task<int> BatchDeleteAsync(CancellationToken token = default) => _typedHelper.BatchDeleteAsync(token);

    public Task<int> BatchDeleteTransactionAsync(CancellationToken token = default) => _typedHelper.BatchDeleteTransactionAsync(token);

    public async Task<T> FirstAsync(CancellationToken token = default) => Unsafe.As<T>((await _typedHelper.FirstAsync(token)).GetRawValue());

    public async Task<T?> FirstOrDefaultAsync(CancellationToken token = default) => Unsafe.As<T>((await _typedHelper.FirstOrDefaultAsync(token))?.GetRawValue());

    public async Task<T> SingleAsync(CancellationToken token = default) => Unsafe.As<T>((await _typedHelper.SingleAsync(token)).GetRawValue());

    public async Task<T?> SingleOrDefaultAsync(CancellationToken token = default) => Unsafe.As<T>((await _typedHelper.SingleOrDefaultAsync(token))?.GetRawValue());

    ISelectedTableQueryable<T> IFilteredTableQueryable<T>.SelectFields<TResult>(Expression<Func<T, TResult>> selector)
    {
        Helper.SetFields(selector);
        return this;
    }

    public ITableSetQueryHelper SetFields(IEnumerable<string> fields) 
    {
        Helper.SetFields(fields);
        return this;
    }

    ISelectedTakenTableQueryable<T> ITakenTableQueryable<T>.SelectFields<TResult>(Expression<Func<T, TResult>> selector) 
    {
        Helper.SetFields(selector);
        return this;
    }

    public ITableSetQueryHelper SetFields<T3, TResult>(Expression<Func<T3, TResult>> exp, bool throwIfNoArgumentsFound = true) where T3 : class, ITableEntity, new() 
    {
        Helper.SetFields(exp, throwIfNoArgumentsFound);
        return this;
    }

    ITakenTableQueryable<T> IFilteredTableQueryable<T>.Take(int amount) 
    {
        Helper.SetAmount(amount);
        return this;
    }

    ISelectedTakenTableQueryable<T> ISelectedTableQueryable<T>.Take(int amount) 
    {
        Helper.SetAmount(amount);
        return this;
    }

    public ITableSetQueryHelper SetAmount(int amount) 
    {
        Helper.SetAmount(amount);
        return this;
    }

    IFilteredTableQueryable<T> IFilteredTableQueryable<T>.Where(Expression<Func<T, bool>> predicate)
    {
        Helper.AddFilter(predicate);
        return this;
    }

    ISelectedTakenTableQueryable<T> ISelectedTakenTableQueryable<T>.Where(Expression<Func<T, bool>> predicate)
    {
        Helper.AddFilter(predicate);
        return this;
    }

    ITakenTableQueryable<T> ITakenTableQueryable<T>.Where(Expression<Func<T, bool>> predicate)
    {
        Helper.AddFilter(predicate);
        return this;
    }

    ISelectedTableQueryable<T> ISelectedTableQueryable<T>.Where(Expression<Func<T, bool>> predicate)
    {
        Helper.AddFilter(predicate);
        return this;
    }

    public ITableSetQueryHelper AddFilter<T3>(Expression<Func<T3, bool>> predicate) 
    {
        Helper.AddFilter(predicate);
        return this;
    }

    ISelectedTakenTableQueryable<T> ISelectedTakenTableQueryable<T>.ExistsIn<TElement>(Expression<Func<T, TElement>> predicate, IEnumerable<TElement> elements)
    {
        Helper.AddExistsInFilter(predicate, elements);
        return this;
    }

    IFilteredTableQueryable<T> IFilteredTableQueryable<T>.ExistsIn<TElement>(Expression<Func<T, TElement>> predicate, IEnumerable<TElement> elements)
    {
        Helper.AddExistsInFilter(predicate, elements);
        return this;
    }

    ITakenTableQueryable<T> ITakenTableQueryable<T>.ExistsIn<TElement>(Expression<Func<T, TElement>> predicate, IEnumerable<TElement> elements) 
    {
        Helper.AddExistsInFilter(predicate, elements);
        return this;
    }

    ISelectedTableQueryable<T> ISelectedTableQueryable<T>.ExistsIn<TElement>(Expression<Func<T, TElement>> predicate, IEnumerable<TElement> elements) 
    {
        Helper.AddExistsInFilter(predicate, elements);
        return this;
    }

    public ITableSetQueryHelper AddExistsInFilter<T3, TElement>(Expression<Func<T3, TElement>> predicate, IEnumerable<TElement> elements) 
    {
        Helper.AddExistsInFilter(predicate, elements);
        return this;
    }

    ISelectedTakenTableQueryable<T> ISelectedTakenTableQueryable<T>.NotExistsIn<TElement>(Expression<Func<T, TElement>> predicate, IEnumerable<TElement> elements)
    {
        Helper.AddNotExistsInFilter(predicate, elements);
        return this;
    }

    IFilteredTableQueryable<T> IFilteredTableQueryable<T>.NotExistsIn<TElement>(Expression<Func<T, TElement>> predicate, IEnumerable<TElement> elements) 
    {
        Helper.AddNotExistsInFilter(predicate, elements);
        return this;
    }

    ITakenTableQueryable<T> ITakenTableQueryable<T>.NotExistsIn<TElement>(Expression<Func<T, TElement>> predicate, IEnumerable<TElement> elements) 
    {
        Helper.AddNotExistsInFilter(predicate, elements);
        return this;
    }

    ISelectedTableQueryable<T> ISelectedTableQueryable<T>.NotExistsIn<TElement>(Expression<Func<T, TElement>> predicate, IEnumerable<TElement> elements) 
    {
        Helper.AddNotExistsInFilter(predicate, elements);
        return this;
    }

    public ITableSetQueryHelper AddNotExistsInFilter<T3, TElement>(Expression<Func<T3, TElement>> predicate, IEnumerable<TElement> elements)  
    {
        Helper.AddNotExistsInFilter(predicate, elements);
        return this;
    }
}