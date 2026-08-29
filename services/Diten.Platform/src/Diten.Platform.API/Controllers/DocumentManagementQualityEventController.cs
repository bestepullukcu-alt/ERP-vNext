using Diten.Platform.API.Controllers.Common;
using Diten.Platform.API.Models.DocumentManagement;
using Diten.Platform.API.Observability;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Features.DocumentManagementControlledDocuments;
using Diten.Platform.Application.Features.DocumentManagementQualityEvent;
using Diten.Platform.Application.Features.DocumentManagementQualityEvent.Commands;
using Diten.Platform.Application.Features.DocumentManagementQualityEvent.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers;

/// <summary>
/// MOD-0029-FU22 — TenantShell document-control quality event / deviation / CAPA API (GMG-QMS-SOP-0001). Thin
/// controller; dispatches via MediatR.
///
/// WHAT THIS BRIDGES: FU13/FU14/FU17/FU20/FU21 already carry QualityEventReference, DeviationReference and
/// CorrectiveActionReference as free-text strings pointing at records held elsewhere. This gives those references
/// a real, queryable home inside document control — without removing the string fields, which still work.
///
/// ⚠️ The deviation here is the GxP quality deviation, NOT MOD-0028-FU09's collection-tree read-back deviation
/// exposed elsewhere in this service. The two are unrelated.
///
/// Deliberately absent: a QMS module, a CAPA workflow engine, an investigation module, a root-cause methodology
/// engine, an effectiveness scheduler, e-signature, MOD-0023 workflow runtime, and any external QMS API call.
/// There is no DELETE verb — cancellation and closure are status changes.
///
/// Layer 1 RBAC REUSES the seeded controlled-documents view/create keys (no AuthService seed change); dedicated
/// <see cref="QualityEventPermissions"/> keys should be seeded in a later hardening FU — closing a critical
/// deviation is a materially different authority from raising an event. TenantId is never read from the client.
/// </summary>
[ApiController]
[Route("api/v1/document-management")]
[Authorize]
public sealed class DocumentManagementQualityEventController : CustomBaseController
{
    private readonly IMediator _mediator;
    private readonly ICorrelationContext _correlationContext;

    public DocumentManagementQualityEventController(IMediator mediator, ICorrelationContext correlationContext)
    {
        _mediator = mediator;
        _correlationContext = correlationContext;
    }

    // ── quality events ────────────────────────────────────────────────────────

    [HttpGet("quality-events")]
    [HasPermission(QualityEventPermissions.QualityEventsView)]
    public async Task<IActionResult> ListEvents(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetDocumentQualityEventsQuery(CorrelationId), ct));

    [HttpGet("quality-events/{id:guid}")]
    [HasPermission(QualityEventPermissions.QualityEventsView)]
    public async Task<IActionResult> EventDetail(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetDocumentQualityEventByIdQuery(id, CorrelationId), ct));

    [HttpGet("quality-events/{id:guid}/source-links")]
    [HasPermission(QualityEventPermissions.QualityEventsView)]
    public async Task<IActionResult> SourceLinks(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetQualityEventSourceLinksQuery(id, CorrelationId), ct));

    [HttpPost("quality-events")]
    [HasPermission(QualityEventPermissions.QualityEventsManage)]
    public async Task<IActionResult> CreateEvent([FromBody] CreateQualityEventApiRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CreateDocumentQualityEventCommand(ToFields(request), CorrelationId), ct));

    [HttpPost("quality-events/{id:guid}/open")]
    [HasPermission(QualityEventPermissions.QualityEventsManage)]
    public async Task<IActionResult> OpenEvent(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new OpenDocumentQualityEventCommand(id, CorrelationId), ct));

    [HttpPost("quality-events/{id:guid}/close")]
    [HasPermission(QualityEventPermissions.QualityEventsManage)]
    public async Task<IActionResult> CloseEvent(Guid id, [FromBody] CloseQualityEventApiRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CloseDocumentQualityEventCommand(id,
            new CloseQualityEventInput(request.ClosureEvidenceReference, request.ClosureSummary), CorrelationId), ct));

    [HttpPost("quality-events/{id:guid}/cancel")]
    [HasPermission(QualityEventPermissions.QualityEventsManage)]
    public async Task<IActionResult> CancelEvent(Guid id, [FromBody] CancelQualityRecordApiRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CancelDocumentQualityEventCommand(id,
            new CancelQualityEventInput(request.Reason), CorrelationId), ct));

    [HttpPost("quality-events/{id:guid}/source-links")]
    [HasPermission(QualityEventPermissions.QualityEventsManage)]
    public async Task<IActionResult> LinkSource(Guid id, [FromBody] LinkQualityEventSourceApiRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new LinkQualityEventSourceCommand(id,
            new LinkQualityEventSourceInput(request.SourceType, request.SourceId, request.EventType,
                request.RegisterEntryId, request.SourceReferenceSnapshot, request.Notes), CorrelationId), ct));

    // ── deviations ────────────────────────────────────────────────────────────

    [HttpGet("deviations")]
    [HasPermission(QualityEventPermissions.DeviationsView)]
    public async Task<IActionResult> ListDeviations(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetDocumentDeviationsQuery(CorrelationId), ct));

    [HttpGet("deviations/{id:guid}")]
    [HasPermission(QualityEventPermissions.DeviationsView)]
    public async Task<IActionResult> DeviationDetail(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetDocumentDeviationByIdQuery(id, CorrelationId), ct));

    [HttpPost("deviations")]
    [HasPermission(QualityEventPermissions.DeviationsManage)]
    public async Task<IActionResult> CreateDeviation([FromBody] CreateDeviationApiRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CreateDocumentDeviationCommand(new CreateDeviationInput(
            request.QualityEventId, request.DeviationTitle, request.DeviationDescription, request.DeviationCategory,
            request.DeviationSeverity, request.OccurredAt, request.ReportedBy, request.RequiresCAPA), CorrelationId), ct));

    [HttpPost("deviations/{id:guid}/open")]
    [HasPermission(QualityEventPermissions.DeviationsManage)]
    public async Task<IActionResult> OpenDeviation(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new OpenDocumentDeviationCommand(id, CorrelationId), ct));

    [HttpPost("deviations/{id:guid}/investigation")]
    [HasPermission(QualityEventPermissions.DeviationsManage)]
    public async Task<IActionResult> RecordInvestigation(Guid id, [FromBody] RecordDeviationInvestigationApiRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new RecordDeviationInvestigationCommand(id,
            new RecordDeviationInvestigationInput(request.RootCauseSummary, request.RootCauseCategory,
                request.ImpactAssessmentSummary, request.PatientProductRegulatoryImpact,
                request.InvestigationEvidenceReference), CorrelationId), ct));

    [HttpPost("deviations/{id:guid}/require-capa")]
    [HasPermission(QualityEventPermissions.DeviationsManage)]
    public async Task<IActionResult> RequireCapa(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new RequireCAPAForDeviationCommand(id, CorrelationId), ct));

    [HttpPost("deviations/{id:guid}/close")]
    [HasPermission(QualityEventPermissions.DeviationsManage)]
    public async Task<IActionResult> CloseDeviation(Guid id, [FromBody] CloseDeviationApiRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CloseDocumentDeviationCommand(id,
            new CloseDeviationInput(request.ClosureEvidenceReference, request.ClosureExceptionJustification), CorrelationId), ct));

    [HttpPost("deviations/{id:guid}/cancel")]
    [HasPermission(QualityEventPermissions.DeviationsManage)]
    public async Task<IActionResult> CancelDeviation(Guid id, [FromBody] CancelQualityRecordApiRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CancelDocumentDeviationCommand(id,
            new CancelDeviationInput(request.Reason), CorrelationId), ct));

    // ── CAPA actions ──────────────────────────────────────────────────────────

    [HttpGet("capa-actions")]
    [HasPermission(QualityEventPermissions.CapaView)]
    public async Task<IActionResult> ListCapa(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetDocumentCAPAActionsQuery(CorrelationId), ct));

    [HttpGet("capa-actions/{id:guid}")]
    [HasPermission(QualityEventPermissions.CapaView)]
    public async Task<IActionResult> CapaDetail(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetDocumentCAPAActionByIdQuery(id, CorrelationId), ct));

    [HttpPost("capa-actions")]
    [HasPermission(QualityEventPermissions.CapaManage)]
    public async Task<IActionResult> CreateCapa([FromBody] CreateCapaActionApiRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CreateDocumentCAPAActionCommand(new CreateCapaActionInput(
            request.QualityEventId, request.DeviationId, request.ActionType, request.ActionTitle,
            request.ActionDescription, request.ActionOwnerUserId, request.ActionOwnerRole, request.DueDate,
            request.EffectivenessCheckRequired, request.EffectivenessDueDate, request.RelatedRegisterEntryIds,
            request.RelatedControlledDocumentIds, request.RelatedExternalDocumentIds), CorrelationId), ct));

    [HttpPost("capa-actions/{id:guid}/start")]
    [HasPermission(QualityEventPermissions.CapaManage)]
    public async Task<IActionResult> StartCapa(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new StartCAPAActionCommand(id, CorrelationId), ct));

    [HttpPost("capa-actions/{id:guid}/complete")]
    [HasPermission(QualityEventPermissions.CapaManage)]
    public async Task<IActionResult> CompleteCapa(Guid id, [FromBody] CompleteCapaActionApiRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CompleteCAPAActionCommand(id,
            new CompleteCapaActionInput(request.CompletionEvidenceReference, request.Comment), CorrelationId), ct));

    [HttpPost("capa-actions/{id:guid}/effectiveness")]
    [HasPermission(QualityEventPermissions.CapaManage)]
    public async Task<IActionResult> RecordEffectiveness(Guid id, [FromBody] RecordCapaEffectivenessApiRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new RecordCAPAEffectivenessCommand(id,
            new RecordCapaEffectivenessInput(request.EffectivenessResult, request.EffectivenessEvidenceReference,
                request.EffectivenessSummary), CorrelationId), ct));

    [HttpPost("capa-actions/{id:guid}/close")]
    [HasPermission(QualityEventPermissions.CapaManage)]
    public async Task<IActionResult> CloseCapa(Guid id, [FromBody] CloseCapaActionApiRequest? request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CloseCAPAActionCommand(id,
            new CloseCapaActionInput(request?.ClosureExceptionJustification), CorrelationId), ct));

    [HttpPost("capa-actions/{id:guid}/cancel")]
    [HasPermission(QualityEventPermissions.CapaManage)]
    public async Task<IActionResult> CancelCapa(Guid id, [FromBody] CancelQualityRecordApiRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CancelCAPAActionCommand(id,
            new CancelCapaActionInput(request.Reason), CorrelationId), ct));

    // ── bridge (idempotent per source + event type) ───────────────────────────

    [HttpPost("quality-events/from-source")]
    [HasPermission(QualityEventPermissions.BridgeManage)]
    public async Task<IActionResult> BridgeFromSource([FromBody] BridgeFromSourceApiRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new BridgeQualityEventFromSourceCommand(
            new BridgeFromSourceInput(request.SourceType, request.SourceId, request.TriggerReason,
                request.SeverityOverride, request.DetectionEvidenceReference), CorrelationId), ct));

    [HttpPost("quality-events/from-gdocp-correction/{correctionId:guid}")]
    [HasPermission(QualityEventPermissions.BridgeManage)]
    public async Task<IActionResult> BridgeFromGDocPCorrection(
        Guid correctionId, [FromBody] BridgeSeverityOverrideApiRequest? request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(
            new BridgeQualityEventFromGDocPCorrectionCommand(correctionId, request?.SeverityOverride, CorrelationId), ct));

    [HttpPost("quality-events/from-obsolete-copy-finding/{findingId:guid}")]
    [HasPermission(QualityEventPermissions.BridgeManage)]
    public async Task<IActionResult> BridgeFromObsoleteCopyFinding(
        Guid findingId, [FromBody] BridgeSeverityOverrideApiRequest? request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(
            new BridgeQualityEventFromObsoleteCopyFindingCommand(findingId, request?.SeverityOverride, CorrelationId), ct));

    [HttpPost("quality-events/from-temporary-issue/{issueId:guid}")]
    [HasPermission(QualityEventPermissions.BridgeManage)]
    public async Task<IActionResult> BridgeFromTemporaryIssue(
        Guid issueId, [FromBody] BridgeSeverityOverrideApiRequest? request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(
            new BridgeQualityEventFromTemporaryIssueCommand(issueId, request?.SeverityOverride, CorrelationId), ct));

    [HttpPost("quality-events/from-external-impact/{assessmentId:guid}")]
    [HasPermission(QualityEventPermissions.BridgeManage)]
    public async Task<IActionResult> BridgeFromExternalImpact(
        Guid assessmentId, [FromBody] BridgeSeverityOverrideApiRequest? request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(
            new BridgeQualityEventFromExternalImpactCommand(assessmentId, request?.SeverityOverride, CorrelationId), ct));

    private static CreateQualityEventInput ToFields(CreateQualityEventApiRequest r) => new(
        r.EventTitle, r.EventDescription, r.EventType, r.EventSeverity, r.SourceType, r.SourceId,
        r.DetectionEvidenceReference, r.RegisterEntryId, r.ControlledDocumentId, r.TemplateVariantId,
        r.ExternalDocumentId, r.DetectedBy, r.ImmediateContainmentRequired, r.ImmediateContainmentSummary,
        r.RequiresDeviation, r.RequiresCAPA, r.DeviationWaiverJustification, r.DeviationWaiverEvidenceReference,
        r.ExternalQualitySystemReference);

    private string CorrelationId =>
        string.IsNullOrWhiteSpace(_correlationContext.CorrelationId) ? HttpContext.TraceIdentifier : _correlationContext.CorrelationId!;
}
