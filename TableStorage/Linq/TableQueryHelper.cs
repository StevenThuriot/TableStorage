using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;

namespace TableStorage.Linq;

public static class TableQueryHelper
{
    public static ISelectedTableQueryable<T> Select<T, TResult>(this TableSet<T> table, Expression<Func<T, TResult>> selector)
        where T : class, ITableEntity, new()
    {
        return new TableSetQueryHelper<T>(table).SetFields(selector);
    }

    public static ITakenTableQueryable<T> Take<T>(this TableSet<T> table, int amount)
        where T : class, ITableEntity, new()
    {
        return new TableSetQueryHelper<T>(table).SetAmount(amount);
    }

    public static IFilteredTableQueryable<T> Where<T>(this TableSet<T> table, Expression<Func<T, bool>> predicate)
        where T : class, ITableEntity, new()
    {
         return new TableSetQueryHelper<T>(table).AddFilter(predicate);
    }

    public static IDistinctedTableQueryable<T> DistinctBy<T, TResult>(this TableSet<T> table, Func<T, TResult> selector, IEqualityComparer<TResult>? equalityComparer = null)
        where T : class, ITableEntity, new()
    {
        return new TableSetQueryHelper<T>(table).SetDistinction(selector, equalityComparer);
    }

    private class TableSetQueryHelper<T> :
        ITableQueryable<T>,
        ISelectedTableQueryable<T>,
        ITakenTableQueryable<T>,
        IFilteredTableQueryable<T>,
        ISelectedTakenTableQueryable<T>,
        IDistinctedTableQueryable<T>,
        ISelectedDistinctedTableQueryable<T>,
        ITakenDistinctedTableQueryable<T>,
        ISelectedTakenDistinctedTableQueryable<T>
        where T : class, ITableEntity, new()
    {
        private readonly TableSet<T> _table;

        private List<string>? _fields;
        private Expression<Func<T, bool>>? _filter;
        private int? _amount;
        private Func<T, bool>? _distinct;

        public TableSetQueryHelper(TableSet<T> table)
        {
            _table = table;
        }

        public async Task<T> FirstAsync(CancellationToken token = default)
        {
            T? result = await FirstOrDefaultAsync(token);
            return result ?? throw new InvalidOperationException("No element satisfies the condition in predicate. -or- The source sequence is empty.");
        }

        private IAsyncEnumerable<T> GetAsyncEnumerable(int? amount, CancellationToken token)
        {
            var query = _filter is null
                        ? _table.QueryAsync((string?)null, null, _fields, token)
                        : _table.QueryAsync(_filter, null, _fields, token);

            if (_distinct is not null)
            {
                query = IterateWithDistinct(query);
            }

            if (_amount.HasValue)
            {
                query = IterateWithAmount(query);
            }

            return query;

            async IAsyncEnumerable<T> IterateWithAmount(IAsyncEnumerable<T> values)
            {
                int count = amount.GetValueOrDefault();
                await foreach (var item in values)
                {
                    yield return item;

                    if (--count == 0)
                    {
                        yield break;
                    }
                }
            }

            async IAsyncEnumerable<T> IterateWithDistinct(IAsyncEnumerable<T> values)
            {
                await foreach (var item in values)
                {
                    if (_distinct(item))
                    {
                        yield return item;
                    }
                }
            }
        }

        public async Task<T?> FirstOrDefaultAsync(CancellationToken token = default)
        {
            await foreach (var item in GetAsyncEnumerable(1, token))
            {
                return item;
            }

            return default;
        }

        public async Task<T> SingleAsync(CancellationToken token = default)
        {
            var result = await SingleOrDefaultAsync(token);
            return result ?? throw new InvalidOperationException("No element satisfies the condition in predicate. -or- The source sequence is empty.");
        }

        public async Task<T?> SingleOrDefaultAsync(CancellationToken token = default)
        {
            T? result = default;
            bool gotOne = false;

            await foreach (var item in GetAsyncEnumerable(2, token))
            {
                if (gotOne)
                {
                    throw new InvalidOperationException("The input sequence contains more than one element.");
                }

                result = item;
                gotOne = true;
            }

            return result;
        }

        public IAsyncEnumerable<T> ToAsyncEnumerableAsync(CancellationToken token = default)
        {
            return GetAsyncEnumerable(_amount, token);
        }

        public async Task<List<T>> ToListAsync(CancellationToken token = default)
        {
            List<T> result = _amount.HasValue ? new(_amount.GetValueOrDefault()) : new();

            await foreach (var item in ToAsyncEnumerableAsync(token))
            {
                result.Add(item);
            }

            return result;
        }

        internal TableSetQueryHelper<T> SetFields<TResult>(Expression<Func<T, TResult>> exp)
        {
            _fields = exp.Body switch
            {
                MemberExpression body => new(1) { body.Member.Name },
                UnaryExpression ubody when ubody.Operand is MemberExpression operand => new(1) { operand.Member.Name },
                NewExpression newExpression => newExpression.Arguments.Cast<MemberExpression>().Select(x => x.Member.Name).ToList(), //assume anonymous class
                _ => throw new NotSupportedException("Select expression is not supported"),
            };

            return this;
        }

        ISelectedTakenTableQueryable<T> ITakenTableQueryable<T>.Select<TResult>(Expression<Func<T, TResult>> selector) => SetFields(selector);

        ISelectedTableQueryable<T> IFilteredTableQueryable<T>.Select<TResult>(Expression<Func<T, TResult>> selector) => SetFields(selector);

        ISelectedTakenTableQueryable<T> ITakenDistinctedTableQueryable<T>.Select<TResult>(Expression<Func<T, TResult>> selector) => SetFields(selector);

        ISelectedDistinctedTableQueryable<T> IDistinctedTableQueryable<T>.Select<TResult>(Expression<Func<T, TResult>> selector) => SetFields(selector);

        internal TableSetQueryHelper<T> SetAmount(int amount)
        {
            if (amount < 1)
            {
                throw new InvalidOperationException("Amount must be a strictly postive integer.");
            }

            _amount = amount;
            return this;
        }

        ISelectedTakenTableQueryable<T> ISelectedTableQueryable<T>.Take(int amount) => SetAmount(amount);

        ITakenTableQueryable<T> IFilteredTableQueryable<T>.Take(int amount) => SetAmount(amount);

        ISelectedTakenDistinctedTableQueryable<T> ISelectedDistinctedTableQueryable<T>.Take(int amount) => SetAmount(amount);

        ISelectedDistinctedTableQueryable<T> IDistinctedTableQueryable<T>.Take(int amount) => SetAmount(amount);

        internal TableSetQueryHelper<T> AddFilter(Expression<Func<T, bool>> predicate)
        {
            if (_filter is null)
            {
                _filter = predicate;
            }
            else
            {
                var invokedExpr = Expression.Invoke(predicate, _filter.Parameters);
                BinaryExpression combinedExpression = Expression.AndAlso(_filter.Body, invokedExpr);
                _filter = Expression.Lambda<Func<T, bool>>(combinedExpression, _filter.Parameters);
            }

            return this;
        }

        ISelectedTableQueryable<T> ISelectedTableQueryable<T>.Where(Expression<Func<T, bool>> predicate)
        {
            return AddFilter(predicate);
        }

        ITakenTableQueryable<T> ITakenTableQueryable<T>.Where(Expression<Func<T, bool>> predicate)
        {
            return AddFilter(predicate);
        }

        IFilteredTableQueryable<T> IFilteredTableQueryable<T>.Where(Expression<Func<T, bool>> predicate)
        {
            return AddFilter(predicate);
        }

        ISelectedTakenTableQueryable<T> ISelectedTakenTableQueryable<T>.Where(Expression<Func<T, bool>> predicate)
        {
            return AddFilter(predicate);
        }

        IDistinctedTableQueryable<T> IDistinctedTableQueryable<T>.Where(Expression<Func<T, bool>> predicate)
        {
            return AddFilter(predicate);
        }

        ISelectedDistinctedTableQueryable<T> ISelectedDistinctedTableQueryable<T>.Where(Expression<Func<T, bool>> predicate)
        {
            return AddFilter(predicate);
        }

        ITakenDistinctedTableQueryable<T> ITakenDistinctedTableQueryable<T>.Where(Expression<Func<T, bool>> predicate)
        {
            return AddFilter(predicate);
        }

        ISelectedTakenDistinctedTableQueryable<T> ISelectedTakenDistinctedTableQueryable<T>.Where(Expression<Func<T, bool>> predicate)
        {
            return AddFilter(predicate);
        }

        internal TableSetQueryHelper<T> SetDistinction<TResult>(Func<T, TResult> selector, IEqualityComparer<TResult>? equalityComparer)
        {
            var set = new HashSet<T>(new FuncComparer<TResult>(selector, equalityComparer));
            _distinct = new Func<T, bool>(set.Add);
            return this;
        }

        private class FuncComparer<TResult> : IEqualityComparer<T>
        {
            private readonly Func<T, TResult> _selector;
            private readonly IEqualityComparer<TResult> _equalityComparer;

            public FuncComparer(Func<T, TResult> selector, IEqualityComparer<TResult>? equalityComparer)
            {
                _selector = selector;
                _equalityComparer = equalityComparer ?? EqualityComparer<TResult>.Default;
            }

            public bool Equals(T? x, T? y)
            {
                if (x is null) return y is null;
                if (y is null) return false;
                return _equalityComparer.Equals(_selector(x), _selector(y));
            }

            public int GetHashCode([DisallowNull] T obj)
            {
                var selection = _selector(obj);
                return selection is null ? 0 : selection.GetHashCode();
            }
        }

        ISelectedDistinctedTableQueryable<T> ISelectedTableQueryable<T>.DistinctBy<TResult>(Func<T, TResult> selector, IEqualityComparer<TResult>? equalityComparer)
        {
            return SetDistinction(selector, equalityComparer);
        }

        ITakenDistinctedTableQueryable<T> ITakenTableQueryable<T>.DistinctBy<TResult>(Func<T, TResult> selector, IEqualityComparer<TResult>? equalityComparer)
        {
            return SetDistinction(selector, equalityComparer);
        }

        IDistinctedTableQueryable<T> IFilteredTableQueryable<T>.DistinctBy<TResult>(Func<T, TResult> selector, IEqualityComparer<TResult>? equalityComparer)
        {
            return SetDistinction(selector, equalityComparer);
        }

        ISelectedTakenDistinctedTableQueryable<T> ISelectedTakenTableQueryable<T>.DistinctBy<TResult>(Func<T, TResult> selector, IEqualityComparer<TResult>? equalityComparer)
        {
            return SetDistinction(selector, equalityComparer);
        }
    }
}