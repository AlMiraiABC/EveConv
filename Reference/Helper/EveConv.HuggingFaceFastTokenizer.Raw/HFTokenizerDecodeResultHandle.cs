using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace EveConv.HuggingFaceFastTokenizer.Raw
{
    internal class HFTokenizerDecodeResultHandle : SafeHandle
    {
        private readonly unsafe TokenizerDecodeResult* decodeResult;

        public unsafe HFTokenizerDecodeResultHandle(TokenizerDecodeResult* result) : base((nint)result, true)
        {
            decodeResult = result;
        }

        public unsafe HFTokenizerDecodeResultHandle(TokenizerDecodeResult result) : this(&result)
        {

        }

        public override bool IsInvalid => handle == 0;

        protected override bool ReleaseHandle()
        {
            unsafe
            {
                NativeMethods.tokenizers_free_decode_results(decodeResult, 1);
            }
            return true;
        }

        public override string ToString()
        {
            unsafe
            {
                if (decodeResult is null || decodeResult->chars is null || decodeResult->len == 0)
                {
                    return string.Empty;
                }
                return Marshal.PtrToStringUTF8((nint)decodeResult->chars, (int)decodeResult->len);
            }
        }
    }
}
