namespace NestFlow_Backend.Models.Entities;

public class Session
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    /// <summary>Session Token 的 SHA-256 雜湊。明文只在建立當下回給瀏覽器。</summary>
    public string TokenHash { get; set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public User? User { get; set; }
}
