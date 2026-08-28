using Diten.Platform.API.Controllers.Common;
using Diten.Platform.API.Models.DocumentManagement;
using Diten.Platform.API.Observability;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Features.DocumentManagementControlledDocuments;
using Diten.Platform.Application.Features.DocumentManagementGDocPCorrection;
using Diten.Platform.Application.Features.DocumentManagementGDocPCorrection.Commands;
using Diten.Platform.Application.Features.DocumentManagementGDocPCorrection.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers;

/// <summary>
/// MOD-0029-FU21 — TenantShell GDocP / ALCOA+ correction trail API (GMG-QMS-SOP-0001 §21). Thin controller;
/// dispatches via MediatR.
///
/// THIS IS NOT THE AUDIT LOG. The platform audit event store records which command an actor ran. This records
/// which regulated FIELD changed, from what value to what value, why, on what evidence, and on whose authority —
/// with a server-stamped time. Both trails run side by side; FU21 replaces neither.
///
/// Deliberately absent: e-signature, CAPA/Quality Event records, data-integrity investigation module, MOD-0023
/// workflow runtime. Deviation references point at records held elsewhere. There is no DELETE verb and no update
/// verb on a correction record — the trail is append-only, and only a review verdict may be applied to it.
///
/// Layer 1 RBAC REUSES the seeded controlled-documents view/create keys (no AuthService seed change); dedicated
/// <see cref="GDocPCorrectionPermissions"/> keys should be seeded in a later hardening FU. Crucially, the review
/// key must not be grantable by the same permission that records a correction, or the second-person review stops
/// being a second person. TenantId is never read from the client.
/// </summary>
[ApiController]
[Route("api/v1/document-management")]
[Authorize]
public sealed class DocumentManagementGDocPCorrectionController : CustomBaseController
{
    private readonly IMediator _mediator;
    private readonly ICorrelationContext _correlationContext;

    public DocumentManagementGDocPCorrectionController(IMediator mediator, ICorrelationContext correlationContext)
    {
        _mediator = mediator;
        _correlationContext = correlationContext;
    }

    // ── correction policies ───────────────────────────────────────────────────

    [HttpGet("gdocp-correction-policies")]
    [HasPermission(GDocPCorrectionPermissions.View)]
    public async Task<IActionResult> ListPolicies(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetGDocPCorrectionPoliciesQuery(CorrelationId), ct));

    [HttpGet("gdocp-correction-policies/{id:guid}")]
    [HasPermission(GDocPCorrectionPermissions.View)]
    public async Task<IActionResult> PolicyDetail(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetGDocPCorrectionPolicyByIdQuery(id, CorrelationId), ct));

    [HttpPost("gdocp-correction-policies")]
    [HasPermission(GDocPCorrectionPermissions.PolicyManage)]
    public async Task<IActionResult> CreatePolicy([FromBody] GDocPCorrectionPolicyApiRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CreateGDocPCorrectionPolicyCommand(ToFields(request), CorrelationId), ct));

    [HttpPost("gdocp-correction-policies/{id:guid}/activate")]
    [HasPermission(GDocPCorrectionPermissions.PolicyManage)]
    public async Task<IActionResult> ActivatePolicy(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new ActivateGDocPCorrectionPolicyCommand(id, CorrelationId), ct));

    /// <summary>Status change only — the policy row survives so past classifications stay explainable.</summary>
    [HttpPost("gdocp-correction-policies/{id:guid}/retire")]
    [HasPermission(GDocPCorrectionPermissions.PolicyManage)]
    public async Task<IActionResult> RetirePolicy(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new RetireGDocPCorrectionPolicyCommand(id, CorrelationId), ct));

    // ── correction records ────────────────────────────────────────────────────

    [HttpGet("gdocp-corrections")]
    [HasPermission(GDocPCorrectionPermissions.View)]
    public async Task<IActionResult> ListCorrections(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetGDocPCorrectionsQuery(CorrelationId), ct));

    [HttpGet("gdocp-corrections/pending-review")]
    [HasPermission(GDocPCorrectionPermissions.View)]
    public async Task<IActionResult> PendingReview(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetPendingGDocPCorrectionReviewsQuery(CorrelationId), ct));

    /// <summary>The correction history of one regulated record — the question an auditor actually asks.</summary>
    [HttpGet("gdocp-corrections/by-subject/{subjectType}/{subjectId:guid}")]
    [HasPermission(GDocPCorrectionPermissions.View)]
    public async Task<IActionResult> BySubject(string subjectType, Guid subjectId, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetGDocPCorrectionsBySubjectQuery(subjectType, subjectId, CorrelationId), ct));

    [HttpGet("gdocp-corrections/{id:guid}")]
    [HasPermission(GDocPCorrectionPermissions.View)]
    public async Task<IActionResult> CorrectionDetail(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetGDocPCorrectionByIdQuery(id, CorrelationId), ct));

    [HttpGet("gdocp-corrections/{id:guid}/reviews")]
    [HasPermission(GDocPCorrectionPermissions.View)]
    public async Task<IActionResult> CorrectionReviews(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetGDocPCorrectionReviewsQuery(id, CorrelationId), ct));

    /// <summary>Records a field correction. The corrector and correction time are stamped server-side.</summary>
    [HttpPost("gdocp-corrections")]
    [HasPermission(GDocPCorrectionPermissions.Record)]
    public async Task<IActionResult> RecordCorrection([FromBody] RecordGDocPCorrectionApiRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new RecordGDocPCorrectionCommand(new RecordGDocPCorrectionInput(
            request.SubjectType, request.SubjectId, request.FieldPath, request.FieldDisplayName,
            request.PreviousValueSnapshot, request.NewValueSnapshot, request.ValueFormat, request.CorrectionType,
            request.CorrectionReason, request.CorrectionEvidenceReference, request.DeviationReference,
            request.RegisterEntryId, request.ControlledDocumentId, request.CorrectedByUserId,
            request.CorrectedByRole, request.RequestedBy, request.SubjectIsApproved, request.SubjectIsEffective),
            CorrelationId), ct));

    [HttpPost("gdocp-corrections/{id:guid}/review")]
    [HasPermission(GDocPCorrectionPermissions.Review)]
    public async Task<IActionResult> Review(Guid id, [FromBody] ReviewGDocPCorrectionApiRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new ReviewGDocPCorrectionCommand(id,
            new ReviewGDocPCorrectionInput(request.ReviewerUserId, request.ReviewerRole,
                request.ReviewEvidenceReference, request.ReviewComment), CorrelationId), ct));

    [HttpPost("gdocp-corrections/{id:guid}/reject")]
    [HasPermission(GDocPCorrectionPermissions.Review)]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectGDocPCorrectionApiRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new RejectGDocPCorrectionCommand(id,
            new RejectGDocPCorrectionInput(request.ReviewerUserId, request.ReviewerRole, request.Reason), CorrelationId), ct));

    private static GDocPCorrectionPolicyInput ToFields(GDocPCorrectionPolicyApiRequest r) => new(
        r.PolicyKey, r.PolicyName, r.SubjectType, r.FieldPathPattern, r.RequiresCorrectionReason,
        r.RequiresEvidenceReference, r.RequiresReview, r.RequiresDeviationReferenceForHighRisk,
        r.AllowCorrectionAfterApproval, r.AllowCorrectionAfterEffective, r.IsBackdatingSensitive,
        r.IsStatusSensitive, r.IsEvidenceSensitive, r.Notes);

    private string CorrelationId =>
        string.IsNullOrWhiteSpace(_correlationContext.CorrelationId) ? HttpContext.TraceIdentifier : _correlationContext.CorrelationId!;
}
