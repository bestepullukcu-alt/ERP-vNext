using Diten.BuildingBlocks.CentralAudit;
using Diten.InventoryService.Application.Common;

namespace Diten.InventoryService.Api.CentralAudit;

/// <summary>
/// BL-501 — Inventory's adapter for the shared central-audit forwarder: tenant, company (legal entity) and user come
/// from the RESOLVED caller scope (<see cref="ITenantContext"/>: signed JWT <c>tenant_id</c> / <c>sub</c>, legal entity
/// from the token or the MDM-verified header — never a body); the name and the actor type from the caller's token when
/// there is one.
///
/// <para>Outside an HTTP request (the recall quarantine sweeper, the MOD-0023 outcome consumer) the scope is the one
/// that code set for the user it acts for (the case's requester / the approver): the record names that user, with
/// actor type <c>TenantUser</c> and no display name.</para>
/// </summary>
public sealed class InventoryCentralAuditScope(
    ITenantContext tenant,
    ICorrelationContext correlation,
    IHttpContextAccessor http) : ICentralAuditScope
{
    public bool IsResolved => tenant.IsResolved;

    public Guid TenantId => tenant.TenantId;

    public Guid? CompanyId => tenant.LegalEntityId;

    public Guid? ActorId => tenant.UserId;

    public string? ActorDisplayName
    {
        get
        {
            var user = http.HttpContext?.User;
            var name = user?.FindFirst("name")?.Value;
            if (!string.IsNullOrWhiteSpace(name))
            {
                return name;
            }

            var full = $"{user?.FindFirst("given_name")?.Value} {user?.FindFirst("family_name")?.Value}".Trim();
            return full.Length == 0 ? null : full;
        }
    }

    public string ActorType => http.HttpContext?.User.FindFirst("actor_type")?.Value switch
    {
        "platform_admin" => CentralAuditActorTypes.PlatformAdministrator,
        "partner_admin" => CentralAuditActorTypes.PartnerAdministrator,
        _ => CentralAuditActorTypes.TenantUser
    };

    /// <summary>The request's correlation id; a background flow has none (the recorder then uses the event id).</summary>
    public Guid CorrelationId
    {
        get
        {
            try
            {
                return correlation.CorrelationId;
            }
            catch (InvalidOperationException)
            {
                return Guid.Empty;
            }
        }
    }
}
