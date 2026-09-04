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
/// Records the execution outcome (completed / missed / rescheduled) against a plan atom (D-EXECUTION-STATUS = A). It
/// UPSERTS the draft <see cref="VisitReportEntity"/> for the plan (1:1). It <b>reads</b> the FU01 plan atom read-only to
/// reject an orphan outcome and to default the reporting resource; it <b>never mutates</b> the atom (FU01 §2.3 + the
/// F-EXECUTED-MARKER gap: FU01 has no "executed" transition, so the report-side outcome is the sole source of truth).
/// <para>The write touches only the <see cref="VisitReportEntity"/> aggregate, so it is a single-document, version-guarded
/// write — no multi-document transaction is needed, because there is no second aggregate to keep consistent (§8.4).</para>
/// </summary>
public sealed class RecordVisitOutcomeHandler : IRequestHandler<RecordVisitOutcomeCommand, Response<Guid>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IVisitReportRepository _reports;
    private readonly IPlannedVisitRepository _plannedVisits;

    public RecordVisitOutcomeHandler(
        ITenantContext tenant, IActorContext actor,
        IVisitReportRepository reports, IPlannedVisitRepository plannedVisits)
    {
        _tenant = tenant;
        _actor = actor;
        _reports = reports;
        _plannedVisits = plannedVisits;
    }

    public async Task<Response<Guid>> Handle(RecordVisitOutcomeCommand request, CancellationToken cancellationToken)
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

        // Read-only orphan guard: a report must link to an existing plan atom (§13). FU01's aggregate is not mutated.
        var plan = await _plannedVisits.GetByIdAsync(tenantId, request.PlannedVisitId, cancellationToken);
        if (plan is null)
        {
            return Fail(new VisitReportValidation.Failure(
                "The planned visit does not exist.", VisitReportErrorCodes.PlannedVisitNotFound, 404));
        }

        if (VisitReportValidation.ValidateOutcome(request.ExecutionOutcome, request.ReasonCode) is { } outcomeFailure)
        {
            return Fail(outcomeFailure);
        }

        var outcome = VisitExecutionOutcome.Normalize(request.ExecutionOutcome);

        DateOnly? rescheduleTo = null;
        if (string.Equals(outcome, VisitExecutionOutcome.Rescheduled, StringComparison.Ordinal)
            && VisitReportValidation.Trim(request.RescheduleToDate) is not null)
        {
            rescheduleTo = VisitReportValidation.ParseDate(request.RescheduleToDate);
            if (rescheduleTo is null)
            {
                return Fail(new VisitReportValidation.Failure(
                    "RescheduleToDate must be an ISO yyyy-MM-dd date.", VisitReportErrorCodes.RescheduleDateInvalid));
            }
        }

        var resourceId = VisitReportValidation.Trim(request.ReportedByResourceId) ?? plan.Resource.ResourceId;
        if (VisitReportValidation.ValidateResourceId(resourceId) is { } resourceFailure)
        {
            return Fail(resourceFailure);
        }

        if (VisitReportValidation.ValidateFreeText(
                "RescheduleNotes", request.RescheduleNotes, VisitReportLimits.MaxNotesLength) is { } notesFailure)
        {
            return Fail(notesFailure);
        }

        var executedAt = VisitReportValidation.ParseInstant(request.ExecutedAt) ?? DateTimeOffset.UtcNow;
        var reason = string.Equals(outcome, VisitExecutionOutcome.Completed, StringComparison.Ordinal)
            ? null
            : VisitReportReasonCodes.Normalize(request.ReasonCode);
        var now = DateTimeOffset.UtcNow;
        var actor = _actor.ActorName;

        var existing = await _reports.GetByPlannedVisitIdAsync(tenantId, request.PlannedVisitId, cancellationToken);
        if (existing is not null)
        {
            // A finalised report is immutable in place: its outcome is not re-recorded, a correction is an amendment.
            if (existing.IsFinalised())
            {
                return Fail(new VisitReportValidation.Failure(
                    "A submitted report's outcome cannot be re-recorded; file an amendment instead.",
                    VisitReportErrorCodes.InvalidTransition, 409));
            }

            var expected = request.ExpectedVersion ?? existing.Version;
            existing.ExecutionOutcome = outcome;
            existing.ReasonCode = reason;
            existing.RescheduleToDate = rescheduleTo;
            existing.RescheduleNotes = VisitReportValidation.Trim(request.RescheduleNotes);
            existing.ReportedByResourceId = resourceId!;
            existing.ExecutedAt = executedAt;
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
            ExecutionOutcome = outcome,
            ReportStatus = VisitReportStatus.Draft,
            ReasonCode = reason,
            RescheduleToDate = rescheduleTo,
            RescheduleNotes = VisitReportValidation.Trim(request.RescheduleNotes),
            ReportedByResourceId = resourceId!,
            ExecutedAt = executedAt,
            CreatedAt = now,
            CreatedBy = actor
        };

        await _reports.InsertAsync(report, cancellationToken);
        return Response<Guid>.Success(report.Id, 201);
    }

    private static Response<Guid> Fail(VisitReportValidation.Failure failure)
        => Response<Guid>.Fail(VisitReportValidation.ToErrors(failure), failure.StatusCode);

    private static Response<Guid> ConcurrencyFail()
        => Response<Guid>.Fail(
            new[] { "The report changed since it was loaded. Reload and try again.", VisitReportErrorCodes.ConcurrencyConflict },
            409);
}
