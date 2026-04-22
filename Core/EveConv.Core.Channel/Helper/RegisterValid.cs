using System.Security.Cryptography;

namespace EveConv.Core.Channel.Helper;

public static class RegisterValid
{
    /// <summary>
    /// Verify signature of this content with RSA.
    /// </summary>
    /// <param name="content">The plain content.</param>
    /// <param name="sign">The specified signature.</param>
    /// <param name="pemPubKey">Public key to verify in RFC 7468 PEM-encoded.</param>
    /// <param name="hashAlgorithm">Hash algorithm.</param>
    /// <param name="paddingMode">Public key padding mode.</param>
    /// <returns><see langword="true"/> if this sign is valid. Otherwise, <see langword="false"/>.</returns>
    public static bool RsaVerifySign(ReadOnlyMemory<byte> content, ReadOnlyMemory<byte> sign,
        ReadOnlySpan<char> pemPubKey,
        HashAlgorithmName hashAlgorithm,
        RSASignaturePadding paddingMode)
    {
        if (content.IsEmpty || sign.IsEmpty || pemPubKey.Length == 0)
        {
            return false;
        }
        try
        {
            using var rsa = RSA.Create();
            rsa.ImportFromPem(pemPubKey);
            return rsa.VerifyData(content.Span, sign.Span, hashAlgorithm, paddingMode);
        }
        catch
        {
            return false;
        }
    }

    public static bool RsaVerifySign(ReadOnlyMemory<byte> content, ReadOnlyMemory<byte> sign, string pemPubKey,
        string hashAlgorithm,
        RSASignaturePaddingMode paddingMode)
    {
        return RsaVerifySign(content, sign, pemPubKey, new HashAlgorithmName(hashAlgorithm),
            paddingMode switch
            {
                RSASignaturePaddingMode.Pkcs1 => RSASignaturePadding.Pkcs1,
                RSASignaturePaddingMode.Pss => RSASignaturePadding.Pss,
                _ => throw new NotSupportedException($"Padding mode {paddingMode} is not supported."),
            });
    }
}
