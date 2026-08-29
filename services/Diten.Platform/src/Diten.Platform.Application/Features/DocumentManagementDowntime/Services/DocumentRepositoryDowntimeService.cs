using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.DocumentManagementDowntime.Services;

/// <summary>
/// MOD-0029-FU20 — repository / DMS downtime event lifecycle (GMG-QMS-SOP-0001 §11.3): open the downtime log,
/// record the restore, evaluate the 2-working-day escalation, and close only once every temporary issue is
/// settled and — where the threshold was exceeded — a BCP assessment reference is on file.
///
/// SOP controls enforced here:
/// • Opening requires detection evidence; the log must exist BEFORE any outside-normal-environment issue.
/// • Restoring requires restore evidence and computes the working-day duration.
/// • An outage beyond 2 working days flags GQD + IT/CSV escalation, raises idempotent escalation records for
///   both roles, and makes a BCP assessment reference mandatory before closure.
/// • Closure is refused while any temporary issue is neither reconciled nor cancelled — that is what stops a
///   temporary controlled issue quietly becoming an uncontrolled copy.
///
/// BOUNDARIES: no scheduler (escalation evaluation is an explicit call), no BCP module, no CAPA module, no
/// e-signature, and the FU16 repository assessment is READ for its boundary statement — never modified.
/// Nothing is hard-deleted; cancellation and closure are status changes.
/// </summary>
public sealed class DocumentRepositoryDowntimeService
{
    private readonly IDocumentRepositoryDowntimeEventRepository _events;
    private readonly IDocumentTemporaryControlledIssueRepository _issues;
    private readonly IDocumentDowntimeEscalationRepository _escalations;
    private readonly IDocumentRepositoryAssessmentRepository _assessments;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _currentUser;

    public DocumentRepositoryDowntimeService(
        IDocumentRepositoryDowntimeEventRepository events,
        IDocumentTemporaryControlledIssueRepository issues,
        IDocumentDowntimeEscalationRepository escalations,
        IDocumentRepositoryAssessmentRepository assessments,
        ITenantContext tenantContext,
        ICurrentUserContext currentUser)
    {
        _events = events;
        _issues = issues;
        _escalations = escalations;
        _assessments = assessments;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
    }

    // ── open ──────────────────────────────────────────────────────────────────

    public async Task<Response<DowntimeEventModel>> OpenAsync(OpenDowntimeEventInput input, string correlationId, CancellationToken ct)
    {
        var tenantId = TenantGuard.RequireTenant(_tenantContext);
        if (string.IsNullOrWhiteSpace(input.DetectionEvidenceReference))
        {
            return Fail("Detection evidence is required to open a downtime event.", 400,
                DowntimeReasonCodes.DetectionEvidenceRequired, correlationId);
        }

        var now = DateTimeOffset.UtcNow;
        var startedAt = input.StartedAt ?? now;
        if (startedAt > now)
        {
            return Fail("The downtime start time cannot be in the future.", 400,
                DowntimeReasonCodes.StartedAtInFuture, correlationId);
        }

        // The FU16 assessment link is validated tenant-scoped; a foreign or unknown id simply does not resolve.
        DocumentRepositoryAssessment? assessment = null;
        if (input.RepositoryAssessmentId is { } assessmentId)
        {
            assessment = await _assessments.GetByIdAsync(assessmentId, ct);
            if (assessment is null)
            {
                return Fail("Repository assessment not found.", 404, DowntimeReasonCodes.NotFoundNonLeakage, correlationId);
            }
        }

        var downtimeEvent = new DocumentRepositoryDowntimeEvent
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            DowntimeNumber = $"DTE-{now:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpperInvariant()}",
            RepositoryAssessmentId = assessment?.Id,
            RepositoryName = Trim(input.RepositoryName) ?? assessment?.RepositoryName,
            DowntimeStatus = DowntimeStatus.Open,
            DowntimeType = DowntimeWire.ParseDowntimeType(input.DowntimeType),
            StartedAt = startedAt,
            StartedBy = _currentUser.ActorName,
            DetectedByUserId = input.DetectedByUserId,
            DetectionEvidenceReference = input.DetectionEvidenceReference.Trim(),
            ImpactSummary = Trim(input.ImpactSummary),
            CorrelationId = correlationId,
            CreatedBy = _currentUser.ActorName
        };

        await _events.CreateAsync(downtimeEvent, ct);
        return Response<DowntimeEventModel>.Success(
            DowntimeWire.ToEvent(downtimeEvent, assessment?.RepositoryType), 201, correlationId);
    }

    // ── restore ───────────────────────────────────────────────────────────────

    public async Task<Response<DowntimeEventModel>> MarkRestoredAsync(
        Guid id, MarkRepositoryRestoredInput input, string correlationId, CancellationToken ct)
    {
        var (fail, downtimeEvent) = await LoadAsync(id, correlationId, ct);
        if (fail is not null)
        {
            return fail;
        }

        if (downtimeEvent!.DowntimeStatus is DowntimeStatus.Closed or DowntimeStatus.Cancelled)
        {
            return Fail($"A {downtimeEvent.DowntimeStatus} downtime event cannot be restored.", 409,
                DowntimeReasonCodes.DowntimeAlreadyClosed, correlationId);
        }

        if (downtimeEvent.RestoredAt is not null)
        {
            return Fail("The repository has already been marked restored.", 409,
                DowntimeReasonCodes.DowntimeAlreadyRestored, correlationId);
        }

        if (string.IsNullOrWhiteSpace(input.RestoreEvidenceReference))
        {
            return Fail("Restore evidence is required to mark the repository restored.", 400,
                DowntimeReasonCodes.RestoreEvidenceRequired, correlationId);
        }

        var now = DateTimeOffset.UtcNow;
        var restoredAt = input.RestoredAt ?? now;
        downtimeEvent.RestoredAt = restoredAt;
        downtimeEvent.RestoredBy = _currentUser.ActorName;
        downtimeEvent.RestoreEvidenceReference = input.RestoreEvidenceReference.Trim();
        downtimeEvent.DurationWorkingDays = DowntimeScheduleCalculator.CountWorkingDays(downtimeEvent.StartedAt, restoredAt);
        downtimeEvent.DowntimeStatus = (await _issues.GetByDowntimeEventAsync(id, ct)).Any(i => !i.IsSettled())
            ? DowntimeStatus.ReconciliationInProgress
            : DowntimeStatus.Restored;

        // SOP §11.3 — reconciliation into the normal system only becomes possible at restore; a restore that
        // follows the issue re-bases every outstanding issue's 3-working-day window.
        foreach (var issue in (await _issues.GetByDowntimeEventAsync(id, ct))
                 .Where(i => i.IssueStatus is TemporaryIssueStatus.Issued or TemporaryIssueStatus.ReconciliationDue
                             && i.IssuedAt is not null))
        {
            issue.ReconciliationDueDate = DowntimeScheduleCalculator.ReconciliationDueDate(issue.IssuedAt!.Value, restoredAt);
            issue.IssueStatus = TemporaryIssueStatus.ReconciliationDue;
            issue.UpdatedAt = now;
            issue.UpdatedBy = _currentUser.ActorName;
            await _issues.UpdateAsync(issue, ct);
        }

        Touch(downtimeEvent, now);
        await _events.UpdateAsync(downtimeEvent, ct);
        return await ToModelAsync(downtimeEvent, correlationId, ct);
    }

    // ── escalation ────────────────────────────────────────────────────────────

    /// <summary>
    /// SOP §11.3 — evaluates the 2-working-day threshold. Explicitly invoked (no scheduler). Escalations are
    /// idempotent per (event, type, role): re-evaluation re-uses open records instead of duplicating them.
    /// </summary>
    public async Task<Response<DowntimeEscalationEvaluationModel>> EvaluateEscalationAsync(
        Guid id, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var downtimeEvent = await _events.GetByIdAsync(id, ct);
        if (downtimeEvent is null)
        {
            return Response<DowntimeEscalationEvaluationModel>.Fail(
                "Downtime event not found.", 404, DowntimeReasonCodes.DowntimeNotFound, correlationId);
        }

        var now = DateTimeOffset.UtcNow;
        var measuredUntil = downtimeEvent.RestoredAt ?? now;
        var duration = DowntimeScheduleCalculator.CountWorkingDays(downtimeEvent.StartedAt, measuredUntil);
        downtimeEvent.DurationWorkingDays = duration;

        var exceeds = DowntimeScheduleCalculator.ExceedsEscalationThreshold(duration);
        if (exceeds)
        {
            downtimeEvent.RequiresGqdItCsvEscalation = true;
            downtimeEvent.EscalatedAt ??= now;
            if (downtimeEvent.DowntimeStatus == DowntimeStatus.Open)
            {
                downtimeEvent.DowntimeStatus = DowntimeStatus.Escalated;
            }

            await EnsureEscalationAsync(downtimeEvent, DowntimeEscalationType.DowntimeExceedsTwoWorkingDays,
                DowntimeEscalationRole.GQD, DowntimeEscalationSeverity.Major,
                $"Repository downtime has exceeded {DowntimeScheduleCalculator.EscalationThresholdWorkingDays} working days " +
                $"(current duration: {duration}). GQD review is required.", null, correlationId, ct);

            await EnsureEscalationAsync(downtimeEvent, DowntimeEscalationType.BcpAssessmentRequired,
                DowntimeEscalationRole.ITCSVOwner, DowntimeEscalationSeverity.Major,
                "Downtime beyond the threshold requires a business continuity (BCP) assessment by IT/CSV. " +
                "Record the assessment reference on the downtime event before closure.", null, correlationId, ct);
        }

        Touch(downtimeEvent, now);
        await _events.UpdateAsync(downtimeEvent, ct);

        var rows = await _escalations.GetByDowntimeEventAsync(id, ct);
        return Response<DowntimeEscalationEvaluationModel>.Success(new DowntimeEscalationEvaluationModel(
            id, duration, exceeds, exceeds && string.IsNullOrWhiteSpace(downtimeEvent.BcpAssessmentReference),
            rows.Select(DowntimeWire.ToEscalation).ToList()), correlationId: correlationId);
    }

    // ── close ─────────────────────────────────────────────────────────────────

    public async Task<Response<DowntimeEventModel>> CloseAsync(
        Guid id, CloseDowntimeEventInput input, string correlationId, CancellationToken ct)
    {
        var (fail, downtimeEvent) = await LoadAsync(id, correlationId, ct);
        if (fail is not null)
        {
            return fail;
        }

        if (downtimeEvent!.DowntimeStatus is DowntimeStatus.Closed or DowntimeStatus.Cancelled)
        {
            return Fail($"The downtime event is already {downtimeEvent.DowntimeStatus}.", 409,
                DowntimeReasonCodes.DowntimeAlreadyClosed, correlationId);
        }

        // A temporary issue must never be abandoned by closing the outage over its head.
        var unsettled = (await _issues.GetByDowntimeEventAsync(id, ct)).Where(i => !i.IsSettled()).ToList();
        if (unsettled.Count > 0)
        {
            return Fail(
                $"{unsettled.Count} temporary controlled issue(s) are not yet reconciled or cancelled; the downtime event cannot be closed.",
                409, DowntimeReasonCodes.UnsettledIssuesBlockClose, correlationId);
        }

        // The threshold decision is re-derived at close time, never trusted from an earlier evaluation.
        var now = DateTimeOffset.UtcNow;
        var duration = DowntimeScheduleCalculator.CountWorkingDays(downtimeEvent.StartedAt, downtimeEvent.RestoredAt ?? now);
        downtimeEvent.DurationWorkingDays = duration;
        var bcp = Trim(input.BcpAssessmentReference) ?? downtimeEvent.BcpAssessmentReference;

        if (DowntimeScheduleCalculator.ExceedsEscalationThreshold(duration) && string.IsNullOrWhiteSpace(bcp))
        {
            return Fail(
                "The downtime exceeded 2 working days; a BCP assessment reference is required before closure.",
                409, DowntimeReasonCodes.BcpAssessmentRequired, correlationId);
        }

        downtimeEvent.BcpAssessmentReference = bcp;
        downtimeEvent.DowntimeStatus = DowntimeStatus.Closed;
        downtimeEvent.ClosedAt = now;
        downtimeEvent.ClosedBy = _currentUser.ActorName;
        downtimeEvent.ClosureNote = Trim(input.ClosureNote);
        Touch(downtimeEvent, now);
        await _events.UpdateAsync(downtimeEvent, ct);
        return await ToModelAsync(downtimeEvent, correlationId, ct);
    }

    // ── reads ─────────────────────────────────────────────────────────────────

    public async Task<Response<DowntimeEventModel>> GetAsync(Guid id, string correlationId, CancellationToken ct)
    {
        var (fail, downtimeEvent) = await LoadAsync(id, correlationId, ct);
        return fail ?? await ToModelAsync(downtimeEvent!, correlationId, ct);
    }

    public async Task<Response<IReadOnlyList<DowntimeEventModel>>> ListAsync(string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var rows = await _events.GetAllForTenantAsync(ct);
        var models = new List<DowntimeEventModel>(rows.Count);
        foreach (var row in rows)
        {
            models.Add(await ToModelUnwrappedAsync(row, ct));
        }

        return Response<IReadOnlyList<DowntimeEventModel>>.Success(models, correlationId: correlationId);
    }

    public async Task<Response<IReadOnlyList<DowntimeEscalationModel>>> GetEscalationsAsync(Guid id, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var downtimeEvent = await _events.GetByIdAsync(id, ct);
        if (downtimeEvent is null)
        {
            return Response<IReadOnlyList<DowntimeEscalationModel>>.Fail(
                "Downtime event not found.", 404, DowntimeReasonCodes.DowntimeNotFound, correlationId);
        }

        var rows = await _escalations.GetByDowntimeEventAsync(id, ct);
        return Response<IReadOnlyList<DowntimeEscalationModel>>.Success(
            rows.Select(DowntimeWire.ToEscalation).ToList(), correlationId: correlationId);
    }

    // ── shared with the issue service ─────────────────────────────────────────

    /// <summary>Idempotent escalation creation per (event, type, role, open-issue): reused by the issue service.</summary>
    internal async Task EnsureEscalationAsync(
        DocumentRepositoryDowntimeEvent downtimeEvent,
        DowntimeEscalationType type,
        DowntimeEscalationRole role,
        DowntimeEscalationSeverity severity,
        string description,
        Guid? temporaryIssueId,
        string correlationId,
        CancellationToken ct)
    {
        var existing = await _escalations.GetByDowntimeEventAsync(downtimeEvent.Id, ct);
        if (existing.Any(e => e.EscalationType == type && e.RequiredRole == role
                              && e.TemporaryControlledIssueId == temporaryIssueId
                              && e.Status is DowntimeEscalationStatus.Open or DowntimeEscalationStatus.Acknowledged))
        {
            return;
        }

        await _escalations.CreateAsync(new DocumentDowntimeEscalation
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantContext.TenantId,
            DowntimeEventId = downtimeEvent.Id,
            TemporaryControlledIssueId = temporaryIssueId,
            EscalationType = type,
            RequiredRole = role,
            Severity = severity,
            Status = DowntimeEscalationStatus.Open,
            Description = description,
            CorrelationId = correlationId,
            CreatedBy = _currentUser.ActorName
        }, ct);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private async Task<Response<DowntimeEventModel>> ToModelAsync(
        DocumentRepositoryDowntimeEvent downtimeEvent, string correlationId, CancellationToken ct) =>
        Response<DowntimeEventModel>.Success(await ToModelUnwrappedAsync(downtimeEvent, ct), correlationId: correlationId);

    private async Task<DowntimeEventModel> ToModelUnwrappedAsync(DocumentRepositoryDowntimeEvent downtimeEvent, CancellationToken ct)
    {
        RepositoryType? repositoryType = null;
        if (downtimeEvent.RepositoryAssessmentId is { } assessmentId)
        {
            repositoryType = (await _assessments.GetByIdAsync(assessmentId, ct))?.RepositoryType;
        }

        return DowntimeWire.ToEvent(downtimeEvent, repositoryType);
    }

    private async Task<(Response<DowntimeEventModel>? Fail, DocumentRepositoryDowntimeEvent? Event)> LoadAsync(
        Guid id, string correlationId, CancellationToken ct)
    {
        TenantGuard.RequireTenant(_tenantContext);
        var downtimeEvent = await _events.GetByIdAsync(id, ct);
        return downtimeEvent is null
            ? (Fail("Downtime event not found.", 404, DowntimeReasonCodes.DowntimeNotFound, correlationId), null)
            : (null, downtimeEvent);
    }

    private void Touch(DocumentRepositoryDowntimeEvent e, DateTimeOffset now)
    {
        e.UpdatedAt = now;
        e.UpdatedBy = _currentUser.ActorName;
    }

    private static Response<DowntimeEventModel> Fail(string error, int status, string reason, string correlationId) =>
        Response<DowntimeEventModel>.Fail(error, status, reason, correlationId);

    private static string? Trim(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}
