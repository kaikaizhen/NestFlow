using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using NestFlow_Backend.Common;

namespace NestFlow_Backend.Helpers;

/// <summary>
/// 以 AES-256-GCM 實作高敏感欄位加解密。
/// 密文格式為 Base64( nonce(12) || tag(16) || cipher(n) )，每次加密都使用隨機 nonce。
/// </summary>
public class CryptoHelper : ICryptoHelper
{
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int KeySize = 32;

    private readonly byte[] _key;

    public CryptoHelper(IOptions<EncryptionOptions> options)
    {
        var rawKey = options.Value.Key;

        if (string.IsNullOrWhiteSpace(rawKey))
        {
            throw new InvalidOperationException(
                "Encryption:Key 未設定，請於 appsettings.Development.json 或環境變數填入 Base64 格式的 32 bytes 金鑰。");
        }

        if (!TryDecodeKey(rawKey, out var key))
        {
            throw new InvalidOperationException(
                $"Encryption:Key 必須為 Base64 格式且解碼後長度為 {KeySize} bytes。");
        }

        _key = key;
    }

    public string? Encrypt(string? plainText)
    {
        if (plainText is null)
        {
            return null;
        }

        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var tag = new byte[TagSize];
        var cipher = new byte[plainBytes.Length];

        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, plainBytes, cipher, tag);

        var payload = new byte[NonceSize + TagSize + cipher.Length];
        Buffer.BlockCopy(nonce, 0, payload, 0, NonceSize);
        Buffer.BlockCopy(tag, 0, payload, NonceSize, TagSize);
        Buffer.BlockCopy(cipher, 0, payload, NonceSize + TagSize, cipher.Length);

        return Convert.ToBase64String(payload);
    }

    public string? Decrypt(string? cipherText)
    {
        if (cipherText is null)
        {
            return null;
        }

        byte[] payload;
        try
        {
            payload = Convert.FromBase64String(cipherText);
        }
        catch (FormatException ex)
        {
            throw new CryptographicException("密文格式錯誤，無法解密。", ex);
        }

        if (payload.Length < NonceSize + TagSize)
        {
            throw new CryptographicException("密文長度不足，無法解密。");
        }

        var nonce = new byte[NonceSize];
        var tag = new byte[TagSize];
        var cipher = new byte[payload.Length - NonceSize - TagSize];

        Buffer.BlockCopy(payload, 0, nonce, 0, NonceSize);
        Buffer.BlockCopy(payload, NonceSize, tag, 0, TagSize);
        Buffer.BlockCopy(payload, NonceSize + TagSize, cipher, 0, cipher.Length);

        var plainBytes = new byte[cipher.Length];

        using var aes = new AesGcm(_key, TagSize);
        aes.Decrypt(nonce, cipher, tag, plainBytes);

        return Encoding.UTF8.GetString(plainBytes);
    }

    public string Hash(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static bool TryDecodeKey(string rawKey, out byte[] key)
    {
        key = [];

        var buffer = new byte[KeySize];
        if (!Convert.TryFromBase64String(rawKey, buffer, out var written) || written != KeySize)
        {
            return false;
        }

        key = buffer;
        return true;
    }
}
