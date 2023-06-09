﻿using System.Linq.Expressions;

namespace TableStorage.Linq;

public interface ICanTakeOneTableQueryable<T>
{
    Task<T> FirstAsync(CancellationToken token = default);
    Task<T?> FirstOrDefaultAsync(CancellationToken token = default);
    Task<T> SingleAsync(CancellationToken token = default);
    Task<T?> SingleOrDefaultAsync(CancellationToken token = default);
}

public interface ITableQueryable<T>
{
    Task<List<T>> ToListAsync(CancellationToken token = default);
    IAsyncEnumerable<T> ToAsyncEnumerableAsync(CancellationToken token = default);
}

public interface ITableEnumerable<T> : ICanTakeOneTableQueryable<T>, ITableQueryable<T> { }

public interface IFilteredTableQueryable<T> : ITableQueryable<T>, ICanTakeOneTableQueryable<T>
    where T : class, ITableEntity, new()
{
    ITableEnumerable<TResult> Select<TResult>(Expression<Func<T, TResult>> selector);
    ISelectedTableQueryable<T> SelectFields<TResult>(Expression<Func<T, TResult>> selector);
    ITakenTableQueryable<T> Take(int amount);
    IFilteredTableQueryable<T> Where(Expression<Func<T, bool>> predicate);
    IFilteredTableQueryable<T> ExistsIn<TElement>(Expression<Func<T, TElement>> predicate, IEnumerable<TElement> elements);
    IFilteredTableQueryable<T> NotExistsIn<TElement>(Expression<Func<T, TElement>> predicate, IEnumerable<TElement> elements);
    IDistinctedTableQueryable<T> DistinctBy<TResult>(Func<T, TResult> selector, IEqualityComparer<TResult>? equalityComparer = null);
}

public interface ISelectedTableQueryable<T> : ITableQueryable<T>, ICanTakeOneTableQueryable<T>
    where T : class, ITableEntity, new()
{
    ISelectedTakenTableQueryable<T> Take(int amount);
    ISelectedTableQueryable<T> Where(Expression<Func<T, bool>> predicate);
    ISelectedTableQueryable<T> ExistsIn<TElement>(Expression<Func<T, TElement>> predicate, IEnumerable<TElement> elements);
    ISelectedTableQueryable<T> NotExistsIn<TElement>(Expression<Func<T, TElement>> predicate, IEnumerable<TElement> elements);
    ISelectedDistinctedTableQueryable<T> DistinctBy<TResult>(Func<T, TResult> selector, IEqualityComparer<TResult>? equalityComparer = null);
}

public interface ITakenTableQueryable<T> : ITableQueryable<T>
    where T : class, ITableEntity, new()
{
    ITableEnumerable<TResult> Select<TResult>(Expression<Func<T, TResult>> selector);
    ISelectedTakenTableQueryable<T> SelectFields<TResult>(Expression<Func<T, TResult>> selector);
    ITakenTableQueryable<T> Where(Expression<Func<T, bool>> predicate);
    ITakenTableQueryable<T> ExistsIn<TElement>(Expression<Func<T, TElement>> predicate, IEnumerable<TElement> elements);
    ITakenTableQueryable<T> NotExistsIn<TElement>(Expression<Func<T, TElement>> predicate, IEnumerable<TElement> elements);
    ITakenDistinctedTableQueryable<T> DistinctBy<TResult>(Func<T, TResult> selector, IEqualityComparer<TResult>? equalityComparer = null);
}

public interface IDistinctedTableQueryable<T> : ITableQueryable<T>, ICanTakeOneTableQueryable<T>
    where T : class, ITableEntity, new()
{
    ITableEnumerable<TResult> Select<TResult>(Expression<Func<T, TResult>> selector);
    ISelectedDistinctedTableQueryable<T> SelectFields<TResult>(Expression<Func<T, TResult>> selector);
    IDistinctedTableQueryable<T> Where(Expression<Func<T, bool>> predicate);
    IDistinctedTableQueryable<T> ExistsIn<TElement>(Expression<Func<T, TElement>> predicate, IEnumerable<TElement> elements);
    IDistinctedTableQueryable<T> NotExistsIn<TElement>(Expression<Func<T, TElement>> predicate, IEnumerable<TElement> elements);
    ISelectedDistinctedTableQueryable<T> Take(int amount);
}

public interface ISelectedTakenTableQueryable<T> : ITableQueryable<T>
    where T : class, ITableEntity, new()
{
    ISelectedTakenTableQueryable<T> Where(Expression<Func<T, bool>> predicate);
    ISelectedTakenTableQueryable<T> ExistsIn<TElement>(Expression<Func<T, TElement>> predicate, IEnumerable<TElement> elements);
    ISelectedTakenTableQueryable<T> NotExistsIn<TElement>(Expression<Func<T, TElement>> predicate, IEnumerable<TElement> elements);
    ISelectedTakenDistinctedTableQueryable<T> DistinctBy<TResult>(Func<T, TResult> selector, IEqualityComparer<TResult>? equalityComparer = null);
}

public interface ISelectedDistinctedTableQueryable<T> : ITableQueryable<T>, ICanTakeOneTableQueryable<T>
    where T : class, ITableEntity, new()
{
    ISelectedTakenDistinctedTableQueryable<T> Take(int amount);
    ISelectedDistinctedTableQueryable<T> Where(Expression<Func<T, bool>> predicate);
    ISelectedDistinctedTableQueryable<T> ExistsIn<TElement>(Expression<Func<T, TElement>> predicate, IEnumerable<TElement> elements);
    ISelectedDistinctedTableQueryable<T> NotExistsIn<TElement>(Expression<Func<T, TElement>> predicate, IEnumerable<TElement> elements);
}

public interface ITakenDistinctedTableQueryable<T> : ITableQueryable<T>
    where T : class, ITableEntity, new()
{
    ITableEnumerable<TResult> Select<TResult>(Expression<Func<T, TResult>> selector);
    ISelectedTakenTableQueryable<T> SelectFields<TResult>(Expression<Func<T, TResult>> selector);
    ITakenDistinctedTableQueryable<T> Where(Expression<Func<T, bool>> predicate);
    ITakenDistinctedTableQueryable<T> ExistsIn<TElement>(Expression<Func<T, TElement>> predicate, IEnumerable<TElement> elements);
    ITakenDistinctedTableQueryable<T> NotExistsIn<TElement>(Expression<Func<T, TElement>> predicate, IEnumerable<TElement> elements);
}

public interface ISelectedTakenDistinctedTableQueryable<T> : ITableQueryable<T>
    where T : class, ITableEntity, new()
{
    ISelectedTakenDistinctedTableQueryable<T> Where(Expression<Func<T, bool>> predicate);
    ISelectedTakenDistinctedTableQueryable<T> ExistsIn<TElement>(Expression<Func<T, TElement>> predicate, IEnumerable<TElement> elements);
    ISelectedTakenDistinctedTableQueryable<T> NotExistsIn<TElement>(Expression<Func<T, TElement>> predicate, IEnumerable<TElement> elements);
}