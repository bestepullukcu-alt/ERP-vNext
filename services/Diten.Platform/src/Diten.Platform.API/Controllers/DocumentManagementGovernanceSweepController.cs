using Diten.Platform.API.Controllers.Common;
using Diten.Platform.API.Observability;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Features.DocumentManagementDowntime;
using Diten.Platform.Application.Features.DocumentManagementElectronicSignature;
using Diten.Platform.Application.Features.DocumentManagementExternalDocuments;
using Diten.Platform.Application.Features.DocumentManagementGovernanceSweep;
using Diten.Platform.Application.Features.DocumentManagementPeriodicReview;
using Diten.Platform.Application.Features.DocumentManagementQualityEvent;
using Diten.Platform.Application.Features.DocumentManagementRetention;
using Diten.Platform.Application.Features.DocumentManagementSuspension;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers;

/// <summary>
/// MOD-0029-FU32 — TenantShell background governance sweep API (GMG-QMS-SOP-0001). Thin controller; dispatches via
/// MediatR.
///
/// BOUNDARY: a sweep is an observer. Every endpoint here evaluates due/overdue/expired/eligible conditions and
/// produces escalations (only via the pre-existing idempotent FU12/FU13/FU20 evaluators) or report lines. Nothing
/// here deletes, purges, closes, approves, makes effective, disposes of, signs or retires a subject, and there is
/// no DELETE verb anywhere in this controller. <c>dryRun: true</c> writes nothing at all, not even run history.
///
/// PERMISSIONS: FU29 seeded no dedicated governance-sweep key, so each endpoint reuses the nearest seeded key of
/// the domain it sweeps, per the FU29A attribution rules — no unseeded key is invented here. Report-only groups
/// deliberately take the narrower view/manage key of their own domain. Future recommendation:
/// platform.document-management.governance-sweeps.view / .run / .manage.
///
/// TenantId is never read from the client; it is resolved server-side from the tenant context.
/// </summary>
[ApiController]
[Route("api/v1/document-management/governance-sweeps")]
[Authorize]
public sealed class DocumentManagementGovernanceSweepController : CustomBaseController
{
    private readonly IMediator _mediator;
    private readonly ICorrelationContext _correlationContext;

    public DocumentManagementGovernanceSweepController(IMediator mediator, ICorrelationContext correlationContext)
    {
        _mediator = mediator;
        _correlationContext = correlationContext;
    }

    /// <summary>Runs every sweep group (or the subset named by <c>sweepKeys</c>) and records one run-history row.</summary>
    [HttpPost("run-all")]
    [HasPermission(DocumentRetentionPermissions.RetentionManage)]
    public async Task<IActionResult> RunAll([FromBody] GovernanceSweepRunInput? input, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new RunAllGovernanceSweepsCommand(Body(input), CorrelationId), ct));

    /// <summary>Reports what a run-all would find. Writes nothing — no escalation, no finding, no history row.</summary>
    [HttpPost("preview")]
    [HasPermission(DocumentRetentionPermissions.RetentionView)]
    public async Task<IActionResult> Preview([FromBody] GovernanceSweepRunInput? input, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new PreviewGovernanceSweepsQuery(Body(input), CorrelationId), ct));

    [HttpPost("periodic-reviews/run")]
    [HasPermission(DocumentPeriodicReviewPermissions.Manage)]
    public async Task<IActionResult> RunPeriodicReviews([FromBody] GovernanceSweepRunInput? input, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new RunPeriodicReviewSweepCommand(Body(input), CorrelationId), ct));

    [HttpPost("external-documents/run")]
    [HasPermission(ExternalDocumentPermissions.Manage)]
    public async Task<IActionResult> RunExternalDocuments([FromBody] GovernanceSweepRunInput? input, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new RunExternalDocumentSweepCommand(Body(input), CorrelationId), ct));

    [HttpPost("temporary-instructions/run")]
    [HasPermission(DocumentSuspensionPermissions.Manage)]
    public async Task<IActionResult> RunTemporaryInstructions([FromBody] GovernanceSweepRunInput? input, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new RunTemporaryInstructionSweepCommand(Body(input), CorrelationId), ct));

    [HttpPost("downtime-temporary-issues/run")]
    [HasPermission(DowntimePermissions.Manage)]
    public async Task<IActionResult> RunDowntimeTemporaryIssues([FromBody] GovernanceSweepRunInput? input, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new RunDowntimeTemporaryIssueSweepCommand(Body(input), CorrelationId), ct));

    /// <summary>Report-only: no CAPA, deviation or quality event is closed, cancelled or marked effective.</summary>
    [HttpPost("quality-capa/run")]
    [HasPermission(QualityEventPermissions.CapaView)]
    public async Task<IActionResult> RunQualityCapa([FromBody] GovernanceSweepRunInput? input, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new RunQualityCapaSweepCommand(Body(input), CorrelationId), ct));

    /// <summary>Report-only: nothing is signed, verified, invalidated or transitioned to Expired.</summary>
    [HttpPost("signature-requests/run")]
    [HasPermission(ElectronicSignaturePermissions.SignaturesView)]
    public async Task<IActionResult> RunSignatureRequests([FromBody] GovernanceSweepRunInput? input, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new RunSignatureRequestSweepCommand(Body(input), CorrelationId), ct));

    /// <summary>Report-only: nothing is deleted, purged or disposed of, and no disposition request is raised.</summary>
    [HttpPost("retention-eligibility/run")]
    [HasPermission(DocumentRetentionPermissions.RetentionView)]
    public async Task<IActionResult> RunRetentionEligibility([FromBody] GovernanceSweepRunInput? input, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new RunRetentionEligibilitySweepCommand(Body(input), CorrelationId), ct));

    /// <summary>Report-only: no hold is released, cancelled or re-scoped.</summary>
    [HttpPost("legal-hold-scope/run")]
    [HasPermission(DocumentRetentionPermissions.LegalHoldView)]
    public async Task<IActionResult> RunLegalHoldScope([FromBody] GovernanceSweepRunInput? input, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new RunLegalHoldScopeSweepCommand(Body(input), CorrelationId), ct));

    [HttpGet("runs")]
    [HasPermission(DocumentRetentionPermissions.RetentionView)]
    public async Task<IActionResult> Runs(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetGovernanceSweepRunsQuery(CorrelationId), ct));

    [HttpGet("runs/{id:guid}")]
    [HasPermission(DocumentRetentionPermissions.RetentionView)]
    public async Task<IActionResult> RunDetail(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetGovernanceSweepRunByIdQuery(id, CorrelationId), ct));

    /// <summary>An omitted body means "run with the defaults" — never a null dereference downstream.</summary>
    private static GovernanceSweepRunInput Body(GovernanceSweepRunInput? input) => input ?? new GovernanceSweepRunInput();

    private string CorrelationId =>
        string.IsNullOrWhiteSpace(_correlationContext.CorrelationId) ? HttpContext.TraceIdentifier : _correlationContext.CorrelationId!;
}
