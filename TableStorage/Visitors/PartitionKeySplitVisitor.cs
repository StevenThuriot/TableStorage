using System.Linq.Expressions;
using System.Reflection;

namespace TableStorage.Visitors;

internal static class PartitionKeySplitVisitor
{
    public static List<Expression<Func<T, bool>>>? TrySplit<T>(Expression<Func<T, bool>> filter)
    {
        ParameterExpression parameter = filter.Parameters[0];
        Expression body = filter.Body;

        // Case 1: Top-level OR chain — e.g. PK == "1" || PK == "2" || (PK == "3" && RK == "a")
        if (body.NodeType is ExpressionType.OrElse)
        {
            List<Expression> disjuncts = [];
            FlattenOr(body, disjuncts);

            return TrySplitDisjuncts<T>(disjuncts, parameter);
        }

        // Case 2: Top-level AND chain with an OR term inside — e.g. (PK == "1" || PK == "2") && RK == "a"
        if (body.NodeType is ExpressionType.AndAlso)
        {
            List<Expression> andTerms = [];
            FlattenAnd(body, andTerms);

            return TrySplitAndWithOrTerm<T>(andTerms, parameter);
        }

        return null;
    }

    private static List<Expression<Func<T, bool>>>? TrySplitDisjuncts<T>(List<Expression> disjuncts, ParameterExpression parameter)
    {
        // Extract partition key from each disjunct
        Dictionary<string, List<Expression>> groups = [];

        foreach (Expression disjunct in disjuncts)
        {
            string? pk = ExtractPartitionKey(disjunct);
            if (pk is null)
            {
                return null;
            }

            if (!groups.TryGetValue(pk, out List<Expression>? list))
            {
                list = [];
                groups[pk] = list;
            }

            list.Add(disjunct);
        }

        if (groups.Count <= 1)
        {
            return null;
        }

        List<Expression<Func<T, bool>>> result = new(groups.Count);

        foreach (List<Expression> group in groups.Values)
        {
            result.Add(Expression.Lambda<Func<T, bool>>(CombineGroup(group), parameter));
        }

        return result;
    }

    private static List<Expression<Func<T, bool>>>? TrySplitAndWithOrTerm<T>(List<Expression> andTerms, ParameterExpression parameter)
    {
        // Find the first AND-term that is an OR with multiple distinct partition keys
        for (int i = 0; i < andTerms.Count; i++)
        {
            Expression term = andTerms[i];

            if (term.NodeType is not ExpressionType.OrElse)
            {
                continue;
            }

            List<Expression> disjuncts = [];
            FlattenOr(term, disjuncts);

            // Check that every disjunct has a partition key
            Dictionary<string, List<Expression>> groups = [];
            bool allHavePk = true;

            foreach (Expression disjunct in disjuncts)
            {
                string? pk = ExtractPartitionKey(disjunct);
                if (pk is null)
                {
                    allHavePk = false;
                    break;
                }

                if (!groups.TryGetValue(pk, out List<Expression>? list))
                {
                    list = [];
                    groups[pk] = list;
                }

                list.Add(disjunct);
            }

            if (!allHavePk || groups.Count <= 1)
            {
                continue;
            }

            // Collect remaining AND terms (everything except the OR term at index i)
            List<Expression> sharedTerms = new(andTerms.Count - 1);
            for (int j = 0; j < andTerms.Count; j++)
            {
                if (j != i)
                {
                    sharedTerms.Add(andTerms[j]);
                }
            }

            Expression? sharedExpression = null;
            if (sharedTerms.Count > 0)
            {
                sharedExpression = sharedTerms[0];
                for (int j = 1; j < sharedTerms.Count; j++)
                {
                    sharedExpression = Expression.AndAlso(sharedExpression, sharedTerms[j]);
                }
            }

            // Build one filter per partition key group, combining group disjuncts with shared AND terms
            List<Expression<Func<T, bool>>> result = new(groups.Count);

            foreach (List<Expression> group in groups.Values)
            {
                Expression groupExpression = CombineGroup(group);

                if (sharedExpression is not null)
                {
                    groupExpression = Expression.AndAlso(groupExpression, sharedExpression);
                }

                result.Add(Expression.Lambda<Func<T, bool>>(groupExpression, parameter));
            }

            return result;
        }

        return null;
    }

    private static Expression CombineGroup(List<Expression> group)
    {
        if (group.Count == 1)
        {
            return group[0];
        }

        // Extract the PK equality expression from the first disjunct
        Expression? pkEquality = GetPartitionKeyEqualityExpression(group[0]);
        if (pkEquality is null)
        {
            // Fallback: just OR them together
            Expression combined = group[0];
            for (int i = 1; i < group.Count; i++)
            {
                combined = Expression.OrElse(combined, group[i]);
            }

            return combined;
        }

        // Strip PK equality from each disjunct, collect remainders
        List<Expression> remainders = new(group.Count);
        foreach (Expression disjunct in group)
        {
            Expression? remainder = StripPartitionKeyEquality(disjunct);
            if (remainder is null)
            {
                // Bare PK equality in group: PK || (PK && X) simplifies to PK
                return pkEquality;
            }

            remainders.Add(remainder);
        }

        // Build: pkEquality && (rem1 || rem2 || ...)
        Expression orRemainders = remainders[0];
        for (int i = 1; i < remainders.Count; i++)
        {
            orRemainders = Expression.OrElse(orRemainders, remainders[i]);
        }

        return Expression.AndAlso(pkEquality, orRemainders);
    }

    private static Expression? GetPartitionKeyEqualityExpression(Expression expression)
    {
        // Direct equality: PK == "value" or "value" == PK
        if (expression is BinaryExpression { NodeType: ExpressionType.Equal } binary && TryGetPartitionKeyEquality(binary) is not null)
        {
            return binary;
        }

        // AND chain: find the PK equality term
        if (expression.NodeType is ExpressionType.AndAlso)
        {
            List<Expression> terms = [];
            FlattenAnd(expression, terms);

            foreach (Expression term in terms)
            {
                if (term is BinaryExpression { NodeType: ExpressionType.Equal } eq && TryGetPartitionKeyEquality(eq) is not null)
                {
                    return eq;
                }
            }
        }

        return null;
    }

    private static Expression? StripPartitionKeyEquality(Expression expression)
    {
        // Direct equality: PK == "value" — no remainder
        if (expression is BinaryExpression { NodeType: ExpressionType.Equal } binary && TryGetPartitionKeyEquality(binary) is not null)
        {
            return null;
        }

        // AND chain: remove the PK equality term, recombine the rest
        if (expression.NodeType is ExpressionType.AndAlso)
        {
            List<Expression> terms = [];
            FlattenAnd(expression, terms);

            List<Expression> remaining = [];
            foreach (Expression term in terms)
            {
                if (term is BinaryExpression { NodeType: ExpressionType.Equal } eq && TryGetPartitionKeyEquality(eq) is not null)
                {
                    continue;
                }

                remaining.Add(term);
            }

            if (remaining.Count == 0)
            {
                return null;
            }

            Expression result = remaining[0];
            for (int i = 1; i < remaining.Count; i++)
            {
                result = Expression.AndAlso(result, remaining[i]);
            }

            return result;
        }

        // Unknown shape — return as-is
        return expression;
    }

    private static string? ExtractPartitionKey(Expression expression)
    {
        // Direct equality: PK == "value" or "value" == PK
        if (expression is BinaryExpression { NodeType: ExpressionType.Equal } binary)
        {
            string? value = TryGetPartitionKeyEquality(binary);
            if (value is not null)
            {
                return value;
            }
        }

        // AND chain: PK == "value" && other conditions — extract PK from any term
        if (expression.NodeType is ExpressionType.AndAlso)
        {
            List<Expression> terms = [];
            FlattenAnd(expression, terms);

            foreach (Expression term in terms)
            {
                if (term is BinaryExpression { NodeType: ExpressionType.Equal } eq)
                {
                    string? value = TryGetPartitionKeyEquality(eq);
                    if (value is not null)
                    {
                        return value;
                    }
                }
            }
        }

        return null;
    }

    private static string? TryGetPartitionKeyEquality(BinaryExpression binary)
    {
        // PK == "value"
        if (IsPartitionKeyAccess(binary.Left))
        {
            return TryGetConstantStringValue(binary.Right);
        }

        // "value" == PK
        if (IsPartitionKeyAccess(binary.Right))
        {
            return TryGetConstantStringValue(binary.Left);
        }

        return null;
    }

    private static bool IsPartitionKeyAccess(Expression expression)
    {
        // Direct: x.PartitionKey
        if (expression is MemberExpression { Member.Name: nameof(ITableEntity.PartitionKey), Expression.NodeType: ExpressionType.Parameter })
        {
            return true;
        }

        // After WhereVisitor proxy rewrite: ((ITableEntity)x).PartitionKey
        if (expression is MemberExpression { Member.Name: nameof(ITableEntity.PartitionKey), Expression: UnaryExpression { NodeType: ExpressionType.Convert, Operand.NodeType: ExpressionType.Parameter } })
        {
            return true;
        }

        return false;
    }

    private static string? TryGetConstantStringValue(Expression expression)
    {
        // Direct constant: "value"
        if (expression is ConstantExpression { Value: string value })
        {
            return value;
        }

        // Closure capture: closure.field or closure.property
        if (expression is MemberExpression { Expression: ConstantExpression container } member)
        {
            object? containerValue = container.Value;
            if (containerValue is null)
            {
                return null;
            }

            if (member.Member is FieldInfo field)
            {
                return field.GetValue(containerValue) as string;
            }

            if (member.Member is PropertyInfo property)
            {
                return property.GetValue(containerValue) as string;
            }
        }

        return null;
    }

    private static void FlattenOr(Expression expression, List<Expression> result)
    {
        if (expression is BinaryExpression { NodeType: ExpressionType.OrElse } binary)
        {
            FlattenOr(binary.Left, result);
            FlattenOr(binary.Right, result);
        }
        else
        {
            result.Add(expression);
        }
    }

    private static void FlattenAnd(Expression expression, List<Expression> result)
    {
        if (expression is BinaryExpression { NodeType: ExpressionType.AndAlso } binary)
        {
            FlattenAnd(binary.Left, result);
            FlattenAnd(binary.Right, result);
        }
        else
        {
            result.Add(expression);
        }
    }
}
