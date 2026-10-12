namespace Diten.Platform.Application.Contracts;

/// <summary>BL-459 — a tenant's live users by lifecycle status, as AuthService counts them (numbers only).</summary>
public sealed record TenantUserCounts(long Total, long Active, long Invited, long Inactive);

/// <summary>
/// BL-459 — reads <see cref="TenantUserCounts"/> from AuthService (<c>GET internal/users/counts?tenantId=</c>, internal key).
/// The platform tenant users summary used to count only its own <c>AdminUsers</c> list — the invited administrators —
/// never the people the tenant itself added. Best-effort: <c>null</c> when AuthService cannot answer; never throws.
/// </summary>
public interface ITenantUserCountReader
{
    Task<TenantUserCounts?> GetCountsAsync(Guid tenantId, CancellationToken ct = default);
}
