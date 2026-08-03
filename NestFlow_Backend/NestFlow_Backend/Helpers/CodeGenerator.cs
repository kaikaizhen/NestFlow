using System.Security.Cryptography;

namespace NestFlow_Backend.Helpers;

public class CodeGenerator : ICodeGenerator
{
    /// <summary>邀請碼字元集，已排除 0、O、1、I、L 等視覺易混字元。</summary>
    private const string InvitationAlphabet = "23456789ABCDEFGHJKMNPQRSTUVWXYZ";

    private const int InvitationCodeLength = 8;

    public string GenerateInvitationCode()
    {
        var chars = new char[InvitationCodeLength];

        for (var i = 0; i < chars.Length; i++)
        {
            chars[i] = InvitationAlphabet[RandomNumberGenerator.GetInt32(InvitationAlphabet.Length)];
        }

        return new string(chars);
    }

    public string GenerateSessionToken()
    {
        return ToUrlSafeBase64(RandomNumberGenerator.GetBytes(32));
    }

    public string GenerateOAuthValue()
    {
        return ToUrlSafeBase64(RandomNumberGenerator.GetBytes(24));
    }

    private static string ToUrlSafeBase64(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
