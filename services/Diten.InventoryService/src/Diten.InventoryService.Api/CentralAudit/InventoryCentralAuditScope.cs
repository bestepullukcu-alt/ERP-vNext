using System.Security.Claims;
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

    /// <summary>
    /// The name AuthService writes (<c>given_name</c> + <c>family_name</c>, or <c>name</c>). Inventory's JWT bearer maps
    /// inbound claims (MapInboundClaims default), so the same claims also arrive as <see cref="ClaimTypes.GivenName"/> /
    /// <see cref="ClaimTypes.Surname"/> / <see cref="ClaimTypes.Name"/> — both spellings are read (measured in the BL-501
    /// end-to-end probe: reading only the JWT names left the record without a name).
    /// </summary>
    public string? ActorDisplayName
    {
        get
        {
            var user = http.HttpContext?.User;
            string? Claim(params string[] types) => types.Select(t => user?.FindFirst(t)?.Value).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

            var name = Claim("name", ClaimTypes.Name);
            if (!string.IsNullOrWhiteSpace(name))
            {
                return name;
            }

            var full = $"{Claim("given_name", ClaimTypes.GivenName)} {Claim("family_name", ClaimTypes.Surname)}".Trim();
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
