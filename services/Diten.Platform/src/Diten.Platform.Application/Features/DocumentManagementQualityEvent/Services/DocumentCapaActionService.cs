using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.DocumentManagementQualityEvent.Services;

/// <summary>
/// MOD-0029-FU22 — CAPA action lifecycle (GMG-QMS-SOP-0001): Draft → Open → InProgress → Completed →
/// (EffectivenessPending → Effective | Ineffective) → Closed.
///
/// A FOUNDATION STATE MACHINE, NOT A WORKFLOW ENGINE. There is no MOD-0023 workflow runtime behind these
/// transitions, no scheduler firing on the effectiveness due date, and no e-signature — completion and
/// effectiveness are attested by an evidence REFERENCE recorded by a human.
///
/// SOP controls enforced here:
/// • An action must hang off a quality event or a deviation — an orphan action has no context to be judged in.
/// • A corrective/preventive action needs an owner AND a due date: an undated commitment is not a commitment.
/// • Completion requires evidence.
/// • Where an effectiveness check is required, completion moves to EffectivenessPending and the action CANNOT be
///   closed until the verdict is recorded.
/// • PRODUCT DECISION — an INEFFECTIVE action can never be closed as effective. Closing it at all requires a
///   documented exception justification, and its parent deviation is pushed back to CAPARequired so the failed
///   action forces new action rather than being quietly absorbed.
///
/// Nothing is hard-deleted.
/// </summary>
public sealed class DocumentCapaActionService
{
    private readonly IDocumentCAPAActionRepository _actions;
    private readonly IDocumentQualityEventRepository _events;
    private readonly IDocumentDeviationRepository _deviations;
    private readonly DocumentQualityEventService _qualityEventService;
    private readonly DocumentDeviationService _deviationService;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _currentUser;

    public DocumentCapaActionService(
        IDocumentCAPAActionRepository actions,
        IDocumentQualityEventRepository events,
        IDocumentDeviationRepository deviations,
        DocumentQualityEventService qualityEventService,
        DocumentDeviationService deviationService,
        ITenantContext tenantContext,
        ICurrentUserContext currentUser)
    {
        _actions = actions;
        _events = events;
        _deviations = deviations;
        _qualityEventService = qualityEventService;
        _deviationService = deviationService;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
    }

    // ── create ────────────────────────────────────────────────────────────────

    public async Task<Response<CapaActionModel>> CreateAsync(
        CreateCapaActionInput input, string correlationId, CancellationToken ct)
    {
        var tenantId = TenantGuard.RequireTenant(_tenantContext);

        if (string.IsNullOrWhiteSpace(input.ActionTitle) || string.IsNullOrWhiteSpace(input.ActionDescription))
        {
            return Fail("A CAPA action title and description are required.", 400,
                QualityEventReasonCodes.ValidationFailed, correlationId);
        }

        // Tenant-scoped parent resolution: a foreign parent simply does not resolve.
        DocumentQualityEvent? qualityEvent = null;
        DocumentDeviation? deviation = null;

        if (input.QualityEventId is { } eventId)
        {
            qualityEvent = await _events.GetByIdAsync(eventId, ct);
        }

        if (input.DeviationId is { } deviationId)
        {
            deviation = await _deviations.GetByIdAsync(deviationId, ct);
        }

        if (qualityEvent is null && deviation is null)
        {
            return Fail("A CAPA action must be linked to a quality event or a deviation.", 400,
                QualityEventReasonCodes.CapaRequiresParent, correlationId);
        }

        var actionType = QualityEventWire.ParseCapaType(input.ActionType);

        if (input.ActionOwnerUserId is null && string.IsNullOrWhiteSpace(input.ActionOwnerRole))
        {
            return Fail("A named CAPA action owner (user or role) is required.", 400,
                QualityEventReasonCodes.CapaOwnerRequired, correlationId);
        }

        // An undated corrective/preventive commitment cannot be tracked or escalated.
        if (actionType is CapaActionType.CorrectiveAction or CapaActionType.PreventiveAction && input.DueDate is null)
        {
            return Fail("A corrective or preventive action requires a due date.", 400,
                QualityEventReasonCodes.CapaDueDateRequired, correlationId);
        }

        var now = DateTimeOffset.UtcNow;
        var action = new DocumentCAPAAction
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CAPANumber = $"CAPA-{now:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpperInvariant()}",
            QualityEventId = qualityEvent?.Id ?? deviation?.QualityEventId,
            DeviationId = deviation?.Id,
            ActionType = actionType,
            ActionTitle = input.ActionTitle.Trim(),
            ActionDescription = input.ActionDescription.Trim(),
            ActionStatus = CapaActionStatus.Open,
            ActionOwnerUserId = input.ActionOwnerUserId,
            ActionOwnerRole = Trim(input.ActionOwnerRole),
            DueDate = input.DueDate,
            EffectivenessCheckRequired = input.EffectivenessCheckRequired,
            EffectivenessDueDate = input.EffectivenessDueDate,
            EffectivenessResult = input.EffectivenessCheckRequired
                ? CapaEffectivenessResult.Pending
                : CapaEffectivenessResult.NotRequired,
            RelatedRegisterEntryIds = input.RelatedRegisterEntryIds?.ToList() ?? [],
            RelatedControlledDocumentIds = input.RelatedControlledDocumentIds?.ToList() ?? [],
            RelatedExternalDocumentIds = input.RelatedExternalDocumentIds?.ToList() ?? [],
            CorrelationId = correlationId,
            CreatedBy = _currentUser.ActorName
        };

        await _actions.CreateAsync(action, ct);

        if (deviation is not null)
        {
            await _deviationService.AttachCapaAsync(deviation.Id, action.Id, ct);
        }

        if (action.QualityEventId is { } linkedEventId)
        {
            await _qualityEventService.AttachCapaAsync(linkedEventId, action.Id, ct);
        }

        return Response<CapaActionModel>.Success(QualityEventWire.ToCapa(action, now), 201, correlationId);
    }

    // ── transitions ───────────────────────────────────────────────────────────

    public async Task<Response<CapaActionModel>> StartAsync(Guid id, string correlationId, CancellationToken ct)
    {
        var (fail, action) = await LoadAsync(id, correlationId, ct);
        if (fail is not null)
        {
            return fail;
        }

        if (action!.ActionStatus is not (CapaActionStatus.Draft or CapaActionStatus.Open))
        {
            return Fail($"A {action.ActionStatus} CAPA action cannot be started.", 409,
                QualityEventReasonCodes.CapaInvalidState, correlationId);
        }

        var now = DateTimeOffset.UtcNow;
        action.ActionStatus = CapaActionStatus.InProgress;
        action.StartedAt = now;
        action.StartedBy = _currentUser.ActorName;
        await PersistAsync(action, now, ct);
        return Ok(action, now, correlationId);
    }

    public async Task<Response<CapaActionModel>> CompleteAsync(
        Guid id, CompleteCapaActionInput input, string correlationId, CancellationToken ct)
    {
        var (fail, action) = await LoadAsync(id, correlationId, ct);
        if (fail is not null)
        {
            return fail;
        }

        if (action!.ActionStatus is not (CapaActionStatus.Open or CapaActionStatus.InProgress))
        {
            return Fail($"A {action.ActionStatus} CAPA action cannot be completed.", 409,
                QualityEventReasonCodes.CapaInvalidState, correlationId);
        }

        if (string.IsNullOrWhiteSpace(input.CompletionEvidenceReference))
        {
            return Fail("Completion evidence is required to complete a CAPA action.", 400,
                QualityEventReasonCodes.CapaCompletionEvidenceRequired, correlationId);
        }

        var now = DateTimeOffset.UtcNow;
        action.CompletionEvidenceReference = input.CompletionEvidenceReference.Trim();
        action.CompletedAt = now;
        action.CompletedBy = _currentUser.ActorName;

        // Where effectiveness must be demonstrated, completion is not the end of the story.
        action.ActionStatus = action.EffectivenessCheckRequired
            ? CapaActionStatus.EffectivenessPending
            : CapaActionStatus.Completed;

        await PersistAsync(action, now, ct);
        return Ok(action, now, correlationId);
    }

    public async Task<Response<CapaActionModel>> RecordEffectivenessAsync(
        Guid id, RecordCapaEffectivenessInput input, string correlationId, CancellationToken ct)
    {
        var (fail, action) = await LoadAsync(id, correlationId, ct);
        if (fail is not null)
        {
            return fail;
        }

        if (action!.ActionStatus is not (CapaActionStatus.EffectivenessPending or CapaActionStatus.Completed))
        {
            return Fail($"Effectiveness can only be recorded on a completed action; this action is {action.ActionStatus}.",
                409, QualityEventReasonCodes.CapaInvalidState, correlationId);
        }

        if (string.IsNullOrWhiteSpace(input.EffectivenessEvidenceReference))
        {
            return Fail("Effectiveness evidence is required.", 400,
                QualityEventReasonCodes.CapaEffectivenessEvidenceRequired, correlationId);
        }

        var result = QualityEventWire.ParseEffectiveness(input.EffectivenessResult);
        if (result is not (CapaEffectivenessResult.Effective or CapaEffectivenessResult.Ineffective))
        {
            return Fail("The effectiveness result must be Effective or Ineffective.", 400,
                QualityEventReasonCodes.ValidationFailed, correlationId);
        }

        var now = DateTimeOffset.UtcNow;
        action.EffectivenessResult = result.Value;
        action.EffectivenessEvidenceReference = input.EffectivenessEvidenceReference.Trim();
        action.EffectivenessSummary = Trim(input.EffectivenessSummary);
        action.EffectivenessRecordedAt = now;
        action.EffectivenessRecordedBy = _currentUser.ActorName;
        action.ActionStatus = result == CapaEffectivenessResult.Effective
            ? CapaActionStatus.Effective
            : CapaActionStatus.Ineffective;

        await PersistAsync(action, now, ct);

        // An ineffective action forces its deviation back to CAPARequired rather than being absorbed.
        if (result == CapaEffectivenessResult.Ineffective && action.DeviationId is { } deviationId)
        {
            await _deviationService.MarkCapaIneffectiveAsync(deviationId, ct);
        }

        return Ok(action, now, correlationId);
    }

    public async Task<Response<CapaActionModel>> CloseAsync(
        Guid id, CloseCapaActionInput input, string correlationId, CancellationToken ct)
    {
        var (fail, action) = await LoadAsync(id, correlationId, ct);
        if (fail is not null)
        {
            return fail;
        }

        // Terminal, not settled: an Effective action is exactly the one we expect to be closing here.
        if (action!.IsTerminal())
        {
            return Fail($"The CAPA action is already {action.ActionStatus}.", 409,
                QualityEventReasonCodes.CapaInvalidState, correlationId);
        }

        var exception = Trim(input.ClosureExceptionJustification);

        // An outstanding effectiveness verdict blocks closure outright.
        if (action.EffectivenessCheckRequired && action.EffectivenessResult == CapaEffectivenessResult.Pending)
        {
            return Fail(
                "This action requires an effectiveness check; record the effectiveness verdict before closing.",
                409, QualityEventReasonCodes.CapaEffectivenessPending, correlationId);
        }

        // An ineffective action can be closed only on a documented exception — never silently as done.
        if (action.EffectivenessResult == CapaEffectivenessResult.Ineffective && exception is null)
        {
            return Fail(
                "This action was found ineffective; closing it requires a documented exception justification, and follow-up action on the deviation.",
                409, QualityEventReasonCodes.CapaIneffectiveRequiresException, correlationId);
        }

        if (action.ActionStatus is CapaActionStatus.Open or CapaActionStatus.InProgress && exception is null)
        {
            return Fail(
                "An incomplete action requires a documented exception justification to close.",
                409, QualityEventReasonCodes.CapaInvalidState, correlationId);
        }

        var now = DateTimeOffset.UtcNow;
        action.ActionStatus = CapaActionStatus.Closed;
        action.ClosureExceptionJustification = exception;
        action.ClosedAt = now;
        action.ClosedBy = _currentUser.ActorName;
        await PersistAsync(action, now, ct);
        return Ok(action, now, correlationId);
    }

    public async Task<Response<CapaActionModel>> CancelAsync(
        Guid id, CancelCapaActionInput input, string correlationId, CancellationToken ct)
    {
        var (fail, action) = await LoadAsync(id, correlationId, ct);
        if (fail is not null)
        {
            return fail;
        }

        if (action!.IsTerminal())
        {
            return Fail($"The CAPA action is already {action.ActionStatus}.", 409,
                QualityEventReasonCodes.CapaInvalidState, correlationId);
        }

        if (string.IsNullOrWhiteSpace(input.Reason))
        {
            return Fail("A cancellation reason is required.", 400, QualityEventReasonCodes.ReasonRequired, correlationId);
        }

        var now = DateTimeOffset.UtcNow;
        action.ActionStatus = CapaActionStatus.Cancelled;
        action.CancellationReason = input.Reason.Trim();
        await PersistAsync(action, now, ct);
        return Ok(action, now, correlationId);
    }

    // ── reads ─────────────────────────────────────────────────────────────────

    public async Task<Response<CapaActionModel>> GetAsync(Guid id, string correlationId, CancellationToken ct)
    {
        var (fail, action) = await LoadAsync(id, correlationId, ct);
        return fail ?? Ok(action!, DateTimeOffset.UtcNow, correlationId);
    }

    public async Task<Response<IReadOnlyList<CapaActionModel>>> ListAsync(string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var now = DateTimeOffset.UtcNow;
        var rows = await _actions.GetAllForTenantAsync(ct);
        return Response<IReadOnlyList<CapaActionModel>>.Success(
            rows.Select(x => QualityEventWire.ToCapa(x, now)).ToList(), correlationId: correlationId);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private async Task PersistAsync(DocumentCAPAAction action, DateTimeOffset now, CancellationToken ct)
    {
        action.UpdatedAt = now;
        action.UpdatedBy = _currentUser.ActorName;
        await _actions.UpdateAsync(action, ct);
    }

    private async Task<(Response<CapaActionModel>? Fail, DocumentCAPAAction? Action)> LoadAsync(
        Guid id, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var action = await _actions.GetByIdAsync(id, ct);
        return action is null
            ? (Fail("CAPA action not found.", 404, QualityEventReasonCodes.CapaNotFound, correlationId), null)
            : (null, action);
    }

    private static Response<CapaActionModel> Ok(DocumentCAPAAction action, DateTimeOffset now, string correlationId) =>
        Response<CapaActionModel>.Success(QualityEventWire.ToCapa(action, now), correlationId: correlationId);

    private static Response<CapaActionModel> Fail(string error, int status, string reason, string correlationId) =>
        Response<CapaActionModel>.Fail(error, status, reason, correlationId);

    private static string? Trim(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}
