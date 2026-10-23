using Diten.BuildingBlocks.CentralAudit;
using Diten.SupplyChainService.Application.Common;

namespace Diten.SupplyChainService.Api.CentralAudit;

/// <summary>
/// BL-501 (WP-A1) — SupplyChain's adapter for the shared central-audit forwarder. SupplyChain has no single tenant
/// context: each module gate (Shipment, Carrier, Load, Return, Claim, Capacity, S&amp;OP) resolves its own and, after
/// authorization, writes the result into the one scoped <see cref="RequestContext"/> the pipeline behaviors read. This
/// adapter reads only that: tenant and actor from the signed token (<c>tenant_id</c> / <c>sub</c>), the legal entity
/// from <c>X-Legal-Entity-Id</c> after MDM said it is the tenant's and Active — never a body. A request no gate resolved
/// leaves the scope empty, and the recorder files nothing (logged as an error).
/// </summary>
public sealed class SupplyChainCentralAuditScope(RequestContext context, IHttpContextAccessor http) : ICentralAuditScope
{
    public bool IsResolved => context.Scope.TenantId != Guid.Empty && context.Scope.ActorId != Guid.Empty;

    public Guid TenantId => context.Scope.TenantId;

    public Guid? CompanyId => context.Scope.LegalEntityId == Guid.Empty ? null : context.Scope.LegalEntityId;

    public Guid? ActorId => context.Scope.ActorId;

    /// <summary>
    /// The name AuthService writes (<c>name</c>, or <c>given_name</c> + <c>family_name</c>). SupplyChain's JWT bearer keeps
    /// the token's own claim names (<c>MapInboundClaims = false</c>), so only those spellings are read.
    /// </summary>
    public string? ActorDisplayName
    {
        get
        {
            var user = http.HttpContext?.User;
            string? Claim(string type) => user?.FindFirst(type)?.Value is { Length: > 0 } value && !string.IsNullOrWhiteSpace(value) ? value : null;

            if (Claim("name") is { } name)
            {
                return name;
            }

            var full = $"{Claim("given_name")} {Claim("family_name")}".Trim();
            return full.Length == 0 ? null : full;
        }
    }

    public string ActorType => http.HttpContext?.User.FindFirst("actor_type")?.Value switch
    {
        "platform_admin" => CentralAuditActorTypes.PlatformAdministrator,
        "partner_admin" => CentralAuditActorTypes.PartnerAdministrator,
        _ => CentralAuditActorTypes.TenantUser
    };

    public Guid CorrelationId => context.CorrelationId;
}
