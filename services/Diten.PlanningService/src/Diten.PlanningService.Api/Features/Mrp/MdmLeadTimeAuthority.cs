using System.Security.Claims;
using Diten.PlanningService.Application.Features.Mrp;

namespace Diten.PlanningService.Api.Features.Mrp;

public sealed class MdmLeadTimeAuthority(IHttpContextAccessor contexts,
    ILegalEntityScopeReader companies) : ILeadTimeAuthority
{
    public async Task<LeadTimeAuthorityResult> CanExecuteAsync(LeadTimeCommand command,
        string permission, CancellationToken cancellationToken)
    {
        var context = contexts.HttpContext;
        var principal = context?.User;
        if (principal?.Identity?.IsAuthenticated != true ||
            context?.Items["DemandPlanning.TenantId"] is not Guid tenant ||
            tenant == Guid.Empty || tenant != command.TenantId ||
            !Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier) ??
                principal.FindFirstValue("sub"), out var actor) || actor != command.ActorId ||
            !principal.HasClaim("permission", permission))
            return LeadTimeAuthorityResult.Forbidden;

        return await companies.ValidateAsync(tenant, command.LegalEntityId, cancellationToken) switch
        {
            LegalEntityReferenceResult.Active => LeadTimeAuthorityResult.Allowed,
            LegalEntityReferenceResult.NotFound => LeadTimeAuthorityResult.Forbidden,
            _ => LeadTimeAuthorityResult.Unavailable
        };
    }
}
