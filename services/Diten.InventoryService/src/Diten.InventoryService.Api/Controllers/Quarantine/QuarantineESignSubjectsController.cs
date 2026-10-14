using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Features.Quarantine;
using Diten.InventoryService.Application.Features.Quarantine.Handlers;
using Diten.InventoryService.Application.Features.Quarantine.Ports;
using Diten.InventoryService.Application.Features.Quarantine.Services;
using Diten.InventoryService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.InventoryService.Api.Controllers.Quarantine;

/// <summary>
/// QUARANTINE-BUNDLE <c>getSignableSubject</c> — the MOD-0022 subject provider (ESIGN-BUNDLE §6), called by Platform with the
/// SIGNER's token and <c>X-Tenant-Id</c> (through the Gateway). Every refusal is 404, one answer with no oracle:
/// <list type="bullet">
/// <item>the caller lacks <c>quarantine.holds.read</c> (pack §8: "the approver therefore also needs quarantine.holds.read");</item>
/// <item>the type is not <c>quarantine.decision</c>, the id is not a decision of the caller's tenant;</item>
/// <item>the caller's company (token claim or <c>X-Legal-Entity-Id</c>) is not the decision's.</item>
/// </list>
/// With no company named (MOD-0022 sends none; AuthService tokens carry none), the scope middleware lets this read-only path
/// through and the decision's own company is taken (ASSUMPTION-0175-Q3-02). The answer is the subject itself (no envelope),
/// like every QUARANTINE-BUNDLE success body.
/// </summary>
[Route("api/quarantine/esign-subjects")]
[Authorize]
public sealed class QuarantineESignSubjectsController : CustomBaseController
{
    private readonly IMediator _mediator;

    public QuarantineESignSubjectsController(IMediator mediator) => _mediator = mediator;

    [HttpGet("{subjectType}/{subjectId}")]
    public async Task<IActionResult> GetSignableSubject(string subjectType, string subjectId, CancellationToken ct)
    {
        if (!User.Claims.Any(c => c.Type == "permission" && c.Value == QuarantinePermissions.HoldsRead)
            || !string.Equals(subjectType, QuarantineSignableActions.SubjectType, StringComparison.Ordinal)
            || !QuarantineSupport.TryParseId(subjectId, out var decisionId))
        {
            return SubjectNotFound();
        }

        var tenant = HttpContext.RequestServices.GetRequiredService<ITenantContext>();
        // the scope middleware resolved a named company, or (no company named) left the scope for the record to set
        var tenantId = tenant.IsResolved ? tenant.TenantId : Claim("tenant_id");
        var userId = tenant.IsResolved ? tenant.UserId : Claim("sub", System.Security.Claims.ClaimTypes.NameIdentifier);
        if (tenantId == Guid.Empty || userId == Guid.Empty)
        {
            return SubjectNotFound();
        }

        var company = await HttpContext.RequestServices.GetRequiredService<IQuarantineEventInbox>().DecisionCompanyAsync(tenantId, decisionId, ct);
        if (company is null || (tenant.IsResolved && tenant.LegalEntityId != company.Value))
        {
            return SubjectNotFound();
        }

        if (!tenant.IsResolved)
        {
            // set before the handler (and the company-scoped repositories it needs) is resolved
            tenant.SetScope(tenantId, company.Value, userId, LegalEntitySources.SubjectRecord);
        }

        return CreateActionResultInstance(await _mediator.Send(new GetSignableSubjectQuery(decisionId), ct));
    }

    private Guid Claim(params string[] types)
    {
        var values = User.Claims.Where(c => types.Contains(c.Type, StringComparer.Ordinal)).Select(c => c.Value).ToList();
        return values.Count == 1 && Guid.TryParse(values[0], out var value) ? value : Guid.Empty;
    }

    private IActionResult SubjectNotFound() => ContractFailure(StatusCodes.Status404NotFound, QuarantineErrorCodes.DecisionNotFound, "Signable subject not found.");
}
