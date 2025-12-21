using System.Text.RegularExpressions;
using EveConv.Abstraction.Cache;
using Microsoft.Extensions.Logging;

namespace EveConv.Cache.Redis;

/// <summary>
/// Enhanced cache operations implementation for RedisCache using SCAN-based pattern matching.
/// </summary>
public partial class RedisCache : IEnhanceCache<object>
{
    private const int DefaultScanPageSize = 100;
    private const int MaxScanPageSize = 1000;

    /// <inheritdoc />
    public async Task<IEnumerable<string>> GetKeysByPatternAsync(string pattern, int size, CancellationToken token = default)
    {
        ThrowIfDisposed();
        ValidatePattern(pattern);
        if (size <= 0 || size > MaxScanPageSize)
        {
            throw new ArgumentOutOfRangeException(nameof(size), $"Maximum results must be [1, {MaxScanPageSize}]");
        }

        try
        {
            var redisPattern = ConvertToRedisPattern(pattern);
            var keys = new List<string>();
            var server = Connection.GetServer(Connection.GetEndPoints().First());

            _logger.LogDebug("Scanning for keys with pattern: {Pattern} (Redis pattern: {RedisPattern})",
                pattern, redisPattern);

            // Use SCAN with pattern for safe key enumeration
            await foreach (var key in server.KeysAsync(pattern: redisPattern, pageSize: DefaultScanPageSize))
            {
                keys.Add(key.ToString());
            }

            _logger.LogDebug("Found {KeyCount} keys matching pattern: {Pattern}", keys.Count, pattern);
            return keys;
        }
        catch (Exception ex) when (ex is not (ArgumentException or ObjectDisposedException))
        {
            _logger.LogError(ex, "Error scanning for keys with pattern: {Pattern}", pattern);
            throw new InvalidOperationException($"Error scanning for keys with pattern: {pattern}", ex);
        }
    }

    /// <summary>
    /// Validates the search pattern for Redis compatibility.
    /// </summary>
    /// <param name="pattern">The pattern to validate.</param>
    /// <exception cref="ArgumentNullException">Thrown when pattern is null.</exception>
    /// <exception cref="ArgumentException">Thrown when pattern contains invalid syntax.</exception>
    private static void ValidatePattern(string pattern)
    {
        ArgumentNullException.ThrowIfNull(pattern);

        if (string.IsNullOrWhiteSpace(pattern))
        {
            throw new ArgumentException("Pattern cannot be empty or whitespace.", nameof(pattern));
        }

        // Check for maximum pattern length
        if (pattern.Length > 1024)
        {
            throw new ArgumentException("Pattern cannot exceed 1024 characters.", nameof(pattern));
        }

        // Check for null characters
        if (pattern.Contains('\0'))
        {
            throw new ArgumentException("Pattern cannot contain null characters.", nameof(pattern));
        }

        // Validate pattern syntax - ensure balanced brackets if any
        if (HasUnbalancedBrackets(pattern))
        {
            throw new ArgumentException("Pattern contains unbalanced brackets.", nameof(pattern));
        }
    }

    /// <summary>
    /// Converts .NET-style wildcard patterns to Redis-compatible patterns.
    /// </summary>
    /// <param name="pattern">The .NET-style pattern to convert.</param>
    /// <returns>A Redis-compatible pattern string.</returns>
    /// <remarks>
    /// Converts:
    /// - * (asterisk) remains * for zero or more characters
    /// - ? (question mark) remains ? for exactly one character
    /// - Other characters are treated literally
    /// </remarks>
    private static string ConvertToRedisPattern(string pattern)
    {
        // Redis patterns use the same wildcards as .NET patterns for basic cases
        // * = zero or more characters
        // ? = exactly one character
        // [ ] = character class (Redis supports this)

        // For now, we'll pass the pattern through as-is since Redis and .NET
        // use compatible wildcard syntax for basic patterns
        return pattern;
    }

    /// <summary>
    /// Checks if the pattern has unbalanced brackets.
    /// </summary>
    /// <param name="pattern">The pattern to check.</param>
    /// <returns>True if brackets are unbalanced; otherwise, false.</returns>
    private static bool HasUnbalancedBrackets(string pattern)
    {
        var bracketCount = 0;
        var inBrackets = false;

        for (int i = 0; i < pattern.Length; i++)
        {
            var ch = pattern[i];

            switch (ch)
            {
                case '[':
                    if (!inBrackets)
                    {
                        inBrackets = true;
                        bracketCount++;
                    }
                    break;

                case ']':
                    if (inBrackets)
                    {
                        inBrackets = false;
                        bracketCount--;
                    }
                    break;

                case '\\':
                    // Skip escaped characters
                    if (i + 1 < pattern.Length)
                    {
                        i++; // Skip next character
                    }
                    break;
            }
        }

        return bracketCount != 0 || inBrackets;
    }

    /// <summary>
    /// Checks if a key matches the specified pattern.
    /// </summary>
    /// <param name="key">The key to check.</param>
    /// <param name="pattern">The pattern to match against.</param>
    /// <returns>True if the key matches the pattern; otherwise, false.</returns>
    /// <remarks>
    /// This method provides local pattern matching without querying Redis.
    /// Useful for filtering results or validating patterns.
    /// </remarks>
    public static bool IsKeyMatchingPattern(string key, string pattern, CancellationToken token = default)
    {
        if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(pattern))
        {
            return false;
        }

        try
        {
            // Convert wildcard pattern to regex
            var regexPattern = "^" + Regex.Escape(pattern)
                .Replace("\\*", ".*")
                .Replace("\\?", ".") + "$";

            return Regex.IsMatch(key, regexPattern, RegexOptions.Compiled);
        }
        catch (Exception)
        {
            // If regex fails, fall back to simple string comparison
            return key.Equals(pattern, StringComparison.Ordinal);
        }
    }
}