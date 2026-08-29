using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.DocumentManagementQualityEvent.Services;

/// <summary>
/// MOD-0029-FU22 — quality event lifecycle (GMG-QMS-SOP-0001): raise → open → (deviation / CAPA) → close.
///
/// SOP controls enforced here:
/// • A non-manual event must carry detection evidence — a bridged event has to say what detected it.
/// • A CRITICAL event must either raise a deviation or record an explicit justification for not doing so. The
///   waiver is recorded rather than forbidden, because a documented decision is auditable while a silent omission
///   is not.
/// • Closure is GATED: an event that required a deviation cannot close while that deviation is open, and one that
///   required CAPA cannot close while any of its actions is unsettled. Closure evidence is mandatory.
///
/// BOUNDARIES: no CAPA workflow engine, no investigation module, no scheduler, no e-signature, no external QMS
/// call. Nothing is hard-deleted; cancellation and closure are status changes.
/// </summary>
public sealed class DocumentQualityEventService
{
    private readonly IDocumentQualityEventRepository _events;
    private readonly IDocumentDeviationRepository _deviations;
    private readonly IDocumentCAPAActionRepository _capaActions;
    private readonly IDocumentQualityEventSourceLinkRepository _sourceLinks;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _currentUser;

    public DocumentQualityEventService(
        IDocumentQualityEventRepository events,
        IDocumentDeviationRepository deviations,
        IDocumentCAPAActionRepository capaActions,
        IDocumentQualityEventSourceLinkRepository sourceLinks,
        ITenantContext tenantContext,
        ICurrentUserContext currentUser)
    {
        _events = events;
        _deviations = deviations;
        _capaActions = capaActions;
        _sourceLinks = sourceLinks;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
    }

    // ── create ────────────────────────────────────────────────────────────────

    public async Task<Response<QualityEventModel>> CreateAsync(
        CreateQualityEventInput input, string correlationId, CancellationToken ct)
    {
        var tenantId = TenantGuard.RequireTenant(_tenantContext);

        if (string.IsNullOrWhiteSpace(input.EventTitle))
        {
            return Fail("A quality event title is required.", 400, QualityEventReasonCodes.TitleRequired, correlationId);
        }

        if (string.IsNullOrWhiteSpace(input.EventDescription))
        {
            return Fail("A quality event description is required.", 400, QualityEventReasonCodes.DescriptionRequired, correlationId);
        }

        var sourceType = QualityEventWire.ParseSourceType(input.SourceType);
        var evidence = Trim(input.DetectionEvidenceReference);

        // A bridged (non-manual) event must be able to point at what detected it.
        if (sourceType != QualityEventSourceType.Manual && evidence is null)
        {
            return Fail("Detection evidence is required for a quality event raised from a system source.", 400,
                QualityEventReasonCodes.DetectionEvidenceRequired, correlationId);
        }

        var severity = QualityEventWire.ParseEventSeverity(input.EventSeverity) ?? QualityEventSeverity.Minor;

        // SOP: a critical event without a deviation must say why, in writing.
        if (severity == QualityEventSeverity.Critical && !input.RequiresDeviation
            && string.IsNullOrWhiteSpace(input.DeviationWaiverJustification))
        {
            return Fail(
                "A critical quality event must require a deviation, or record an explicit justification for not doing so.",
                409, QualityEventReasonCodes.CriticalRequiresDeviation, correlationId);
        }

        var now = DateTimeOffset.UtcNow;
        var qualityEvent = new DocumentQualityEvent
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            QualityEventNumber = $"QE-{now:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpperInvariant()}",
            EventTitle = input.EventTitle.Trim(),
            EventDescription = input.EventDescription.Trim(),
            EventType = QualityEventWire.ParseEventType(input.EventType),
            EventSeverity = severity,
            EventStatus = QualityEventStatus.Draft,
            SourceType = sourceType,
            SourceId = input.SourceId,
            RegisterEntryId = input.RegisterEntryId,
            ControlledDocumentId = input.ControlledDocumentId,
            TemplateVariantId = input.TemplateVariantId,
            ExternalDocumentId = input.ExternalDocumentId,
            DetectedAt = now,
            DetectedBy = Trim(input.DetectedBy) ?? _currentUser.ActorName,
            DetectionEvidenceReference = evidence,
            ImmediateContainmentRequired = input.ImmediateContainmentRequired,
            ImmediateContainmentSummary = Trim(input.ImmediateContainmentSummary),
            RequiresDeviation = input.RequiresDeviation,
            RequiresCAPA = input.RequiresCAPA,
            DeviationWaiverJustification = Trim(input.DeviationWaiverJustification),
            DeviationWaiverEvidenceReference = Trim(input.DeviationWaiverEvidenceReference),
            ExternalQualitySystemReference = Trim(input.ExternalQualitySystemReference),
            CorrelationId = correlationId,
            CreatedBy = _currentUser.ActorName
        };

        await _events.CreateAsync(qualityEvent, ct);
        return Response<QualityEventModel>.Success(QualityEventWire.ToEvent(qualityEvent), 201, correlationId);
    }

    public async Task<Response<QualityEventModel>> OpenAsync(Guid id, string correlationId, CancellationToken ct)
    {
        var (fail, qualityEvent) = await LoadAsync(id, correlationId, ct);
        if (fail is not null)
        {
            return fail;
        }

        if (qualityEvent!.EventStatus != QualityEventStatus.Draft)
        {
            return Fail($"A {qualityEvent.EventStatus} quality event cannot be opened.", 409,
                QualityEventReasonCodes.EventInvalidState, correlationId);
        }

        qualityEvent.EventStatus = qualityEvent.RequiresDeviation
            ? QualityEventStatus.UnderAssessment
            : QualityEventStatus.Open;
        Touch(qualityEvent);
        await _events.UpdateAsync(qualityEvent, ct);
        return Response<QualityEventModel>.Success(QualityEventWire.ToEvent(qualityEvent), correlationId: correlationId);
    }

    // ── close / cancel ────────────────────────────────────────────────────────

    public async Task<Response<QualityEventModel>> CloseAsync(
        Guid id, CloseQualityEventInput input, string correlationId, CancellationToken ct)
    {
        var (fail, qualityEvent) = await LoadAsync(id, correlationId, ct);
        if (fail is not null)
        {
            return fail;
        }

        if (qualityEvent!.IsSettled())
        {
            return Fail($"The quality event is already {qualityEvent.EventStatus}.", 409,
                QualityEventReasonCodes.EventInvalidState, correlationId);
        }

        if (string.IsNullOrWhiteSpace(input.ClosureEvidenceReference))
        {
            return Fail("Closure evidence is required to close a quality event.", 400,
                QualityEventReasonCodes.ClosureEvidenceRequired, correlationId);
        }

        // A required deviation must actually be settled — the event cannot be closed over its head.
        if (qualityEvent.RequiresDeviation)
        {
            var linked = await _deviations.GetByQualityEventAsync(id, ct);
            if (linked.Count == 0 || linked.Any(d => !d.IsSettled()))
            {
                return Fail(
                    "This quality event required a deviation; the deviation must be closed or cancelled before the event can close.",
                    409, QualityEventReasonCodes.DeviationNotClosed, correlationId);
            }
        }

        if (qualityEvent.RequiresCAPA)
        {
            var actions = await _capaActions.GetByQualityEventAsync(id, ct);
            if (actions.Count == 0 || actions.Any(a => !a.IsSettled()))
            {
                return Fail(
                    "This quality event required CAPA; every CAPA action must be effective, closed or cancelled first.",
                    409, QualityEventReasonCodes.CapaNotSettled, correlationId);
            }
        }

        var now = DateTimeOffset.UtcNow;
        qualityEvent.EventStatus = QualityEventStatus.Closed;
        qualityEvent.ClosureEvidenceReference = input.ClosureEvidenceReference.Trim();
        qualityEvent.ClosureSummary = Trim(input.ClosureSummary);
        qualityEvent.ClosedAt = now;
        qualityEvent.ClosedBy = _currentUser.ActorName;
        Touch(qualityEvent);
        await _events.UpdateAsync(qualityEvent, ct);

        // Settle the source links so a re-run of the same detection can legitimately raise a new event.
        foreach (var link in (await _sourceLinks.GetByQualityEventAsync(id, ct))
                 .Where(l => l.LinkStatus == QualityEventSourceLinkStatus.Active))
        {
            link.LinkStatus = QualityEventSourceLinkStatus.Closed;
            link.UpdatedAt = now;
            link.UpdatedBy = _currentUser.ActorName;
            await _sourceLinks.UpdateAsync(link, ct);
        }

        return Response<QualityEventModel>.Success(QualityEventWire.ToEvent(qualityEvent), correlationId: correlationId);
    }

    public async Task<Response<QualityEventModel>> CancelAsync(
        Guid id, CancelQualityEventInput input, string correlationId, CancellationToken ct)
    {
        var (fail, qualityEvent) = await LoadAsync(id, correlationId, ct);
        if (fail is not null)
        {
            return fail;
        }

        if (qualityEvent!.IsSettled())
        {
            return Fail($"The quality event is already {qualityEvent.EventStatus}.", 409,
                QualityEventReasonCodes.EventInvalidState, correlationId);
        }

        if (string.IsNullOrWhiteSpace(input.Reason))
        {
            return Fail("A cancellation reason is required.", 400, QualityEventReasonCodes.ReasonRequired, correlationId);
        }

        qualityEvent.EventStatus = QualityEventStatus.Cancelled;
        qualityEvent.CancellationReason = input.Reason.Trim();
        Touch(qualityEvent);
        await _events.UpdateAsync(qualityEvent, ct);
        return Response<QualityEventModel>.Success(QualityEventWire.ToEvent(qualityEvent), correlationId: correlationId);
    }

    // ── source links ──────────────────────────────────────────────────────────

    public async Task<Response<QualityEventSourceLinkModel>> LinkSourceAsync(
        Guid id, LinkQualityEventSourceInput input, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var qualityEvent = await _events.GetByIdAsync(id, ct);
        if (qualityEvent is null)
        {
            return Response<QualityEventSourceLinkModel>.Fail(
                "Quality event not found.", 404, QualityEventReasonCodes.QualityEventNotFound, correlationId);
        }

        var sourceType = QualityEventWire.ParseSourceType(input.SourceType);
        var eventType = string.IsNullOrWhiteSpace(input.EventType)
            ? qualityEvent.EventType
            : QualityEventWire.ParseEventType(input.EventType);

        // Idempotent: the same source/type pair does not get a second link on the same event.
        var existing = (await _sourceLinks.GetByQualityEventAsync(id, ct))
            .FirstOrDefault(l => l.SourceType == sourceType && l.SourceId == input.SourceId && l.EventType == eventType);
        if (existing is not null)
        {
            return Response<QualityEventSourceLinkModel>.Success(
                QualityEventWire.ToLink(existing), correlationId: correlationId);
        }

        var link = await CreateLinkAsync(qualityEvent, sourceType, input.SourceId, eventType,
            input.RegisterEntryId, input.SourceReferenceSnapshot, input.Notes, correlationId, ct);
        return Response<QualityEventSourceLinkModel>.Success(QualityEventWire.ToLink(link), 201, correlationId);
    }

    public async Task<Response<IReadOnlyList<QualityEventSourceLinkModel>>> GetSourceLinksAsync(
        Guid id, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var qualityEvent = await _events.GetByIdAsync(id, ct);
        if (qualityEvent is null)
        {
            return Response<IReadOnlyList<QualityEventSourceLinkModel>>.Fail(
                "Quality event not found.", 404, QualityEventReasonCodes.QualityEventNotFound, correlationId);
        }

        var rows = await _sourceLinks.GetByQualityEventAsync(id, ct);
        return Response<IReadOnlyList<QualityEventSourceLinkModel>>.Success(
            rows.Select(QualityEventWire.ToLink).ToList(), correlationId: correlationId);
    }

    // ── reads ─────────────────────────────────────────────────────────────────

    public async Task<Response<QualityEventModel>> GetAsync(Guid id, string correlationId, CancellationToken ct)
    {
        var (fail, qualityEvent) = await LoadAsync(id, correlationId, ct);
        return fail ?? Response<QualityEventModel>.Success(
            QualityEventWire.ToEvent(qualityEvent!), correlationId: correlationId);
    }

    public async Task<Response<IReadOnlyList<QualityEventModel>>> ListAsync(string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var rows = await _events.GetAllForTenantAsync(ct);
        return Response<IReadOnlyList<QualityEventModel>>.Success(
            rows.Select(QualityEventWire.ToEvent).ToList(), correlationId: correlationId);
    }

    // ── internal: used by the bridge and the deviation/CAPA services ──────────

    internal async Task<DocumentQualityEventSourceLink> CreateLinkAsync(
        DocumentQualityEvent qualityEvent,
        QualityEventSourceType sourceType,
        Guid sourceId,
        QualityEventType eventType,
        Guid? registerEntryId,
        string? sourceReferenceSnapshot,
        string? notes,
        string correlationId,
        CancellationToken ct)
    {
        var link = new DocumentQualityEventSourceLink
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantContext.TenantId,
            QualityEventId = qualityEvent.Id,
            SourceType = sourceType,
            SourceId = sourceId,
            EventType = eventType,
            RegisterEntryId = registerEntryId ?? qualityEvent.RegisterEntryId,
            LinkStatus = QualityEventSourceLinkStatus.Active,
            SourceReferenceSnapshot = Trim(sourceReferenceSnapshot),
            Notes = Trim(notes),
            CorrelationId = correlationId,
            CreatedBy = _currentUser.ActorName
        };
        await _sourceLinks.CreateAsync(link, ct);
        return link;
    }

    /// <summary>Records the deviation linkage back onto the event and advances its status.</summary>
    internal async Task AttachDeviationAsync(Guid qualityEventId, Guid deviationId, CancellationToken ct)
    {
        var qualityEvent = await _events.GetByIdAsync(qualityEventId, ct);
        if (qualityEvent is null)
        {
            return;
        }

        qualityEvent.DeviationId = deviationId;
        if (qualityEvent.EventStatus is QualityEventStatus.Draft or QualityEventStatus.Open or QualityEventStatus.UnderAssessment)
        {
            qualityEvent.EventStatus = QualityEventStatus.DeviationOpened;
        }

        Touch(qualityEvent);
        await _events.UpdateAsync(qualityEvent, ct);
    }

    /// <summary>Records the CAPA linkage back onto the event and advances its status.</summary>
    internal async Task AttachCapaAsync(Guid qualityEventId, Guid capaActionId, CancellationToken ct)
    {
        var qualityEvent = await _events.GetByIdAsync(qualityEventId, ct);
        if (qualityEvent is null)
        {
            return;
        }

        if (!qualityEvent.CAPAActionIds.Contains(capaActionId))
        {
            qualityEvent.CAPAActionIds.Add(capaActionId);
        }

        if (qualityEvent.EventStatus is not (QualityEventStatus.Closed or QualityEventStatus.Cancelled))
        {
            qualityEvent.EventStatus = QualityEventStatus.CAPAInProgress;
        }

        Touch(qualityEvent);
        await _events.UpdateAsync(qualityEvent, ct);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private async Task<(Response<QualityEventModel>? Fail, DocumentQualityEvent? Event)> LoadAsync(
        Guid id, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var qualityEvent = await _events.GetByIdAsync(id, ct);
        return qualityEvent is null
            ? (Fail("Quality event not found.", 404, QualityEventReasonCodes.QualityEventNotFound, correlationId), null)
            : (null, qualityEvent);
    }

    private void Touch(DocumentQualityEvent e)
    {
        e.UpdatedAt = DateTimeOffset.UtcNow;
        e.UpdatedBy = _currentUser.ActorName;
    }

    private static Response<QualityEventModel> Fail(string error, int status, string reason, string correlationId) =>
        Response<QualityEventModel>.Fail(error, status, reason, correlationId);

    private static string? Trim(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}
