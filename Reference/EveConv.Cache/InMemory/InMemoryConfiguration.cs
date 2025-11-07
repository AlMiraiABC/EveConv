using System;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace EveConv.Cache.InMemory;

/// <summary>
/// Configuration options for the in-memory cache implementation.
/// </summary>
public class InMemoryConfiguration : MemoryCacheOptions, IOptions<InMemoryConfiguration>
{
    /// <summary>
    /// Gets or sets the default time-to-live for cached items when not specified.
    /// If null, items will not expire by default.
    /// </summary>
    public TimeSpan? DefaultTtl { get; set; }

    public InMemoryConfiguration Value => this;
}