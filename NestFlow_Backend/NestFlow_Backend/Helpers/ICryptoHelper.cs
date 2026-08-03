namespace NestFlow_Backend.Helpers;

/// <summary>
/// 高敏感欄位加解密。純函數，不含商業邏輯、不存取資料庫。
/// </summary>
public interface ICryptoHelper
{
    /// <summary>
    /// 將明文加密為可存入資料庫的 Base64 字串。傳入 null 時回傳 null。
    /// </summary>
    string? Encrypt(string? plainText);

    /// <summary>
    /// 將資料庫取出的 Base64 密文還原為明文。傳入 null 時回傳 null。
    /// </summary>
    string? Decrypt(string? cipherText);

    /// <summary>
    /// 產生不可逆雜湊，供邀請碼、Session Token 等只需比對不需還原的值使用。
    /// </summary>
    string Hash(string value);
}
