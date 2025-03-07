using System.Linq.Expressions;

namespace TableStorage.Linq;
internal static class Helpers
{
    public static Expression<Func<T, bool>> CreateExistsInFilter<T, TElement>(this Expression<Func<T, TElement>> predicate, IEnumerable<TElement> elements)
    {
        if (elements is null)
        {
            throw new ArgumentNullException(nameof(elements));
        }

        Expression filter = BuildFilterExpression();

        return Expression.Lambda<Func<T, bool>>(filter, predicate.Parameters);

        Expression BuildFilterExpression()
        {
            using IEnumerator<TElement> enumerator = elements.GetEnumerator();

            if (!enumerator.MoveNext())
            {
                return Expression.Constant(false);
            }

            BinaryExpression filter = GetFilterForElement();
            while (enumerator.MoveNext())
            {
                filter = Expression.OrElse(filter, GetFilterForElement());
            }

            return filter;

            BinaryExpression GetFilterForElement()
            {
                return Expression.Equal(predicate.Body, Expression.Constant(enumerator.Current));
            }
        }
    }

    public static Expression<Func<T, bool>> CreateNotExistsInFilter<T, TElement>(this Expression<Func<T, TElement>> predicate, IEnumerable<TElement> elements)
    {
        if (elements is null)
        {
            throw new ArgumentNullException(nameof(elements));
        }

        Expression filter = BuildFilterExpression();

        return Expression.Lambda<Func<T, bool>>(filter, predicate.Parameters);

        Expression BuildFilterExpression()
        {
            using IEnumerator<TElement> enumerator = elements.GetEnumerator();

            if (!enumerator.MoveNext())
            {
                return Expression.Constant(true);
            }

            BinaryExpression filter = GetFilterForElement();
            while (enumerator.MoveNext())
            {
                filter = Expression.AndAlso(filter, GetFilterForElement());
            }

            return filter;

            BinaryExpression GetFilterForElement()
            {
                return Expression.NotEqual(predicate.Body, Expression.Constant(enumerator.Current));
            }
        }
    }

    public static async Task<T> FirstAsync<T>(IAsyncEnumerable<T> table, CancellationToken token)
    {
        T? result = await FirstOrDefaultAsync(table, token);
        return result ?? throw new InvalidOperationException("No element satisfies the condition in predicate. -or- The source sequence is empty.");
    }

    public static async Task<T?> FirstOrDefaultAsync<T>(IAsyncEnumerable<T> table, CancellationToken token)
    {
        await foreach (T? item in table.WithCancellation(token))
        {
            return item;
        }

        return default;
    }

    public static async Task<T> SingleAsync<T>(IAsyncEnumerable<T> table, CancellationToken token = default)
    {
        T? result = await SingleOrDefaultAsync(table, token);
        return result ?? throw new InvalidOperationException("No element satisfies the condition in predicate. -or- The source sequence is empty.");
    }

    public static async Task<T?> SingleOrDefaultAsync<T>(IAsyncEnumerable<T> table, CancellationToken token = default)
    {
        T? result = default;
        bool gotOne = false;

        await foreach (T? item in table.WithCancellation(token))
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
}
