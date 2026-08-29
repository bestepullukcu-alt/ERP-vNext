using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;
using Microsoft.Extensions.Options;

namespace Diten.Platform.Application.Features.DocumentManagementLifecycle.Services;

/// <summary>
/// MOD-0029-FU08 — controlled document lifecycle status engine (GMG-QMS-SOP-0001 §6.2). Drives
/// <c>DocumentMasterRegisterEntry.LifecycleStatus</c> through a structurally validated transition matrix, records a
/// permanent transition ledger row, and enforces the single-effective / supersession rule. UID/Code stay immutable
/// (only the FU07 allocation engine sets them). This FU implements NO approval workflow (FU09) and NO non-waivable
/// release-gate engine (FU10) — MarkEffective consults the FU06/FU10 extension-point fields but does not compute
/// gates. No hard delete.
/// </summary>
public sealed class DocumentLifecycleService
{
    private readonly IDocumentMasterRegisterRepository _register;
    private readonly IDocumentLifecycleTransitionRecordRepository _transitions;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _currentUser;
    private readonly DocumentLifecycleOptions _options;
    private readonly IApprovedPendingEffectiveGate? _approvalGate;
    private readonly IReleaseGateEvaluationPort? _releaseGate;

    public DocumentLifecycleService(
        IDocumentMasterRegisterRepository register,
        IDocumentLifecycleTransitionRecordRepository transitions,
        ITenantContext tenantContext,
        ICurrentUserContext currentUser,
        IOptions<DocumentLifecycleOptions> options,
        // MOD-0029-FU09 approval gate (optional port). Null → no approval gating (backward compatible).
        IApprovedPendingEffectiveGate? approvalGate = null,
        // MOD-0029-FU10 release gate (optional port). Null → no release gating (backward compatible).
        IReleaseGateEvaluationPort? releaseGate = null)
    {
        _register = register;
        _transitions = transitions;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
        _options = options.Value;
        _approvalGate = approvalGate;
        _releaseGate = releaseGate;
    }

    public async Task<Response<LifecycleStateModel>> GetStateAsync(Guid registerEntryId, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var entry = await _register.GetByIdAsync(registerEntryId, ct);
        if (entry is null)
        {
            return Fail("Register entry not found.", 404, LifecycleReasonCodes.NotFoundNonLeakage, correlationId);
        }

        var warnings = new List<string>();
        var state = LifecycleWire.ToState(entry, warnings);

        if (entry.LifecycleStatus == ControlledDocumentLifecycleStatus.InReview)
        {
            var approvalBlock = _approvalGate is null
                ? "Approval gate is unavailable; the document cannot enter Approved-pending-effective."
                : await _approvalGate.EvaluateAsync(entry, ct);
            if (approvalBlock is not null)
            {
                warnings.Add(approvalBlock);
                state = state with { CanMarkApprovedPendingEffective = false };
            }
        }

        if (entry.LifecycleStatus == ControlledDocumentLifecycleStatus.ApprovedPendingEffective
            && !string.Equals(entry.ApprovalEvidenceStatus, "Complete", StringComparison.OrdinalIgnoreCase))
        {
            warnings.Add("Approval evidence must be Complete before the document can become Effective.");
            state = state with { CanMarkEffective = false };
        }

        state = state with { Warnings = warnings };
        return Response<LifecycleStateModel>.Success(state, correlationId: correlationId);
    }

    public async Task<Response<IReadOnlyList<LifecycleTransitionRecordModel>>> GetTransitionsAsync(Guid registerEntryId, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var entry = await _register.GetByIdAsync(registerEntryId, ct);
        if (entry is null)
        {
            return Response<IReadOnlyList<LifecycleTransitionRecordModel>>.Fail("Register entry not found.", 404, LifecycleReasonCodes.NotFoundNonLeakage, correlationId);
        }

        var rows = await _transitions.GetByRegisterEntryAsync(registerEntryId, ct);
        return Response<IReadOnlyList<LifecycleTransitionRecordModel>>.Success(rows.Select(LifecycleWire.ToRecord).ToList(), correlationId: correlationId);
    }

    public async Task<Response<LifecycleStateModel>> TransitionAsync(Guid registerEntryId, TransitionDocumentLifecycleInput input, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);

        var target = LifecycleWire.ParseStatus(input.TargetStatus);
        if (target is null)
        {
            return Fail("A valid target status is required.", 400, LifecycleReasonCodes.ValidationFailed, correlationId);
        }

        var entry = await _register.GetByIdAsync(registerEntryId, ct);
        if (entry is null)
        {
            return Fail("Register entry not found.", 404, LifecycleReasonCodes.NotFoundNonLeakage, correlationId);
        }

        if (input.ExpectedVersion is { } expected && expected != entry.Version)
        {
            return Fail("The register entry was modified by another operation; reload and retry.", 409, LifecycleReasonCodes.StaleVersion, correlationId);
        }

        var from = entry.LifecycleStatus;
        if (!from.CanTransition(target.Value))
        {
            return Fail($"Transition from {from} to {target} is not permitted.", 409, LifecycleReasonCodes.InvalidTransition, correlationId);
        }

        // Reason is required for the states that stop or end use.
        var reason = TrimOrNull(input.Reason);
        if (RequiresReason(target.Value) && reason is null)
        {
            return Fail($"A reason is required to transition to {target}.", 400, LifecycleReasonCodes.ReasonRequired, correlationId);
        }

        var warnings = new List<string>();

        // MOD-0029-FU09 seam: when an approval-gate adapter is present it may block InReview → ApprovedPendingEffective
        // (only when the approval-required policy is switched on; otherwise the adapter allows it).
        if (target == ControlledDocumentLifecycleStatus.ApprovedPendingEffective
            && from == ControlledDocumentLifecycleStatus.InReview)
        {
            if (_approvalGate is null)
            {
                return Fail(
                    "Approval gate is unavailable; the document cannot enter Approved-pending-effective.",
                    409,
                    LifecycleReasonCodes.ApprovalIncomplete,
                    correlationId);
            }

            var block = await _approvalGate.EvaluateAsync(entry, ct);
            if (block is not null)
            {
                return Fail(block, 409, LifecycleReasonCodes.ApprovalIncomplete, correlationId);
            }
        }

        // MarkEffective (from ApprovedPendingEffective) carries the full go-live guards. A revert from UnderRevision
        // to Effective re-affirms the existing effective version and skips the date guard.
        if (target == ControlledDocumentLifecycleStatus.Effective && from == ControlledDocumentLifecycleStatus.ApprovedPendingEffective)
        {
            var guard = await ApplyMarkEffectiveGuardsAsync(entry, input, warnings, correlationId, ct);
            if (guard is not null)
            {
                return guard;
            }
        }

        // Supersession: superseding the previous effective entry (SOP §6.2). Applies to MarkEffective and to an
        // explicit UnderRevision → Superseded on the outgoing entry with a named replacement.
        if (input.RelatedReplacementRegisterEntryId is { } replacementId && replacementId != Guid.Empty)
        {
            var supersede = await SupersedePreviousAsync(entry, replacementId, correlationId, ct);
            if (supersede is not null)
            {
                return supersede;
            }
        }

        var fromStatus = entry.LifecycleStatus;
        entry.LifecycleStatus = target.Value;
        entry.StatusReason = reason;
        entry.LastTransitionAt = DateTimeOffset.UtcNow;
        entry.LastTransitionBy = _currentUser.ActorName;
        entry.Version += 1;
        entry.UpdatedAt = DateTimeOffset.UtcNow;
        entry.UpdatedBy = _currentUser.ActorName;
        await _register.UpdateAsync(entry, ct);

        await RecordTransitionAsync(entry.Id, fromStatus, target.Value, reason, input.EvidenceReference, input.Comment,
            target == ControlledDocumentLifecycleStatus.Effective ? entry.EffectiveDate : null,
            input.RelatedReplacementRegisterEntryId, correlationId, ct);

        return Response<LifecycleStateModel>.Success(LifecycleWire.ToState(entry, warnings), correlationId: correlationId);
    }

    // ── MarkEffective guards (structural only — release gate/approval engines are FU09/FU10) ────────────

    private async Task<Response<LifecycleStateModel>?> ApplyMarkEffectiveGuardsAsync(
        DocumentMasterRegisterEntry entry, TransitionDocumentLifecycleInput input, List<string> warnings, string correlationId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(entry.PermanentUid) || string.IsNullOrWhiteSpace(entry.DocumentCode))
        {
            return Fail("A Permanent UID and Document Code are required before a document can become Effective.", 409, LifecycleReasonCodes.MissingIdentifier, correlationId);
        }

        var effectiveDate = input.EffectiveDate ?? DateTimeOffset.UtcNow;
        if (input.EffectiveDate is { } supplied && supplied.UtcDateTime.Date < DateTimeOffset.UtcNow.UtcDateTime.Date)
        {
            return Fail("The effective date cannot be retroactive (SOP §4 — effective date shall not be retroactive).", 400, LifecycleReasonCodes.RetroactiveEffectiveDate, correlationId);
        }

        // FU10 extension points: a stored Blocked gate / missing approval evidence blocks effective release.
        if (string.Equals(entry.LastReleaseGateEvaluationStatus, "Blocked", StringComparison.OrdinalIgnoreCase))
        {
            return Fail("The last release-gate evaluation is Blocked; the document cannot become Effective.", 409, LifecycleReasonCodes.ReleaseGateBlocked, correlationId);
        }

        if (!string.Equals(entry.ApprovalEvidenceStatus, "Complete", StringComparison.OrdinalIgnoreCase))
        {
            return Fail("Approval evidence must be Complete before the document can become Effective.", 409, LifecycleReasonCodes.ApprovalEvidenceMissing, correlationId);
        }

        // Single-effective rule (SOP §6.2): no OTHER entry with the same Permanent UID may already be Effective,
        // unless it is the named replacement being superseded in this same transition.
        var effectives = await _register.ListAsync(new MasterRegisterListFilter(LifecycleStatus: ControlledDocumentLifecycleStatus.Effective), ct);
        var conflict = effectives.FirstOrDefault(x =>
            x.Id != entry.Id && x.PermanentUid == entry.PermanentUid && x.Id != input.RelatedReplacementRegisterEntryId);
        if (conflict is not null)
        {
            return Fail("Another Effective register entry already exists for this Permanent UID.", 409, LifecycleReasonCodes.DuplicateEffective, correlationId);
        }

        // MOD-0029-FU10 non-waivable release gates (SOP §19/§21). When a gate-engine adapter is present and the entry
        // is subject to hard gating (policy on, or entry flagged, or Critical), evaluate the six gates LIVE and block
        // unless the evaluation is Complete. With no adapter (e.g. FU08 unit fixtures) the prior warning behaviour
        // stands — backward compatible.
        if (_releaseGate is not null)
        {
            var block = await _releaseGate.EvaluateForEffectiveAsync(entry, ct);
            if (block is not null)
            {
                return Fail(block, 409, LifecycleReasonCodes.ReleaseGateIncomplete, correlationId);
            }
        }
        else
        {
            var gateEvaluated = !string.IsNullOrWhiteSpace(entry.LastReleaseGateEvaluationStatus);
            if (!gateEvaluated)
            {
                if (_options.RequireReleaseGateForEffective && entry.RequiresReleaseGateEvaluation)
                {
                    return Fail("A passing release-gate evaluation is required before Effective (policy enabled).", 409, LifecycleReasonCodes.ReleaseGateBlocked, correlationId);
                }

                if (entry.IsControlledDocument || entry.Criticality == DocumentCriticality.Critical)
                {
                    warnings.Add("Release gate not yet evaluated (SOP §19 non-waivable gates — FU10 pending).");
                }
            }
        }

        if (string.IsNullOrWhiteSpace(input.EvidenceReference))
        {
            warnings.Add("No approval/release evidence reference supplied for the effective release.");
        }

        entry.EffectiveDate = effectiveDate;
        return null;
    }

    private async Task<Response<LifecycleStateModel>?> SupersedePreviousAsync(DocumentMasterRegisterEntry newEntry, Guid replacementId, string correlationId, CancellationToken ct)
    {
        var previous = await _register.GetByIdAsync(replacementId, ct);
        if (previous is null)
        {
            return Fail("The named replacement register entry was not found.", 404, LifecycleReasonCodes.NotFoundNonLeakage, correlationId);
        }

        // Only supersede an entry that is actually in force; otherwise leave it untouched (idempotent, non-destructive).
        if (previous.LifecycleStatus.IsOperationallyEffective())
        {
            var previousFrom = previous.LifecycleStatus;
            previous.LifecycleStatus = ControlledDocumentLifecycleStatus.Superseded;
            previous.SupersededByRegisterEntryId = newEntry.Id;
            previous.StatusReason = "Superseded by a newer effective version.";
            previous.LastTransitionAt = DateTimeOffset.UtcNow;
            previous.LastTransitionBy = _currentUser.ActorName;
            previous.Version += 1;
            previous.UpdatedAt = DateTimeOffset.UtcNow;
            previous.UpdatedBy = _currentUser.ActorName;
            await _register.UpdateAsync(previous, ct);

            await RecordTransitionAsync(previous.Id, previousFrom, ControlledDocumentLifecycleStatus.Superseded,
                "Superseded by a newer effective version.", evidenceReference: null, comment: null,
                effectiveDate: null, relatedReplacement: newEntry.Id, correlationId, ct);

            newEntry.SupersedesRegisterEntryId = previous.Id;
        }

        return null;
    }

    private Task RecordTransitionAsync(
        Guid registerEntryId, ControlledDocumentLifecycleStatus from, ControlledDocumentLifecycleStatus to,
        string? reason, string? evidenceReference, string? comment, DateTimeOffset? effectiveDate,
        Guid? relatedReplacement, string correlationId, CancellationToken ct) =>
        _transitions.CreateAsync(new DocumentLifecycleTransitionRecord
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantContext.TenantId,
            RegisterEntryId = registerEntryId,
            FromStatus = from,
            ToStatus = to,
            TransitionReason = reason,
            EvidenceReference = TrimOrNull(evidenceReference),
            Comment = TrimOrNull(comment),
            EffectiveDate = effectiveDate,
            RelatedReplacementRegisterEntryId = relatedReplacement == Guid.Empty ? null : relatedReplacement,
            PerformedAt = DateTimeOffset.UtcNow,
            PerformedBy = _currentUser.ActorName,
            CorrelationId = correlationId,
            CreatedBy = _currentUser.ActorName
        }, ct);

    private static bool RequiresReason(ControlledDocumentLifecycleStatus target) =>
        target is ControlledDocumentLifecycleStatus.Suspended
            or ControlledDocumentLifecycleStatus.Retired
            or ControlledDocumentLifecycleStatus.Superseded;

    private static Response<LifecycleStateModel> Fail(string error, int status, string reason, string correlationId) =>
        Response<LifecycleStateModel>.Fail(error, status, reason, correlationId);

    private static string? TrimOrNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
