using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EveConv.Abstraction.Cache
{
    /// <summary>
    /// Provides advanced cache operations including pattern-based key searches for enhanced cache management.
    /// </summary>
    /// <typeparam name="TValue">The type of values stored in the cache.</typeparam>
    public interface IEnhanceCache<TValue>
    {
        /// <summary>
        /// Finds cache keys that match the specified pattern using wildcard matching.
        /// </summary>
        /// <param name="pattern">The search pattern string supporting wildcard characters. Cannot be null.</param>
        /// <param name="size">The maximum number of keys to return. Must be positive.</param>
        /// <returns>A task containing an enumerable of keys that match the specified pattern.</returns>
        /// <exception cref="ArgumentNullException">Thrown when pattern is null.</exception>
        /// <exception cref="ArgumentException">Thrown when pattern contains invalid syntax.</exception>
        /// <remarks>
        /// <para>Supported wildcard characters:</para>
        /// <list type="bullet">
        /// <item><description>* - Matches zero or more characters</description></item>
        /// <item><description>? - Matches exactly one character</description></item>
        /// </list>
        /// <para>Pattern examples:</para>
        /// <list type="bullet">
        /// <item><description>"user:*" - Matches all keys starting with "user:"</description></item>
        /// <item><description>"temp:???:*" - Matches keys with "temp:" prefix and exactly 3 characters before additional content</description></item>
        /// <item><description>"*:profile" - Matches all keys ending with ":profile"</description></item>
        /// </list>
        /// <para>Returns only the matching keys for flexibility in subsequent cache operations.</para>
        /// </remarks>
        Task<IEnumerable<string>> GetKeysByPatternAsync(string pattern, int size);
    }
}