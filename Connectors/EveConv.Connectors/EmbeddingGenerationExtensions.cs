using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.AI;

namespace EveConv.Connectors
{
    public static class EmbeddingGenerationExtensions
    {
        public static EmbeddingGenerationOptions ToOptions(this IDictionary<string, object?>? options)
        {
            if (options is null || options.Count == 0)
            {
                return new();
            }
            var opt = new Dictionary<string, object?>(options, StringComparer.OrdinalIgnoreCase);
            var result = new EmbeddingGenerationOptions();
            if (opt.TryGetValue(nameof(EmbeddingGenerationOptions.Dimensions), out var dimensionsObj)
                && TryToInt(dimensionsObj, out var dimensions))
            {
                result.Dimensions = dimensions;
                opt.Remove(nameof(EmbeddingGenerationOptions.Dimensions));
            }
            if (opt.TryGetValue(nameof(EmbeddingGenerationOptions.ModelId), out var modelIdObj))
            {
                result.ModelId = modelIdObj?.ToString();
                opt.Remove(nameof(EmbeddingGenerationOptions.ModelId));
            }
            if (opt.TryGetValue(nameof(EmbeddingGenerationOptions.AdditionalProperties), out var additionalPropertiesObj))
            {
                if (additionalPropertiesObj is AdditionalPropertiesDictionary additionalPropertiesDict)
                {
                    result.AdditionalProperties = additionalPropertiesDict;
                    opt.Remove(nameof(EmbeddingGenerationOptions.AdditionalProperties));
                }
                else if (additionalPropertiesObj is IDictionary<string, object?> dict)
                {
                    result.AdditionalProperties = new(dict);
                    opt.Remove(nameof(EmbeddingGenerationOptions.AdditionalProperties));
                }
            }
            if (opt.Count == 0)
            {
                return result;
            }
            if (result.AdditionalProperties is null)
            {
                result.AdditionalProperties = new(opt);
                return result;
            }
            foreach (var o in opt)
            {
                result.AdditionalProperties.TryAdd(o.Key, o.Value);
            }
            return result;
        }

        public static Dictionary<string, object> ToOptions(this EmbeddingGenerationOptions? options)
        {
            var opt = new Dictionary<string, object>();
            if (options?.AdditionalProperties is not null)
            {
                opt = options.AdditionalProperties.Where(i => i.Value is not null).ToDictionary(i => i.Key, i => i.Value!);
            }
            if (options?.Dimensions is not null)
            {
                opt.TryAdd(nameof(EmbeddingGenerationOptions.Dimensions), options.Dimensions.Value);
            }
            return opt;
        }

        private static bool TryToInt(object? obj, out int result)
        {
            if (obj is null)
            {
                result = 0;
                return false;
            }
            try
            {
                result = Convert.ToInt32(obj);
                return true;
            }
            catch
            {
                result = 0;
                return false;
            }
        }
    }
}
