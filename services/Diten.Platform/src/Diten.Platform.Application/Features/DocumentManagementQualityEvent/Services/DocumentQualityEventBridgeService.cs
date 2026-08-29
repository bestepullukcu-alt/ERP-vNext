using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.DocumentManagementQualityEvent.Services;

/// <summary>
/// MOD-0029-FU22 — raises a quality event from an existing FU aggregate, applying the trigger mapping
/// (GMG-QMS-SOP-0001).
///
/// PULL, NOT PUSH — and deliberately so: the bridge READS the source aggregate through its existing repository
/// contract. It is NOT injected into FU17's copy service, FU20's issue service or FU21's correction recorder.
/// Injecting it would change those features' behaviour and validation surface and break their existing tests,
/// which the task forbids. The consequence is that bridging is an explicit call (API or a future adapter), not an
/// automatic consequence of the source event — recorded as a remaining gap.
///
/// IDEMPOTENT: the source link table is checked before anything is created, so re-running a detection that already
/// raised an OPEN event returns that event instead of creating a duplicate. Once the original event is closed, the
/// same source may legitimately raise a new one.
///
/// EXISTING STRING REFERENCES ARE NEVER REMOVED. The source's own <c>QualityEventReference</c> /
/// <c>DeviationReference</c> fields are snapshotted onto the link for traceability and left exactly as they are;
/// FU22 does not migrate or overwrite them.
/// </summary>
public sealed class DocumentQualityEventBridgeService
{
    private readonly DocumentQualityEventService _qualityEvents;
    private readonly IDocumentQualityEventRepository _events;
    private readonly IDocumentQualityEventSourceLinkRepository _sourceLinks;
    private readonly IDocumentObsoleteCopyFindingRepository _obsoleteFindings;
    private readonly IDocumentTemporaryControlledIssueRepository _temporaryIssues;
    private readonly IDocumentGDocPCorrectionRecordRepository _corrections;
    private readonly IExternalDocumentImpactAssessmentRepository _externalImpacts;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _currentUser;

    public DocumentQualityEventBridgeService(
        DocumentQualityEventService qualityEvents,
        IDocumentQualityEventRepository events,
        IDocumentQualityEventSourceLinkRepository sourceLinks,
        IDocumentObsoleteCopyFindingRepository obsoleteFindings,
        IDocumentTemporaryControlledIssueRepository temporaryIssues,
        IDocumentGDocPCorrectionRecordRepository corrections,
        IExternalDocumentImpactAssessmentRepository externalImpacts,
        ITenantContext tenantContext,
        ICurrentUserContext currentUser)
    {
        _qualityEvents = qualityEvents;
        _events = events;
        _sourceLinks = sourceLinks;
        _obsoleteFindings = obsoleteFindings;
        _temporaryIssues = temporaryIssues;
        _corrections = corrections;
        _externalImpacts = externalImpacts;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
    }

    // ── FU17: obsolete copy finding ───────────────────────────────────────────

    public async Task<Response<QualityEventModel>> FromObsoleteCopyFindingAsync(
        Guid findingId, string? severityOverride, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var finding = await _obsoleteFindings.GetByIdAsync(findingId, ct);
        if (finding is null)
        {
            return Fail("Obsolete copy finding not found.", 404, QualityEventReasonCodes.SourceNotFound, correlationId);
        }

        var mapping = Override(
            DocumentQualityEventTriggerMapper.FromObsoleteCopyFinding(finding.FindingType), severityOverride);

        return await RaiseAsync(
            QualityEventSourceType.ObsoleteCopyFinding, finding.Id, mapping,
            title: $"Obsolete copy finding: {finding.FindingType}",
            description: finding.Description,
            detectionEvidence: finding.ResolutionEvidenceReference ?? finding.FindingKey,
            registerEntryId: finding.RegisterEntryId,
            controlledDocumentId: null,
            externalDocumentId: null,
            // The existing free-text reference is snapshotted, never removed from the finding.
            sourceReferenceSnapshot: finding.QualityEventReference ?? finding.DeviationReference,
            correlationId, ct);
    }

    // ── FU20: temporary controlled issue ──────────────────────────────────────

    public async Task<Response<QualityEventModel>> FromTemporaryIssueAsync(
        Guid issueId, string? severityOverride, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var issue = await _temporaryIssues.GetByIdAsync(issueId, ct);
        if (issue is null)
        {
            return Fail("Temporary controlled issue not found.", 404, QualityEventReasonCodes.SourceNotFound, correlationId);
        }

        var mapping = Override(
            DocumentQualityEventTriggerMapper.FromTemporaryIssue(issue.IssueStatus), severityOverride);

        return await RaiseAsync(
            QualityEventSourceType.TemporaryControlledIssue, issue.Id, mapping,
            title: $"Temporary controlled issue {issue.IssueNumber}: {issue.IssueStatus}",
            description: issue.IssueReason
                ?? $"Temporary controlled issue {issue.IssueNumber} requires quality assessment ({issue.IssueStatus}).",
            detectionEvidence: issue.ReconciliationEvidenceReference ?? issue.ApprovalEvidenceReference ?? issue.IssueNumber,
            registerEntryId: issue.RegisterEntryId,
            controlledDocumentId: issue.ControlledDocumentId,
            externalDocumentId: null,
            sourceReferenceSnapshot: issue.DeviationReference ?? issue.CorrectiveActionReference,
            correlationId, ct);
    }

    // ── FU21: GDocP correction ────────────────────────────────────────────────

    public async Task<Response<QualityEventModel>> FromGDocPCorrectionAsync(
        Guid correctionId, string? severityOverride, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var correction = await _corrections.GetByIdAsync(correctionId, ct);
        if (correction is null)
        {
            return Fail("GDocP correction record not found.", 404, QualityEventReasonCodes.SourceNotFound, correlationId);
        }

        var mapping = Override(
            DocumentQualityEventTriggerMapper.FromGDocPCorrection(correction.CorrectionType, correction.IsBackdatingCorrection),
            severityOverride);

        return await RaiseAsync(
            QualityEventSourceType.GDocPCorrection, correction.Id, mapping,
            title: $"GDocP correction {correction.CorrectionNumber}: {correction.CorrectionType} on {correction.FieldPath}",
            description:
                $"Field '{correction.FieldPath}' corrected from '{correction.PreviousValueSnapshot}' to " +
                $"'{correction.NewValueSnapshot}'. Reason: {correction.CorrectionReason}",
            detectionEvidence: correction.CorrectionEvidenceReference ?? correction.CorrectionNumber,
            registerEntryId: correction.RegisterEntryId,
            controlledDocumentId: correction.ControlledDocumentId,
            externalDocumentId: null,
            sourceReferenceSnapshot: correction.DeviationReference,
            correlationId, ct);
    }

    // ── FU14: external document impact assessment ─────────────────────────────

    public async Task<Response<QualityEventModel>> FromExternalImpactAssessmentAsync(
        Guid assessmentId, string? severityOverride, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var assessment = await _externalImpacts.GetByIdAsync(assessmentId, ct);
        if (assessment is null)
        {
            return Fail("External document impact assessment not found.", 404,
                QualityEventReasonCodes.SourceNotFound, correlationId);
        }

        var mapping = Override(
            DocumentQualityEventTriggerMapper.FromExternalImpact(assessment.RecommendedAction), severityOverride);

        return await RaiseAsync(
            QualityEventSourceType.ExternalDocumentImpactAssessment, assessment.Id, mapping,
            title: $"External regulatory impact: {assessment.RecommendedAction}",
            description: assessment.ImpactSummary
                ?? $"External document impact assessment recommends {assessment.RecommendedAction}.",
            detectionEvidence: assessment.AssessmentEvidenceReference ?? assessment.Id.ToString(),
            registerEntryId: null,
            controlledDocumentId: null,
            externalDocumentId: assessment.ExternalDocumentRegisterEntryId,
            sourceReferenceSnapshot: assessment.ActionReference,
            correlationId, ct);
    }

    // ── generic bridge ────────────────────────────────────────────────────────

    /// <summary>
    /// Bridges a source FU22 has no dedicated reader for (suspension case, periodic review escalation, downtime
    /// event, release gate, training). The caller supplies the detection evidence and reason; the mapping is
    /// applied from the source type.
    /// </summary>
    public async Task<Response<QualityEventModel>> FromSourceAsync(
        BridgeFromSourceInput input, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var sourceType = QualityEventWire.ParseSourceType(input.SourceType);

        if (string.IsNullOrWhiteSpace(input.DetectionEvidenceReference))
        {
            return Fail("Detection evidence is required to bridge a quality event from a system source.", 400,
                QualityEventReasonCodes.DetectionEvidenceRequired, correlationId);
        }

        var mapping = Override(sourceType switch
        {
            QualityEventSourceType.SuspensionCase => DocumentQualityEventTriggerMapper.FromSuspensionCase(),
            QualityEventSourceType.PeriodicReviewEscalation =>
                DocumentQualityEventTriggerMapper.FromPeriodicReviewEscalation(ReviewEscalationSeverity.Major),
            _ => new QualityTriggerMappingModel(
                nameof(QualityEventType.Other), nameof(QualityEventSeverity.Minor),
                nameof(QualityDeviationCategory.DocumentationControl), nameof(QualityDeviationSeverity.Minor),
                RequiresDeviation: false, RequiresCAPA: false, ImmediateContainmentRequired: false,
                $"No dedicated mapping for source type {sourceType}; assessed as minor pending human review.")
        }, input.SeverityOverride);

        return await RaiseAsync(
            sourceType, input.SourceId, mapping,
            title: $"Document control quality event from {sourceType}",
            description: Trim(input.TriggerReason) ?? $"Quality event bridged from {sourceType}.",
            detectionEvidence: input.DetectionEvidenceReference,
            registerEntryId: null, controlledDocumentId: null, externalDocumentId: null,
            sourceReferenceSnapshot: null,
            correlationId, ct);
    }

    // ── core ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Idempotent raise: an OPEN event already linked to this (source, event type) is returned unchanged.
    /// </summary>
    private async Task<Response<QualityEventModel>> RaiseAsync(
        QualityEventSourceType sourceType,
        Guid sourceId,
        QualityTriggerMappingModel mapping,
        string title,
        string description,
        string detectionEvidence,
        Guid? registerEntryId,
        Guid? controlledDocumentId,
        Guid? externalDocumentId,
        string? sourceReferenceSnapshot,
        string correlationId,
        CancellationToken ct)
    {
        var eventType = QualityEventWire.ParseEventType(mapping.EventType);

        var existingLinks = await _sourceLinks.GetBySourceAsync(sourceType, sourceId, ct);
        foreach (var link in existingLinks.Where(l => l.EventType == eventType))
        {
            var existing = await _events.GetByIdAsync(link.QualityEventId, ct);
            if (existing is not null && !existing.IsSettled())
            {
                return Response<QualityEventModel>.Success(QualityEventWire.ToEvent(existing), correlationId: correlationId);
            }
        }

        var created = await _qualityEvents.CreateAsync(new CreateQualityEventInput(
            EventTitle: title,
            EventDescription: description,
            EventType: mapping.EventType,
            EventSeverity: mapping.EventSeverity,
            SourceType: sourceType.ToString(),
            SourceId: sourceId,
            DetectionEvidenceReference: detectionEvidence,
            RegisterEntryId: registerEntryId,
            ControlledDocumentId: controlledDocumentId,
            TemplateVariantId: null,
            ExternalDocumentId: externalDocumentId,
            DetectedBy: _currentUser.ActorName,
            ImmediateContainmentRequired: mapping.ImmediateContainmentRequired,
            ImmediateContainmentSummary: mapping.ImmediateContainmentRequired
                ? "Immediate containment indicated by the trigger mapping; confirm the affected copies/records are secured."
                : null,
            RequiresDeviation: mapping.RequiresDeviation,
            RequiresCAPA: mapping.RequiresCAPA,
            // A critical mapping always raises a deviation, so no waiver justification is needed here.
            DeviationWaiverJustification: null,
            DeviationWaiverEvidenceReference: null,
            ExternalQualitySystemReference: null), correlationId, ct);

        if (!created.IsSuccessful || created.Data is null)
        {
            return created;
        }

        var qualityEvent = await _events.GetByIdAsync(created.Data.Id, ct);
        if (qualityEvent is not null)
        {
            await _qualityEvents.CreateLinkAsync(qualityEvent, sourceType, sourceId, eventType, registerEntryId,
                sourceReferenceSnapshot, mapping.MappingRationale, correlationId, ct);
        }

        return created;
    }

    private static QualityTriggerMappingModel Override(QualityTriggerMappingModel mapping, string? severityOverride) =>
        DocumentQualityEventTriggerMapper.WithSeverityOverride(
            mapping, QualityEventWire.ParseEventSeverity(severityOverride));

    private static Response<QualityEventModel> Fail(string error, int status, string reason, string correlationId) =>
        Response<QualityEventModel>.Fail(error, status, reason, correlationId);

    private static string? Trim(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}
