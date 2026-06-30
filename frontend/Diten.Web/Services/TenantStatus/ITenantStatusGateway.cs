namespace Diten.Web.Services.TenantStatus;

/// <summary>
/// FIX-4 — S2S read client to Platform for a tenant's liveness, used by the shell session guard. Best-effort
/// and FAIL-OPEN: returns <c>null</c> on any inability to verify (no key, Platform unreachable/slow/5xx,
/// malformed body) so a transient blip never signs everyone out. A non-null result is a DEFINITIVE answer the
/// guard can act on. Successful lookups are short-cached (~30s) to keep the per-request guard cheap.
/// </summary>
public interface ITenantStatusGateway
{
    Task<TenantLiveness?> GetTenantLivenessAsync(Guid tenantId, CancellationToken ct = default);
}

/// <summary>Definitive tenant liveness: whether the tenant exists and is currently Active.</summary>
public sealed record TenantLiveness(bool Exists, bool IsActive);
