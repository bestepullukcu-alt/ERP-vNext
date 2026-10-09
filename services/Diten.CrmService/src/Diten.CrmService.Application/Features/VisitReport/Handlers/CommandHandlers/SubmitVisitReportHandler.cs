using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.VisitReport.Commands;
using Diten.CrmService.Application.Features.VisitReport.Contract;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using Diten.CrmService.Application.Features.VisitWorkspace;
using MediatR;
using VisitReportEntity = Diten.CrmService.Domain.Entities.VisitReport;

namespace Diten.CrmService.Application.Features.VisitReport.Handlers.CommandHandlers;

/// <summary>
/// Records + submits a COMPLETED visit's report (D-REPORT-PERSISTENCE = A). Keyed by <c>PlannedVisitId</c> (1:1), it
/// fills an existing draft report or creates one, sets <c>ExecutionOutcome = completed</c>, records the ACTUAL content
/// presented (incl. the actual StageIndex + MatchedPlan — §4.4), samples, feedback + follow-up, and moves the report to
/// <c>submitted</c>. FU02 writes NO advanced cursor onto the plan atom (D-STAGE-ADVANCE = B); the atom is read-only here.
/// <para><b>Immutability (D-EDIT-WINDOW).</b> Re-submitting an already-finalised report is allowed only inside the short
/// correction window; past it the report is immutable in place and a correction must be an append-only amendment (409).
/// Single-aggregate write, version-guarded — no transaction needed.</para>
/// </summary>
public sealed class SubmitVisitReportHandler : IRequestHandler<SubmitVisitReportCommand, Response<Guid>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IVisitReportRepository _reports;
    private readonly IPlannedVisitRepository _plannedVisits;

    private readonly ICallerScope _caller;
    private readonly TimeProvider _clock;
    private readonly IVisitRescheduleUnitOfWork? _reschedule;
    private readonly VisitWorkspaceDays? _days;

    public SubmitVisitReportHandler(
        ITenantContext tenant, IActorContext actor,
        IVisitReportRepository reports, IPlannedVisitRepository plannedVisits, ICallerScope caller,
        TimeProvider? clock = null,
        // WP-VW-W2 (K-W1 = A) — the atomic report + new visit write, and the reschedule date rule (re-checked here).
        IVisitRescheduleUnitOfWork? reschedule = null,
        VisitWorkspaceDays? days = null)
    {
        _reschedule = reschedule;
        _days = days;
        _caller = caller;
        _clock = clock ?? TimeProvider.System;
        _tenant = tenant;
        _actor = actor;
        _reports = reports;
        _plannedVisits = plannedVisits;
    }

    public async Task<Response<Guid>> Handle(SubmitVisitReportCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<Guid>.Fail("Tenant context is required.", 400);
        }

        if (request.PlannedVisitId == Guid.Empty)
        {
            return Fail(new VisitReportValidation.Failure(
                "PlannedVisitId is required.", VisitReportErrorCodes.PlannedVisitRequired));
        }

        var plan = await _plannedVisits.GetByIdAsync(tenantId, request.PlannedVisitId, cancellationToken);
        // WP-VP-2 (B-1) — a rep reports only on their OWN planned visits; another rep's plan is as absent as a missing one.
        if (plan is null || !_caller.MayAccess(Diten.CrmService.Application.Features.PlannedVisit.PlannedVisitPermissions.ReadAll, plan.Resource.ResourceId))
        {
            return Fail(new VisitReportValidation.Failure(
                "The planned visit does not exist.", VisitReportErrorCodes.PlannedVisitNotFound, 404));
        }

        // WP-VW-W1 — the reporter is the caller; only the read-all holder submits for someone else.
        var readAll = _caller.HasPermission(Diten.CrmService.Application.Features.PlannedVisit.PlannedVisitPermissions.ReadAll);
        var (reporterAllowed, reporter) = _caller.ResolveWriteResource(
            Diten.CrmService.Application.Features.PlannedVisit.PlannedVisitPermissions.ReadAll, request.ReportedByResourceId);
        if (!reporterAllowed)
        {
            return Fail(new VisitReportValidation.Failure(
                "A visit report can only be submitted by the signed-in resource.", VisitOwnership.ResourceNotCaller, 403));
        }

        // WP-VW-W1 — a cancelled plan takes no report.
        if (VisitReportValidation.ValidatePlanNotCancelled(plan) is { } cancelledFailure)
        {
            return Fail(cancelledFailure);
        }

        // WP-VW-W2 — missed / rescheduled: finalise the recorded draft outcome (a rescheduled one creates the new visit).
        var mode = VisitReportValidation.Trim(request.ExecutionOutcome) is { } asked
            ? VisitExecutionOutcome.Normalize(asked)
            : VisitExecutionOutcome.Completed;
        if (!VisitExecutionOutcome.IsKnown(mode))
        {
            return Fail(new VisitReportValidation.Failure(
                $"Unsupported ExecutionOutcome '{mode}'. Known values: {string.Join(", ", VisitExecutionOutcome.All)}.",
                VisitReportErrorCodes.UnsupportedVocabularyValue));
        }

        if (!string.Equals(mode, VisitExecutionOutcome.Completed, StringComparison.Ordinal))
        {
            return await FinaliseOutcomeAsync(tenantId, plan, mode, reporter, readAll, request.ExpectedVersion, cancellationToken);
        }

        // WP-E2E-FIX-1 (E9-B5) — a report is submitted only on or after the visit's planned day.
        if (VisitReportValidation.ValidateDue(plan.PlannedDate, VisitReportValidation.Today(_clock)) is { } dueFailure)
        {
            return Fail(dueFailure);
        }

        if (VisitReportValidation.ValidateReportContent(request.ContentActuals, request.Samples, request.Feedback)
            is { } contentFailure)
        {
            return Fail(contentFailure);
        }

        var existing = await _reports.GetByPlannedVisitIdAsync(tenantId, request.PlannedVisitId, cancellationToken);

        // WP-VW-W2 — a reschedule that already created its new visit is not turned into a completed report.
        if (existing?.RescheduledToPlannedVisitId is not null)
        {
            return Fail(new VisitReportValidation.Failure(
                "This visit was rescheduled and its new visit exists; correct it with an amendment.",
                VisitWorkspaceErrorCodes.RescheduleAlreadyApplied, 409));
        }

        // WP-VW-W1 — past the deadline a FIRST submit (none yet, or a draft) is refused for the rep. A report already
        // submitted keeps its 60-minute in-place correction window, and amendments stay unlimited.
        if (existing?.IsFinalised() != true
            && VisitReportValidation.ValidateDeadline(plan.PlannedDate, _clock.GetUtcNow(), readAll) is { } deadlineFailure)
        {
            return Fail(deadlineFailure);
        }

        var resourceId = reporter
                         ?? existing?.ReportedByResourceId
                         ?? plan.Resource.ResourceId;
        if (VisitReportValidation.ValidateResourceId(resourceId) is { } resourceFailure)
        {
            return Fail(resourceFailure);
        }

        var now = DateTimeOffset.UtcNow;
        var actor = _actor.ActorName;
        var executedAt = VisitReportValidation.ParseInstant(request.ExecutedAt)
                         ?? existing?.ExecutedAt
                         ?? now;

        if (existing is not null)
        {
            // A finalised report is immutable in place after the correction window — a change must then be an amendment.
            if (existing.IsFinalised() && !existing.IsWithinEditWindow(now))
            {
                return Fail(new VisitReportValidation.Failure(
                    "The correction window has closed; file an append-only amendment instead of editing in place.",
                    VisitReportErrorCodes.EditWindowClosed, 409));
            }

            var expected = request.ExpectedVersion ?? existing.Version;
            ApplyCompletedReport(existing, request, resourceId!, executedAt);
            existing.UpdatedAt = now;
            existing.UpdatedBy = actor;

            var replaced = await _reports.ReplaceAsync(existing, expected, cancellationToken);
            return replaced ? Response<Guid>.Success(existing.Id) : ConcurrencyFail();
        }

        var report = new VisitReportEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PlannedVisitId = request.PlannedVisitId,
            CreatedAt = now,
            CreatedBy = actor
        };
        ApplyCompletedReport(report, request, resourceId!, executedAt);

        await _reports.InsertAsync(report, cancellationToken);
        return Response<Guid>.Success(report.Id, 201);
    }

    /// <summary>
    /// WP-VW-W2 — submits a NOT-DONE (<c>missed</c>) or RESCHEDULED outcome: the draft recorded by the outcome command
    /// (reason, note, reschedule date) becomes the submitted report. A rescheduled report creates the new planned visit
    /// in the SAME all-or-nothing write (K-W1 = A); a second submit inside the correction window is a no-op (idempotent,
    /// never a second visit).
    /// </summary>
    private async Task<Response<Guid>> FinaliseOutcomeAsync(
        Guid tenantId, Domain.Entities.PlannedVisit plan, string mode, string? reporter, bool readAll, int? expectedVersion,
        CancellationToken cancellationToken)
    {
        var rescheduled = string.Equals(mode, VisitExecutionOutcome.Rescheduled, StringComparison.Ordinal);

        // A missed visit is closed only on or after its day; rescheduling stays free (E9-B5).
        if (!rescheduled
            && VisitReportValidation.ValidateDue(plan.PlannedDate, VisitReportValidation.Today(_clock)) is { } dueFailure)
        {
            return Fail(dueFailure);
        }

        var existing = await _reports.GetByPlannedVisitIdAsync(tenantId, plan.Id, cancellationToken);
        if (existing is null || !string.Equals(existing.ExecutionOutcome, mode, StringComparison.Ordinal))
        {
            return Fail(new VisitReportValidation.Failure(
                $"Record the '{mode}' outcome (with its reason) before submitting it.",
                VisitReportErrorCodes.InvalidTransition, 409));
        }

        var now = _clock.GetUtcNow();
        if (existing.IsFinalised())
        {
            // Already submitted: inside the correction window the same submit is a no-op (no second visit); after it, an
            // amendment is the only correction.
            return existing.IsWithinEditWindow(now)
                ? Response<Guid>.Success(existing.Id)
                : Fail(new VisitReportValidation.Failure(
                    "The correction window has closed; file an append-only amendment instead of editing in place.",
                    VisitReportErrorCodes.EditWindowClosed, 409));
        }

        if (VisitReportValidation.ValidateDeadline(plan.PlannedDate, now, readAll) is { } deadlineFailure)
        {
            return Fail(deadlineFailure);
        }

        var expected = expectedVersion ?? existing.Version;
        existing.ReportStatus = VisitReportStatus.Submitted;
        existing.SubmittedAt = now;
        existing.ReportedByResourceId = reporter ?? existing.ReportedByResourceId;
        existing.UpdatedAt = now;
        existing.UpdatedBy = _actor.ActorName;

        if (!rescheduled)
        {
            return await _reports.ReplaceAsync(existing, expected, cancellationToken)
                ? Response<Guid>.Success(existing.Id)
                : ConcurrencyFail();
        }

        // K-W1 = A — the new day is required now, and re-checked (time has passed since the draft).
        var today = VisitReportValidation.Today(_clock);
        if (existing.RescheduleToDate is not { } newDay
            || (_days is not null && !await _days.CanRescheduleToAsync(plan.Resource.ResourceId, newDay, today, cancellationToken)))
        {
            return Fail(new VisitReportValidation.Failure(
                "A rescheduled visit needs a new day after today, on a working day inside the active cycle period.",
                VisitWorkspaceErrorCodes.RescheduleDateInvalid));
        }

        if (_reschedule is null)
        {
            return Fail(new VisitReportValidation.Failure(
                "The reschedule could not be written. Nothing was saved — please try again.",
                VisitWorkspaceErrorCodes.ReferenceDataUnavailable, 503));
        }

        var newVisit = await BuildRescheduledVisitAsync(tenantId, plan, newDay, now, cancellationToken);
        existing.RescheduledToPlannedVisitId = newVisit.Id;
        return await _reschedule.SubmitWithNewVisitAsync(existing, expected, newVisit, cancellationToken)
            ? Response<Guid>.Success(existing.Id)
            : ConcurrencyFail();
    }

    /// <summary>K-W1 = A — the new planned visit of a reschedule: the same target / institution / rep / content (product
    /// names included), on the new day, no time, <c>Source = reschedule</c>, linked back to the visit it replaces.</summary>
    private async Task<Domain.Entities.PlannedVisit> BuildRescheduledVisitAsync(
        Guid tenantId, Domain.Entities.PlannedVisit plan, DateOnly newDay, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var stem = plan.VisitCode.Length > PlannedVisitLimits.MaxVisitCodeLength - 12
            ? plan.VisitCode[..(PlannedVisitLimits.MaxVisitCodeLength - 12)]
            : plan.VisitCode;
        var code = $"{stem}-R{newDay:yyMMdd}";
        for (var n = 2; (await _plannedVisits.ListByCodeAsync(tenantId, code, cancellationToken)).Any(v => !v.IsArchived()); n++)
        {
            code = $"{stem}-R{newDay:yyMMdd}-{n}";
        }

        return new Domain.Entities.PlannedVisit
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            VisitCode = code,
            TargetType = plan.TargetType,
            TargetId = plan.TargetId,
            AccountId = plan.AccountId,
            ContactId = plan.ContactId,
            AccountContactLinkId = plan.AccountContactLinkId,
            PlannedDate = newDay,
            PlannedDurationMinutes = plan.PlannedDurationMinutes,
            Resource = new PlannedVisitResourceRef
            {
                ResourceId = plan.Resource.ResourceId,
                ResourceType = plan.Resource.ResourceType,
                DisplayName = plan.Resource.DisplayName
            },
            PositionCode = plan.PositionCode,
            PositionId = plan.PositionId,
            VisitPurpose = plan.VisitPurpose,
            VisitType = plan.VisitType,
            Objective = plan.Objective,
            BusinessUnit = plan.BusinessUnit,
            TerritoryNodeId = plan.TerritoryNodeId,
            TerritoryModelId = plan.TerritoryModelId,
            CampaignId = plan.CampaignId,
            PlanStatus = PlannedVisitStatus.Planned,
            Source = PlannedVisitSource.Reschedule,
            RescheduledFromPlannedVisitId = plan.Id,
            Slot = new PlannedVisitScheduleSlot(),
            Frequency = plan.Frequency,
            Consent = plan.Consent,
            Content = plan.Content,
            Selection = plan.Selection,
            ContentItems = (plan.ContentItems ?? new List<PlannedVisitContentItem>()).ToList(),
            CreatedAt = now,
            CreatedBy = _actor.ActorName
        };
    }

    /// <summary>Fills the completed-visit report shape and finalises it (submitted). Shared by the create and the
    /// in-window in-place edit paths so they can never diverge.</summary>
    private static void ApplyCompletedReport(
        VisitReportEntity report, SubmitVisitReportCommand request, string resourceId, DateTimeOffset executedAt)
    {
        report.ExecutionOutcome = VisitExecutionOutcome.Completed;
        report.ReasonCode = null; // completed carries no missed/rescheduled reason
        report.RescheduleToDate = null;
        report.RescheduleNotes = null;
        report.ReportedByResourceId = resourceId;
        report.ExecutedAt = executedAt;
        report.ContentActuals = VisitReportMapper.FromInput(request.ContentActuals);
        report.Samples = VisitReportMapper.FromInput(request.Samples);
        report.Feedback = VisitReportMapper.FromInput(request.Feedback);
        report.ReportStatus = VisitReportStatus.Submitted;
        report.SubmittedAt = report.SubmittedAt ?? DateTimeOffset.UtcNow;
    }

    private static Response<Guid> Fail(VisitReportValidation.Failure failure)
        => Response<Guid>.Fail(VisitReportValidation.ToErrors(failure), failure.StatusCode);

    private static Response<Guid> ConcurrencyFail()
        => Response<Guid>.Fail(
            new[] { "The report changed since it was loaded. Reload and try again.", VisitReportErrorCodes.ConcurrencyConflict },
            409);
}
