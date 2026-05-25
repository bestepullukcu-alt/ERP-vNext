namespace Diten.Platform.Common.Authorization;

public sealed record EntitlementCheckResult(
    bool IsAllowed,
    EntitlementKind Kind,
    string Code,
    EntitlementDenyReason? DenyReason = null,
    DateTimeOffset? ExpiresAtUtc = null,
    bool IsCacheable = true)
{
    private readonly IReadOnlyList<EntitlementDataScope>? _effectiveScopes;

    public IReadOnlyList<EntitlementDataScope> EffectiveScopes
    {
        get => _effectiveScopes ?? Array.Empty<EntitlementDataScope>();
        init => _effectiveScopes = value;
    }

    public EntitlementResolutionSource ResolvedFrom { get; init; } = EntitlementResolutionSource.Unknown;

    public DateTimeOffset ResolvedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public static EntitlementCheckResult Allowed(
        EntitlementKind kind,
        string code,
        DateTimeOffset? expiresAtUtc = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        return new EntitlementCheckResult(true, kind, code, null, expiresAtUtc);
    }

    public static EntitlementCheckResult Allowed(
        EntitlementKind kind,
        string code,
        DateTimeOffset? expiresAtUtc,
        IReadOnlyList<EntitlementDataScope>? effectiveScopes,
        EntitlementResolutionSource resolvedFrom,
        DateTimeOffset? resolvedAtUtc = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        return new EntitlementCheckResult(true, kind, code, null, expiresAtUtc)
        {
            EffectiveScopes = effectiveScopes ?? Array.Empty<EntitlementDataScope>(),
            ResolvedFrom = resolvedFrom,
            ResolvedAtUtc = resolvedAtUtc ?? DateTimeOffset.UtcNow
        };
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
