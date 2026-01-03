using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace EveConv.HuggingFaceFastTokenizer
{
    public class HFTokenizerConfig
    {
        [JsonPropertyName("added_tokens_decoder")]
        public IDictionary<long, HFTokenizerAddedTokensDecoder> AddedTokensDecoder { get; set; } = new Dictionary<long, HFTokenizerAddedTokensDecoder>();

        [JsonPropertyName("bos_token")]
        public string BosToken { get; set; } = string.Empty;

        [JsonPropertyName("eos_token")]
        public string EosToken { get; set; } = string.Empty;

        [JsonPropertyName("pad_token")]
        public string PadToken { get; set; } = string.Empty;

        [JsonPropertyName("sep_token")]
        public string SepToken { get; set; } = string.Empty;

        [JsonPropertyName("cls_token")]
        public string ClsToken { get; set; } = string.Empty;

        [JsonPropertyName("mask_token")]
        public string MaskToken { get; set; } = string.Empty;

        [JsonPropertyName("unk_token")]
        public string UnkToken { get; set; } = string.Empty;

        [JsonPropertyName("clean_up_tokenization_spaces")]
        public bool CleanUpTokenizationSpaces { get; set; } = true;

        [JsonPropertyName("tokenizer_class")]
        public string TokenizerClass { get; set; } = string.Empty;

        [JsonPropertyName("add_prefix_space")]
        public bool AddPrefixSpace { get; set; } = false;

        [JsonPropertyName("model_max_length")]
        public long ModelMaxLength { get; set; } = -1;

        [JsonPropertyName("chat_template")]
        public string ChatTemplate { get; set; } = string.Empty;

        [JsonPropertyName("errors")]
        public string Errors { get; set; } = string.Empty;

        [JsonPropertyName("padding_side")]
        public string PaddingSide { get; set; } = string.Empty;

        [JsonPropertyName("truncation_side")]
        public string TruncationSide { get; set; } = string.Empty;

        [JsonPropertyName("model_input_names")]
        public IList<string> ModelInputNames { get; set; } = [];

        [JsonPropertyName("do_lower_case")]
        public bool? DoLowerCase { get; set; }

        [JsonPropertyName("strip_accents")]
        public bool? StripAccents { get; set; }

        [JsonPropertyName("tokenize_chinese_chars")]
        public bool? TokenizeChineseChars { get; set; }

        [JsonPropertyName("special_tokens_map_file")]
        public string SpecialTokensMapFile { get; set; } = string.Empty;

        [JsonPropertyName("tokenizer_file")]
        public string TokenizerFile { get; set; } = string.Empty;

        [JsonPropertyName("name_or_path")]
        public string NameOrPath { get; set; } = string.Empty;

        [JsonPropertyName("revision")]
        public string Revision { get; set; } = string.Empty;

        [JsonPropertyName("use_fast")]
        public bool? UseFast { get; set; }

        [JsonPropertyName("legacy")]
        public bool? Legacy { get; set; }
    }

    public class HFTokenizerAddedTokensDecoder
    {
        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;

        [JsonPropertyName("single_word")]
        public bool SingleWord { get; set; } = false;

        [JsonPropertyName("lstrip")]
        public bool LStrip { get; set; } = false;

        [JsonPropertyName("rstrip")]
        public bool RStrip { get; set; } = false;

        [JsonPropertyName("normalized")]
        public bool Normalized { get; set; } = false;

        [JsonPropertyName("special")]
        public bool Special { get; set; } = false;
    }
}
