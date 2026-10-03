namespace Diten.Platform.Application.Contracts;

/// <summary>
/// BL-521 — WHICH TENANTS THE PLATFORM ACTOR OF THIS REQUEST MAY TOUCH. The one place the rule lives: the tenant list,
/// the tenant's own endpoints, its subscription and its modules all ask here; none re-derives it.
///
/// <para>A platform administrator is unrestricted. A partner administrator is limited to the tenants its
/// <c>PlatformAdministrator</c> record allows — read on the server for each request (CT decision: never from the
/// token, whose list would grow and whose revocation would wait for the token to expire). Comparable: SAP limits a
/// partner by organisational unit / company code, Oracle by data access sets — both on the server.</para>
///
/// <para>⚠ FAIL CLOSED. A partner whose record cannot be read, is not active, or allows no tenant may touch NONE — never
/// "all". Any other caller (no principal, a tenant user) may touch none either.</para>
/// </summary>
public interface IPlatformActorTenantScope
{
    /// <summary>The scope of this request's actor, read once per request.</summary>
    Task<PlatformTenantScope> GetAsync(CancellationToken ct = default);
}

/// <summary>What a platform actor may touch: everything, or exactly <see cref="TenantIds"/> (possibly none).</summary>
public sealed class PlatformTenantScope
{
    private PlatformTenantScope(bool isUnrestricted, IReadOnlySet<Guid> tenantIds)
    {
        IsUnrestricted = isUnrestricted;
        TenantIds = tenantIds;
    }

    public static PlatformTenantScope Unrestricted { get; } = new(true, new HashSet<Guid>());
    public static PlatformTenantScope None { get; } = new(false, new HashSet<Guid>());
    public static PlatformTenantScope Only(IEnumerable<Guid> tenantIds) =>
        new(false, tenantIds.Where(id => id != Guid.Empty).ToHashSet());

    public bool IsUnrestricted { get; }

    /// <summary>The allowed tenants when restricted; empty means none. Ignored when unrestricted.</summary>
    public IReadOnlySet<Guid> TenantIds { get; }

    public bool Allows(Guid tenantId) => IsUnrestricted || TenantIds.Contains(tenantId);

    /// <summary>The filter a query applies: null = no filter (unrestricted), otherwise only these ids.</summary>
    public IReadOnlyCollection<Guid>? QueryFilter => IsUnrestricted ? null : TenantIds.ToList();
}

/// <summary>
/// BL-521 — endpoint metadata: a <c>partner_admin</c> may reach this endpoint (it is tenant-scoped, or a partner-owned
/// object). Without it both platform gates refuse a partner 403. Implemented by the API's PartnerAdminScopeAttribute;
/// declared here so the authorization policy (Infrastructure) can see it.
/// </summary>
public interface IPartnerAdminAllowed
{
}
