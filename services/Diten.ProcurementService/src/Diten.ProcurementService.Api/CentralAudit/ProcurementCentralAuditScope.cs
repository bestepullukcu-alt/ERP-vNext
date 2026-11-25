using System.Security.Claims;
using Diten.BuildingBlocks.CentralAudit;
using Diten.ProcurementService.Application.Common;
using Diten.ProcurementService.Application.Interfaces;

namespace Diten.ProcurementService.Api.CentralAudit;

/// <summary>
/// BL-501 (MOD-0140 DL-3, §7, §21) — Procurement's adapter for the shared central-audit forwarder, Inventory's pattern
/// (<c>InventoryCentralAuditScope</c>): tenant and company (legal entity) come from the RESOLVED caller scope
/// (<see cref="ITenantContext"/>: the signed JWT, or the header the tenant middleware accepted — never a body), the user
/// from the token's <c>sub</c>; actor type <c>TenantUser</c> unless the token says platform / partner administrator.
/// DL-3 has no system-actor path (the onboarding reconciliation that needs <c>System</c> is DL-4, §7).
/// </summary>
public sealed class ProcurementCentralAuditScope(
    ITenantContext tenant,
    ICurrentUserContext user,
    IHttpContextAccessor http) : ICentralAuditScope
{
    public bool IsResolved => tenant.IsResolved;

    public Guid TenantId => tenant.TenantId;

    public Guid? CompanyId => tenant.LegalEntityId == Guid.Empty ? null : tenant.LegalEntityId;

    public Guid? ActorId => user.UserId == Guid.Empty ? null : user.UserId;

    /// <summary>The name AuthService writes (<c>name</c>, or <c>given_name</c> + <c>family_name</c>); both claim spellings are read.</summary>
    public string? ActorDisplayName
    {
        get
        {
            var principal = http.HttpContext?.User;
            string? Claim(params string[] types) => types.Select(t => principal?.FindFirst(t)?.Value).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

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

    /// <summary>The caller's <c>X-Correlation-Id</c> when it is a uuid; otherwise none (the recorder then uses the event id).</summary>
    public Guid CorrelationId =>
        Guid.TryParse(http.HttpContext?.Request.Headers["X-Correlation-Id"].ToString(), out var id) ? id : Guid.Empty;
}
