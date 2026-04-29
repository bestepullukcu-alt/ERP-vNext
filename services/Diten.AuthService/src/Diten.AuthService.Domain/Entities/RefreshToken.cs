namespace Diten.AuthService.Domain.Entities;

public sealed class RefreshToken : EntityBase
{
    private RefreshToken() { }

    public RefreshToken(
        Guid userId,
        string tokenHash,
        DateTime expiresAt,
        string createdByIp,
        Guid tenantId,
        string actorType,
        string? userAgent = null,
        Guid? sessionId = null,
        string? deviceId = null)
    {
        UserId = userId;
        Token = tokenHash;
        ExpiresAt = expiresAt;
        CreatedByIp = createdByIp;
        TenantId = tenantId;
        ActorType = actorType;
        UserAgent = userAgent;
        SessionId = sessionId ?? Guid.NewGuid();
        DeviceId = deviceId;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid UserId { get; private set; }
    // Stores a keyed hash of the refresh token, never the raw token.
    public string Token { get; private set; } = string.Empty;
    public DateTime ExpiresAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public string? ReplacedByToken { get; private set; }
    public string? ReplacedByTokenHash { get; private set; }
    public string CreatedByIp { get; private set; } = string.Empty;
    public string? UserAgent { get; private set; }
    public Guid SessionId { get; private set; }
    public string? DeviceId { get; private set; }
    public string? RevokedByIp { get; private set; }
    public string? RevokedReason { get; private set; }
    public string ActorType { get; private set; } = string.Empty;

    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    public bool IsActive => RevokedAt == null && !IsExpired;

    public void Revoke(string? replacedByTokenHash = null, string? revokedByIp = null, string? revokedReason = null)
    {
        RevokedAt = DateTime.UtcNow;
        ReplacedByToken = null;
        ReplacedByTokenHash = replacedByTokenHash;
        RevokedByIp = revokedByIp;
        RevokedReason = revokedReason;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
