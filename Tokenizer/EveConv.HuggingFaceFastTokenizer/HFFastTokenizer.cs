using EveConv.Abstraction.Diagnostic;
using EveConv.Abstraction.Tokenizer;
using EveConv.HuggingFaceFastTokenizer.Raw;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EveConv.HuggingFaceFastTokenizer
{
    public class HFFastTokenizer : ITextTokenizer<long>
    {
        private readonly ILogger<HFFastTokenizer> _logger;
        private readonly HFFastTokenizerConfiguration _config;

        private readonly HFTokenizer _tokenizer;

        public HFFastTokenizer(IOptions<HFFastTokenizerConfiguration> options, ILoggerFactory? loggerFactory = null)
        {
            ArgumentNullException.ThrowIfNull(options);
            options.Value.Valid();
            this._logger = (loggerFactory ?? DefaultLogger.Factory).CreateLogger<HFFastTokenizer>();
            this._config = options.Value;
            _tokenizer = HFTokenizer.FromFile(this._config.TokenizerJsonPath);
        }

        public async Task<long[]> TokenizeAsync(string input, IDictionary<string, object>? context = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return [];
            }
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Tokenizing input [1] [{len}]", input.Length);
            }
            var opt = new TokenizeOptions(context);
            var result = Array.ConvertAll(_tokenizer.Encode(input, opt.AddSpecialTokens), Convert.ToInt64);
            if (_logger.IsEnabled(LogLevel.Trace))
            {
                _logger.LogTrace("Tokenized result: [1] [{len}]", result.Length);
            }
            return result;
        }

        public async Task<long[][]> TokenizeBatchAsync(string[] inputs, IDictionary<string, object>? context = null, CancellationToken cancellationToken = default)
        {
            if (inputs is null || inputs.Length == 0)
            {
                return [];
            }
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Tokenizing batch inputs [{count}] [{lens}]", inputs.Length, string.Join(',', inputs.Select(i => i?.Length ?? 0)));
            }
            var opt = new TokenizeOptions(context);
            var embeddings = _tokenizer.BatchEncode(inputs, opt.AddSpecialTokens);
            var results = new long[embeddings.Length][];
            for (int i = 0; i < embeddings.Length; i++)
            {
                results[i] = Array.ConvertAll(embeddings[i], Convert.ToInt64);
            }
            if (_logger.IsEnabled(LogLevel.Trace))
            {
                _logger.LogTrace("Tokenized batch results: [{count}] [{lens}]", results.Length, string.Join(',', results.Select(r => r.Length)));
            }
            return results;
        }

    }

    internal class TokenizeOptions
    {
        public bool AddSpecialTokens { get; set; } = false;

        public TokenizeOptions(IDictionary<string, object>? context)
        {
            if (context is null)
            {
                return;
            }
            var ctx = new Dictionary<string, object>(context, StringComparer.OrdinalIgnoreCase);
            if (ctx.TryGetValue("AddSpecialTokens", out var addSpecialTokensObj)
                && addSpecialTokensObj is bool addSpecialTokens)
            {
                AddSpecialTokens = addSpecialTokens;
            }
        }
    }
}
