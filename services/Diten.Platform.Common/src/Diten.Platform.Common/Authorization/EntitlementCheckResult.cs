namespace Diten.Platform.Common.Authorization;

public sealed record EntitlementCheckResult(
    bool IsAllowed,
    EntitlementKind Kind,
    string Code,
    EntitlementDenyReason? DenyReason = null,
    DateTimeOffset? ExpiresAtUtc = null,
    bool IsCacheable = true)
{
    public static EntitlementCheckResult Allowed(
        EntitlementKind kind,
        string code,
        DateTimeOffset? expiresAtUtc = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        return new EntitlementCheckResult(true, kind, code, null, expiresAtUtc);
    }

    public static EntitlementCheckResult Denied(
        EntitlementKind kind,
        string code,
        EntitlementDenyReason denyReason,
        DateTimeOffset? expiresAtUtc = null,
        bool isCacheable = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        return new EntitlementCheckResult(false, kind, code, denyReason, expiresAtUtc, isCacheable);
    }
}
