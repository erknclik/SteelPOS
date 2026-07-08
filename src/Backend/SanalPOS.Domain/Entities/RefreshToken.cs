using SanalPOS.Domain.Common;

namespace SanalPOS.Domain.Entities;

/// <summary>
/// Refresh token kaydı. Token'ın kendisi değil SHA-256 hash'i saklanır (bkz. docs/11-guvenlik.md §2).
/// Her kullanımda rotate edilir; tekrar kullanım tespitinde kullanıcının tüm oturumları iptal edilir.
/// </summary>
public class RefreshToken : BaseEntity
{
    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTime ExpiresAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public string? ReplacedByTokenHash { get; private set; }

    protected RefreshToken()
    {
    }

    public RefreshToken(Guid userId, string tokenHash, DateTime expiresAt)
    {
        UserId = userId;
        TokenHash = tokenHash;
        ExpiresAt = expiresAt;
    }

    public bool IsActive => RevokedAt is null && ExpiresAt > DateTime.UtcNow;

    public void Revoke(string? replacedByTokenHash = null)
    {
        RevokedAt = DateTime.UtcNow;
        ReplacedByTokenHash = replacedByTokenHash;
    }
}
