using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.DocumentManagementQualityEvent.Services;

/// <summary>
/// MOD-0029-FU22 — GxP deviation lifecycle (GMG-QMS-SOP-0001): raise from a quality event → investigate → CAPA →
/// close.
///
/// ⚠️ This is the QUALITY deviation, not MOD-0028-FU09's collection-tree read-back deviation. See
/// <see cref="DocumentDeviation"/> for why the two must never be merged.
///
/// SOP controls enforced here:
/// • A deviation always hangs off a quality event — there is no free-floating deviation.
/// • A CRITICAL deviation cannot close without BOTH a root cause AND a patient/product/regulatory impact verdict.
///   "NotAssessed" is never a closure basis: not having looked is not the same as having found no impact.
/// • A deviation that required CAPA cannot close while any action is unsettled — unless a documented closure
///   exception is recorded, which is auditable in a way a silent skip is not.
/// • Closure evidence is mandatory.
///
/// FU22 records what an investigator concluded; it implements no investigation module and no root-cause
/// methodology engine. Nothing is hard-deleted.
/// </summary>
public sealed class DocumentDeviationService
{
    private readonly IDocumentDeviationRepository _deviations;
    private readonly IDocumentQualityEventRepository _events;
    private readonly IDocumentCAPAActionRepository _capaActions;
    private readonly DocumentQualityEventService _qualityEventService;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _currentUser;

    public DocumentDeviationService(
        IDocumentDeviationRepository deviations,
        IDocumentQualityEventRepository events,
        IDocumentCAPAActionRepository capaActions,
        DocumentQualityEventService qualityEventService,
        ITenantContext tenantContext,
        ICurrentUserContext currentUser)
    {
        _deviations = deviations;
        _events = events;
        _capaActions = capaActions;
        _qualityEventService = qualityEventService;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
    }

    // ── create / open ─────────────────────────────────────────────────────────

    public async Task<Response<DeviationModel>> CreateAsync(
        CreateDeviationInput input, string correlationId, CancellationToken ct)
    {
        var tenantId = TenantGuard.RequireTenant(_tenantContext);

        // Tenant-scoped: a foreign quality event simply does not resolve.
        var qualityEvent = await _events.GetByIdAsync(input.QualityEventId, ct);
        if (qualityEvent is null)
        {
            return Fail("Quality event not found; a deviation must be raised from a recorded quality event.", 404,
                QualityEventReasonCodes.DeviationRequiresQualityEvent, correlationId);
        }

        if (string.IsNullOrWhiteSpace(input.DeviationTitle) || string.IsNullOrWhiteSpace(input.DeviationDescription))
        {
            return Fail("A deviation title and description are required.", 400,
                QualityEventReasonCodes.ValidationFailed, correlationId);
        }

        var now = DateTimeOffset.UtcNow;
        var deviation = new DocumentDeviation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            DeviationNumber = $"DEV-{now:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpperInvariant()}",
            QualityEventId = qualityEvent.Id,
            DeviationTitle = input.DeviationTitle.Trim(),
            DeviationDescription = input.DeviationDescription.Trim(),
            DeviationCategory = QualityEventWire.ParseDeviationCategory(input.DeviationCategory),
            DeviationSeverity = QualityEventWire.ParseDeviationSeverity(input.DeviationSeverity),
            DeviationStatus = QualityDeviationStatus.Open,
            OccurredAt = input.OccurredAt,
            DetectedAt = now,
            ReportedBy = Trim(input.ReportedBy) ?? _currentUser.ActorName,
            RequiresCAPA = input.RequiresCAPA,
            CorrelationId = correlationId,
            CreatedBy = _currentUser.ActorName
        };

        await _deviations.CreateAsync(deviation, ct);
        await _qualityEventService.AttachDeviationAsync(qualityEvent.Id, deviation.Id, ct);
        return Response<DeviationModel>.Success(QualityEventWire.ToDeviation(deviation), 201, correlationId);
    }

    public async Task<Response<DeviationModel>> OpenInvestigationAsync(Guid id, string correlationId, CancellationToken ct)
    {
        var (fail, deviation) = await LoadAsync(id, correlationId, ct);
        if (fail is not null)
        {
            return fail;
        }

        if (deviation!.IsSettled())
        {
            return Fail($"A {deviation.DeviationStatus} deviation cannot be moved to investigation.", 409,
                QualityEventReasonCodes.DeviationInvalidState, correlationId);
        }

        deviation.DeviationStatus = QualityDeviationStatus.UnderInvestigation;
        Touch(deviation);
        await _deviations.UpdateAsync(deviation, ct);
        return Response<DeviationModel>.Success(QualityEventWire.ToDeviation(deviation), correlationId: correlationId);
    }

    // ── investigation ─────────────────────────────────────────────────────────

    public async Task<Response<DeviationModel>> RecordInvestigationAsync(
        Guid id, RecordDeviationInvestigationInput input, string correlationId, CancellationToken ct)
    {
        var (fail, deviation) = await LoadAsync(id, correlationId, ct);
        if (fail is not null)
        {
            return fail;
        }

        if (deviation!.IsSettled())
        {
            return Fail($"A {deviation.DeviationStatus} deviation cannot be investigated further.", 409,
                QualityEventReasonCodes.DeviationInvalidState, correlationId);
        }

        deviation.RootCauseSummary = Trim(input.RootCauseSummary) ?? deviation.RootCauseSummary;
        deviation.RootCauseCategory = input.RootCauseCategory is null
            ? deviation.RootCauseCategory
            : QualityEventWire.ParseRootCause(input.RootCauseCategory);
        deviation.ImpactAssessmentSummary = Trim(input.ImpactAssessmentSummary) ?? deviation.ImpactAssessmentSummary;
        deviation.PatientProductRegulatoryImpact = input.PatientProductRegulatoryImpact is null
            ? deviation.PatientProductRegulatoryImpact
            : QualityEventWire.ParseImpact(input.PatientProductRegulatoryImpact);
        deviation.InvestigationEvidenceReference = Trim(input.InvestigationEvidenceReference) ?? deviation.InvestigationEvidenceReference;

        deviation.DeviationStatus = deviation.RootCauseCategory == DeviationRootCauseCategory.NotAssessed
            ? QualityDeviationStatus.RootCausePending
            : QualityDeviationStatus.UnderInvestigation;

        Touch(deviation);
        await _deviations.UpdateAsync(deviation, ct);
        return Response<DeviationModel>.Success(QualityEventWire.ToDeviation(deviation), correlationId: correlationId);
    }

    public async Task<Response<DeviationModel>> RequireCapaAsync(Guid id, string correlationId, CancellationToken ct)
    {
        var (fail, deviation) = await LoadAsync(id, correlationId, ct);
        if (fail is not null)
        {
            return fail;
        }

        if (deviation!.IsSettled())
        {
            return Fail($"A {deviation.DeviationStatus} deviation cannot require new CAPA.", 409,
                QualityEventReasonCodes.DeviationInvalidState, correlationId);
        }

        deviation.RequiresCAPA = true;
        deviation.DeviationStatus = QualityDeviationStatus.CAPARequired;
        Touch(deviation);
        await _deviations.UpdateAsync(deviation, ct);
        return Response<DeviationModel>.Success(QualityEventWire.ToDeviation(deviation), correlationId: correlationId);
    }

    // ── close / cancel ────────────────────────────────────────────────────────

    public async Task<Response<DeviationModel>> CloseAsync(
        Guid id, CloseDeviationInput input, string correlationId, CancellationToken ct)
    {
        var (fail, deviation) = await LoadAsync(id, correlationId, ct);
        if (fail is not null)
        {
            return fail;
        }

        if (deviation!.IsSettled())
        {
            return Fail($"The deviation is already {deviation.DeviationStatus}.", 409,
                QualityEventReasonCodes.DeviationInvalidState, correlationId);
        }

        if (string.IsNullOrWhiteSpace(input.ClosureEvidenceReference))
        {
            return Fail("Closure evidence is required to close a deviation.", 400,
                QualityEventReasonCodes.ClosureEvidenceRequired, correlationId);
        }

        // SOP: a critical deviation is never closed on an unexamined basis.
        if (deviation.DeviationSeverity == QualityDeviationSeverity.Critical)
        {
            if (string.IsNullOrWhiteSpace(deviation.RootCauseSummary)
                || deviation.RootCauseCategory == DeviationRootCauseCategory.NotAssessed)
            {
                return Fail("A critical deviation cannot be closed without a recorded root cause.", 409,
                    QualityEventReasonCodes.RootCauseRequired, correlationId);
            }

            if (deviation.PatientProductRegulatoryImpact == DeviationImpactAssessment.NotAssessed)
            {
                return Fail(
                    "A critical deviation cannot be closed without a patient/product/regulatory impact assessment.",
                    409, QualityEventReasonCodes.ImpactAssessmentRequired, correlationId);
            }
        }

        var exception = Trim(input.ClosureExceptionJustification);
        if (deviation.RequiresCAPA)
        {
            var actions = await _capaActions.GetByDeviationAsync(id, ct);
            var outstanding = actions.Count == 0 || actions.Any(a => !a.IsSettled());
            if (outstanding && exception is null)
            {
                return Fail(
                    "This deviation requires CAPA; every action must be effective, closed or cancelled — or record a closure exception justification.",
                    409, QualityEventReasonCodes.DeviationRequiresCapa, correlationId);
            }
        }

        var now = DateTimeOffset.UtcNow;
        deviation.DeviationStatus = QualityDeviationStatus.Closed;
        deviation.ClosureEvidenceReference = input.ClosureEvidenceReference.Trim();
        deviation.ClosureExceptionJustification = exception;
        deviation.ClosedAt = now;
        deviation.ClosedBy = _currentUser.ActorName;
        Touch(deviation);
        await _deviations.UpdateAsync(deviation, ct);
        return Response<DeviationModel>.Success(QualityEventWire.ToDeviation(deviation), correlationId: correlationId);
    }

    public async Task<Response<DeviationModel>> CancelAsync(
        Guid id, CancelDeviationInput input, string correlationId, CancellationToken ct)
    {
        var (fail, deviation) = await LoadAsync(id, correlationId, ct);
        if (fail is not null)
        {
            return fail;
        }

        if (deviation!.IsSettled())
        {
            return Fail($"The deviation is already {deviation.DeviationStatus}.", 409,
                QualityEventReasonCodes.DeviationInvalidState, correlationId);
        }

        if (string.IsNullOrWhiteSpace(input.Reason))
        {
            return Fail("A cancellation reason is required.", 400, QualityEventReasonCodes.ReasonRequired, correlationId);
        }

        deviation.DeviationStatus = QualityDeviationStatus.Cancelled;
        deviation.CancellationReason = input.Reason.Trim();
        Touch(deviation);
        await _deviations.UpdateAsync(deviation, ct);
        return Response<DeviationModel>.Success(QualityEventWire.ToDeviation(deviation), correlationId: correlationId);
    }

    // ── reads ─────────────────────────────────────────────────────────────────

    public async Task<Response<DeviationModel>> GetAsync(Guid id, string correlationId, CancellationToken ct)
    {
        var (fail, deviation) = await LoadAsync(id, correlationId, ct);
        return fail ?? Response<DeviationModel>.Success(
            QualityEventWire.ToDeviation(deviation!), correlationId: correlationId);
    }

    public async Task<Response<IReadOnlyList<DeviationModel>>> ListAsync(string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var rows = await _deviations.GetAllForTenantAsync(ct);
        return Response<IReadOnlyList<DeviationModel>>.Success(
            rows.Select(QualityEventWire.ToDeviation).ToList(), correlationId: correlationId);
    }

    // ── internal ──────────────────────────────────────────────────────────────

    /// <summary>Records a CAPA linkage on the deviation. Called by the CAPA service.</summary>
    internal async Task AttachCapaAsync(Guid deviationId, Guid capaActionId, CancellationToken ct)
    {
        var deviation = await _deviations.GetByIdAsync(deviationId, ct);
        if (deviation is null)
        {
            return;
        }

        if (!deviation.CAPAActionIds.Contains(capaActionId))
        {
            deviation.CAPAActionIds.Add(capaActionId);
        }

        if (!deviation.IsSettled())
        {
            deviation.DeviationStatus = QualityDeviationStatus.CAPAInProgress;
        }

        Touch(deviation);
        await _deviations.UpdateAsync(deviation, ct);
    }

    /// <summary>
    /// An ineffective CAPA pushes its deviation back to CAPARequired: the failure is surfaced rather than absorbed.
    /// </summary>
    internal async Task MarkCapaIneffectiveAsync(Guid deviationId, CancellationToken ct)
    {
        var deviation = await _deviations.GetByIdAsync(deviationId, ct);
        if (deviation is null || deviation.IsSettled())
        {
            return;
        }

        deviation.RequiresCAPA = true;
        deviation.DeviationStatus = QualityDeviationStatus.CAPARequired;
        Touch(deviation);
        await _deviations.UpdateAsync(deviation, ct);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private async Task<(Response<DeviationModel>? Fail, DocumentDeviation? Deviation)> LoadAsync(
        Guid id, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var deviation = await _deviations.GetByIdAsync(id, ct);
        return deviation is null
            ? (Fail("Deviation not found.", 404, QualityEventReasonCodes.DeviationNotFound, correlationId), null)
            : (null, deviation);
    }

    private void Touch(DocumentDeviation d)
    {
        d.UpdatedAt = DateTimeOffset.UtcNow;
        d.UpdatedBy = _currentUser.ActorName;
    }

    private static Response<DeviationModel> Fail(string error, int status, string reason, string correlationId) =>
        Response<DeviationModel>.Fail(error, status, reason, correlationId);

    private static string? Trim(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}
