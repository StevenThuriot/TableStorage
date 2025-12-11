using Azure.Data.Tables;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Text;
using TableStorage.Fluent;
using TableStorage.Tests.Models;

namespace TableStorage.Tests;

public static class Accessors
{
    // The method we're invoking has this signature:
    //     private static bool IsCompatibleObject(object? value)
    // 
    // Our extern signature must include the target type as the first method parameter
    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "Bind")]
    public static extern string Bind(TableClient instance, Expression expression);
}

public class ExpressionTest
{
    [Fact]
    public void TestSimpleReplacement()
    {
        Expression<Func<Model, bool>> predicate = x => x.MyProperty1 > 2 && x.MyProperty2 == "test2";

        ParameterExpression param = Expression.Parameter(typeof(FluentPartitionTableEntity<Model, Model4>), "e");
        var lambda = Expression.Lambda<Func<FluentPartitionTableEntity<Model, Model4>, bool>>(predicate.Body, param);
        Console.Write(lambda);
        string lambdaStr = Accessors.Bind(null!, lambda);
        Console.WriteLine(lambdaStr);

        var visitor = new FluentVisitor(lambda.Parameters[0]);
        var modified = visitor.VisitAndConvert(lambda, "Fluent");
        Console.WriteLine(modified);
        var compiled = modified.Compile();
    }

    [Fact]
    public void TestExpression()
    {
        var combined = Compose<Model, Model4>(x => x.MyProperty1 > 2 && x.MyProperty2 == "test2", x => x.PrettyPartition != "" && x.MyProperty2 == "test3", Expression.AndAlso);
        Console.Write(combined);
        string query = Accessors.Bind(null!, combined);
        Console.WriteLine(query);
    }

    private static Expression Compose<T1, T2>(Expression<Func<T1, bool>> first, Expression<Func<T2, bool>> second, Func<Expression, Expression, Expression> merge)
    {
        // build parameter map (from parameters of second to parameters of first)
        //var map = first.Parameters
        //    .Select((f, i) => new { f, s = second.Parameters[i] })
        //    .ToDictionary(p => p.s, p => p.f);
        // replace parameters in the second lambda expression with parameters from the first
        var secondBody =/* ParameterRebinder.ReplaceParameters(map,*/ second.Body/*)*/;
        // apply composition of lambda expression bodies to parameters from the first expression 
        return Expression.Lambda(merge(first.Body, secondBody), first.Parameters);
    }

    public class ParameterRebinder(Dictionary<ParameterExpression, ParameterExpression> map) : ExpressionVisitor
    {
        private readonly Dictionary<ParameterExpression, ParameterExpression> _map = map ?? [];

        public static Expression ReplaceParameters(Dictionary<ParameterExpression, ParameterExpression> map, Expression exp)
        {
            return new ParameterRebinder(map).Visit(exp);
        }
        protected override Expression VisitParameter(ParameterExpression node)
        {
            if (_map.TryGetValue(node, out var replacement))
            {
                node = replacement;
            }

            return base.VisitParameter(node);
        }
    }
}
