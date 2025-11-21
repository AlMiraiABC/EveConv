using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.RegularExpressions;
using EveConv.Abstraction.VectorStorage;
using Qdrant.Client.Grpc;
using QMatch = Qdrant.Client.Grpc.Match;
using QRange = Qdrant.Client.Grpc.Range;

namespace EveConv.Storage.Qdrant
{
    /// <summary>
    /// Helper class to build Qdrant <see cref="Filter"/> from <see cref="Searchable"/>
    /// </summary>
    internal static class FilterBuilder
    {
        // supported numeric types to convert to long or double.
        private readonly static Type[] NUMERIC_TYPES = [
            typeof(sbyte), typeof(short), typeof(int), typeof(long),
            typeof(byte), typeof(ushort), typeof(uint), typeof(ulong),
            typeof(decimal), typeof(float), typeof(double)];

        /// <summary>
        /// Creates a <see cref="Filter"/> from <see cref="Searchable"/>
        /// </summary>
        /// <param name="filter">A searchable instance.</param>
        /// <returns>A Qdrant <see cref="Filter"/> if successfully, otherwise, <see langword="null"/>.</returns>
        [return: NotNullIfNotNull(nameof(filter))]
        internal static Filter? Build(Searchable? filter)
        {
            if (filter is null)
            {
                return null;
            }
            return filter switch
            {
                SearchFilter g => BuildGroupFilter(g),
                _ => BuildLeafFilterDynamic(filter)
            };
        }

        /// <summary>
        /// Handle composite SearchFilter (AND / OR / NOT semantics)
        /// </summary>
        private static Filter BuildGroupFilter(SearchFilter group)
        {
            var result = new Filter();
            // AND => Must
            foreach (var andItem in group.And ?? [])
            {
                var child = Build(andItem);
                if (child is null) continue;
                MergeChildIntoMust(result, child);
            }
            // OR => Should
            foreach (var orItem in group.Or ?? [])
            {
                var child = Build(orItem);
                if (child is null) continue;
                MergeChildIntoShould(result, child);
            }
            // NOT => MustNot
            foreach (var notItem in group.Not ?? [])
            {
                var child = Build(notItem);
                if (child is null) continue;
                MergeChildIntoMustNot(result, child);
            }
            return result;
        }

        /// <summary>
        /// Dynamically dispatch generic <see cref="SearchCondition{V}"/>
        /// </summary>
        private static Filter BuildLeafFilterDynamic(Searchable leaf)
        {
            var t = leaf.GetType();
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(SearchCondition<>))
            {
                // Use dynamic to call generic implementation
                return BuildLeafFilter((dynamic)leaf);
            }
            throw new NotSupportedException($"Unsupported filter leaf type: {t.FullName}");
        }

        /// <summary>
        /// Build Filter for a single <see cref="SearchCondition{V}"/>
        /// </summary>
        private static Filter BuildLeafFilter<V>(SearchCondition<V> condition)
        {
            var filter = new Filter();
            if (string.IsNullOrWhiteSpace(condition.Field))
            {
                return filter;
            }
            var field = condition.Field;
            Condition? c = null;
            // Eq
            if (condition.EqualsTo is not null)
            {
                var match = new QMatch();
                if (condition.EqualsTo is bool bv)
                {
                    c = Conditions.Match(field, bv);
                }
                else if (condition.EqualsTo is string sv)
                {
                    c = Conditions.MatchKeyword(field, sv);
                }
                else if (NUMERIC_TYPES.Contains(typeof(V)))
                {
                    c = Conditions.Match(field, Convert.ToInt64(condition.EqualsTo));
                }
                throw new NotSupportedException($"Type of {nameof(SearchCondition<>.EqualsTo)}: {typeof(V)} is not supported");
            }
            // Fuzzy match
            if (!string.IsNullOrWhiteSpace(condition.FuzzyMatch))
            {
                c = Conditions.MatchPhrase(condition.Field, condition.FuzzyMatch);
            }
            // Range
            if (condition.GreaterThan is not null
                || condition.GreaterThanOrEqualsTo is not null
                || condition.LessThan is not null
                || condition.LessThanOrEqualsTo is not null)
            {
                if (typeof(V) == typeof(DateTime))
                {
                    var dt = (condition as SearchCondition<DateTime>)!;
                    c = Conditions.DatetimeRange(
                        field,
                        lt: dt.LessThan,
                        lte: dt.LessThanOrEqualsTo,
                        gt: dt.GreaterThan,
                        gte: dt.GreaterThanOrEqualsTo);
                }
                if (NUMERIC_TYPES.Contains(typeof(V)))
                {
                    var range = new QRange();
                    c = Conditions.Range(field, range);
                    if (condition.LessThan is not null)
                    {
                        range.Lt = Convert.ToDouble(condition.LessThan);
                    }
                    if (condition.LessThanOrEqualsTo is not null)
                    {
                        range.Lte = Convert.ToDouble(condition.LessThanOrEqualsTo);
                    }
                    if (condition.GreaterThan is not null)
                    {
                        range.Gt = Convert.ToDouble(condition.GreaterThan);
                    }
                    if (condition.GreaterThanOrEqualsTo is not null)
                    {
                        range.Gte = Convert.ToDouble(condition.GreaterThanOrEqualsTo);
                    }
                }
            }
            // In
            if (condition.In is not null && condition.In.Any())
            {
                if (typeof(V) == typeof(string))
                {
                    c = Conditions.Match(field, condition.In.Select(i => i?.ToString() ?? string.Empty).ToArray());
                }
                else if (NUMERIC_TYPES.Contains(typeof(V)))
                {
                    c = Conditions.Match(field, condition.In.Select(i => Convert.ToInt64(i)).ToArray());
                }
                throw new NotSupportedException($"Type of {nameof(SearchCondition<>.In)}: {typeof(V)} is not supported");
            }
            // NotIn
            if (condition.NotIn is not null && condition.NotIn.Any())
            {
                if (typeof(V) == typeof(string))
                {
                    c = Conditions.Match(field, condition.NotIn.Select(i => i?.ToString() ?? string.Empty).ToArray());
                }
                if (NUMERIC_TYPES.Contains(typeof(V)))
                {
                    c = Conditions.MatchExcept(field, condition.NotIn.Select(i => Convert.ToInt64(i)).ToArray());
                }
                throw new NotSupportedException($"Type of {nameof(SearchCondition<>.NotIn)}: {typeof(V)} is not supported");
            }
            if (c is not null)
            {
                filter.Must.Add(c);
            }
            return filter;
        }

        #region Merge helpers

        private static void MergeChildIntoMust(Filter target, Filter child)
        {
            // If child has ONLY Must and MustNot (no Should) we can flatten
            if (child.Should.Count == 0)
            {
                foreach (var c in child.Must) target.Must.Add(c);
                foreach (var c in child.MustNot) target.MustNot.Add(c);
            }
            else
            {
                // Preserve grouping by nesting
                target.Must.Add(Conditions.Filter(child));
            }
        }

        private static void MergeChildIntoShould(Filter target, Filter child)
        {
            // If child is simple (no Should + no MustNot) flatten Must into Should
            if (child.Should.Count == 0 && child.MustNot.Count == 0)
            {
                foreach (var c in child.Must) target.Should.Add(c);
            }
            else
            {
                target.Should.Add(Conditions.Filter(child));
            }
        }

        private static void MergeChildIntoMustNot(Filter target, Filter child)
        {
            // If child simple, flatten
            if (child.Should.Count == 0)
            {
                foreach (var c in child.Must) target.MustNot.Add(c);
                foreach (var c in child.MustNot) target.Must.Add(c); // double negative: NOT(NOT) => MUST
            }
            else
            {
                target.MustNot.Add(Conditions.Filter(child));
            }
        }

        #endregion
    }
}
