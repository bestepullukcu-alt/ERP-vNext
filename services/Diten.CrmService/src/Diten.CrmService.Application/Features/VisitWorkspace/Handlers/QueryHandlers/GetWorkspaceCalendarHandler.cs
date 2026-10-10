using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Common.ReferenceValidation;
using Diten.CrmService.Application.Features.Contact;
using Diten.CrmService.Application.Features.CyclePeriod.Read;
using Diten.CrmService.Application.Features.Segmentation.Resolution;
using Diten.CrmService.Application.Features.VisitPlanning.TargetStatus;
using Diten.CrmService.Application.Features.PlannedVisit;
using Diten.CrmService.Application.Features.VisitPlanning;
using Diten.CrmService.Application.Features.VisitReport;
using Diten.CrmService.Application.Features.VisitReport.Contract;
using Diten.CrmService.Application.Features.VisitReport.Handlers.QueryHandlers;
using Diten.CrmService.Application.Features.VisitReport.Queries;
using Diten.CrmService.Application.Features.VisitWorkspace.Queries;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;
using PlannedVisitEntity = Diten.CrmService.Domain.Entities.PlannedVisit;

namespace Diten.CrmService.Application.Features.VisitWorkspace.Handlers.QueryHandlers;

/// <summary>
/// WP-VW-W2 (A4) — the visit workspace calendar: ONE read for the rep's window (≤ 42 days).
/// <list type="bullet">
/// <item><b>Written visits</b> — exactly the W1 execution calendar (<see cref="GetVisitCalendarHandler"/>: work status,
/// deadline, names, content), plus the W2 fields read from the same atoms / reports (source, reschedule links, the
/// cancel reason code + note, pins).</item>
/// <item><b>Draft weeks</b> — the preview visits of the rep's planning sessions (the EXISTING engine preview, no new
/// calculation) in weeks that are still drafts: <c>workStatus = draft</c>, <c>plannedVisitId = null</c>,
/// <c>previewKey</c>. The <c>draft</c> status exists only here.</item>
/// <item><b>Weeks</b> — state (draft / approved / past / none), whether the caller may approve / reopen it (the existing
/// endpoints' keys), capacity / planned minutes, visit count and the visits that did not fit (unscheduled).</item>
/// <item><b>Days</b> — kind (holiday), capacity, planned and free minutes (<see cref="VisitWorkspaceDays"/>; a draft
/// week's day takes the preview's planned minutes).</item>
/// </list>
/// The caller is the resource; another resource needs read-all (else 403 resource_not_caller). Read-only.
/// </summary>
public sealed class GetWorkspaceCalendarHandler : IRequestHandler<GetWorkspaceCalendarQuery, Response<WorkspaceCalendarDto>>
{
    private readonly ITenantContext _tenant;
    private readonly ICallerScope _caller;
    private readonly GetVisitCalendarHandler _calendar;
    private readonly IPlannedVisitRepository _plannedVisits;
    private readonly IVisitReportRepository _reports;
    private readonly IPlanningSessionRepository _sessions;
    private readonly IWorkspacePlanPreviewSource? _previews;
    private readonly VisitWorkspaceDays _days;
    private readonly VisitTargetNameReader _names;
    private readonly TimeProvider _clock;
    private readonly IContactSegmentSetReader? _segments;
    private readonly IReferenceDataCatalogReader? _catalog;
    private readonly ICyclePeriodReader? _periods;

    public GetWorkspaceCalendarHandler(
        ITenantContext tenant,
        ICallerScope caller,
        GetVisitCalendarHandler calendar,
        IPlannedVisitRepository plannedVisits,
        IVisitReportRepository reports,
        IPlanningSessionRepository sessions,
        VisitWorkspaceDays days,
        VisitTargetNameReader names,
        IWorkspacePlanPreviewSource? previews = null,
        TimeProvider? clock = null,
        // W2-BE-d — segment badges (the 3D source), reference labels (specialty / city / district), the period name.
        IContactSegmentSetReader? segments = null,
        IReferenceDataCatalogReader? catalog = null,
        ICyclePeriodReader? periods = null)
    {
        _segments = segments;
        _catalog = catalog;
        _periods = periods;
        _tenant = tenant;
        _caller = caller;
        _calendar = calendar;
        _plannedVisits = plannedVisits;
        _reports = reports;
        _sessions = sessions;
        _days = days;
        _names = names;
        _previews = previews;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<Response<WorkspaceCalendarDto>> Handle(
        GetWorkspaceCalendarQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<WorkspaceCalendarDto>.Fail("Tenant context is required.", 400);
        }

        var parsedFrom = VisitReportValidation.ParseDate(request.From);
        var parsedTo = VisitReportValidation.ParseDate(request.To);
        if (parsedFrom is not { } from || parsedTo is not { } to || to < from
            || to.DayNumber - from.DayNumber + 1 > VisitWorkspaceLimits.MaxWindowDays)
        {
            return Response<WorkspaceCalendarDto>.Fail(
                new[]
                {
                    $"A from/to window (yyyy-MM-dd, from ≤ to, at most {VisitWorkspaceLimits.MaxWindowDays} days) is required.",
                    VisitReportErrorCodes.CalendarRangeInvalid
                },
                400);
        }

        var (allowed, resourceId) = _caller.ResolveWriteResource(PlannedVisitPermissions.ReadAll, request.ResourceId);
        if (!allowed || resourceId is null)
        {
            return Response<WorkspaceCalendarDto>.Fail(
                new[] { "Only your own calendar can be read.", VisitOwnership.ResourceNotCaller }, 403);
        }

        // ── written visits: the W1 calendar, then the W2 fields from the same atoms / reports ──────────────────────
        var w1 = await _calendar.Handle(
            new GetVisitCalendarQuery(from.ToString("yyyy-MM-dd"), to.ToString("yyyy-MM-dd"), resourceId), cancellationToken);
        if (!w1.IsSuccessful || w1.Data is null)
        {
            return Response<WorkspaceCalendarDto>.Fail(w1.Errors ?? new[] { "Calendar read failed." }, w1.StatusCode);
        }

        var ids = w1.Data.Items.Select(i => i.PlannedVisitId).ToList();
        var atoms = (await _plannedVisits.ListByIdsAsync(tenantId, ids, cancellationToken)).ToDictionary(a => a.Id);
        var reports = (await _reports.ListByPlannedVisitIdsAsync(tenantId, ids, cancellationToken))
            .GroupBy(r => r.PlannedVisitId).ToDictionary(g => g.Key, g => g.First());

        var sessions = (await _sessions.ListAsync(tenantId, cancellationToken))
            .Where(s => string.Equals(s.ResourceId, resourceId, StringComparison.Ordinal)
                        && !string.Equals(s.Status, PlanningSessionStatus.Archived, StringComparison.Ordinal))
            .OrderByDescending(s => s.UpdatedAt ?? s.CreatedAt)
            .ToList();
        var pins = sessions.SelectMany(s => s.DayPins).ToList();

        var visits = new List<WorkspaceVisitDto>();
        foreach (var item in w1.Data.Items)
        {
            atoms.TryGetValue(item.PlannedVisitId, out var atom);
            reports.TryGetValue(item.PlannedVisitId, out var report);
            var date = DateOnly.Parse(item.PlannedDate);
            visits.Add(new WorkspaceVisitDto(
                item.PlannedVisitId, null, item.VisitCode, item.PlannedDate,
                PlanningWeekCalendar.MondayOf(date).ToString("yyyy-MM-dd"),
                item.SlotStartTime ?? item.PlannedStartTime, item.PlannedEndTime, item.SlotSequenceOrder,
                atom?.PlannedDurationMinutes,
                item.TargetType, item.TargetId, atom?.AccountId, atom?.ContactId,
                item.TargetDisplayName, item.TargetInactive, item.ResourceId, item.PlanStatus,
                item.WorkStatus ?? VisitWorkStatus.Planned, item.ReportDeadline, item.ManagerAttention,
                item.ReportState, item.VisitReportId, item.ExecutionOutcome,
                item.PlannedContent ?? Array.Empty<VisitCalendarPlannedContentDto>(),
                atom?.Source ?? PlannedVisitSource.Manual,
                atom?.RescheduledFromPlannedVisitId, report?.RescheduledToPlannedVisitId,
                item.CancellationReason, atom?.CancellationReasonCode, atom?.CancellationNote,
                IsPinned: pins.Any(p => p.TargetId == item.TargetId
                                        && string.Equals(p.Date, item.PlannedDate, StringComparison.Ordinal)),
                // W2-BE-b (CT wiring) — the rep's pinned start time of a time pin on that day
                PinnedTime: pins.FirstOrDefault(p => p.TargetId == item.TargetId
                                                     && string.Equals(p.Date, item.PlannedDate, StringComparison.Ordinal)
                                                     && !string.IsNullOrWhiteSpace(p.StartTime))?.StartTime,
                IsExtra: atom?.Selection?.Extra ?? false));
        }

        // ── draft weeks: the engine preview of each session touching the window ────────────────────────────────────
        var weekStates = new Dictionary<DateOnly, (string State, PlanningSession Session, WeekCapacityDto? Capacity, IReadOnlyList<UnscheduledPreview> Unplaced)>();
        var draftDayMinutes = new Dictionary<DateOnly, int>();
        var draftTargets = new List<(Guid? AccountId, Guid? ContactId, string TargetType, Guid TargetId)>();
        var draftVisits = new List<(PlannedSlotPreview Slot, string WeekStart, string? PinMove)>();
        if (_previews is not null)
        {
            foreach (var session in sessions)
            {
                if (await _previews.PreviewAsync(session, cancellationToken) is not { } preview)
                {
                    continue;
                }

                var weeks = preview.Weeks ?? Array.Empty<PlanningWeekDto>();
                for (var i = 0; i < weeks.Count; i++)
                {
                    var monday = DateOnly.Parse(weeks[i].WeekStart);
                    if (monday.AddDays(6) < from || monday > to || weekStates.ContainsKey(monday))
                    {
                        continue;
                    }

                    var index = i;
                    weekStates[monday] = (
                        StateOf(weeks[i].Status),
                        session,
                        preview.WeekCapacity?.FirstOrDefault(c => c.WeekStart == weeks[index].WeekStart),
                        preview.Unscheduled.Where(u => u.WeekNumber == index).ToList());
                }

                foreach (var day in preview.Days ?? Array.Empty<PlanningDayPreview>())
                {
                    var date = DateOnly.Parse(day.Date);
                    if (date >= from && date <= to && weekStates.TryGetValue(PlanningWeekCalendar.MondayOf(date), out var w)
                        && w.Session.Id == session.Id && w.State == WorkspaceWeekStates.Draft)
                    {
                        draftDayMinutes[date] = day.PlannedMinutes;
                    }
                }

                foreach (var slot in preview.Scheduled)
                {
                    var date = DateOnly.Parse(slot.PlannedDate);
                    var monday = PlanningWeekCalendar.MondayOf(date);
                    if (slot.IsFixed || date < from || date > to
                        || !weekStates.TryGetValue(monday, out var w) || w.Session.Id != session.Id
                        || w.State != WorkspaceWeekStates.Draft)
                    {
                        continue;
                    }

                    // W2-BE-d (5) — why the engine moved this visit off its pinned day / time (the preview's own move list)
                    var move = (preview.PinOverflow ?? Array.Empty<PinOverflowPreview>())
                        .FirstOrDefault(p => p.TargetId == slot.TargetId && p.ToDate == slot.PlannedDate);
                    draftVisits.Add((slot, monday.ToString("yyyy-MM-dd"), move?.Reason));
                    draftTargets.Add((slot.AccountId, slot.ContactId, slot.TargetType, slot.TargetId));
                }
            }
        }

        // ── W2-BE-c (C2 + C4) — ONE bulk name read for every card and every unplaced visit: written atoms, draft slots,
        // and the unplaced targets (a doctor's institution from the plan's own selection). ──────────────────────────
        var contactAccount = sessions.SelectMany(s => s.Selection.SelectedContacts)
            .GroupBy(c => c.ContactId).ToDictionary(g => g.Key, g => g.First().AccountId);
        var unplacedAll = weekStates.Values.SelectMany(w => w.Unplaced).ToList();
        Guid? UnplacedContact(UnscheduledPreview u)
            => u.ContactId ?? (string.Equals(u.TargetType, PlannedVisitTargetType.Contact, StringComparison.Ordinal) ? u.TargetId : null);
        Guid? UnplacedAccount(UnscheduledPreview u)
            => VisitTargetNameReader.NamedByInstitution(u.TargetType)
                ? u.TargetId
                : UnplacedContact(u) is { } c && contactAccount.TryGetValue(c, out var acc) ? acc : null;
        var names = await _names.ReadAsync(
            tenantId,
            atoms.Values.Select(a => a.AccountId)
                .Concat(atoms.Values.Where(a => VisitTargetNameReader.NamedByInstitution(a.TargetType)).Select(a => (Guid?)a.TargetId))
                .Concat(draftTargets.Select(t => t.AccountId))
                .Concat(draftTargets.Where(t => VisitTargetNameReader.NamedByInstitution(t.TargetType)).Select(t => (Guid?)t.TargetId))
                .Concat(unplacedAll.Select(UnplacedAccount)),
            draftTargets.Select(t => t.ContactId).Concat(unplacedAll.Select(UnplacedContact)).Concat(atoms.Values.Select(a => a.ContactId)),
            cancellationToken);
        for (var i = 0; i < visits.Count; i++)
        {
            if (visits[i].PlannedVisitId is { } id && atoms.TryGetValue(id, out var atom))
            {
                visits[i] = visits[i] with
                {
                    AccountDisplayName = names.Account(
                        atom.AccountId ?? (VisitTargetNameReader.NamedByInstitution(atom.TargetType) ? atom.TargetId : null))
                };
            }
        }

        if (draftVisits.Count > 0)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var (slot, weekStart, pinMove) in draftVisits)
            {
                var key = $"{weekStart}|{slot.TargetType}|{slot.TargetId}";
                for (var n = 2; !keys.Add(key); n++)
                {
                    key = $"{weekStart}|{slot.TargetType}|{slot.TargetId}#{n}";
                }

                var target = names.For(slot.TargetType, slot.TargetId, slot.AccountId, slot.ContactId);
                visits.Add(new WorkspaceVisitDto(
                    null, key, null, slot.PlannedDate, weekStart, slot.StartTime, slot.EndTime, slot.SequenceOrder,
                    slot.DurationMinutes, slot.TargetType, slot.TargetId, slot.AccountId, slot.ContactId,
                    target.Target ?? slot.ContactDisplayName, target.TargetInactive, resourceId, null,
                    VisitWorkspaceLimits.DraftWorkStatus, null, false, "none", null, null,
                    (slot.ContentItems ?? Array.Empty<VisitContentSequence.VisitContentItem>())
                        .Select(c => new VisitCalendarPlannedContentDto(
                            c.ProductId, c.ProductCode, c.Role, c.JourneyId, c.JourneyCode, c.StageId, c.StageIndex,
                            c.StageCode, c.StageName,
                            c.Steps.Select(s => new VisitCalendarPlannedStepDto(s.Title, s.Type)).ToList(),
                            c.ProductName))
                        .ToList(),
                    PlannedVisitSource.RoutePlan, null, null, null, null, null,
                    slot.IsPinned, PinnedTime: slot.PinnedTime, slot.IsExtra, // W2-BE-b (CT wiring)
                    AccountDisplayName: target.Account,
                    PinMoveReason: pinMove));
            }
        }

        // ── days + weeks ──────────────────────────────────────────────────────────────────────────────────────────
        var dayRows = await _days.ReadAsync(resourceId, from, to, cancellationToken);
        var days = dayRows.Select(d =>
        {
            var planned = draftDayMinutes.TryGetValue(d.Date, out var draft) ? draft : d.PlannedMinutes;
            return new WorkspaceDayDto(
                d.Date.ToString("yyyy-MM-dd"), d.Kind, d.IsHoliday, d.IsHoliday ? d.HolidayName : null, d.CapacityMinutes, planned,
                Math.Max(0, d.CapacityMinutes - planned));
        }).ToList();

        var today = PlanningWeekCalendar.Today(_clock.GetUtcNow());
        var mayApply = _caller.HasPermission(VisitPlanningPermissions.Apply)
                       && _caller.HasPermission(VisitPlanningPermissions.PlannedVisitManage);
        var weeksOut = new List<WorkspaceWeekDto>();
        for (var monday = PlanningWeekCalendar.MondayOf(from); monday <= to; monday = monday.AddDays(7))
        {
            // CT (live E4) — the ISO week number ("44. Hafta"), not the week's index inside the window
            var number = System.Globalization.ISOWeek.GetWeekOfYear(monday.ToDateTime(TimeOnly.MinValue));
            var inWeek = days.Where(d => PlanningWeekCalendar.MondayOf(DateOnly.Parse(d.Date)) == monday).ToList();
            var weekVisits = visits.Count(v => v.WeekStart == monday.ToString("yyyy-MM-dd")
                                               && v.WorkStatus != VisitWorkStatus.Cancelled);
            if (weekStates.TryGetValue(monday, out var w))
            {
                weeksOut.Add(new WorkspaceWeekDto(
                    monday.ToString("yyyy-MM-dd"), number, w.State, w.Session.Id,
                    CanApprove: mayApply && w.State == WorkspaceWeekStates.Draft,
                    CanReopen: mayApply && w.State == WorkspaceWeekStates.Approved,
                    w.Capacity?.CapacityMinutes ?? inWeek.Sum(d => d.CapacityMinutes),
                    w.Capacity?.PlannedMinutes ?? inWeek.Sum(d => d.PlannedMinutes),
                    w.Capacity?.VisitCount ?? weekVisits,
                    w.Unplaced.Count,
                    SessionVersion: w.Session.Version,
                    Unplaced: w.Unplaced
                        .Select(u => new WorkspaceUnplacedDto(
                            u.TargetType, u.TargetId,
                            VisitTargetNameReader.NamedByInstitution(u.TargetType) ? names.Account(u.TargetId) : names.Contact(UnplacedContact(u)),
                            names.Account(UnplacedAccount(u)), u.Reason))
                        .ToList()));
            }
            else
            {
                weeksOut.Add(new WorkspaceWeekDto(
                    monday.ToString("yyyy-MM-dd"), number,
                    monday.AddDays(6) < today ? WorkspaceWeekStates.Past : WorkspaceWeekStates.None, null,
                    false, false, inWeek.Sum(d => d.CapacityMinutes), inWeek.Sum(d => d.PlannedMinutes), weekVisits, 0,
                    SessionVersion: null, Unplaced: Array.Empty<WorkspaceUnplacedDto>()));
            }
        }

        // ── W2-BE-d (2–4) — specialty (code + label), segment badges, institution address on every card ────────────
        var language = GetVisitReasonsHandler.NormalizeLanguage(request.Language);
        Guid? AccountOf(WorkspaceVisitDto v) => v.AccountId ?? (VisitTargetNameReader.NamedByInstitution(v.TargetType) ? v.TargetId : null);
        var labels = new WorkspaceReferenceLabels(_catalog);
        await labels.LoadAsync(ContactReferenceValidation.MedicalSpecialtySet, visits.Any(v => names.ContactSpecialty(v.ContactId) is not null), cancellationToken);
        var accountsOnCards = visits.Select(v => names.AccountTarget(AccountOf(v))).Where(a => a is not null).ToList();
        await labels.LoadAsync(ContactReferenceValidation.CitySet, accountsOnCards.Any(a => !string.IsNullOrWhiteSpace(a!.CityRef)), cancellationToken);
        await labels.LoadAsync(ContactReferenceValidation.DistrictSet, accountsOnCards.Any(a => !string.IsNullOrWhiteSpace(a!.DistrictRef)), cancellationToken);
        var doctorIds = visits.Select(v => v.ContactId).OfType<Guid>().Distinct().ToList();
        var segmentSet = _segments is null || doctorIds.Count == 0
            ? null
            : await _segments.ReadAsync(tenantId, doctorIds, _clock.GetUtcNow(), cancellationToken);
        for (var i = 0; i < visits.Count; i++)
        {
            var v = visits[i];
            var code = names.ContactSpecialty(v.ContactId);
            visits[i] = v with
            {
                SpecialtyCode = code,
                SpecialtyLabel = labels.Label(ContactReferenceValidation.MedicalSpecialtySet, code, language),
                Badges = segmentSet is not null && v.ContactId is { } doctor
                    ? ContactPeriodStatusReader.SegmentBadges(segmentSet, doctor)
                    : Array.Empty<string>(),
                AccountAddress = AddressOf(names.AccountTarget(AccountOf(v)), labels, language)
            };
        }

        // W2-BE-d (1) — the period of the window: the first day that lies in one of the rep's periods.
        string? periodName = null;
        if (_periods is not null && dayRows.FirstOrDefault(d => d.CyclePeriodId is not null)?.CyclePeriodId is { } periodId)
        {
            periodName = (await _periods.GetByIdAsync(periodId, cancellationToken))?.CycleName;
        }

        var ordered = visits
            .OrderBy(v => v.PlannedDate, StringComparer.Ordinal)
            .ThenBy(v => v.SequenceOrder ?? int.MaxValue)
            .ThenBy(v => v.StartTime ?? string.Empty, StringComparer.Ordinal)
            .ToList();

        return Response<WorkspaceCalendarDto>.Success(new WorkspaceCalendarDto(
            from.ToString("yyyy-MM-dd"), to.ToString("yyyy-MM-dd"), resourceId, ordered, weeksOut, days, periodName));
    }

    /// <summary>W2-BE-d (4) — "line · district, city" from the institution record; the district / city are their reference
    /// labels (a code without a published label is left out, never shown raw). Null when there is nothing to show.</summary>
    public static string? AddressOf(NamedTarget? account, WorkspaceReferenceLabels labels, string language)
    {
        if (account is null)
        {
            return null;
        }

        var place = string.Join(", ", new[]
        {
            labels.Label(ContactReferenceValidation.DistrictSet, account.DistrictRef, language),
            labels.Label(ContactReferenceValidation.CitySet, account.CityRef, language)
        }.Where(s => !string.IsNullOrWhiteSpace(s)));
        var parts = new[] { account.AddressLine?.Trim(), place }.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
        return parts.Count == 0 ? null : string.Join(" · ", parts);
    }

    /// <summary>The preview's week status → the workspace state (an empty, not approved week has no plan yet).</summary>
    public static string StateOf(string? previewStatus) => previewStatus switch
    {
        PlanningWeekDisplayStatus.Past => WorkspaceWeekStates.Past,
        PlanningWeekDisplayStatus.Approved => WorkspaceWeekStates.Approved,
        PlanningWeekDisplayStatus.Draft => WorkspaceWeekStates.Draft,
        _ => WorkspaceWeekStates.None
    };
}
