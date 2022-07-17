using System.Linq.Expressions;

namespace TableStorage.Linq;

public static class TableQueryHelper
{
    public static ISelectedTableQueryable<T> Select<T, TResult>(this TableSet<T> table, Expression<Func<T, TResult>> selector)
        where T : class, ITableEntity, new()
    {
        TableSetQueryHelper<T> helper = new(table);
        helper.SetFields(selector);
        return helper;
    }

    public static ITakenTableQueryable<T> Take<T>(this TableSet<T> table, int amount)
        where T : class, ITableEntity, new()
    {
        TableSetQueryHelper<T> helper = new(table);
        helper.Amount = amount;
        return helper;
    }

    public static IFilteredTableQueryable<T> Where<T>(this TableSet<T> table, Expression<Func<T, bool>> predicate)
        where T : class, ITableEntity, new()
    {
        TableSetQueryHelper<T> helper = new(table);
        helper.AddFilter(predicate);
        return helper;
    }

    private class TableSetQueryHelper<T> : ITableQueryable<T>, ISelectedTableQueryable<T>, ITakenTableQueryable<T>, IFilteredTableQueryable<T>, ISelectedTakenTableQueryable<T>
        where T : class, ITableEntity, new()
    {
        private readonly TableSet<T> _table;

        private List<string>? _fields;
        private Expression<Func<T, bool>>? _filter;
        public int? Amount { get; set; }

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

            if (!amount.HasValue)
            {
                return query;
            }

            return IterateWithAmount();

            async IAsyncEnumerable<T> IterateWithAmount()
            {
                int count = 0;
                await foreach (var item in query)
                {
                    yield return item;

                    if (++count == amount.GetValueOrDefault())
                    {
                        yield break;
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
            return GetAsyncEnumerable(Amount, token);
        }
        public async Task<List<T>> ToListAsync(CancellationToken token = default)
        {
            List<T> result = Amount.HasValue ? new(Amount.GetValueOrDefault()) : new();

            await foreach (var item in ToAsyncEnumerableAsync(token))
            {
                result.Add(item);
            }

            return result;
        }

        internal void SetFields<TResult>(Expression<Func<T, TResult>> exp)
        {
            if (exp.Body is not MemberExpression body)
            {
                if (exp.Body is UnaryExpression ubody && ubody.Operand is MemberExpression operand)
                {
                    _fields = new(1)
                    {
                        operand.Member.Name
                    };
                }
                else
                {
                    if (typeof(TResult).IsClass && typeof(TResult).Namespace is null)
                    {
                        //assume anonymous class
                        _fields = typeof(TResult).GetProperties().Select(x => x.Name).ToList();
                    }
                    else
                    {
                        throw new NotSupportedException("Select expression is not supported");
                    }
                }
            }
            else
            {
                _fields = new(1)
            {
                body.Member.Name
            };
            }
        }

        ISelectedTakenTableQueryable<T> ITakenTableQueryable<T>.Select<TResult>(Expression<Func<T, TResult>> selector)
        {
            SetFields(selector);
            return this;
        }

        ISelectedTableQueryable<T> IFilteredTableQueryable<T>.Select<TResult>(Expression<Func<T, TResult>> selector)
        {
            SetFields(selector);
            return this;
        }

        ISelectedTakenTableQueryable<T> ISelectedTableQueryable<T>.Take(int amount)
        {
            Amount = amount;
            return this;
        }

        ITakenTableQueryable<T> IFilteredTableQueryable<T>.Take(int amount)
        {
            Amount = amount;
            return this;
        }

        internal void AddFilter(Expression<Func<T, bool>> predicate)
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
        }

        ISelectedTableQueryable<T> ISelectedTableQueryable<T>.Where(Expression<Func<T, bool>> predicate)
        {
            AddFilter(predicate);
            return this;
        }

        ITakenTableQueryable<T> ITakenTableQueryable<T>.Where(Expression<Func<T, bool>> predicate)
        {
            AddFilter(predicate);
            return this;
        }

        IFilteredTableQueryable<T> IFilteredTableQueryable<T>.Where(Expression<Func<T, bool>> predicate)
        {
            AddFilter(predicate);
            return this;
        }

        ISelectedTakenTableQueryable<T> ISelectedTakenTableQueryable<T>.Where(Expression<Func<T, bool>> predicate)
        {
            AddFilter(predicate);
            return this;
        }
    }
}