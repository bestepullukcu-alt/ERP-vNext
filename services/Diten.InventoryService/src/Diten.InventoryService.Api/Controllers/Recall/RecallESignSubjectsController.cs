using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Features.Recall.Handlers;
using Diten.InventoryService.Application.Features.Recall.Ports;
using Diten.InventoryService.Domain.Recall.Rules;
using Diten.InventoryService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Diten.InventoryService.Api.Controllers.Recall;

/// <summary>
/// RECALL <c>getSignableSubject</c> (R4) — the MOD-0022 subject provider (ESIGN-BUNDLE §6), called by Platform with the SIGNER's token and
/// <c>X-Tenant-Id</c> (config <c>ESign:SubjectProviders:recall:BaseUrl</c>, operator config, never a manifest). The MOD-0176
/// <c>ExpiryESignSubjectsController</c> pattern, copied; every refusal is one 404 <c>RECALL_SUBJECT_NOT_FOUND</c> (no oracle):
/// <list type="bullet">
/// <item>the key for the subject type is missing (RECALL 1.4.0 <c>x-permission-by-subject-type</c>, O-R5-2 / CT 2026-10-14):
/// <c>recall.case</c> and <c>recall.settings-change</c> need <c>recall.cases.read</c>; <c>recall.drill-run</c> needs <c>recall.cases.read</c>
/// OR <c>recall.drills.read</c>;</item>
/// <item>an unknown type (R5: <c>recall.drill-run</c> is served), a subject not of the caller's tenant, a company named by the caller that is
/// not the record's;</item>
/// <item>a settings change prepared by another user.</item>
/// </list>
/// With no company named (MOD-0022 sends none), the scope middleware lets this read-only path through and the record's own company is taken.
/// </summary>
[Route("api/recall/esign-subjects")]
[Authorize]
public sealed class RecallESignSubjectsController : CustomBaseController
{
    private readonly IMediator _mediator;

    public RecallESignSubjectsController(IMediator mediator) => _mediator = mediator;

    [HttpGet("{subjectType}/{subjectId}")]
    public async Task<IActionResult> GetSignableSubject(string subjectType, string subjectId, CancellationToken ct)
    {
        if (subjectType is not (RecallSignableActions.CaseSubjectType or RecallSignableActions.SettingsChangeSubjectType or RecallSignableActions.DrillRunSubjectType)
            || !User.Claims.Any(c => c.Type == "permission" && KeysFor(subjectType).Contains(c.Value, StringComparer.Ordinal))
            || !Guid.TryParseExact(subjectId, "D", out var id))
        {
            return SubjectNotFound();
        }

        var tenant = HttpContext.RequestServices.GetRequiredService<ITenantContext>();
        var tenantId = tenant.IsResolved ? tenant.TenantId : Claim("tenant_id");
        var userId = tenant.IsResolved ? tenant.UserId : Claim("sub", System.Security.Claims.ClaimTypes.NameIdentifier);
        if (tenantId == Guid.Empty || userId == Guid.Empty)
        {
            return SubjectNotFound();
        }

        var subject = await HttpContext.RequestServices.GetRequiredService<IRecallSubjectDirectory>().SubjectAsync(tenantId, subjectType, id, ct);
        if (subject is not { } found || (tenant.IsResolved && tenant.LegalEntityId != found.LegalEntityId) || (found.Owner is { } owner && owner != userId))
        {
            return SubjectNotFound();
        }

        if (!tenant.IsResolved)
        {
            // set before the handler (and the company-scoped repository it needs) is resolved
            tenant.SetScope(tenantId, found.LegalEntityId, userId, LegalEntitySources.SubjectRecord);
        }

        return CreateActionResultInstance(await _mediator.Send(new GetRecallSignableSubjectQuery(subjectType, id), ct));
    }

    /// <summary>RECALL 1.4.0 <c>x-permission-by-subject-type</c> — any one key suffices.</summary>
    internal static string[] KeysFor(string subjectType) => subjectType == RecallSignableActions.DrillRunSubjectType
        ? [RecallPermissions.CasesRead, RecallPermissions.DrillsRead]
        : [RecallPermissions.CasesRead];

    private Guid Claim(params string[] types)
    {
        var values = User.Claims.Where(c => types.Contains(c.Type, StringComparer.Ordinal)).Select(c => c.Value).ToList();
        return values.Count == 1 && Guid.TryParse(values[0], out var value) ? value : Guid.Empty;
    }

    private IActionResult SubjectNotFound() => ContractFailure(StatusCodes.Status404NotFound, RecallErrorCodes.SubjectNotFound, "Signable subject not found.");
}
