using System.Security.Cryptography;
using System.Text;

namespace NestFlow_Backend.Helpers;

public class LineSignatureValidator : ILineSignatureValidator
{
    public bool IsValid(string channelSecret, ReadOnlySpan<byte> body, string? signature)
    {
        if (string.IsNullOrWhiteSpace(channelSecret) || string.IsNullOrWhiteSpace(signature))
        {
            return false;
        }

        Span<byte> computed = stackalloc byte[32];

        if (!HMACSHA256.TryHashData(Encoding.UTF8.GetBytes(channelSecret), body, computed, out _))
        {
            return false;
        }

        Span<byte> provided = stackalloc byte[32];

        if (!Convert.TryFromBase64String(signature, provided, out var written) || written != 32)
        {
            return false;
        }

        // 固定時間比對，避免以回應時間推測簽章
        return CryptographicOperations.FixedTimeEquals(computed, provided);
    }
}
