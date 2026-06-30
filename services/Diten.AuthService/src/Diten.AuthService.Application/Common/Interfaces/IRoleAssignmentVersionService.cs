namespace Diten.AuthService.Application.Common.Interfaces;

/// <summary>
/// MOD-0018-FU13 — server-side authorization-cache invalidation. The cache key
/// (<see cref="Common.Authorization.AuthCacheContract.BuildKey"/>) embeds a monotonically increasing
/// per-tenant <c>roleAssignmentVersion</c>. Every role/permission mutation bumps this version, so the next
/// resolve computes a NEW key → the previously cached snapshot (carrying the now-stale grants) is unreachable
/// and a fresh resolve runs immediately, rather than lingering up to the 300s cache TTL.
/// </summary>
public interface IRoleAssignmentVersionService
{
    /// <summary>Current version for the tenant (0 if never mutated).</summary>
    Task<long> GetAsync(Guid tenantId, CancellationToken ct);

    /// <summary>Atomically increments and returns the tenant's new version. Upserts on first use.</summary>
    Task<long> IncrementAsync(Guid tenantId, CancellationToken ct);
}
