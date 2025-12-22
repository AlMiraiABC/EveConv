using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using EveConv.Abstraction;
using EveConv.Abstraction.Diagnostic;
using EveConv.Abstraction.DocParser;
using Microsoft.Extensions.Logging;

namespace EveConv.DocDecoder
{
    /// <summary>
    /// Document parser manager.
    /// </summary>
    public class DocumentParser
    {
        private readonly ILogger<DocumentParser> _logger;
        private readonly IMimeTypeDetection _mimeTypeDetection;
        /// <summary>
        /// Document parsers sorted by priority.
        /// </summary>
        /// <remarks>{Priority, {Type, Instance}}</remarks>
        private readonly SortedDictionary<int, ConcurrentDictionary<Type, IDocumentParser>> _parsers = new(new PriorityComaprer());
        /// <summary>
        /// Accepted parser cache.
        /// </summary>
        /// <remarks>{FileType, Parser}</remarks>
        private readonly ConcurrentDictionary<string, IDocumentParser> _acceptCache = [];

        public DocumentParser(IMimeTypeDetection mimeTypeDetection, ILoggerFactory? loggerFactory = null)
        {
            _logger = (loggerFactory ?? DefaultLogger.Factory).CreateLogger<DocumentParser>();
            this._mimeTypeDetection = mimeTypeDetection;
        }

        public DocumentParser(IEnumerable<IDocumentParser> parsers, IMimeTypeDetection mimeTypeDetection, ILoggerFactory? loggerFactory = null)
            : this(mimeTypeDetection, loggerFactory)
        {
            foreach (var parser in parsers)
            {
                RegisterParser(parser);
            }
        }

        /// <summary>
        /// Register a document parser.
        /// </summary>
        /// <param name="parser">The document parser.</param>
        /// <remarks>
        ///     If the same type of <paramref name="parser"/> has been exists, it will be replaced.
        ///     This is not thread-safe.
        ///     This will clear the accpet cache.
        /// </remarks>
        public void RegisterParser(IDocumentParser parser)
        {
            ArgumentNullException.ThrowIfNull(parser);
            if (!_parsers.TryGetValue(parser.Priority, out var parsers))
            {
                _parsers[parser.Priority] = [];
                parsers = _parsers[parser.Priority];
            }
            if (parsers.ContainsKey(parser.GetType()))
            {
                if (_logger.IsEnabled(LogLevel.Warning))
                {
                    _logger.LogWarning("Document parser {parser} has been registered", parser.GetType());
                }
            }
            parsers[parser.GetType()] = parser;
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Registered document parser: {parser} with priority {priority}", parser.GetType(), parser.Priority);
            }
            _acceptCache.Clear();
        }

        /// <summary>
        /// Unregister a documen parser by type.
        /// </summary>
        /// <param name="parser">The document parser.</param>
        /// <remarks>
        ///     Do nothing if this type not registered.
        ///     This is not thread-safe.
        ///     This will clear the accept cache.
        /// </remarks>
        public void UnregisterParser(IDocumentParser? parser)
        {
            if (parser is null)
            {
                return;
            }
            if (!_parsers.TryGetValue(parser.Priority, out var parsers))
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("Document parser {parser} doesn't registered.", parser.GetType());
                }
                return;
            }
            parsers.TryRemove(parser.GetType(), out _);
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Unregisterd document parser: {parser} with priority {priority}", parser.GetType(), parser.Priority);
            }
            _acceptCache.Clear();
        }

        /// <summary>
        /// Asynchronously parse a document from a stream.
        /// </summary>
        /// <param name="source">Source path to identified this stream.</param>
        /// <param name="fileStream">Stream of file content.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>Parsed document.</returns>
        /// <exception cref="NotSupportedException">File type is not supported.</exception>
        public async Task<Document> ParseAsync(string source, Stream fileStream, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(source);
            var fileType = this._mimeTypeDetection.GetFileType(source);
            if (!_acceptCache.TryGetValue(fileType, out var parser))
            {
                foreach (var p in _parsers.Values.SelectMany(p => p.Values))
                {
                    if (p.Accept(fileType))
                    {
                        parser = p;
                        break;
                    }
                }
            }
            else
            {
                if (_logger.IsEnabled(LogLevel.Trace))
                {
                    _logger.LogTrace("Got cached accept parser {parser} of file type {filetype} from {source}", parser.GetType(), fileType, source);
                }
            }
            if (parser is null)
            {
                throw new NotSupportedException($"Type of {source} is not supported.");
            }
            _acceptCache.TryAdd(fileType, parser);
            return await parser.ParseAsync(source, fileStream, cancellationToken);
        }

        private class PriorityComaprer : IComparer<int>
        {
            public int Compare(int x, int y)
            {
                return -x.CompareTo(y);
            }
        }
    }

}
