using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using NestFlow_Backend.Common;
using NestFlow_Backend.Helpers;

namespace NestFlow_Backend.Tests;

public class CryptoHelperTests
{
    private const string KeyA = "MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY=";
    private const string KeyB = "ZmVkY2JhOTg3NjU0MzIxMGZlZGNiYTk4NzY1NDMyMTA=";

    [Fact]
    public void 加密後解密_應還原為原始明文()
    {
        var helper = CreateHelper(KeyA);
        const string plainText = "U1234567890abcdef 機密識別碼";

        var cipherText = helper.Encrypt(plainText);
        var restored = helper.Decrypt(cipherText);

        Assert.NotNull(cipherText);
        Assert.NotEqual(plainText, cipherText);
        Assert.Equal(plainText, restored);
    }

    [Fact]
    public void 以不同金鑰解密_應拋出例外()
    {
        var cipherText = CreateHelper(KeyA).Encrypt("機密資料");
        var otherHelper = CreateHelper(KeyB);

        Assert.Throws<AuthenticationTagMismatchException>(() => otherHelper.Decrypt(cipherText));
    }

    [Fact]
    public void 金鑰未設定_應在建構時拋出例外()
    {
        Assert.Throws<InvalidOperationException>(() => CreateHelper(string.Empty));
    }

    private static ICryptoHelper CreateHelper(string key)
    {
        return new CryptoHelper(Options.Create(new EncryptionOptions { Key = key }));
    }
}
