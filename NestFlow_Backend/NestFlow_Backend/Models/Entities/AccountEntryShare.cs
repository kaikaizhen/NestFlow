namespace NestFlow_Backend.Models.Entities;

/// <summary>一筆家庭支出中，單一參與者實際應負擔的金額。</summary>
public class AccountEntryShare
{
    public Guid Id { get; set; }

    public Guid AccountEntryId { get; set; }

    /// <summary>家庭成員時有值；臨時朋友則只保留名稱。</summary>
    public Guid? UserId { get; set; }

    public string ParticipantName { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public AccountEntry? AccountEntry { get; set; }

    public User? User { get; set; }
}
