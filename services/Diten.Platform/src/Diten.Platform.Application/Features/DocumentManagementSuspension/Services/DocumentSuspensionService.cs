using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.DocumentManagementLifecycle;
using Diten.Platform.Application.Features.DocumentManagementLifecycle.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.DocumentManagementSuspension.Services;

/// <summary>
/// MOD-0029-FU13 — urgent withdrawal / suspension case orchestration (GMG-QMS-SOP-0001 §12.1). Records the SOP chain
/// (report → QA notify → GQD escalation → GQD/independent-QA approval with a communication plan → execution with
/// access-removal, notice and affected-records evidence → close with a deviation/corrective action) and delegates the
/// actual lifecycle change to the FU08 engine — this FU never mutates a status directly and never deletes anything.
///
/// NOT a CAPA/quality-event module and NOT a workflow engine: the deviation and corrective action are captured as
/// REFERENCES (extension points). Access policy is NOT rewritten here — routine use is already denied by the FU08
/// lifecycle (Suspended is not operationally effective).
/// </summary>
public sealed class DocumentSuspensionService
{
    private readonly IDocumentMasterRegisterRepository _register;
    private readonly IDocumentSuspensionCaseRepository _cases;
    private readonly IDocumentPeriodicReviewEscalationRepository _escalations;
    private readonly DocumentLifecycleService _lifecycle;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _currentUser;

    private readonly IControlledCopyWithdrawalPort? _copyWithdrawal;

    public DocumentSuspensionService(
        IDocumentMasterRegisterRepository register,
        IDocumentSuspensionCaseRepository cases,
        IDocumentPeriodicReviewEscalationRepository escalations,
        DocumentLifecycleService lifecycle,
        ITenantContext tenantContext,
        ICurrentUserContext currentUser,
        // MOD-0029-FU17 controlled-copy withdrawal port (optional). Null → no automatic withdrawal plan.
        IControlledCopyWithdrawalPort? copyWithdrawal = null)
    {
        _register = register;
        _cases = cases;
        _escalations = escalations;
        _lifecycle = lifecycle;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
        _copyWithdrawal = copyWithdrawal;
    }

    public async Task<Response<SuspensionCaseModel>> OpenAsync(Guid registerEntryId, OpenSuspensionCaseInput input, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var entry = await _register.GetByIdAsync(registerEntryId, ct);
        if (entry is null)
        {
            return Fail("Register entry not found.", 404, SuspensionReasonCodes.NotFoundNonLeakage, correlationId);
        }

        var trigger = SuspensionWire.ParseTrigger(input.TriggerType);
        if (trigger is null || string.IsNullOrWhiteSpace(input.TriggerDescription))
        {
            return Fail("A valid trigger type and description are required.", 400, SuspensionReasonCodes.ValidationFailed, correlationId);
        }

        // A document already out of use has nothing to suspend.
        if (entry.LifecycleStatus is ControlledDocumentLifecycleStatus.Retired or ControlledDocumentLifecycleStatus.Superseded or ControlledDocumentLifecycleStatus.Suspended)
        {
            return Fail($"A {entry.LifecycleStatus} document cannot be suspended.", 409, SuspensionReasonCodes.NotEligible, correlationId);
        }

        // Idempotent: an already-open case for the entry is returned as-is (product decision).
        var open = await _cases.GetOpenAsync(registerEntryId, ct);
        if (open is not null)
        {
            return Response<SuspensionCaseModel>.Success(SuspensionWire.ToCase(open), correlationId: correlationId);
        }

        // Optional link back to the FU12 escalation that triggered this (validated against the entry's escalations).
        Guid? escalationId = null;
        if (input.SourcePeriodicReviewEscalationId is { } srcId && srcId != Guid.Empty)
        {
            var known = await _escalations.GetByRegisterEntryAsync(registerEntryId, ct);
            if (known.All(x => x.Id != srcId))
            {
                return Fail("The referenced periodic-review escalation was not found for this document.", 404, SuspensionReasonCodes.NotFoundNonLeakage, correlationId);
            }

            escalationId = srcId;
        }

        var history = await _cases.GetByRegisterEntryAsync(registerEntryId, ct);
        var now = DateTimeOffset.UtcNow;
        var suspensionCase = new DocumentSuspensionCase
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantContext.TenantId,
            RegisterEntryId = registerEntryId,
            CaseNumber = history.Count + 1,
            CaseStatus = SuspensionCaseStatus.Opened,
            TriggerType = trigger.Value,
            TriggerDescription = input.TriggerDescription.Trim(),
            ReportedAt = now,
            ReportedBy = _currentUser.ActorName,
            QaNotifiedAt = now, // SOP §12.1: the reporting user notifies QA immediately.
            SourcePeriodicReviewEscalationId = escalationId,
            CorrelationId = correlationId,
            CreatedBy = _currentUser.ActorName
        };
        await _cases.CreateAsync(suspensionCase, ct);
        return Response<SuspensionCaseModel>.Success(SuspensionWire.ToCase(suspensionCase), 201, correlationId);
    }

    public async Task<Response<SuspensionCaseModel>> EscalateAsync(Guid registerEntryId, Guid caseId, EscalateSuspensionCaseInput input, string correlationId, CancellationToken ct)
    {
        var (fail, _, c) = await LoadAsync(registerEntryId, caseId, correlationId, ct);
        if (fail is not null)
        {
            return fail;
        }

        var now = DateTimeOffset.UtcNow;
        c!.CaseStatus = SuspensionCaseStatus.Escalated;
        c.EscalatedToGqdAt = now;              // SOP §12.1: same working day.
        c.DocumentOwnerNotifiedAt = now;
        c.UpdatedAt = now;
        c.UpdatedBy = _currentUser.ActorName;
        await _cases.UpdateAsync(c, ct);
        return Response<SuspensionCaseModel>.Success(SuspensionWire.ToCase(c), correlationId: correlationId);
    }

    public async Task<Response<SuspensionCaseModel>> ApproveAsync(Guid registerEntryId, Guid caseId, ApproveSuspensionInput input, string correlationId, CancellationToken ct)
    {
        var (fail, _, c) = await LoadAsync(registerEntryId, caseId, correlationId, ct);
        if (fail is not null)
        {
            return fail;
        }

        var decision = SuspensionWire.ParseDecision(input.Decision);
        var role = SuspensionWire.ParseRole(input.ApprovedByRole);
        if (decision is null || role is null)
        {
            return Fail("A valid decision and approver role are required.", 400, SuspensionReasonCodes.ValidationFailed, correlationId);
        }

        // SOP §12.1: the GQD or an independent QA delegate approves.
        if (!SuspensionApprovers.IsPermitted(role.Value))
        {
            return Fail($"A suspension must be approved by the GQD or an independent qualified QA delegate, not {role}.", 409, SuspensionReasonCodes.ApproverRoleInvalid, correlationId);
        }

        if (string.IsNullOrWhiteSpace(input.DecisionReason))
        {
            return Fail("A decision reason is required.", 400, SuspensionReasonCodes.ReasonRequired, correlationId);
        }

        if (string.IsNullOrWhiteSpace(input.CommunicationPlanReference))
        {
            return Fail("A communication plan reference is required (SOP §12.1).", 400, SuspensionReasonCodes.CommunicationPlanRequired, correlationId);
        }

        var now = DateTimeOffset.UtcNow;
        c!.CaseStatus = SuspensionCaseStatus.Approved;
        c.Decision = decision.Value;
        c.DecisionReason = input.DecisionReason.Trim();
        c.ApprovedBy = _currentUser.ActorName;
        c.ApprovedByRole = role.Value;
        c.ApprovedAt = now;
        c.CommunicationPlanReference = input.CommunicationPlanReference.Trim();
        c.UpdatedAt = now;
        c.UpdatedBy = _currentUser.ActorName;
        await _cases.UpdateAsync(c, ct);
        return Response<SuspensionCaseModel>.Success(SuspensionWire.ToCase(c), correlationId: correlationId);
    }

    public async Task<Response<SuspensionCaseModel>> RejectAsync(Guid registerEntryId, Guid caseId, RejectSuspensionInput input, string correlationId, CancellationToken ct)
    {
        var (fail, _, c) = await LoadAsync(registerEntryId, caseId, correlationId, ct);
        if (fail is not null)
        {
            return fail;
        }

        if (string.IsNullOrWhiteSpace(input.Reason))
        {
            return Fail("A rejection reason is required.", 400, SuspensionReasonCodes.ReasonRequired, correlationId);
        }

        c!.CaseStatus = SuspensionCaseStatus.Rejected;
        c.DecisionReason = input.Reason.Trim();
        c.UpdatedAt = DateTimeOffset.UtcNow;
        c.UpdatedBy = _currentUser.ActorName;
        await _cases.UpdateAsync(c, ct);
        return Response<SuspensionCaseModel>.Success(SuspensionWire.ToCase(c), correlationId: correlationId);
    }

    public async Task<Response<SuspensionCaseModel>> ExecuteAsync(Guid registerEntryId, Guid caseId, ExecuteSuspensionInput input, string correlationId, CancellationToken ct)
    {
        var (fail, entry, c) = await LoadAsync(registerEntryId, caseId, correlationId, ct);
        if (fail is not null)
        {
            return fail;
        }

        if (c!.CaseStatus != SuspensionCaseStatus.Approved || c.Decision != SuspensionDecision.Suspend)
        {
            return Fail("An approved case with a SUSPEND decision is required before execution.", 409, SuspensionReasonCodes.CaseNotApproved, correlationId);
        }

        // SOP §12.1 execution evidence: remove access, issue the notice, identify affected records/batches/activities.
        if (string.IsNullOrWhiteSpace(input.SuspensionNoticeReference)
            || string.IsNullOrWhiteSpace(input.AccessRemovalEvidenceReference)
            || string.IsNullOrWhiteSpace(input.AffectedRecordsBatchesActivitiesReference))
        {
            return Fail("Suspension notice, access-removal and affected records/batches/activities evidence are all required.", 400, SuspensionReasonCodes.EvidenceRequired, correlationId);
        }

        // The lifecycle change is delegated to the FU08 engine (matrix, reason and transition record all apply).
        var transition = await _lifecycle.TransitionAsync(registerEntryId,
            new TransitionDocumentLifecycleInput(nameof(ControlledDocumentLifecycleStatus.Suspended), c.DecisionReason ?? c.TriggerDescription,
                input.SuspensionNoticeReference, "Suspension executed (MOD-0029-FU13).", null, null, null),
            correlationId, ct);
        if (!transition.IsSuccessful)
        {
            return Fail($"Lifecycle suspension failed: {string.Join("; ", transition.Errors)}", transition.StatusCode,
                SuspensionReasonCodes.LifecycleTransitionFailed, correlationId);
        }

        var now = DateTimeOffset.UtcNow;
        c.CaseStatus = SuspensionCaseStatus.Executed;
        c.SuspensionNoticeReference = input.SuspensionNoticeReference.Trim();
        c.AccessRemovalEvidenceReference = input.AccessRemovalEvidenceReference.Trim();
        c.AffectedRecordsBatchesActivitiesReference = input.AffectedRecordsBatchesActivitiesReference.Trim();
        c.ExecutedAt = now;
        c.ExecutedBy = _currentUser.ActorName;
        c.UpdatedAt = now;
        c.UpdatedBy = _currentUser.ActorName;
        await _cases.UpdateAsync(c, ct);

        // MOD-0029-FU17 seam: raise a controlled-copy withdrawal plan for the now-suspended document, when available.
        if (_copyWithdrawal is not null)
        {
            await _copyWithdrawal.OnDocumentWithdrawnAsync(entry!, ControlledDocumentLifecycleStatus.Suspended, correlationId, ct);
        }

        return Response<SuspensionCaseModel>.Success(SuspensionWire.ToCase(c), correlationId: correlationId);
    }

    public async Task<Response<SuspensionCaseModel>> CloseAsync(Guid registerEntryId, Guid caseId, CloseSuspensionCaseInput input, string correlationId, CancellationToken ct)
    {
        var (fail, _, c) = await LoadAsync(registerEntryId, caseId, correlationId, ct);
        if (fail is not null)
        {
            return fail;
        }

        // SOP §12.1: a deviation / corrective action is opened within 5 working days for a quality-family trigger.
        if (SuspensionWire.RequiresDeviation(c!.TriggerType)
            && string.IsNullOrWhiteSpace(input.DeviationReference)
            && string.IsNullOrWhiteSpace(input.CorrectiveActionReference))
        {
            return Fail("A deviation or corrective action reference is required to close a quality/regulatory/data-integrity case.", 400, SuspensionReasonCodes.DeviationRequired, correlationId);
        }

        var warnings = new List<string>();
        if (string.IsNullOrWhiteSpace(input.ReplacementPlanReference))
        {
            warnings.Add("No replacement plan reference was recorded for this suspension.");
        }

        var now = DateTimeOffset.UtcNow;
        c.CaseStatus = SuspensionCaseStatus.Closed;
        c.DeviationReference = TrimOrNull(input.DeviationReference);
        c.CorrectiveActionReference = TrimOrNull(input.CorrectiveActionReference);
        c.ReplacementPlanReference = TrimOrNull(input.ReplacementPlanReference);
        c.ClosedAt = now;
        c.ClosedBy = _currentUser.ActorName;
        c.UpdatedAt = now;
        c.UpdatedBy = _currentUser.ActorName;
        await _cases.UpdateAsync(c, ct);
        return Response<SuspensionCaseModel>.Success(SuspensionWire.ToCase(c, warnings), correlationId: correlationId);
    }

    public async Task<Response<IReadOnlyList<SuspensionCaseModel>>> ListAsync(Guid registerEntryId, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var entry = await _register.GetByIdAsync(registerEntryId, ct);
        if (entry is null)
        {
            return Response<IReadOnlyList<SuspensionCaseModel>>.Fail("Register entry not found.", 404, SuspensionReasonCodes.NotFoundNonLeakage, correlationId);
        }

        var rows = await _cases.GetByRegisterEntryAsync(registerEntryId, ct);
        return Response<IReadOnlyList<SuspensionCaseModel>>.Success(rows.Select(x => SuspensionWire.ToCase(x)).ToList(), correlationId: correlationId);
    }

    /// <summary>Internal seam used by the temporary-instruction control to open/link a suspension case on expiry.</summary>
    internal async Task<DocumentSuspensionCase> OpenInternalAsync(
        Guid registerEntryId, SuspensionTriggerType trigger, string description, string correlationId, CancellationToken ct)
    {
        var open = await _cases.GetOpenAsync(registerEntryId, ct);
        if (open is not null)
        {
            return open;
        }

        var history = await _cases.GetByRegisterEntryAsync(registerEntryId, ct);
        var now = DateTimeOffset.UtcNow;
        var suspensionCase = new DocumentSuspensionCase
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantContext.TenantId,
            RegisterEntryId = registerEntryId,
            CaseNumber = history.Count + 1,
            CaseStatus = SuspensionCaseStatus.Opened,
            TriggerType = trigger,
            TriggerDescription = description,
            ReportedAt = now,
            ReportedBy = _currentUser.ActorName,
            QaNotifiedAt = now,
            CorrelationId = correlationId,
            CreatedBy = _currentUser.ActorName
        };
        await _cases.CreateAsync(suspensionCase, ct);
        return suspensionCase;
    }

    private async Task<(Response<SuspensionCaseModel>? Fail, DocumentMasterRegisterEntry? Entry, DocumentSuspensionCase? Case)> LoadAsync(
        Guid registerEntryId, Guid caseId, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var entry = await _register.GetByIdAsync(registerEntryId, ct);
        if (entry is null)
        {
            return (Fail("Register entry not found.", 404, SuspensionReasonCodes.NotFoundNonLeakage, correlationId), null, null);
        }

        var c = await _cases.GetByIdAsync(caseId, ct);
        if (c is null || c.RegisterEntryId != registerEntryId)
        {
            return (Fail("Suspension case not found.", 404, SuspensionReasonCodes.CaseNotFound, correlationId), null, null);
        }

        return (null, entry, c);
    }

    private static Response<SuspensionCaseModel> Fail(string error, int status, string reason, string correlationId) =>
        Response<SuspensionCaseModel>.Fail(error, status, reason, correlationId);

    private static string? TrimOrNull(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}
