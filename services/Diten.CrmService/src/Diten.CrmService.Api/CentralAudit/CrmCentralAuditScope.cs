using System.Security.Claims;
using Diten.BuildingBlocks.CentralAudit;
using Diten.CrmService.Application.Common;

namespace Diten.CrmService.Api.CentralAudit;

/// <summary>
/// CRM-AUD-KN-01 (BL-501) — CRM's adapter for the shared central-audit forwarder. Tenant, user and name come from the
/// caller's RESOLVED scope — never from a request body, never a fixed / default tenant:
/// <list type="bullet">
/// <item>Tenant: the signed token's <c>tenant_id</c> when it carries one, otherwise CRM's resolved
/// <see cref="ITenantContext"/> (<c>TenantResolutionMiddleware</c>: the <c>X-Tenant-Id</c> header, refused 400 when it
/// contradicts the token). Both present and different → not resolved (no record filed, logged as an error by the
/// recorder) — the middleware already refuses that request; this is the second lock.</item>
/// <item>User: <c>sub</c> (or the inbound-mapped <see cref="ClaimTypes.NameIdentifier"/>), when it is a Guid.</item>
/// </list>
/// </summary>
public sealed class CrmCentralAuditScope(ITenantContext tenant, IHttpContextAccessor http) : ICentralAuditScope
{
    private ClaimsPrincipal? User => http.HttpContext?.User;

    public bool IsResolved => ResolveTenant() is not null;

    public Guid TenantId => ResolveTenant() ?? Guid.Empty;

    /// <summary>CRM resolves no legal entity for its Knowledge writes.</summary>
    public Guid? CompanyId => null;

    public Guid? ActorId => Guid.TryParse(Claim("sub", ClaimTypes.NameIdentifier), out var id) && id != Guid.Empty ? id : null;

    /// <summary>The name AuthService writes (<c>name</c>, or <c>given_name</c> + <c>family_name</c>); Platform masks it.</summary>
    public string? ActorDisplayName
    {
        get
        {
            var name = Claim("name", ClaimTypes.Name);
            if (!string.IsNullOrWhiteSpace(name))
            {
                return name;
            }

            var full = $"{Claim("given_name", ClaimTypes.GivenName)} {Claim("family_name", ClaimTypes.Surname)}".Trim();
            return full.Length == 0 ? null : full;
        }
    }

    public string ActorType => Claim("actor_type") switch
    {
        "platform_admin" => CentralAuditActorTypes.PlatformAdministrator,
        "partner_admin" => CentralAuditActorTypes.PartnerAdministrator,
        _ => CentralAuditActorTypes.TenantUser
    };

    /// <summary>The gateway's <c>X-Correlation-Id</c> when it is a Guid; otherwise empty (the recorder uses the event id).</summary>
    public Guid CorrelationId =>
        Guid.TryParse(http.HttpContext?.Request.Headers["X-Correlation-Id"].ToString(), out var id) ? id : Guid.Empty;

    private Guid? ResolveTenant()
    {
        var resolved = tenant.TenantId;
        var signed = Guid.TryParse(Claim("tenant_id"), out var fromToken) && fromToken != Guid.Empty ? fromToken : (Guid?)null;

        if (signed is { } token)
        {
            return resolved is null || resolved == token ? token : null;
        }

        return resolved is { } id && id != Guid.Empty ? id : null;
    }

    private string? Claim(params string[] types) =>
        types.Select(type => User?.FindFirst(type)?.Value).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
}
