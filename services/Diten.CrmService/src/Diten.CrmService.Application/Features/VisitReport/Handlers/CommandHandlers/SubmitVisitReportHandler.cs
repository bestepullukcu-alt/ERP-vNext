using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.VisitReport.Commands;
using Diten.CrmService.Application.Features.VisitReport.Contract;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
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

    public SubmitVisitReportHandler(
        ITenantContext tenant, IActorContext actor,
        IVisitReportRepository reports, IPlannedVisitRepository plannedVisits)
    {
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
        if (plan is null)
        {
            return Fail(new VisitReportValidation.Failure(
                "The planned visit does not exist.", VisitReportErrorCodes.PlannedVisitNotFound, 404));
        }

        if (VisitReportValidation.ValidateReportContent(request.ContentActuals, request.Samples, request.Feedback)
            is { } contentFailure)
        {
            return Fail(contentFailure);
        }

        var existing = await _reports.GetByPlannedVisitIdAsync(tenantId, request.PlannedVisitId, cancellationToken);
        var resourceId = VisitReportValidation.Trim(request.ReportedByResourceId)
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
