using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Options;

namespace EveConv.HuggingFaceFastTokenizer
{
    public class HFFastTokenizerConfiguration : IOptions<HFFastTokenizerConfiguration>
    {
        /// <summary>
        /// Path of tokenizer.json file.
        /// </summary>
        public string TokenizerJsonPath { get; set; } = string.Empty;

        public HFFastTokenizerConfiguration Value => this;

        internal void Valid()
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(TokenizerJsonPath);
        }
    }
}
