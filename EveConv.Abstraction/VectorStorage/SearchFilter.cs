using System;
using System.Collections.Generic;
using System.Text;

namespace EveConv.Abstraction.VectorStorage
{
    /// <summary>
    /// Abstract base class for search options.
    /// </summary>
    public abstract class Searchable
    {
    }

    /// <summary>
    /// Search filter group to combine filters.
    /// </summary>
    public class SearchFilter : Searchable
    {
        /// <summary>
        /// All of these filters must be matched.
        /// </summary>
        public IEnumerable<Searchable> And { get; set; } = [];
        /// <summary>
        /// At least one of these filters should be matched.
        /// </summary>
        public IEnumerable<Searchable> Or { get; set; } = [];
        /// <summary>
        /// Excluded that must not be matched.
        /// </summary>
        public IEnumerable<Searchable> Not { get; set; } = [];
    }

    /// <summary>
    /// Search condition item for field value comparisons.
    /// </summary>
    /// <typeparam name="V">Type of value to compare.</typeparam>
    public class SearchCondition<V> : Searchable
    {
        /// <summary>
        /// Field key to filter on.
        /// </summary>
        public string Field { get; init; } = string.Empty;
        /// <summary>
        /// The lower exclusive bound for the value range filter.
        /// </summary>
        public V? GreaterThan { get; init; }
        /// <summary>
        /// The lower inclusive bound for the value range filter.
        /// </summary>
        public V? GreaterThanOrEqualsTo { get; init; }
        /// <summary>
        /// The upper exclusive bound for the value range filter.
        /// </summary>
        public V? LessThan { get; init; }
        /// <summary>
        /// The upper inclusive bound for the value range filter.
        /// </summary>
        public V? LessThanOrEqualsTo { get; init; }
        /// <summary>
        /// Equality match value.
        /// </summary>
        public V? EqualsTo { get; init; }
        /// <summary>
        /// Fuzzy match string used for approximate matching operations.
        /// </summary>
        public string? FuzzyMatch { get; init; }
        /// <summary>
        /// At least one of these values must be matched.
        /// </summary>
        public IEnumerable<V>? In { get; init; }
        /// <summary>
        /// All of these values must not be matched.
        /// </summary>
        public IEnumerable<V>? NotIn { get; init; }
    }
}
