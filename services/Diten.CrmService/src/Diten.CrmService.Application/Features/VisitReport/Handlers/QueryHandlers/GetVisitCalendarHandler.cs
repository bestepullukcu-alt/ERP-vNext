using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.VisitReport.Contract;
using Diten.CrmService.Application.Features.VisitReport.Queries;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.VisitReport.Handlers.QueryHandlers;

/// <summary>
/// The Day/Week EXECUTION calendar read (D-CALENDAR-UI = A). It reads the FU01 <c>PlannedVisit</c> atoms in the window
/// (optionally narrowed to one resource) and JOINS each with its FU02 report state (none / draft / submitted / amended).
/// Read-only: it mutates neither aggregate. The atoms are read through FU01's own repository seam and filtered in memory
/// (never a server-side sort over the DateOnly / DateTimeOffset fields — parallel-arrays). The report state is a single
/// bulk read for the whole window, never one read per visit.
/// </summary>
public sealed class GetVisitCalendarHandler : IRequestHandler<GetVisitCalendarQuery, Response<VisitCalendarDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IPlannedVisitRepository _plannedVisits;
    private readonly IVisitReportRepository _reports;

    public GetVisitCalendarHandler(
        ITenantContext tenant, IPlannedVisitRepository plannedVisits, IVisitReportRepository reports)
    {
        _tenant = tenant;
        _plannedVisits = plannedVisits;
        _reports = reports;
    }

    public async Task<Response<VisitCalendarDto>> Handle(
        GetVisitCalendarQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<VisitCalendarDto>.Fail("Tenant context is required.", 400);
        }

        var from = VisitReportValidation.ParseDate(request.From);
        var to = VisitReportValidation.ParseDate(request.To);
        if (from is not { } fromDate || to is not { } toDate)
        {
            return Response<VisitCalendarDto>.Fail(
                new[] { "A valid from/to date window (yyyy-MM-dd) is required.", VisitReportErrorCodes.RescheduleDateInvalid },
                400);
        }

        if (toDate < fromDate)
        {
            (fromDate, toDate) = (toDate, fromDate);
        }

        var atoms = await _plannedVisits.ListAsync(tenantId, cancellationToken);
        IEnumerable<Domain.Entities.PlannedVisit> window = atoms
            .Where(v => !v.IsArchived() && v.PlannedDate >= fromDate && v.PlannedDate <= toDate);

        if (VisitReportValidation.Trim(request.ResourceId) is { } resourceId)
        {
            window = window.Where(v => string.Equals(v.Resource.ResourceId, resourceId, StringComparison.Ordinal));
        }

        var visits = window.ToList();

        var reportsByPlan = (await _reports.ListByPlannedVisitIdsAsync(
                tenantId, visits.Select(v => v.Id).ToList(), cancellationToken))
            .GroupBy(r => r.PlannedVisitId)
            .ToDictionary(g => g.Key, g => g.First());

        var items = visits
            .OrderBy(v => v.PlannedDate)
            .ThenBy(v => v.Slot.SequenceOrder ?? int.MaxValue)
            .ThenBy(v => v.Slot.SlotStartTime ?? v.PlannedStartTime ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(v => v.VisitCode, StringComparer.Ordinal)
            .Select(v => ToCalendarItem(v, reportsByPlan.GetValueOrDefault(v.Id)))
            .ToList();

        var dto = new VisitCalendarDto(
            fromDate.ToString("yyyy-MM-dd"), toDate.ToString("yyyy-MM-dd"), items, items.Count);
        return Response<VisitCalendarDto>.Success(dto);
    }

    private static VisitCalendarItemDto ToCalendarItem(
        Domain.Entities.PlannedVisit v, Domain.Entities.VisitReport? report)
    {
        var reportState = report is null
            ? "none"
            : report.ReportStatus;

        return new VisitCalendarItemDto(
            v.Id,
            v.VisitCode,
            v.PlannedDate.ToString("yyyy-MM-dd"),
            v.PlannedStartTime,
            v.PlannedEndTime,
            v.Slot.SequenceOrder,
            v.Slot.SlotStartTime,
            v.TargetType,
            v.TargetId,
            v.Resource.ResourceId,
            v.PlanStatus,
            v.Content?.JourneyId,
            v.Content?.StageId,
            v.Content?.StageIndex,
            report?.Id,
            reportState,
            report?.ExecutionOutcome,
            report?.ContentActuals?.StageIndex,
            report?.ContentActuals?.MatchedPlan);
    }
}
