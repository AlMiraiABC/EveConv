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
    public async Task<IEnumerable<string>> GetKeysByPatternAsync(string pattern, int size, CancellationToken token = default)
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

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Pattern search for '{Pattern}' found {Count} keys (limit: {Size})",
                    pattern, matchingKeys.Count, size);
            }

            return matchingKeys;
        }
        catch (ArgumentException ex)
        {
            if (_logger.IsEnabled(LogLevel.Error))
            {
                _logger.LogError(ex, "Invalid pattern syntax: {Pattern}", pattern);
            }
            throw new ArgumentException($"Invalid pattern syntax: {pattern}", nameof(pattern), ex);
        }
    }

    private static Regex ConvertPatternToRegex(string pattern)
    {
        try
        {
            var escaped = Regex.Escape(pattern);

            var regexPattern = escaped
                .Replace("\\*", ".*")
                .Replace("\\?", ".");

            regexPattern = $"^{regexPattern}$";

            return new Regex(regexPattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
        }
        catch (ArgumentException ex)
        {
            throw new ArgumentException($"Cannot convert pattern '{pattern}' to regex", ex);
        }
    }
}