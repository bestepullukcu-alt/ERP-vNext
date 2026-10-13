namespace Diten.AuthService.Domain.Entities;

/// <summary>
/// MOD-0022 E1 — KS-3 "password once within a window" (policy 1–60 min). One row per (tenant, user); a new password
/// re-authentication that asks for a window replaces it. Bound to the browser that opened it (user-agent hash).
/// Revoked by password change, forced change, administrator password set and logout. The policy that decides whether
/// a window ticket is good enough for a given signature lives in MOD-0022 (Platform), not here.
/// </summary>
public sealed class ESignReauthWindow : EntityBase
{
    public const int MaxMinutes = 60;

    private ESignReauthWindow() { }

    public ESignReauthWindow(Guid tenantId, Guid userId, string userAgentHash, DateTime passwordVerifiedAtUtc, DateTime expiresAtUtc)
    {
        TenantId = tenantId;
        UserId = userId;
        UserAgentHash = userAgentHash;
        PasswordVerifiedAtUtc = passwordVerifiedAtUtc;
        ExpiresAtUtc = expiresAtUtc;
    }

    public Guid UserId { get; private set; }
    public string UserAgentHash { get; private set; } = string.Empty;
    public DateTime PasswordVerifiedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }
    public string? RevokedReason { get; private set; }
}
