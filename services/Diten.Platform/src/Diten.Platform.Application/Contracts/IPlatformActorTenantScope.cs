using Diten.Platform.Domain.Entities;

namespace Diten.Platform.Application.Contracts;

/// <summary>
/// BL-521 — WHICH TENANTS THE PLATFORM ACTOR OF THIS REQUEST MAY TOUCH, AND WHETHER IT IS A PLATFORM ADMINISTRATOR AT
/// ALL. The one place the rule lives: the tenant list, the tenant's own endpoints, its subscription and its modules, the
/// two platform policies and the permission filter all ask here; none re-derives it.
///
/// <para>A platform administrator — a <c>platform_admin</c> token whose OWN administrator record is live (not deleted,
/// active) and is not a partner record — is unrestricted. A partner administrator — a <c>partner_admin</c> token, OR a
/// record that says PartnerAdmin whatever the token says (FIX1 F1: a demoted administrator keeps its old token for up to
/// its lifetime) — is limited to the tenants its <c>PlatformAdministrator</c> record allows, read on the server for each
/// request (CT decision: never from the token, whose list would grow and whose revocation would wait for the token to
/// expire). Comparable: SAP limits a partner by organisational unit / company code, Oracle by data access sets — both on
/// the server.</para>
///
/// <para>⚠ FAIL CLOSED. An actor whose record is missing, deleted or not active, or allows no tenant may touch NONE —
/// never "all" (FIX1 F7: this holds for a <c>platform_admin</c> token too, the same rule the permission filter already
/// applied). Any other caller (no principal, a tenant user) may touch none either.</para>
///
/// <para>⚠ A RECORD THAT CANNOT BE READ IS NO ANSWER, NOT "NONE" (FIX2 item 3): <see cref="GetAsync"/> throws
/// <see cref="PlatformAdministratorRecordUnavailableException"/> and the request is answered 503 — never all, and never a
/// silent 403 / empty list with a false <c>PartnerTenantScopeViolation</c> security event for a partner who did nothing.</para>
/// </summary>
public interface IPlatformActorTenantScope
{
    /// <summary>
    /// The scope of this request's actor, read once per request. Throws
    /// <see cref="PlatformAdministratorRecordUnavailableException"/> when the administrator record cannot be read.
    /// </summary>
    Task<PlatformTenantScope> GetAsync(CancellationToken ct = default);
}

/// <summary>
/// BL-521 FIX2 item 3 — the administrator record this request's scope depends on could not be read (the store did not
/// answer, the record does not deserialize). The request has NO answer: the API's exception handler turns this into 503
/// (reason <see cref="ReasonCode"/>), nothing of the action runs, and no security event is written — an outage is not a
/// partner probing a tenant. Never caught to mean "none" or "all".
/// </summary>
public sealed class PlatformAdministratorRecordUnavailableException : Exception
{
    /// <summary>The stable reason code of the 503 answer.</summary>
    public const string ReasonCode = "PLATFORM_ADMINISTRATOR_RECORD_UNAVAILABLE";

    public PlatformAdministratorRecordUnavailableException(Exception innerException)
        : base("The platform administrator record could not be read.", innerException)
    {
    }
}

/// <summary>What a platform actor may touch: everything, or exactly <see cref="TenantIds"/> (possibly none).</summary>
public sealed class PlatformTenantScope
{
    private PlatformTenantScope(bool isUnrestricted, bool isPartner, IReadOnlySet<Guid> tenantIds)
    {
        IsUnrestricted = isUnrestricted;
        IsPartner = isPartner;
        TenantIds = tenantIds;
    }

    /// <summary>A platform administrator confirmed by its own live record.</summary>
    public static PlatformTenantScope Unrestricted { get; } = new(true, false, new HashSet<Guid>());

    /// <summary>Nothing: no principal, a tenant user, or a platform actor without a live record.</summary>
    public static PlatformTenantScope None { get; } = new(false, false, new HashSet<Guid>());

    /// <summary>
    /// A partner administrator's tenants. Never the platform system tenant (FIX1 F6): that is the home of the platform
    /// administrators, not a customer a partner can be given — a record that names it does not open it.
    /// </summary>
    public static PlatformTenantScope Only(IEnumerable<Guid> tenantIds) =>
        new(false, true, tenantIds
            .Where(id => id != Guid.Empty && !SystemTenantRules.IsSystemTenantId(id))
            .ToHashSet());

    /// <summary>
    /// A platform administrator confirmed by its record: the only actor the global (unmarked) platform endpoints and the
    /// <c>PlatformAdminOnly</c> policy admit.
    /// </summary>
    public bool IsUnrestricted { get; }

    /// <summary>A partner administrator (by its token or by its record) with a live record.</summary>
    public bool IsPartner { get; }

    /// <summary>The allowed tenants when restricted; empty means none. Ignored when unrestricted.</summary>
    public IReadOnlySet<Guid> TenantIds { get; }

    public bool Allows(Guid tenantId) => IsUnrestricted || TenantIds.Contains(tenantId);

    /// <summary>The filter a query applies: null = no filter (unrestricted), otherwise only these ids.</summary>
    public IReadOnlyCollection<Guid>? QueryFilter => IsUnrestricted ? null : TenantIds.ToList();
}

/// <summary>
/// BL-521 — endpoint metadata: a <c>partner_admin</c> may reach this endpoint (it is tenant-scoped, a list that filters
/// by the scope, or names no tenant at all). Without it both platform gates refuse a partner 403. Implemented by the
/// API's PartnerAdminScopeAttribute; declared here so the authorization policy (Infrastructure) can see it.
/// </summary>
public interface IPartnerAdminAllowed
{
}
