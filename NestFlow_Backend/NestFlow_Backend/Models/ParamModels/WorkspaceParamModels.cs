using System.ComponentModel.DataAnnotations;

namespace NestFlow_Backend.Models.ParamModels;

public class CreateWorkspaceParamModel
{
    [Required(ErrorMessage = "請輸入名稱。")]
    [StringLength(50, MinimumLength = 1, ErrorMessage = "名稱長度必須介於 1 到 50 個字元。")]
    public string Name { get; set; } = string.Empty;

    /// <summary>第一版只接受 personal 或 family。</summary>
    [Required(ErrorMessage = "請指定類型。")]
    public string Type { get; set; } = string.Empty;
}

public class RenameWorkspaceParamModel
{
    [Required(ErrorMessage = "請輸入名稱。")]
    [StringLength(50, MinimumLength = 1, ErrorMessage = "名稱長度必須介於 1 到 50 個字元。")]
    public string Name { get; set; } = string.Empty;
}

public class JoinWorkspaceParamModel
{
    [Required(ErrorMessage = "請輸入邀請碼。")]
    [StringLength(8, MinimumLength = 8, ErrorMessage = "邀請碼必須為 8 碼。")]
    public string Code { get; set; } = string.Empty;
}

public class SetDefaultWorkspaceParamModel
{
    [Required(ErrorMessage = "請指定資料空間。")]
    public Guid WorkspaceId { get; set; }
}
