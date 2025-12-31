using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace EveConv.HuggingFaceFastTokenizer
{
    internal class HFTokenizerEncodeResultHandle : SafeHandle
    {
        private readonly unsafe TokenizerEncodeResult* encodeResult;

        internal unsafe HFTokenizerEncodeResultHandle(TokenizerEncodeResult* result) : base((nint)result, true)
        {
            encodeResult = result;
        }

        public override bool IsInvalid => handle == 0;

        protected override bool ReleaseHandle()
        {
            unsafe
            {
                NativeMethods.tokenizers_free_encode_results(encodeResult, 1);
            }
            return true;
        }

        public uint[] TokenIds
        {
            get
            {
                unsafe
                {
                    if (encodeResult is null || encodeResult->token_ids is null || encodeResult->len == 0)
                    {
                        return [];
                    }
                    var result = new uint[encodeResult->len];
                    for (nuint i = 0; i < encodeResult->len; i++)
                    {
                        result[i] = encodeResult->token_ids[i];
                    }
                    return result;
                }
            }
        }
    }
}
