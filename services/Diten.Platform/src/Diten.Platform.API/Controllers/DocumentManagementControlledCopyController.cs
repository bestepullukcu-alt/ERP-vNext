using Diten.Platform.API.Controllers.Common;
using Diten.Platform.API.Models.DocumentManagement;
using Diten.Platform.API.Observability;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Features.DocumentManagementControlledCopy;
using Diten.Platform.Application.Features.DocumentManagementControlledCopy.Commands;
using Diten.Platform.Application.Features.DocumentManagementControlledCopy.Queries;
using Diten.Platform.Application.Features.DocumentManagementControlledDocuments;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers;

/// <summary>
/// MOD-0029-FU17 — TenantShell controlled copy / obsolete copy reconciliation API (GMG-QMS-SOP-0001 §9.13, §18
/// LOG-0002). Thin controller; dispatches via MediatR. It maintains the Controlled Copy Log, drives copy withdrawal and
/// feeds FU10 Gate 6 — it is NOT a CAPA module (deviation / quality-event links are references). Layer 1 RBAC REUSES the
/// seeded controlled-documents create/view keys (no AuthService seed change); dedicated
/// <see cref="DocumentControlledCopyPermissions"/> keys should be seeded in FU06A hardening. TenantId is never read from
/// the client — it is resolved server-side.
/// </summary>
[ApiController]
[Route("api/v1/document-management")]
[Authorize]
public sealed class DocumentManagementControlledCopyController : CustomBaseController
{
    private readonly IMediator _mediator;
    private readonly ICorrelationContext _correlationContext;

    public DocumentManagementControlledCopyController(IMediator mediator, ICorrelationContext correlationContext)
    {
        _mediator = mediator;
        _correlationContext = correlationContext;
    }

    // ── controlled copies ─────────────────────────────────────────────────────

    [HttpGet("document-master-register/{id:guid}/controlled-copies")]
    [HasPermission(DocumentControlledCopyPermissions.View)]
    public async Task<IActionResult> Copies(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetControlledCopiesQuery(id, CorrelationId), ct));

    [HttpPost("document-master-register/{id:guid}/controlled-copies")]
    [HasPermission(DocumentControlledCopyPermissions.Manage)]
    public async Task<IActionResult> RegisterCopy(Guid id, [FromBody] RegisterControlledCopyApiRequest request, CancellationToken ct)
    {
        var input = new RegisterControlledCopyInput(request.CopyType, request.CopyNumber, request.LocationType, request.LocationDescription,
            request.HolderUserId, request.HolderRole, request.HolderDepartment, request.ControlledDocumentId, request.ControlledDocumentVersionId, request.RepositoryAssessmentId);
        return CreateActionResultInstance(await _mediator.Send(new RegisterControlledCopyCommand(id, input, CorrelationId), ct));
    }

    [HttpPost("document-master-register/{id:guid}/controlled-copies/{copyId:guid}/withdraw")]
    [HasPermission(DocumentControlledCopyPermissions.Manage)]
    public async Task<IActionResult> Withdraw(Guid id, Guid copyId, [FromBody] WithdrawControlledCopyApiRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new WithdrawControlledCopyCommand(id, copyId, new WithdrawControlledCopyInput(request.WithdrawalEvidenceReference), CorrelationId), ct));

    [HttpPost("document-master-register/{id:guid}/controlled-copies/{copyId:guid}/reconcile")]
    [HasPermission(DocumentControlledCopyPermissions.Reconcile)]
    public async Task<IActionResult> Reconcile(Guid id, Guid copyId, [FromBody] ReconcileControlledCopyApiRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new ReconcileControlledCopyCommand(id, copyId, new ReconcileControlledCopyInput(request.ReconciliationEvidenceReference), CorrelationId), ct));

    [HttpPost("document-master-register/{id:guid}/controlled-copies/{copyId:guid}/mark-missing")]
    [HasPermission(DocumentControlledCopyPermissions.Manage)]
    public async Task<IActionResult> MarkMissing(Guid id, Guid copyId, [FromBody] MarkControlledCopyMissingApiRequest? request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new MarkControlledCopyMissingCommand(id, copyId, new MarkControlledCopyMissingInput(request?.Comment), CorrelationId), ct));

    [HttpPost("document-master-register/{id:guid}/controlled-copies/{copyId:guid}/mark-obsolete")]
    [HasPermission(DocumentControlledCopyPermissions.Manage)]
    public async Task<IActionResult> MarkObsolete(Guid id, Guid copyId, [FromBody] MarkControlledCopyObsoleteApiRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new MarkControlledCopyObsoleteCommand(id, copyId, new MarkControlledCopyObsoleteInput(request.ObsoleteReason, request.LocationDescription), CorrelationId), ct));

    // ── withdrawal plans ──────────────────────────────────────────────────────

    [HttpGet("document-master-register/{id:guid}/copy-withdrawal-plans")]
    [HasPermission(DocumentControlledCopyPermissions.View)]
    public async Task<IActionResult> Plans(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetWithdrawalPlansQuery(id, CorrelationId), ct));

    [HttpPost("document-master-register/{id:guid}/copy-withdrawal-plans/generate")]
    [HasPermission(DocumentControlledCopyPermissions.Manage)]
    public async Task<IActionResult> GeneratePlan(Guid id, [FromBody] GenerateWithdrawalPlanApiRequest? request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GenerateWithdrawalPlanCommand(id, new GenerateWithdrawalPlanInput(request?.TriggerType, request?.DueDate), CorrelationId), ct));

    [HttpPost("document-master-register/{id:guid}/copy-withdrawal-plans/{planId:guid}/complete")]
    [HasPermission(DocumentControlledCopyPermissions.Manage)]
    public async Task<IActionResult> CompletePlan(Guid id, Guid planId, [FromBody] CompleteWithdrawalPlanApiRequest? request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CompleteWithdrawalPlanCommand(id, planId, new CompleteWithdrawalPlanInput(request?.PlanEvidenceReference, request?.MissingDeviationReference), CorrelationId), ct));

    [HttpGet("document-master-register/{id:guid}/copy-withdrawal-readiness")]
    [HasPermission(DocumentControlledCopyPermissions.View)]
    public async Task<IActionResult> Readiness(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetCopyWithdrawalReadinessQuery(id, CorrelationId), ct));

    // ── obsolete reconciliation ───────────────────────────────────────────────

    [HttpGet("document-master-register/{id:guid}/obsolete-copy-findings")]
    [HasPermission(DocumentControlledCopyPermissions.View)]
    public async Task<IActionResult> Findings(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetObsoleteCopyFindingsQuery(id, CorrelationId), ct));

    [HttpPost("document-master-register/{id:guid}/obsolete-copy-reconciliation/evaluate")]
    [HasPermission(DocumentControlledCopyPermissions.Reconcile)]
    public async Task<IActionResult> EvaluateReconciliation(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new EvaluateObsoleteCopyReconciliationCommand(id, CorrelationId), ct));

    [HttpPost("document-master-register/{id:guid}/obsolete-copy-findings/{findingId:guid}/resolve")]
    [HasPermission(DocumentControlledCopyPermissions.Manage)]
    public async Task<IActionResult> ResolveFinding(Guid id, Guid findingId, [FromBody] ResolveObsoleteCopyFindingApiRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new ResolveObsoleteCopyFindingCommand(id, findingId,
            new ResolveObsoleteFindingInput(request.ResolutionEvidenceReference, request.DeviationReference, request.QualityEventReference), CorrelationId), ct));

    private string CorrelationId =>
        string.IsNullOrWhiteSpace(_correlationContext.CorrelationId) ? HttpContext.TraceIdentifier : _correlationContext.CorrelationId!;
}
