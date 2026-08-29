using Diten.Platform.API.Controllers.Common;
using Diten.Platform.API.Models.DocumentManagement;
using Diten.Platform.API.Observability;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Features.DocumentManagementControlledDocuments;
using Diten.Platform.Application.Features.DocumentManagementSuspension;
using Diten.Platform.Application.Features.DocumentManagementSuspension.Commands;
using Diten.Platform.Application.Features.DocumentManagementSuspension.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers;

/// <summary>
/// MOD-0029-FU13 — TenantShell suspension / urgent withdrawal / retirement / temporary-instruction API (GMG-QMS-SOP-0001
/// §12.1, §9.16, §6.1 class 7). Thin controller; dispatches via MediatR. Layer 1 RBAC REUSES the seeded
/// controlled-documents create/view keys (no AuthService seed change); dedicated
/// <see cref="DocumentSuspensionPermissions"/> keys should be seeded in FU06A hardening. TenantId is never read from
/// the client — it is resolved server-side.
/// </summary>
[ApiController]
[Route("api/v1/document-management")]
[Authorize]
public sealed class DocumentManagementSuspensionController : CustomBaseController
{
    private readonly IMediator _mediator;
    private readonly ICorrelationContext _correlationContext;

    public DocumentManagementSuspensionController(IMediator mediator, ICorrelationContext correlationContext)
    {
        _mediator = mediator;
        _correlationContext = correlationContext;
    }

    // ── suspension ───────────────────────────────────────────────────────────

    [HttpGet("document-master-register/{id:guid}/suspension-cases")]
    [HasPermission(DocumentSuspensionPermissions.View)]
    public async Task<IActionResult> SuspensionCases(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetSuspensionCasesQuery(id, CorrelationId), ct));

    [HttpPost("document-master-register/{id:guid}/suspension-cases")]
    [HasPermission(DocumentSuspensionPermissions.Manage)]
    public async Task<IActionResult> OpenSuspension(Guid id, [FromBody] OpenSuspensionCaseApiRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new OpenSuspensionCaseCommand(id,
            new OpenSuspensionCaseInput(request.TriggerType, request.TriggerDescription, request.SourcePeriodicReviewEscalationId), CorrelationId), ct));

    [HttpPost("document-master-register/{id:guid}/suspension-cases/{caseId:guid}/escalate")]
    [HasPermission(DocumentSuspensionPermissions.Manage)]
    public async Task<IActionResult> EscalateSuspension(Guid id, Guid caseId, [FromBody] EscalateSuspensionCaseApiRequest? request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new EscalateSuspensionCaseCommand(id, caseId, new EscalateSuspensionCaseInput(request?.Comment), CorrelationId), ct));

    [HttpPost("document-master-register/{id:guid}/suspension-cases/{caseId:guid}/approve")]
    [HasPermission(DocumentSuspensionPermissions.Approve)]
    public async Task<IActionResult> ApproveSuspension(Guid id, Guid caseId, [FromBody] ApproveSuspensionApiRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new ApproveSuspensionCommand(id, caseId,
            new ApproveSuspensionInput(request.Decision, request.DecisionReason, request.ApprovedByRole, request.CommunicationPlanReference), CorrelationId), ct));

    [HttpPost("document-master-register/{id:guid}/suspension-cases/{caseId:guid}/reject")]
    [HasPermission(DocumentSuspensionPermissions.Approve)]
    public async Task<IActionResult> RejectSuspension(Guid id, Guid caseId, [FromBody] RejectSuspensionApiRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new RejectSuspensionCommand(id, caseId, new RejectSuspensionInput(request.Reason), CorrelationId), ct));

    [HttpPost("document-master-register/{id:guid}/suspension-cases/{caseId:guid}/execute")]
    [HasPermission(DocumentSuspensionPermissions.Manage)]
    public async Task<IActionResult> ExecuteSuspension(Guid id, Guid caseId, [FromBody] ExecuteSuspensionApiRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new ExecuteSuspensionCommand(id, caseId,
            new ExecuteSuspensionInput(request.SuspensionNoticeReference, request.AccessRemovalEvidenceReference, request.AffectedRecordsBatchesActivitiesReference), CorrelationId), ct));

    [HttpPost("document-master-register/{id:guid}/suspension-cases/{caseId:guid}/close")]
    [HasPermission(DocumentSuspensionPermissions.Manage)]
    public async Task<IActionResult> CloseSuspension(Guid id, Guid caseId, [FromBody] CloseSuspensionCaseApiRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CloseSuspensionCaseCommand(id, caseId,
            new CloseSuspensionCaseInput(request.DeviationReference, request.CorrectiveActionReference, request.ReplacementPlanReference), CorrelationId), ct));

    // ── retirement ───────────────────────────────────────────────────────────

    [HttpGet("document-master-register/{id:guid}/retirement-cases")]
    [HasPermission(DocumentSuspensionPermissions.View)]
    public async Task<IActionResult> RetirementCases(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetRetirementCasesQuery(id, CorrelationId), ct));

    [HttpPost("document-master-register/{id:guid}/retirement-cases")]
    [HasPermission(DocumentSuspensionPermissions.Manage)]
    public async Task<IActionResult> RequestRetirement(Guid id, [FromBody] RequestRetirementApiRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new RequestRetirementCommand(id,
            new RequestRetirementInput(request.RetirementReason, request.JustificationReference, request.TransitionAssessmentReference,
                request.ReplacementDocumentUid, request.ReplacementDocumentCode), CorrelationId), ct));

    [HttpPost("document-master-register/{id:guid}/retirement-cases/{caseId:guid}/approve")]
    [HasPermission(DocumentSuspensionPermissions.RetirementApprove)]
    public async Task<IActionResult> ApproveRetirement(Guid id, Guid caseId, [FromBody] ApproveRetirementApiRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new ApproveRetirementCommand(id, caseId, new ApproveRetirementInput(request.ApprovedByRole), CorrelationId), ct));

    [HttpPost("document-master-register/{id:guid}/retirement-cases/{caseId:guid}/reject")]
    [HasPermission(DocumentSuspensionPermissions.RetirementApprove)]
    public async Task<IActionResult> RejectRetirement(Guid id, Guid caseId, [FromBody] RejectRetirementApiRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new RejectRetirementCommand(id, caseId, new RejectRetirementInput(request.Reason), CorrelationId), ct));

    [HttpPost("document-master-register/{id:guid}/retirement-cases/{caseId:guid}/execute")]
    [HasPermission(DocumentSuspensionPermissions.Manage)]
    public async Task<IActionResult> ExecuteRetirement(Guid id, Guid caseId, [FromBody] ExecuteRetirementApiRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new ExecuteRetirementCommand(id, caseId,
            new ExecuteRetirementInput(request.CommunicationEvidenceReference, request.ArchivalEvidenceReference), CorrelationId), ct));

    // ── temporary instruction ────────────────────────────────────────────────

    [HttpGet("document-master-register/{id:guid}/temporary-instruction")]
    [HasPermission(DocumentSuspensionPermissions.View)]
    public async Task<IActionResult> TemporaryInstruction(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetTemporaryInstructionQuery(id, CorrelationId), ct));

    [HttpPost("document-master-register/{id:guid}/temporary-instruction/start")]
    [HasPermission(DocumentSuspensionPermissions.Manage)]
    public async Task<IActionResult> StartTemporaryInstruction(Guid id, [FromBody] StartTemporaryInstructionApiRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new StartTemporaryInstructionControlCommand(id,
            new StartTemporaryInstructionInput(request.ValidFrom, request.ValidUntil), CorrelationId), ct));

    [HttpPost("document-master-register/{id:guid}/temporary-instruction/evaluate-expiry")]
    [HasPermission(DocumentSuspensionPermissions.Manage)]
    public async Task<IActionResult> EvaluateTemporaryExpiry(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new EvaluateTemporaryInstructionExpiryCommand(id, CorrelationId), ct));

    [HttpPost("document-master-register/{id:guid}/temporary-instruction/close")]
    [HasPermission(DocumentSuspensionPermissions.Manage)]
    public async Task<IActionResult> CloseTemporaryInstruction(Guid id, [FromBody] CloseTemporaryInstructionApiRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CloseTemporaryInstructionCommand(id,
            new CloseTemporaryInstructionInput(request.ExpiryAction, request.ExpiryActionEvidenceReference, request.ReplacementRegisterEntryId), CorrelationId), ct));

    private string CorrelationId =>
        string.IsNullOrWhiteSpace(_correlationContext.CorrelationId) ? HttpContext.TraceIdentifier : _correlationContext.CorrelationId!;
}
