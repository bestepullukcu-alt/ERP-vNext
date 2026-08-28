using Diten.Platform.API.Controllers.Common;
using Diten.Platform.API.Models.DocumentManagement;
using Diten.Platform.API.Observability;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Features.DocumentManagementControlledDocuments;
using Diten.Platform.Application.Features.DocumentManagementDowntime;
using Diten.Platform.Application.Features.DocumentManagementDowntime.Commands;
using Diten.Platform.Application.Features.DocumentManagementDowntime.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers;

/// <summary>
/// MOD-0029-FU20 — TenantShell repository / DMS downtime and temporary controlled issue API
/// (GMG-QMS-SOP-0001 §11.3). Thin controller; dispatches via MediatR.
///
/// THE PROCESS THIS EXPOSES: log the outage BEFORE anything is issued outside the normal environment → request a
/// temporary controlled issue → approve it with a stated mechanism and evidence → issue tracked FU17 controlled
/// copies → reconcile within 3 working days. An outage beyond 2 working days raises GQD + IT/CSV escalations and
/// makes a BCP assessment reference mandatory before the event can be closed.
///
/// Deliberately absent: e-signature, qualified electronic signature provider integration, CAPA/Quality Event
/// records, BCP module, MOD-0023 workflow runtime, and any scheduler — escalation and overdue evaluation are
/// explicit calls. There is no DELETE verb: cancellation and closure are status changes.
///
/// Layer 1 RBAC REUSES the seeded controlled-documents view/create keys (no AuthService seed change); dedicated
/// <see cref="DowntimePermissions"/> keys should be seeded in a later hardening FU — approving an
/// outside-normal-environment issue in particular deserves its own key. TenantId is never read from the client.
/// </summary>
[ApiController]
[Route("api/v1/document-management/repository-downtime-events")]
[Authorize]
public sealed class DocumentManagementDowntimeController : CustomBaseController
{
    private readonly IMediator _mediator;
    private readonly ICorrelationContext _correlationContext;

    public DocumentManagementDowntimeController(IMediator mediator, ICorrelationContext correlationContext)
    {
        _mediator = mediator;
        _correlationContext = correlationContext;
    }

    // ── downtime events ───────────────────────────────────────────────────────

    [HttpGet]
    [HasPermission(DowntimePermissions.View)]
    public async Task<IActionResult> List(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetRepositoryDowntimeEventsQuery(CorrelationId), ct));

    [HttpGet("{id:guid}")]
    [HasPermission(DowntimePermissions.View)]
    public async Task<IActionResult> Detail(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetRepositoryDowntimeEventByIdQuery(id, CorrelationId), ct));

    [HttpGet("{id:guid}/escalations")]
    [HasPermission(DowntimePermissions.View)]
    public async Task<IActionResult> Escalations(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetDowntimeEscalationsQuery(id, CorrelationId), ct));

    /// <summary>Opens the downtime log. SOP §11.3 requires this BEFORE any outside-normal-environment issue.</summary>
    [HttpPost]
    [HasPermission(DowntimePermissions.Manage)]
    public async Task<IActionResult> Open([FromBody] OpenDowntimeEventApiRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new OpenRepositoryDowntimeEventCommand(
            new OpenDowntimeEventInput(request.DetectionEvidenceReference, request.DowntimeType,
                request.RepositoryAssessmentId, request.RepositoryName, request.StartedAt, request.DetectedByUserId,
                request.ImpactSummary), CorrelationId), ct));

    [HttpPost("{id:guid}/restore")]
    [HasPermission(DowntimePermissions.Manage)]
    public async Task<IActionResult> Restore(Guid id, [FromBody] MarkRepositoryRestoredApiRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new MarkRepositoryRestoredCommand(id,
            new MarkRepositoryRestoredInput(request.RestoreEvidenceReference, request.RestoredAt), CorrelationId), ct));

    /// <summary>Explicit evaluation of the 2-working-day threshold. Idempotent; there is no scheduler.</summary>
    [HttpPost("{id:guid}/evaluate-escalation")]
    [HasPermission(DowntimePermissions.Manage)]
    public async Task<IActionResult> EvaluateEscalation(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new EvaluateDowntimeEscalationCommand(id, CorrelationId), ct));

    [HttpPost("{id:guid}/close")]
    [HasPermission(DowntimePermissions.Manage)]
    public async Task<IActionResult> Close(Guid id, [FromBody] CloseDowntimeEventApiRequest? request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CloseRepositoryDowntimeEventCommand(id,
            new CloseDowntimeEventInput(request?.BcpAssessmentReference, request?.ClosureNote), CorrelationId), ct));

    // ── temporary controlled issues ───────────────────────────────────────────

    [HttpGet("{id:guid}/temporary-issues")]
    [HasPermission(DowntimePermissions.View)]
    public async Task<IActionResult> TemporaryIssues(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetTemporaryControlledIssuesQuery(id, CorrelationId), ct));

    [HttpPost("{id:guid}/temporary-issues")]
    [HasPermission(DowntimePermissions.TemporaryIssue)]
    public async Task<IActionResult> RequestTemporaryIssue(Guid id, [FromBody] RequestTemporaryIssueApiRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new RequestTemporaryControlledIssueCommand(id,
            new RequestTemporaryIssueInput(request.RegisterEntryId, request.ControlledDocumentId,
                request.ControlledDocumentVersionId, request.IssueReason, request.RecipientRole,
                request.RecipientDepartment, request.RecipientUserIds), CorrelationId), ct));

    /// <summary>Outside-normal-environment approval (SOP §11.3). Required before any copy is issued.</summary>
    [HttpPost("{id:guid}/temporary-issues/{issueId:guid}/approve")]
    [HasPermission(DowntimePermissions.TemporaryIssue)]
    public async Task<IActionResult> ApproveTemporaryIssue(
        Guid id, Guid issueId, [FromBody] ApproveTemporaryIssueApiRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new ApproveTemporaryControlledIssueCommand(id, issueId,
            new ApproveTemporaryIssueInput(request.ApprovedByRole, request.ApprovalMechanism,
                request.ApprovalEvidenceReference, request.ApprovedByUserId), CorrelationId), ct));

    /// <summary>Creates FU17 controlled copies of type TemporaryControlledIssue and starts the 3-working-day clock.</summary>
    [HttpPost("{id:guid}/temporary-issues/{issueId:guid}/issue-copy")]
    [HasPermission(DowntimePermissions.TemporaryIssue)]
    public async Task<IActionResult> IssueCopies(
        Guid id, Guid issueId, [FromBody] IssueTemporaryControlledCopyApiRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new IssueTemporaryControlledCopyCommand(id, issueId,
            new IssueTemporaryControlledCopyInput(request.IssuedCopyCount, request.TemporaryLocationDescription,
                request.LocationType), CorrelationId), ct));

    [HttpPost("{id:guid}/temporary-issues/{issueId:guid}/reconcile")]
    [HasPermission(DowntimePermissions.Reconcile)]
    public async Task<IActionResult> Reconcile(
        Guid id, Guid issueId, [FromBody] ReconcileTemporaryIssueApiRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new ReconcileTemporaryControlledIssueCommand(id, issueId,
            new ReconcileTemporaryIssueInput(request.ReconciliationEvidenceReference, request.DeviationReference,
                request.CorrectiveActionReference, request.MissingReconciliationReason,
                request.WithdrawCopiesInsteadOfReconcile), CorrelationId), ct));

    [HttpPost("{id:guid}/temporary-issues/{issueId:guid}/evaluate-overdue")]
    [HasPermission(DowntimePermissions.Reconcile)]
    public async Task<IActionResult> EvaluateOverdue(Guid id, Guid issueId, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new EvaluateTemporaryIssueOverdueCommand(id, issueId, CorrelationId), ct));

    [HttpPost("{id:guid}/temporary-issues/{issueId:guid}/cancel")]
    [HasPermission(DowntimePermissions.TemporaryIssue)]
    public async Task<IActionResult> Cancel(
        Guid id, Guid issueId, [FromBody] CancelTemporaryIssueApiRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CancelTemporaryControlledIssueCommand(id, issueId,
            new CancelTemporaryIssueInput(request.Reason), CorrelationId), ct));

    private string CorrelationId =>
        string.IsNullOrWhiteSpace(_correlationContext.CorrelationId) ? HttpContext.TraceIdentifier : _correlationContext.CorrelationId!;
}
