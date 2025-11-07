using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using EveConv.Abstraction.Cache;
using Microsoft.Extensions.Logging;

namespace EveConv.Cache.InMemory;

/// <summary>
/// Enhanced cache operations implementation for InMemoryCache.
/// </summary>
public partial class InMemoryCache : IEnhanceCache<object>
{
    /// <summary>
    /// Finds cache keys that match the specified pattern using wildcard matching.
    /// </summary>
    /// <param name="pattern">The search pattern string supporting wildcard characters. Cannot be null.</param>
    /// <param name="size">The maximum number of keys to return. Must be positive.</param>
    /// <returns>A task containing an enumerable of keys that match the specified pattern.</returns>
    /// <exception cref="ArgumentNullException">Thrown when pattern is null.</exception>
    /// <exception cref="ArgumentException">Thrown when pattern contains invalid syntax or size is not positive.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the cache has been disposed.</exception>
    public Task<IEnumerable<string>> GetKeysByPatternAsync(string pattern, int size)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(pattern);

        if (size <= 0)
        {
            throw new ArgumentException("Size must be positive.", nameof(size));
        }

        try
        {
            var regex = ConvertPatternToRegex(pattern);
            var matchingKeys = _memoryCache.Keys
                .Cast<string>()
                .Where(key => regex.IsMatch(key))
                .Take(size)
                .OrderBy(key => key) // Consistent ordering
                .ToList();

            _logger.LogDebug("Pattern search for '{Pattern}' found {Count} keys (limit: {Size})",
                pattern, matchingKeys.Count, size);

            return Task.FromResult<IEnumerable<string>>(matchingKeys);
        }
        catch (ArgumentException ex)
        {
            _logger.LogError(ex, "Invalid pattern syntax: {Pattern}", pattern);
            throw new ArgumentException($"Invalid pattern syntax: {pattern}", nameof(pattern), ex);
        }
    }

    /// <summary>
    /// Converts a wildcard pattern to a regular expression.
    /// </summary>
    /// <param name="pattern">The wildcard pattern to convert.</param>
    /// <returns>A compiled regular expression.</returns>
    /// <exception cref="ArgumentException">Thrown when the pattern cannot be converted to a valid regex.</exception>
    private static Regex ConvertPatternToRegex(string pattern)
    {
        try
        {
            // Escape regex special characters except * and ?
            var escaped = Regex.Escape(pattern);

            // Replace escaped wildcards with regex equivalents
            var regexPattern = escaped
                .Replace("\\*", ".*")  // * matches zero or more characters
                .Replace("\\?", ".");  // ? matches exactly one character

            // Anchor the pattern to match the entire string
            regexPattern = $"^{regexPattern}$";

            return new Regex(regexPattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
        }
        catch (ArgumentException ex)
        {
            throw new ArgumentException($"Cannot convert pattern '{pattern}' to regex", ex);
        }
    }
}