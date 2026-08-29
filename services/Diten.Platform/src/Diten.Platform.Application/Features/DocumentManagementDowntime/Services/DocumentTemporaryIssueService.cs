using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.DocumentManagementDowntime.Services;

/// <summary>
/// MOD-0029-FU20 — temporary controlled issue lifecycle (GMG-QMS-SOP-0001 §11.3): request → approve (outside
/// normal environment) → issue copies → reconcile within 3 working days.
///
/// SOP controls enforced here:
/// • An issue only exists under an open downtime event — the downtime log always precedes the issue.
/// • Only an operationally effective document (Effective / UnderRevision) can be issued; Suspended, Retired and
///   Superseded documents are refused outright.
/// • An UNAPPROVED repository cannot be the source of a controlled copy: issuing from it would create an
///   uncontrolled copy, so the request is blocked (no emergency override exists in this FU — deliberately).
/// • Approval requires a permitted role (GQD / GQD deputy / QA Documentation / IT-CSV / Local QA), a stated
///   mechanism (wet signature or a qualified/separate mechanism) and an evidence reference. FU20 validates NO
///   signature — the mechanism is the approver's statement, never a platform e-signature claim.
/// • Issuing creates FU17 <see cref="DocumentControlledCopy"/> rows of type TemporaryControlledIssue, one per
///   copy, so the temporary copies are tracked by the same reconciliation machinery as every other copy.
/// • The 3-working-day reconciliation clock runs from the LATER of issue and restore; reconciliation needs
///   evidence; a late reconciliation additionally needs a deviation reference.
///
/// Nothing here is hard-deleted, no FU17 code is rewritten (only its repository contract is consumed), and no
/// CAPA / workflow / e-signature machinery is implemented.
/// </summary>
public sealed class DocumentTemporaryIssueService
{
    private readonly IDocumentRepositoryDowntimeEventRepository _events;
    private readonly IDocumentTemporaryControlledIssueRepository _issues;
    private readonly IDocumentMasterRegisterRepository _register;
    private readonly IDocumentControlledCopyRepository _copies;
    private readonly IDocumentRepositoryAssessmentRepository _assessments;
    private readonly DocumentRepositoryDowntimeService _downtimeService;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _currentUser;

    public DocumentTemporaryIssueService(
        IDocumentRepositoryDowntimeEventRepository events,
        IDocumentTemporaryControlledIssueRepository issues,
        IDocumentMasterRegisterRepository register,
        IDocumentControlledCopyRepository copies,
        IDocumentRepositoryAssessmentRepository assessments,
        DocumentRepositoryDowntimeService downtimeService,
        ITenantContext tenantContext,
        ICurrentUserContext currentUser)
    {
        _events = events;
        _issues = issues;
        _register = register;
        _copies = copies;
        _assessments = assessments;
        _downtimeService = downtimeService;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
    }

    // ── request ───────────────────────────────────────────────────────────────

    public async Task<Response<TemporaryControlledIssueModel>> RequestAsync(
        Guid downtimeEventId, RequestTemporaryIssueInput input, string correlationId, CancellationToken ct)
    {
        var tenantId = TenantGuard.RequireTenant(_tenantContext);
        var downtimeEvent = await _events.GetByIdAsync(downtimeEventId, ct);
        if (downtimeEvent is null)
        {
            return Fail("Downtime event not found.", 404, DowntimeReasonCodes.DowntimeNotFound, correlationId);
        }

        if (!downtimeEvent.AcceptsTemporaryIssues())
        {
            return Fail($"A {downtimeEvent.DowntimeStatus} downtime event does not accept temporary issues.", 409,
                DowntimeReasonCodes.DowntimeNotAcceptingIssues, correlationId);
        }

        var entry = await _register.GetByIdAsync(input.RegisterEntryId, ct);
        if (entry is null)
        {
            return Fail("Register entry not found.", 404, DowntimeReasonCodes.RegisterEntryNotFound, correlationId);
        }

        // SOP §11.3 — only an operationally effective document may be issued outside the normal environment.
        if (entry.LifecycleStatus is not (ControlledDocumentLifecycleStatus.Effective or ControlledDocumentLifecycleStatus.UnderRevision))
        {
            return Fail(
                $"A {entry.LifecycleStatus} document cannot be issued as a temporary controlled copy; only Effective or UnderRevision documents qualify.",
                409, DowntimeReasonCodes.DocumentNotOperational, correlationId);
        }

        // PRODUCT DECISION: an unapproved repository BLOCKS the issue rather than warning. Issuing a controlled
        // copy from an unapproved repository is exactly how an uncontrolled copy is born, and no emergency
        // override mechanism exists in this FU (it would require evidence + e-signature machinery that is out of
        // scope). FU16 approval must come first.
        if (downtimeEvent.RepositoryAssessmentId is { } assessmentId)
        {
            var assessment = await _assessments.GetByIdAsync(assessmentId, ct);
            if (assessment?.RepositoryType == RepositoryType.UnapprovedRepository)
            {
                return Fail(
                    "The affected repository is assessed as UNAPPROVED; a temporary controlled issue from it would create an uncontrolled copy. Approve the repository assessment first.",
                    409, DowntimeReasonCodes.UnapprovedRepositoryBlocked, correlationId);
            }
        }

        var now = DateTimeOffset.UtcNow;
        var issue = new DocumentTemporaryControlledIssue
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            DowntimeEventId = downtimeEventId,
            RegisterEntryId = entry.Id,
            ControlledDocumentId = input.ControlledDocumentId ?? entry.ControlledDocumentId,
            ControlledDocumentVersionId = input.ControlledDocumentVersionId,
            IssueNumber = $"TCI-{now:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpperInvariant()}",
            IssueStatus = TemporaryIssueStatus.Requested,
            IssueReason = Trim(input.IssueReason),
            RequestedAt = now,
            RequestedBy = _currentUser.ActorName,
            RecipientUserIds = input.RecipientUserIds?.ToList() ?? [],
            RecipientRole = Trim(input.RecipientRole),
            RecipientDepartment = Trim(input.RecipientDepartment),
            CorrelationId = correlationId,
            CreatedBy = _currentUser.ActorName
        };

        await _issues.CreateAsync(issue, ct);
        return Response<TemporaryControlledIssueModel>.Success(DowntimeWire.ToIssue(issue, now), 201, correlationId);
    }

    // ── approve ───────────────────────────────────────────────────────────────

    public async Task<Response<TemporaryControlledIssueModel>> ApproveAsync(
        Guid downtimeEventId, Guid issueId, ApproveTemporaryIssueInput input, string correlationId, CancellationToken ct)
    {
        var (fail, _, issue) = await LoadAsync(downtimeEventId, issueId, correlationId, ct);
        if (fail is not null)
        {
            return fail;
        }

        if (issue!.IssueStatus != TemporaryIssueStatus.Requested)
        {
            return Fail($"A {issue.IssueStatus} issue cannot be approved.", 409,
                DowntimeReasonCodes.IssueInvalidState, correlationId);
        }

        var role = DowntimeWire.ParseApproverRole(input.ApprovedByRole);
        if (role is null || !TemporaryIssueApprovers.IsPermitted(role.Value))
        {
            return Fail(
                "An outside-normal-environment issue must be approved by GQD, QA Documentation, IT/CSV or Local QA (SOP §11.3).",
                409, DowntimeReasonCodes.ApproverRoleInvalid, correlationId);
        }

        var mechanism = DowntimeWire.ParseMechanism(input.ApprovalMechanism);
        if (mechanism is null)
        {
            return Fail("A valid approval mechanism (wet signature / qualified electronic / separate mechanism) is required.",
                400, DowntimeReasonCodes.ApprovalMechanismRequired, correlationId);
        }

        if (string.IsNullOrWhiteSpace(input.ApprovalEvidenceReference))
        {
            return Fail("Approval evidence is required for an outside-normal-environment issue.", 400,
                DowntimeReasonCodes.ApprovalEvidenceRequired, correlationId);
        }

        var now = DateTimeOffset.UtcNow;
        issue.IssueStatus = TemporaryIssueStatus.Approved;
        issue.ApprovedBy = _currentUser.ActorName;
        issue.ApprovedByUserId = input.ApprovedByUserId ?? _currentUser.UserId;
        issue.ApprovedByRole = role.Value.ToString();
        issue.ApprovedAt = now;
        issue.ApprovalMechanism = mechanism;
        issue.ApprovalEvidenceReference = input.ApprovalEvidenceReference.Trim();
        await PersistAsync(issue, now, ct);
        return Response<TemporaryControlledIssueModel>.Success(DowntimeWire.ToIssue(issue, now), correlationId: correlationId);
    }

    // ── issue copies ──────────────────────────────────────────────────────────

    public async Task<Response<TemporaryControlledIssueModel>> IssueCopiesAsync(
        Guid downtimeEventId, Guid issueId, IssueTemporaryControlledCopyInput input, string correlationId, CancellationToken ct)
    {
        var (fail, downtimeEventOrNull, issueOrNull) = await LoadAsync(downtimeEventId, issueId, correlationId, ct);
        if (fail is not null)
        {
            return fail;
        }

        var downtimeEvent = downtimeEventOrNull!;
        var issue = issueOrNull!;

        if (issue.IssueStatus != TemporaryIssueStatus.Approved)
        {
            return Fail("The temporary issue must be approved before copies can be issued.", 409,
                DowntimeReasonCodes.IssueNotApproved, correlationId);
        }

        if (input.IssuedCopyCount <= 0)
        {
            return Fail("The issued copy count must be greater than zero.", 400,
                DowntimeReasonCodes.CopyCountInvalid, correlationId);
        }

        if (string.IsNullOrWhiteSpace(input.TemporaryLocationDescription))
        {
            return Fail("A temporary location description is required.", 400,
                DowntimeReasonCodes.TemporaryLocationRequired, correlationId);
        }

        var now = DateTimeOffset.UtcNow;

        // FU17 integration: each temporary copy becomes a real DocumentControlledCopy so the standard copy
        // reconciliation machinery sees it. Copy numbering continues the entry's existing sequence.
        var existingCopies = await _copies.GetByRegisterEntryAsync(issue.RegisterEntryId, ct);
        var nextCopyNumber = existingCopies.Count == 0 ? 1 : existingCopies.Max(c => c.CopyNumber) + 1;
        var locationType = DowntimeWire.ParseLocationType(input.LocationType);

        for (var i = 0; i < input.IssuedCopyCount; i++)
        {
            var copy = new DocumentControlledCopy
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantContext.TenantId,
                RegisterEntryId = issue.RegisterEntryId,
                ControlledDocumentId = issue.ControlledDocumentId,
                ControlledDocumentVersionId = issue.ControlledDocumentVersionId,
                CopyNumber = nextCopyNumber + i,
                CopyType = ControlledCopyType.TemporaryControlledIssue,
                CopyStatus = ControlledCopyStatus.Active,
                LocationType = locationType,
                LocationDescription = input.TemporaryLocationDescription.Trim(),
                HolderRole = issue.RecipientRole,
                HolderDepartment = issue.RecipientDepartment,
                RepositoryAssessmentId = downtimeEvent.RepositoryAssessmentId,
                IssuedAt = now,
                IssuedBy = _currentUser.ActorName,
                Comment = $"Temporary controlled issue {issue.IssueNumber} during downtime {downtimeEvent.DowntimeNumber}.",
                CorrelationId = correlationId,
                CreatedBy = _currentUser.ActorName
            };
            await _copies.CreateAsync(copy, ct);
            issue.RelatedControlledCopyIds.Add(copy.Id);
        }

        issue.IssueStatus = downtimeEvent.RestoredAt is null
            ? TemporaryIssueStatus.Issued
            : TemporaryIssueStatus.ReconciliationDue;
        issue.IssuedAt = now;
        issue.IssuedBy = _currentUser.ActorName;
        issue.IssuedCopyCount = input.IssuedCopyCount;
        issue.TemporaryLocationDescription = input.TemporaryLocationDescription.Trim();

        // SOP §11.3 — the 3-working-day clock runs from the later of issue and restore.
        issue.ReconciliationDueDate = DowntimeScheduleCalculator.ReconciliationDueDate(now, downtimeEvent.RestoredAt);

        await PersistAsync(issue, now, ct);
        return Response<TemporaryControlledIssueModel>.Success(DowntimeWire.ToIssue(issue, now), correlationId: correlationId);
    }

    // ── reconcile ─────────────────────────────────────────────────────────────

    public async Task<Response<TemporaryControlledIssueModel>> ReconcileAsync(
        Guid downtimeEventId, Guid issueId, ReconcileTemporaryIssueInput input, string correlationId, CancellationToken ct)
    {
        var (fail, downtimeEvent, issue) = await LoadAsync(downtimeEventId, issueId, correlationId, ct);
        if (fail is not null)
        {
            return fail;
        }

        if (issue!.IssueStatus is not (TemporaryIssueStatus.Issued or TemporaryIssueStatus.ReconciliationDue or TemporaryIssueStatus.Overdue))
        {
            return Fail($"A {issue.IssueStatus} issue cannot be reconciled.", 409,
                DowntimeReasonCodes.IssueInvalidState, correlationId);
        }

        if (string.IsNullOrWhiteSpace(input.ReconciliationEvidenceReference))
        {
            return Fail("Reconciliation evidence is required.", 400,
                DowntimeReasonCodes.ReconciliationEvidenceRequired, correlationId);
        }

        var now = DateTimeOffset.UtcNow;
        var late = issue.ReconciliationDueDate is { } due && now > due;

        // SOP §11.3 — a missed 3-working-day window is a deviation; late reconciliation demands its reference.
        if (late && string.IsNullOrWhiteSpace(input.DeviationReference) && string.IsNullOrWhiteSpace(issue.DeviationReference))
        {
            return Fail(
                "The 3-working-day reconciliation window has passed; a deviation reference is required to reconcile late.",
                409, DowntimeReasonCodes.DeviationReferenceRequired, correlationId);
        }

        issue.IssueStatus = TemporaryIssueStatus.Reconciled;
        issue.ReconciledAt = now;
        issue.ReconciledBy = _currentUser.ActorName;
        issue.ReconciliationEvidenceReference = input.ReconciliationEvidenceReference.Trim();
        issue.DeviationReference = Trim(input.DeviationReference) ?? issue.DeviationReference;
        issue.CorrectiveActionReference = Trim(input.CorrectiveActionReference) ?? issue.CorrectiveActionReference;
        issue.MissingReconciliationReason = Trim(input.MissingReconciliationReason) ?? issue.MissingReconciliationReason;

        // FU17 integration: the temporary copies are settled with the same evidence — Reconciled by default,
        // Withdrawn when the caller states the physical copies were pulled back instead.
        foreach (var copyId in issue.RelatedControlledCopyIds)
        {
            var copy = await _copies.GetByIdAsync(copyId, ct);
            if (copy is null)
            {
                continue;
            }

            if (input.WithdrawCopiesInsteadOfReconcile)
            {
                copy.CopyStatus = ControlledCopyStatus.Withdrawn;
                copy.WithdrawnAt = now;
                copy.WithdrawnBy = _currentUser.ActorName;
                copy.WithdrawalEvidenceReference = issue.ReconciliationEvidenceReference;
            }
            else
            {
                copy.CopyStatus = ControlledCopyStatus.Reconciled;
                copy.ReconciledAt = now;
                copy.ReconciledBy = _currentUser.ActorName;
                copy.ReconciliationEvidenceReference = issue.ReconciliationEvidenceReference;
            }

            copy.UpdatedAt = now;
            copy.UpdatedBy = _currentUser.ActorName;
            await _copies.UpdateAsync(copy, ct);
        }

        await PersistAsync(issue, now, ct);
        await SettleDowntimeStatusAsync(downtimeEvent!, now, ct);
        return Response<TemporaryControlledIssueModel>.Success(DowntimeWire.ToIssue(issue, now), correlationId: correlationId);
    }

    // ── overdue evaluation ────────────────────────────────────────────────────

    /// <summary>
    /// Explicitly invoked (no scheduler). Marks the issue Overdue once its window has passed and raises the
    /// idempotent ReconciliationOverdue (QA Documentation) and MissingReconciliation (GQD) escalations.
    /// </summary>
    public async Task<Response<TemporaryControlledIssueModel>> EvaluateOverdueAsync(
        Guid downtimeEventId, Guid issueId, string correlationId, CancellationToken ct)
    {
        var (fail, downtimeEvent, issue) = await LoadAsync(downtimeEventId, issueId, correlationId, ct);
        if (fail is not null)
        {
            return fail;
        }

        var now = DateTimeOffset.UtcNow;
        if (issue!.IssueStatus is TemporaryIssueStatus.Issued or TemporaryIssueStatus.ReconciliationDue
            && issue.ReconciliationDueDate is { } due && now > due)
        {
            issue.IssueStatus = TemporaryIssueStatus.Overdue;
            await PersistAsync(issue, now, ct);

            await _downtimeService.EnsureEscalationAsync(downtimeEvent!, DowntimeEscalationType.ReconciliationOverdue,
                DowntimeEscalationRole.QADocumentation, DowntimeEscalationSeverity.Major,
                $"Temporary controlled issue {issue.IssueNumber} has passed its 3-working-day reconciliation due date ({due:yyyy-MM-dd}).",
                issue.Id, correlationId, ct);

            await _downtimeService.EnsureEscalationAsync(downtimeEvent!, DowntimeEscalationType.MissingReconciliation,
                DowntimeEscalationRole.GQD, DowntimeEscalationSeverity.Critical,
                $"Temporary controlled issue {issue.IssueNumber} is unreconciled past its due date; a deviation must be raised (reference required to reconcile).",
                issue.Id, correlationId, ct);
        }

        return Response<TemporaryControlledIssueModel>.Success(DowntimeWire.ToIssue(issue, now), correlationId: correlationId);
    }

    // ── cancel ────────────────────────────────────────────────────────────────

    public async Task<Response<TemporaryControlledIssueModel>> CancelAsync(
        Guid downtimeEventId, Guid issueId, CancelTemporaryIssueInput input, string correlationId, CancellationToken ct)
    {
        var (fail, downtimeEvent, issue) = await LoadAsync(downtimeEventId, issueId, correlationId, ct);
        if (fail is not null)
        {
            return fail;
        }

        if (string.IsNullOrWhiteSpace(input.Reason))
        {
            return Fail("A cancellation reason is required.", 400, DowntimeReasonCodes.ReasonRequired, correlationId);
        }

        // Copies already in the field cannot be waved away by cancelling — they must be reconciled/withdrawn.
        if (issue!.RelatedControlledCopyIds.Count > 0 && issue.IssueStatus is not TemporaryIssueStatus.Requested and not TemporaryIssueStatus.Approved)
        {
            return Fail("Copies have already been issued; reconcile (or withdraw) them instead of cancelling.", 409,
                DowntimeReasonCodes.IssueInvalidState, correlationId);
        }

        var now = DateTimeOffset.UtcNow;
        issue.IssueStatus = TemporaryIssueStatus.Cancelled;
        issue.CancellationReason = input.Reason.Trim();
        await PersistAsync(issue, now, ct);
        await SettleDowntimeStatusAsync(downtimeEvent!, now, ct);
        return Response<TemporaryControlledIssueModel>.Success(DowntimeWire.ToIssue(issue, now), correlationId: correlationId);
    }

    public async Task<Response<IReadOnlyList<TemporaryControlledIssueModel>>> GetByDowntimeEventAsync(
        Guid downtimeEventId, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var downtimeEvent = await _events.GetByIdAsync(downtimeEventId, ct);
        if (downtimeEvent is null)
        {
            return Response<IReadOnlyList<TemporaryControlledIssueModel>>.Fail(
                "Downtime event not found.", 404, DowntimeReasonCodes.DowntimeNotFound, correlationId);
        }

        var now = DateTimeOffset.UtcNow;
        var rows = await _issues.GetByDowntimeEventAsync(downtimeEventId, ct);
        return Response<IReadOnlyList<TemporaryControlledIssueModel>>.Success(
            rows.Select(x => DowntimeWire.ToIssue(x, now)).ToList(), correlationId: correlationId);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    /// <summary>Once every issue is settled, a restored event's status graduates to Reconciled.</summary>
    private async Task SettleDowntimeStatusAsync(DocumentRepositoryDowntimeEvent downtimeEvent, DateTimeOffset now, CancellationToken ct)
    {
        if (downtimeEvent.DowntimeStatus is not (DowntimeStatus.Restored or DowntimeStatus.ReconciliationInProgress))
        {
            return;
        }

        var all = await _issues.GetByDowntimeEventAsync(downtimeEvent.Id, ct);
        if (all.All(i => i.IsSettled()))
        {
            downtimeEvent.DowntimeStatus = DowntimeStatus.Reconciled;
            downtimeEvent.UpdatedAt = now;
            downtimeEvent.UpdatedBy = _currentUser.ActorName;
            await _events.UpdateAsync(downtimeEvent, ct);
        }
    }

    private async Task PersistAsync(DocumentTemporaryControlledIssue issue, DateTimeOffset now, CancellationToken ct)
    {
        issue.UpdatedAt = now;
        issue.UpdatedBy = _currentUser.ActorName;
        await _issues.UpdateAsync(issue, ct);
    }

    private async Task<(Response<TemporaryControlledIssueModel>? Fail, DocumentRepositoryDowntimeEvent? Event, DocumentTemporaryControlledIssue? Issue)>
        LoadAsync(Guid downtimeEventId, Guid issueId, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var downtimeEvent = await _events.GetByIdAsync(downtimeEventId, ct);
        if (downtimeEvent is null)
        {
            return (Fail("Downtime event not found.", 404, DowntimeReasonCodes.DowntimeNotFound, correlationId), null, null);
        }

        var issue = await _issues.GetByIdAsync(issueId, ct);
        return issue is null || issue.DowntimeEventId != downtimeEventId
            ? (Fail("Temporary controlled issue not found.", 404, DowntimeReasonCodes.IssueNotFound, correlationId), downtimeEvent, null)
            : (null, downtimeEvent, issue);
    }

    private static Response<TemporaryControlledIssueModel> Fail(string error, int status, string reason, string correlationId) =>
        Response<TemporaryControlledIssueModel>.Fail(error, status, reason, correlationId);

    private static string? Trim(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}
