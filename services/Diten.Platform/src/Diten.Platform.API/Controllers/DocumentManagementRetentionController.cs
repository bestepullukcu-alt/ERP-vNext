using Diten.Platform.API.Controllers.Common;
using Diten.Platform.API.Models.DocumentManagement;
using Diten.Platform.API.Observability;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Features.DocumentManagementControlledDocuments;
using Diten.Platform.Application.Features.DocumentManagementRetention;
using Diten.Platform.Application.Features.DocumentManagementRetention.Commands;
using Diten.Platform.Application.Features.DocumentManagementRetention.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers;

/// <summary>
/// MOD-0029-FU15 — TenantShell Retention Schedule &amp; Litigation Hold API (GMG-QMS-SOP-0001 §22). Thin
/// controller; dispatches via MediatR.
///
/// CRITICAL BOUNDARY: there is NO DELETE VERB anywhere in this controller and no endpoint that destroys data.
/// "execute-marker" writes a governance evidence marker and leaves the subject record fully intact. Retiring a
/// policy and releasing a hold are status changes. Actual purge is deliberately out of scope for FU15.
///
/// Also deliberately absent: automated purge jobs, schedulers, e-signature, CAPA records and MOD-0023 workflow
/// runtime integration. Evaluation is strictly opt-in — nothing is evaluated in the background.
///
/// Layer 1 RBAC REUSES the seeded controlled-documents view/create keys (no AuthService seed change); dedicated
/// <see cref="DocumentRetentionPermissions"/> keys — especially a separate legal-hold release key — should be
/// seeded in a later hardening FU. TenantId is never read from the client; it is resolved server-side.
/// </summary>
[ApiController]
[Route("api/v1/document-management")]
[Authorize]
public sealed class DocumentManagementRetentionController : CustomBaseController
{
    private readonly IMediator _mediator;
    private readonly ICorrelationContext _correlationContext;

    public DocumentManagementRetentionController(IMediator mediator, ICorrelationContext correlationContext)
    {
        _mediator = mediator;
        _correlationContext = correlationContext;
    }

    // ── retention policies ────────────────────────────────────────────────────

    [HttpGet("retention-policies")]
    [HasPermission(DocumentRetentionPermissions.RetentionView)]
    public async Task<IActionResult> ListPolicies(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetRetentionPoliciesQuery(CorrelationId), ct));

    [HttpGet("retention-policies/{id:guid}")]
    [HasPermission(DocumentRetentionPermissions.RetentionView)]
    public async Task<IActionResult> PolicyDetail(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetRetentionPolicyByIdQuery(id, CorrelationId), ct));

    [HttpPost("retention-policies")]
    [HasPermission(DocumentRetentionPermissions.RetentionManage)]
    public async Task<IActionResult> CreatePolicy([FromBody] RetentionPolicyApiRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CreateRetentionPolicyCommand(ToFields(request), CorrelationId), ct));

    [HttpPut("retention-policies/{id:guid}")]
    [HasPermission(DocumentRetentionPermissions.RetentionManage)]
    public async Task<IActionResult> UpdatePolicy(Guid id, [FromBody] RetentionPolicyApiRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new UpdateRetentionPolicyCommand(id, ToFields(request), CorrelationId), ct));

    [HttpPost("retention-policies/{id:guid}/activate")]
    [HasPermission(DocumentRetentionPermissions.RetentionManage)]
    public async Task<IActionResult> ActivatePolicy(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new ActivateRetentionPolicyCommand(id, CorrelationId), ct));

    /// <summary>Status change only — the policy row is retained so historic verdicts stay explainable.</summary>
    [HttpPost("retention-policies/{id:guid}/retire")]
    [HasPermission(DocumentRetentionPermissions.RetentionManage)]
    public async Task<IActionResult> RetirePolicy(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new RetireRetentionPolicyCommand(id, CorrelationId), ct));

    // ── retention evaluation (opt-in only) ────────────────────────────────────

    [HttpPost("retention/evaluate")]
    [HasPermission(DocumentRetentionPermissions.RetentionManage)]
    public async Task<IActionResult> Evaluate([FromBody] EvaluateRetentionApiRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new EvaluateRetentionSubjectCommand(
            new EvaluateRetentionInput(request.SubjectType, request.SubjectId, request.RegisterEntryId,
                request.ControlledDocumentId, request.TriggerDate, request.RetentionClass), CorrelationId), ct));

    [HttpGet("retention/eligible")]
    [HasPermission(DocumentRetentionPermissions.RetentionView)]
    public async Task<IActionResult> Eligible(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetEligibleRetentionSubjectsQuery(CorrelationId), ct));

    [HttpGet("retention/subjects/{subjectType}/{subjectId:guid}")]
    [HasPermission(DocumentRetentionPermissions.RetentionView)]
    public async Task<IActionResult> Subject(string subjectType, Guid subjectId, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetRetentionSubjectQuery(subjectType, subjectId, CorrelationId), ct));

    // ── legal holds ───────────────────────────────────────────────────────────

    [HttpGet("legal-holds")]
    [HasPermission(DocumentRetentionPermissions.LegalHoldView)]
    public async Task<IActionResult> ListHolds(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetLegalHoldsQuery(CorrelationId), ct));

    [HttpGet("legal-holds/{id:guid}")]
    [HasPermission(DocumentRetentionPermissions.LegalHoldView)]
    public async Task<IActionResult> HoldDetail(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetLegalHoldByIdQuery(id, CorrelationId), ct));

    [HttpGet("legal-holds/{id:guid}/subjects")]
    [HasPermission(DocumentRetentionPermissions.LegalHoldView)]
    public async Task<IActionResult> HoldSubjects(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetLegalHoldSubjectsQuery(id, CorrelationId), ct));

    [HttpPost("legal-holds")]
    [HasPermission(DocumentRetentionPermissions.LegalHoldManage)]
    public async Task<IActionResult> CreateHold([FromBody] LegalHoldApiRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CreateLegalHoldCommand(new LegalHoldFieldsInput(
            request.HoldTitle, request.HoldKey, request.HoldReason, request.ScopeType, request.RegisterEntryIds,
            request.ControlledDocumentIds, request.SubjectTypes, request.ExternalDocumentIds, request.ScopeDescription,
            request.IssuedByLegalUserId, request.IssuedByLegalRole, request.EffectiveFrom, request.EffectiveUntil),
            CorrelationId), ct));

    /// <summary>Requires Legal approval evidence (SOP §22).</summary>
    [HttpPost("legal-holds/{id:guid}/activate")]
    [HasPermission(DocumentRetentionPermissions.LegalHoldManage)]
    public async Task<IActionResult> ActivateHold(Guid id, [FromBody] ActivateLegalHoldApiRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new ActivateLegalHoldCommand(id, new ActivateLegalHoldInput(
            request.LegalApprovalEvidenceReference, request.GqdConcurrenceUserId, request.GqdConcurrenceEvidenceReference),
            CorrelationId), ct));

    /// <summary>Requires BOTH Legal written release approval AND GQD concurrence (SOP §22).</summary>
    [HttpPost("legal-holds/{id:guid}/release")]
    [HasPermission(DocumentRetentionPermissions.LegalHoldRelease)]
    public async Task<IActionResult> ReleaseHold(Guid id, [FromBody] ReleaseLegalHoldApiRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new ReleaseLegalHoldCommand(id, new ReleaseLegalHoldInput(
            request.ReleaseLegalApprovalReference, request.ReleaseGqdConcurrenceReference), CorrelationId), ct));

    [HttpPost("legal-holds/{id:guid}/subjects")]
    [HasPermission(DocumentRetentionPermissions.LegalHoldManage)]
    public async Task<IActionResult> AddHoldSubject(Guid id, [FromBody] AddLegalHoldSubjectApiRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new AddLegalHoldSubjectCommand(
            id, request.SubjectType, request.SubjectId, request.RegisterEntryId, CorrelationId), ct));

    // ── disposition requests ──────────────────────────────────────────────────

    [HttpGet("disposition-requests")]
    [HasPermission(DocumentRetentionPermissions.RetentionView)]
    public async Task<IActionResult> ListDispositions(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetDispositionRequestsQuery(CorrelationId), ct));

    [HttpGet("disposition-requests/{id:guid}")]
    [HasPermission(DocumentRetentionPermissions.RetentionView)]
    public async Task<IActionResult> DispositionDetail(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetDispositionRequestByIdQuery(id, CorrelationId), ct));

    [HttpPost("disposition-requests")]
    [HasPermission(DocumentRetentionPermissions.DispositionManage)]
    public async Task<IActionResult> CreateDisposition([FromBody] DispositionRequestApiRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CreateDispositionRequestCommand(
            new CreateDispositionRequestInput(request.SubjectType, request.SubjectId, request.RegisterEntryId, request.Comment),
            CorrelationId), ct));

    [HttpPost("disposition-requests/{id:guid}/submit")]
    [HasPermission(DocumentRetentionPermissions.DispositionManage)]
    public async Task<IActionResult> SubmitDisposition(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new SubmitDispositionRequestCommand(id, CorrelationId), ct));

    [HttpPost("disposition-requests/{id:guid}/approve")]
    [HasPermission(DocumentRetentionPermissions.DispositionApprove)]
    public async Task<IActionResult> ApproveDisposition(Guid id, [FromBody] ApproveDispositionApiRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new ApproveDispositionRequestCommand(id,
            new ApproveDispositionInput(request.ApprovalEvidenceReference, request.ApprovedByUserId), CorrelationId), ct));

    [HttpPost("disposition-requests/{id:guid}/reject")]
    [HasPermission(DocumentRetentionPermissions.DispositionApprove)]
    public async Task<IActionResult> RejectDisposition(Guid id, [FromBody] RejectDispositionApiRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new RejectDispositionRequestCommand(id,
            new RejectDispositionInput(request.Reason), CorrelationId), ct));

    /// <summary>
    /// Writes the disposition evidence marker. THIS DOES NOT DELETE THE SUBJECT — the record remains intact and
    /// retrievable; only the governance decision is recorded.
    /// </summary>
    [HttpPost("disposition-requests/{id:guid}/execute-marker")]
    [HasPermission(DocumentRetentionPermissions.DispositionManage)]
    public async Task<IActionResult> ExecuteDispositionMarker(Guid id, [FromBody] ExecuteDispositionMarkerApiRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new ExecuteDispositionMarkerCommand(id,
            new ExecuteDispositionMarkerInput(request.ExecutionEvidenceReference), CorrelationId), ct));

    private static RetentionPolicyFieldsInput ToFields(RetentionPolicyApiRequest r) => new(
        r.PolicyKey, r.PolicyName, r.SubjectType, r.RetentionClass, r.MinimumRetentionYears, r.RetentionTrigger,
        r.RetainWhileEffective, r.RetainAfterRetirementYears, r.RetainAfterSupersessionYears, r.IsPermanentRetention,
        r.RegulatoryBasis, r.Jurisdiction, r.IsLongestApplicableCandidate);

    private string CorrelationId =>
        string.IsNullOrWhiteSpace(_correlationContext.CorrelationId) ? HttpContext.TraceIdentifier : _correlationContext.CorrelationId!;
}
