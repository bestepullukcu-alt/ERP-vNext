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
/// <para>WP-VW-W1 — each cell also carries the plan's cancellation reason, the product names of its content items
/// (snapshot first, else ONE bulk MDM read for the whole window, fail-open), and the derived work status + report
/// deadline + manager-attention flag (<see cref="VisitWorkStatus"/>, computed here, never stored).</para>
/// </summary>
public sealed class GetVisitCalendarHandler : IRequestHandler<GetVisitCalendarQuery, Response<VisitCalendarDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IPlannedVisitRepository _plannedVisits;
    private readonly IVisitReportRepository _reports;
    private readonly ICallerScope _caller;
    private readonly Diten.CrmService.Application.Features.PlannedVisit.VisitTargetNameReader _names;
    private readonly IProductNameReader? _productNames;
    private readonly TimeProvider _clock;

    public GetVisitCalendarHandler(
        ITenantContext tenant, IPlannedVisitRepository plannedVisits, IVisitReportRepository reports,
        ICallerScope caller, Diten.CrmService.Application.Features.PlannedVisit.VisitTargetNameReader names,
        IProductNameReader? productNames = null,
        TimeProvider? clock = null)
    {
        _caller = caller;
        _names = names;
        _tenant = tenant;
        _plannedVisits = plannedVisits;
        _reports = reports;
        _productNames = productNames;
        _clock = clock ?? TimeProvider.System;
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
                new[] { "A valid from/to date window (yyyy-MM-dd) is required.", VisitReportErrorCodes.CalendarRangeInvalid },
                400);
        }

        if (toDate < fromDate)
        {
            (fromDate, toDate) = (toDate, fromDate);
        }

        if (!TryParseWorkStatuses(request.WorkStatus, out var wanted))
        {
            return Response<VisitCalendarDto>.Fail(
                new[]
                {
                    $"Unsupported workStatus '{request.WorkStatus}'. Known values: {string.Join(", ", VisitWorkStatus.All)}.",
                    VisitReportErrorCodes.WorkStatusInvalid
                },
                400);
        }

        var atoms = await _plannedVisits.ListAsync(tenantId, cancellationToken);
        IEnumerable<Domain.Entities.PlannedVisit> window = atoms
            .Where(v => !v.IsArchived() && v.PlannedDate >= fromDate && v.PlannedDate <= toDate)
            // WP-VP-2 (B-1) — the calendar shows the caller's own plans unless they hold read-all.
            .Where(v => _caller.MayAccess(Diten.CrmService.Application.Features.PlannedVisit.PlannedVisitPermissions.ReadAll, v.Resource.ResourceId));

        if (VisitReportValidation.Trim(request.ResourceId) is { } resourceId)
        {
            window = window.Where(v => string.Equals(v.Resource.ResourceId, resourceId, StringComparison.Ordinal));
        }

        var visits = window.ToList();

        var reportsByPlan = (await _reports.ListByPlannedVisitIdsAsync(
                tenantId, visits.Select(v => v.Id).ToList(), cancellationToken))
            .GroupBy(r => r.PlannedVisitId)
            .ToDictionary(g => g.Key, g => g.First());

        // WP-VW-W1 — the work status is derived per cell from the plan, its report and ONE "now" for the whole read.
        var now = _clock.GetUtcNow();
        var cells = visits
            .Select(v =>
            {
                var report = reportsByPlan.GetValueOrDefault(v.Id);
                return (Visit: v, Report: report, Status: VisitWorkStatus.Derive(v, report, now));
            })
            .Where(c => wanted is null || wanted.Contains(c.Status))
            .ToList();

        // WP-VP-2 (B-8) — the card's target name, one read per master for the whole window.
        var names = await _names.ReadAsync(
            tenantId,
            cells.Select(c => c.Visit.AccountId).Concat(cells
                .Where(c => Diten.CrmService.Application.Features.PlannedVisit.VisitTargetNameReader.NamedByInstitution(c.Visit.TargetType))
                .Select(c => (Guid?)c.Visit.TargetId)),
            cells.Select(c => c.Visit.ContactId),
            cancellationToken);

        // WP-VW-W1 — product names: the approval snapshot first; the rest in ONE bulk MDM read (fail-open: no names).
        var unnamed = Diten.CrmService.Application.Features.PlannedVisit.PlannedVisitMapper
            .UnnamedProductIds(cells.Select(c => c.Visit)).Distinct().ToList();
        IReadOnlyDictionary<Guid, string> productNames = _productNames is not null && unnamed.Count > 0
            ? await _productNames.ReadNamesAsync(unnamed, cancellationToken)
            : new Dictionary<Guid, string>();

        var items = cells
            .OrderBy(c => c.Visit.PlannedDate)
            .ThenBy(c => c.Visit.Slot.SequenceOrder ?? int.MaxValue)
            .ThenBy(c => c.Visit.Slot.SlotStartTime ?? c.Visit.PlannedStartTime ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(c => c.Visit.VisitCode, StringComparer.Ordinal)
            .Select(c =>
            {
                var target = names.For(c.Visit.TargetType, c.Visit.TargetId, c.Visit.AccountId, c.Visit.ContactId);
                return ToCalendarItem(c.Visit, c.Report, productNames) with
                {
                    TargetDisplayName = target.Target,
                    TargetInactive = target.TargetInactive,
                    WorkStatus = c.Status,
                    ReportDeadline = VisitReportDeadline.For(c.Visit.PlannedDate),
                    ManagerAttention = VisitWorkStatus.NeedsManagerAttention(c.Status)
                };
            })
            .ToList();

        var dto = new VisitCalendarDto(
            fromDate.ToString("yyyy-MM-dd"), toDate.ToString("yyyy-MM-dd"), items, items.Count);
        return Response<VisitCalendarDto>.Success(dto);
    }

    /// <summary>The optional comma-separated work-status filter. Absent / blank ⇒ no filter (null). Any unknown code, or
    /// a value with no code at all, is refused (fail-closed) — never silently ignored.</summary>
    private static bool TryParseWorkStatuses(string? raw, out HashSet<string>? wanted)
    {
        wanted = null;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return true;
        }

        var codes = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => s.ToLowerInvariant())
            .ToList();
        if (codes.Count == 0 || codes.Any(c => !VisitWorkStatus.IsKnown(c)))
        {
            return false;
        }

        wanted = codes.ToHashSet(StringComparer.Ordinal);
        return true;
    }

    private static VisitCalendarItemDto ToCalendarItem(
        Domain.Entities.PlannedVisit v, Domain.Entities.VisitReport? report, IReadOnlyDictionary<Guid, string> productNames)
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
            report?.ContentActuals?.MatchedPlan,
            PlannedContent: ToPlannedContent(v, productNames),
            CancellationReason: v.CancellationReason);
    }

    /// <summary>WP-E2E-FIX-1 (E9-B2) — the atom's own ContentItems, summarised (no extra read). An atom without items
    /// yields an empty list; the legacy single <c>Content</c> stays on the PlannedJourneyId/StageId/StageIndex fields.
    /// WP-VW-W1 — each item's product name: the snapshot, else the bulk-read name, else null.</summary>
    private static IReadOnlyList<VisitCalendarPlannedContentDto> ToPlannedContent(
        Domain.Entities.PlannedVisit v, IReadOnlyDictionary<Guid, string> productNames)
        => (v.ContentItems ?? new List<PlannedVisitContentItem>())
            .Select(i => new VisitCalendarPlannedContentDto(
                i.ProductId, i.ProductCode, i.Role, i.JourneyId, i.JourneyCode, i.StageId, i.StageIndex, i.StageCode,
                i.StageName,
                (i.Steps ?? new List<PlannedVisitContentStep>())
                    .Select(s => new VisitCalendarPlannedStepDto(s.Title, s.Type))
                    .ToList(),
                ProductName: string.IsNullOrWhiteSpace(i.ProductName)
                    ? productNames.GetValueOrDefault(i.ProductId)
                    : i.ProductName.Trim()))
            .ToList();
}
