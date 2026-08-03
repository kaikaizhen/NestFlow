using System.ComponentModel.DataAnnotations;

namespace NestFlow_Backend.Common;

/// <summary>
/// 資料加密設定。實際金鑰只存在 appsettings.Development.json 或環境變數，不進版控。
/// </summary>
public class EncryptionOptions
{
    public const string SectionName = "Encryption";

    /// <summary>
    /// AES-256-GCM 主金鑰，Base64 字串，解碼後必須為 32 bytes。
    /// </summary>
    [Required(AllowEmptyStrings = false, ErrorMessage = "Encryption:Key 未設定，請於 appsettings.Development.json 或環境變數填入 Base64 格式的 32 bytes 金鑰。")]
    public string Key { get; set; } = string.Empty;
}
