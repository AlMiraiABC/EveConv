using System.Text.RegularExpressions;
using EveConv.Abstraction.Cache;
using Microsoft.Extensions.Logging;

namespace EveConv.Cache.RocksDb;

/// <summary>
/// Enhanced cache operations implementation for RocksDbCache.
/// </summary>
public partial class RocksDbCache : IEnhanceCache<object>
{
    /// <inheritdoc />
    public Task<IEnumerable<string>> GetKeysByPatternAsync(string pattern, int size, CancellationToken token = default)
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
            var matchingKeys = new List<string>();

            using var iterator = _db.NewIterator();

            // Extract longest prefix without wildcards to narrow the scan range
            var seekPrefix = ExtractPrefix(pattern);
            if (seekPrefix is not null)
            {
                SeekByPrefix(iterator, seekPrefix, size, regex, matchingKeys, token);
            }
            else
            {
                SeekAll(iterator, size, regex, matchingKeys, token);
            }

            matchingKeys.Sort(StringComparer.Ordinal); // Consistent ordering

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Pattern search for '{Pattern}' found {Count} keys (limit: {Size})",
                    pattern, matchingKeys.Count, size);
            }

            return Task.FromResult<IEnumerable<string>>(matchingKeys);
        }
        catch (OperationCanceledException)
        {
            throw;
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

    /// <summary>
    /// Extracts the longest prefix without wildcards from a pattern.
    /// Returns null if no useful prefix can be extracted (e.g., pattern starts with wildcard).
    /// </summary>
    private static string? ExtractPrefix(string pattern)
    {
        for (int i = 0; i < pattern.Length; i++)
        {
            if (pattern[i] is '*' or '?')
            {
                return i > 0 ? pattern[..i] : null;
            }
        }
        // No wildcards — the entire pattern is a literal prefix
        return pattern.Length > 0 ? pattern : null;
    }

    /// <summary>
    /// Converts a wildcard pattern to a regular expression.
    /// </summary>
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

    /// <summary>
    /// Seeks to the given prefix and collects matching keys within that prefix range.
    /// Stops when the iterator leaves the prefix or the size limit is reached.
    /// </summary>
    private void SeekByPrefix(RocksDbSharp.Iterator iterator, string seekPrefix, int size,
        Regex regex, List<string> matchingKeys, CancellationToken token)
    {
        iterator.Seek(seekPrefix);
        while (iterator.Valid() && matchingKeys.Count < size)
        {
            token.ThrowIfCancellationRequested();
            var key = iterator.StringKey();
            if (!key.StartsWith(seekPrefix))
                break;

            AddIfMatch(key, regex, iterator, matchingKeys);
            iterator.Next();
        }
    }

    /// <summary>
    /// Full-scans all keys and collects those matching the regex up to the size limit.
    /// </summary>
    private void SeekAll(RocksDbSharp.Iterator iterator, int size,
        Regex regex, List<string> matchingKeys, CancellationToken token)
    {
        iterator.SeekToFirst();
        while (iterator.Valid() && matchingKeys.Count < size)
        {
            token.ThrowIfCancellationRequested();
            AddIfMatch(iterator.StringKey(), regex, iterator, matchingKeys);
            iterator.Next();
        }
    }

    /// <summary>
    /// If the key matches the regex and the associated cache entry is not expired,
    /// adds it to the matching keys list.
    /// </summary>
    private void AddIfMatch(string key, Regex regex, RocksDbSharp.Iterator iterator, List<string> matchingKeys)
    {
        if (!regex.IsMatch(key))
            return;

        if (TryReadEntry(iterator.Value(), iterator.Key()) is not null)
            matchingKeys.Add(key);
    }
}
