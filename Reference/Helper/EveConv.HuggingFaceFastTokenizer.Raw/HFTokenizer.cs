using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace EveConv.HuggingFaceFastTokenizer.Raw
{
    /// <summary>
    /// Hugging Face Fast Tokenizer wrapper.
    /// </summary>
    /// <remarks>Doesn't support training.</remarks>
    public class HFTokenizer : SafeHandle
    {
        #region creator

        /// <summary>
        /// Create a tokenizer from a file.
        /// </summary>
        /// <param name="path">The specified file path.</param>
        /// <returns>A <see cref="HFTokenizer"/> instance.</returns>
        /// <remarks>
        ///     The file should be hugging face tokenizer.json format.
        /// </remarks>
        /// <exception cref="FileNotFoundException">The specified file path doesn't exist.</exception>
        public static HFTokenizer FromFile(string path)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("File not found.", path);
            }
            unsafe
            {
                using var cpath = new CStr(path); // safe to dispose, reading immediately.
                var wrapper = NativeMethods.tokenizers_new_from_file(cpath.Ptr, cpath.Len);
                return new HFTokenizer(wrapper);
            }
        }

        /// <summary>
        /// Create a tokenizer from content string.
        /// </summary>
        /// <param name="content">The specified content string.</param>
        /// <returns>A <see cref="HFTokenizer"/> instance.</returns>
        /// <remarks>The content should be hugging face tokenizer.json format.</remarks>
        public static HFTokenizer FromContent(string content)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(content);
            unsafe
            {
                using var ccontent = new CStr(content); // safe to dispose, reading immediately.
                var wrapper = NativeMethods.tokenizers_new_from_str(ccontent.Ptr, ccontent.Len);
                return new HFTokenizer(wrapper);
            }
        }

        /// <summary>
        /// Create a tokenizer from a readable stream.
        /// </summary>
        /// <param name="stream">The specified readable stream to get content.</param>
        /// <returns>A <see cref="HFTokenizer"/> instance.</returns>
        /// <remarks>
        ///     This will read all content from <paramref name="stream"/> at once.
        ///     The content should be hugging face tokenizer.json format.
        /// </remarks>
        /// <seealso cref="FromStreamAsync(Stream, CancellationToken)"/>
        public static HFTokenizer FromStream(Stream stream)
        {
            using StreamReader sr = new(stream);
            var content = sr.ReadToEnd();
            return FromContent(content);
        }

        /// <summary>
        /// Asynchronously create a tokenizer from a readable stream.
        /// </summary>
        /// <param name="stream">The specified readable stream to get content.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns>A <see cref="HFTokenizer"/> instance.</returns>
        /// <remarks>
        ///     This will read all content from <paramref name="stream"/> at once.
        ///     The content should be hugging face tokenizer.json format.
        /// </remarks>
        /// <seealso cref="FromStream(Stream)"/>
        public static async Task<HFTokenizer> FromStreamAsync(Stream stream, CancellationToken token = default)
        {
            using StreamReader sr = new(stream);
            var content = await sr.ReadToEndAsync(token);
            return FromContent(content);
        }

        #endregion

        /// <summary>
        /// Create a tokenizer from raw pointer.
        /// </summary>
        /// <param name="wrapper">The tokenizer wrapper raw pointer.</param>
        /// <exception cref="HFTokenizerException">Pointer is null.</exception>
        internal unsafe HFTokenizer(TokenizerWrapper* wrapper) : base((nint)wrapper, true)
        {
            if (wrapper is null)
            {
                throw HFTokenizerException.FromLastError();
            }
        }

        #region override

        public override bool IsInvalid => handle == 0;

        protected override bool ReleaseHandle()
        {
            unsafe
            {
                NativeMethods.tokenizers_free((TokenizerWrapper*)handle);
            }
            return true;
        }

        public new nint DangerousAddRef(ref bool success)
        {
            success = false;
            ObjectDisposedException.ThrowIf(IsClosed || IsInvalid, typeof(HFTokenizer));
            base.DangerousAddRef(ref success);
            return handle;
        }

        #endregion

        /// <summary>
        /// Encode the input string into token IDs.
        /// </summary>
        /// <param name="input">The specified input string.</param>
        /// <param name="addSpecialTokens">Determine whether to add special token ID.</param>
        /// <returns>A set of token ID.</returns>
        /// <seealso cref="Decode(uint[], bool)"/>
        /// <seealso cref="BatchEncode(string[], bool)"/>
        public uint[] Encode(string input, bool addSpecialTokens = false)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(input);
            return RefWrap(() =>
            {
                unsafe
                {
                    using var cinput = new CStr(input);
                    TokenizerEncodeResult encodeResult;
                    var result = NativeMethods.tokenizers_encode((TokenizerWrapper*)handle, cinput.Ptr, cinput.Len, addSpecialTokens ? 1 : 0, &encodeResult);
                    if (HFTokenizerException.CheckError(result, out var ex))
                    {
                        throw ex;
                    }
                    using var encoded = new HFTokenizerEncodeResultHandle(&encodeResult);
                    return encoded.TokenIds;
                }
            });
        }

        /// <summary>
        /// Decode the token IDs into a string.
        /// </summary>
        /// <param name="tokenIds">The specified token ids got from <see cref="Encode(string, bool)"/>.</param>
        /// <param name="skipSpecialTokens">Determine whether skip/ignore the special tokens ids.</param>
        /// <returns>Decoded string.</returns>
        /// <exception cref="ArgumentException">Token IDs cannot be null or empty.</exception>
        public string Decode(uint[] tokenIds, bool skipSpecialTokens = false)
        {
            if (tokenIds is null || tokenIds.Length == 0)
            {
                throw new ArgumentException("Token IDs cannot be null or empty.", nameof(tokenIds));
            }
            return RefWrap(() =>
            {
                unsafe
                {
                    fixed (uint* ptr = tokenIds)
                    {
                        TokenizerDecodeResult decodeResult = new();
                        var result = NativeMethods.tokenizers_decode((TokenizerWrapper*)handle, ptr, (nuint)tokenIds.Length, skipSpecialTokens ? 1 : 0, &decodeResult);
                        if (HFTokenizerException.CheckError(result, out var ex))
                        {
                            throw ex;
                        }
                        using var decoded = new HFTokenizerDecodeResultHandle(&decodeResult);
                        return decoded.ToString();
                    }
                }
            });
        }

        /// <summary>
        /// Batch encode multiple input strings into token IDs.
        /// </summary>
        /// <param name="inputs">The input strings.</param>
        /// <param name="addSpecialTokens">Determine whether add special token IDs.</param>
        /// <returns>2-dimension token IDs array. Each row per input.</returns>
        /// <remarks>This is more quickly than <see cref="Encode(string, bool)"/>.</remarks>
        /// <exception cref="ArgumentException">Inputs cannot be null or empty.</exception>
        /// <exception cref="HFTokenizerException">Encode failed.</exception>
        /// <seealso cref="Encode(string, bool)"/>
        /// <seealso cref="BatchDecode(uint[][], bool)"/>
        public uint[][] BatchEncode(string[] inputs, bool addSpecialTokens = false)
        {
            if (inputs is null || inputs.Length == 0)
            {
                throw new ArgumentException("Inputs cannot be null or empty.", nameof(inputs));
            }
            return RefWrap(() =>
            {
                unsafe
                {
                    var count = inputs.Length;
                    var cstrs = new CStr[inputs.Length];
                    var ptrs = stackalloc byte*[count];
                    var lens = stackalloc nuint[count];
                    for (int i = 0; i < inputs.Length; i++)
                    {
                        cstrs[i] = new CStr(inputs[i]);
                        ptrs[i] = cstrs[i].Ptr;
                        lens[i] = cstrs[i].Len;
                    }
                    TokenizerEncodeResult* encodeResults = stackalloc TokenizerEncodeResult[count];
                    var result = NativeMethods.tokenizers_encode_batch((TokenizerWrapper*)handle, ptrs, lens, (nuint)count, addSpecialTokens ? 1 : 0, encodeResults);
                    for (int i = 0; i < inputs.Length; i++)
                    {
                        cstrs[i].Dispose();
                    }
                    if (HFTokenizerException.CheckError(result, out var ex))
                    {
                        throw ex;
                    }
                    var hfResults = new HFTokenizerEncodeResultHandle[count];
                    for (int i = 0; i < count; i++)
                    {
                        hfResults[i] = new HFTokenizerEncodeResultHandle(&encodeResults[i]);
                    }
                    uint[][] decodeds = new uint[count][];
                    for (int i = 0; i < count; i++)
                    {
                        decodeds[i] = hfResults[i].TokenIds;
                        hfResults[i].Dispose();
                    }
                    return decodeds;
                }
            });
        }

        /// <summary>
        /// Batch decode multiple token IDs into strings.
        /// </summary>
        /// <param name="tokenIdsList">2-dimension tokenIDs array. Each row per string.</param>
        /// <param name="skipSpecialTokens">Determine whether skip/ignore special token IDs.</param>
        /// <returns>A set of string. Each element per token IDs.</returns>
        /// <exception cref="ArgumentException">Token IDs cannot be null or empty.</exception>
        /// <exception cref="HFTokenizerException">Decode failed.</exception>
        public string[] BatchDecode(uint[][] tokenIdsList, bool skipSpecialTokens = false)
        {
            if (tokenIdsList is null || tokenIdsList.Length == 0)
            {
                throw new ArgumentException("Token IDs list cannot be null or empty.", nameof(tokenIdsList));
            }
            return RefWrap(() =>
            {
                unsafe
                {
                    var count = tokenIdsList.Length;
                    var ptrs = stackalloc uint*[count];
                    var lens = stackalloc nuint[count];
                    for (int i = 0; i < count; i++)
                    {
                        if (tokenIdsList[i] is null || tokenIdsList[i].Length == 0)
                        {
                            throw new ArgumentException($"Token IDs at index {i} cannot be null or empty.", nameof(tokenIdsList));
                        }
                        fixed (uint* ptr = tokenIdsList[i])
                        {
                            ptrs[i] = ptr;
                            lens[i] = (nuint)tokenIdsList[i].Length;
                        }
                    }
                    TokenizerDecodeResult* decodeResults = stackalloc TokenizerDecodeResult[count];
                    var result = NativeMethods.tokenizers_decode_batch((TokenizerWrapper*)handle, ptrs, lens, (nuint)count, skipSpecialTokens ? 1 : 0, decodeResults);
                    if (HFTokenizerException.CheckError(result, out var ex))
                    {
                        throw ex;
                    }
                    var hfResults = new HFTokenizerDecodeResultHandle[count];
                    for (int i = 0; i < count; i++)
                    {
                        hfResults[i] = new HFTokenizerDecodeResultHandle(&decodeResults[i]);
                    }
                    string[] decodeds = new string[count];
                    for (int i = 0; i < count; i++)
                    {
                        decodeds[i] = hfResults[i].ToString();
                        hfResults[i].Dispose();
                    }
                    return decodeds;
                }
            });
        }

        /// <summary>
        /// Get string by token ID from vocabulary.
        /// </summary>
        /// <param name="id">Token ID.</param>
        /// <returns>Value of this token ID.</returns>
        /// <exception cref="HFTokenizerException">Token ID not found.</exception>
        public string IdToToken(uint id)
        {
            return RefWrap(() =>
            {
                unsafe
                {
                    TokenizerDecodeResult decodeResult = new();
                    var result = NativeMethods.tokenizers_id_to_token((TokenizerWrapper*)handle, id, &decodeResult);
                    if (HFTokenizerException.CheckError(result, out var ex))
                    {
                        throw ex;
                    }
                    using var decoded = new HFTokenizerDecodeResultHandle(&decodeResult);
                    return decoded.ToString();
                }
            });
        }

        /// <summary>
        /// Get token ID by string from vocabulary.
        /// </summary>
        /// <param name="token">Token string.</param>
        /// <returns>Token ID of this token string.</returns>
        /// <exception cref="ArgumentException">Token cannot be null or whitespace.</exception>
        /// <exception cref="HFTokenizerException">Token value not found.</exception>
        public uint TokenToId(string token)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(token);
            return RefWrap(() =>
            {
                unsafe
                {
                    using var ctoken = new CStr(token);
                    uint id = 0;
                    var result = NativeMethods.tokenizers_token_to_id((TokenizerWrapper*)handle, ctoken.Ptr, ctoken.Len, &id);
                    if (HFTokenizerException.CheckError(result, out var ex))
                    {
                        throw ex;
                    }
                    return id;
                }
            });
        }

        /// <summary>
        /// Ensures the references of handle are properly added and released.
        /// </summary>
        /// <typeparam name="T">Result type of <paramref name="func"/>.</typeparam>
        /// <param name="func">Execution.</param>
        /// <returns>Execute result of <paramref name="func"/>.</returns>
        /// <remarks>Because of the native methods needs a raw pointer instead of safe handle, it should manage references manually.</remarks>
        private unsafe T RefWrap<T>(Func<T> func)
        {
            bool addRef = false;
            DangerousAddRef(ref addRef);
            try
            {
                return func();
            }
            finally
            {
                if (addRef)
                {
                    DangerousRelease();
                }
            }
        }
    }

}
