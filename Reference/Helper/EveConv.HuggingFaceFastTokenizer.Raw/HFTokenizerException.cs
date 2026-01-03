using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;

namespace EveConv.HuggingFaceFastTokenizer.Raw
{
    public class HFTokenizerException : Exception
    {
        public HFTokenizerException(string message) : base(message)
        {
        }

        internal static bool CheckError(int result, [NotNullWhen(true)] out HFTokenizerException? exception)
        {
            if (result == 0)
            {
                exception = null;
                return false;
            }
            exception = FromLastError();
            return true;
        }

        internal static HFTokenizerException FromLastError()
        {
            unsafe
            {
                byte* errPtr;
                nuint errLen;
                NativeMethods.tokenizers_get_last_error(&errPtr, &errLen);
                if (errPtr is null)
                {
                    return new HFTokenizerException("Unknown error");
                }
                var message = Marshal.PtrToStringUTF8((nint)errPtr, (int)errLen);

                return new HFTokenizerException(message);
            }
        }
    }
}
