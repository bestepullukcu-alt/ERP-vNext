using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.TimeEntry.Commands;
using Diten.Platform.Application.Features.TimeEntry.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Enums.TimeEntry;
using Diten.Platform.Domain.Repositories;
using MediatR;
using TimeEntryRow = Diten.Platform.Domain.Entities.TimeEntry.TimeEntry;

namespace Diten.Platform.Application.Features.TimeEntry.Handlers.CommandHandlers;

/// <summary>
/// MOD-0280-FU01 §3.2 <c>SaveTimeEntries</c> — the caller's manual rows for one week's open draft, as a whole set.
///
/// <para><b>Order matters.</b> Every check runs before anything is written, and the week row is claimed FIRST with a
/// compare-and-set on its version: two saves of one week cannot both pass, and the loser gets 409 with nothing
/// written (§13 "Concurrency").</para>
///
/// <para>No reason is asked in a draft (D5). A draft of any size is fine; a day above 660 minutes is saved and flagged,
/// a day above 960 is refused (A3).</para>
/// </summary>
public sealed class SaveTimeEntriesHandler : IRequestHandler<SaveTimeEntriesCommand, Response<TimesheetWeekMutationDto>>
{
    private readonly ITimesheetWeekReader _reader;
    private readonly ITimesheetWeekRepository _weeks;
    private readonly ITimeEntryRepository _entries;
    private readonly IWorkCategoryRepository _categories;
    private readonly ITimeEntryTaskGateway _tasks;
    private readonly ICurrentUserContext _currentUser;
    private readonly ITenantContext _tenantContext;

    public SaveTimeEntriesHandler(
        ITimesheetWeekReader reader,
        ITimesheetWeekRepository weeks,
        ITimeEntryRepository entries,
        IWorkCategoryRepository categories,
        ITimeEntryTaskGateway tasks,
        ICurrentUserContext currentUser,
        ITenantContext tenantContext)
    {
        _reader = reader;
        _weeks = weeks;
        _entries = entries;
        _categories = categories;
        _tasks = tasks;
        _currentUser = currentUser;
        _tenantContext = tenantContext;
    }

    public async Task<Response<TimesheetWeekMutationDto>> Handle(SaveTimeEntriesCommand request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!WeekCalendar.TryParse(request.WeekKey, out var monday))
        {
            return Fail("Week key is not an ISO week.", 400, TimeEntryReasonCodes.WeekKeyInvalid, request);
        }

        var userId = _currentUser.UserId;
        var context = await _reader.LoadAsync(userId, monday, ct);

        // ── The week must be open to writes (§12 "any write to a week") ──────────────────────────────────────────
        var refusal = TimesheetRules.WriteRefusal(context);
        if (refusal is not null)
        {
            return Fail("This week is not open for changes.", 409, refusal, request);
        }

        var rows = (request.Request.Entries ?? [])
            .Select(r => r with { CategoryCode = r.CategoryCode?.Trim().ToUpperInvariant(), Note = r.Note?.Trim() })
            .ToList();

        // ── Dates: inside the week, never after the person's local today (D5) ────────────────────────────────────
        if (rows.Any(r => r.LocalDate < context.Monday || r.LocalDate > context.Sunday))
        {
            return Fail("A row's date is outside this week.", 400, TimeEntryReasonCodes.DateOutsideWeek, request);
        }

        if (rows.Any(r => r.LocalDate > context.LocalToday))
        {
            return Fail("Time cannot be recorded for a future day.", 400, TimeEntryReasonCodes.FutureDate, request);
        }

        var open = context.Open;
        List<TimeEntryRow> existing = open is null ? [] : (await _entries.ListByWeekAsync(open.Id, ct)).ToList();

        // ── Targets: the category exists and is active — a row the draft ALREADY holds (same day, same category) may
        //    keep a since-retired category, but it cannot spread to a new day; the task is one the person can read ─
        var categoryRows = rows.Where(r => r.CategoryCode is not null).ToList();
        if (categoryRows.Count > 0)
        {
            var catalogue = (await _categories.ListAsync(ct)).ToDictionary(c => c.Code, StringComparer.Ordinal);
            foreach (var row in categoryRows)
            {
                if (!catalogue.TryGetValue(row.CategoryCode!, out var category))
                {
                    return Fail("Unknown work category.", 400, TimeEntryReasonCodes.TargetInvalid, request);
                }

                var alreadyInDraft = existing.Any(e => e.CategoryCode == row.CategoryCode && e.LocalDate == row.LocalDate);
                if (!category.IsActive && !alreadyInDraft)
                {
                    return Fail("This work category is no longer active.", 400, TimeEntryReasonCodes.CategoryInactive, request);
                }
            }
        }

        var taskIds = rows.Where(r => r.TaskItemId is not null).Select(r => r.TaskItemId!.Value).Distinct().ToList();
        if (taskIds.Count > 0)
        {
            // "Does not exist" and "you cannot read it" are ONE answer (F5): no existence leak through a time row.
            var readable = await _tasks.ReadableTaskIdsAsync(userId, taskIds, ct);
            if (taskIds.Any(id => !readable.Contains(id)))
            {
                return Fail("Unknown task.", 400, TimeEntryReasonCodes.TargetInvalid, request);
            }
        }

        // ── Day totals (A3): the new manual set plus every non-manual row the draft already holds ──────────────────
        var otherSources = existing.Where(e => e.Source != TimeEntrySource.Manual).ToList();
        var dayTotals = rows
            .Select(r => (r.LocalDate, Minutes: r.DurationMinutes))
            .Concat(otherSources.Select(e => (e.LocalDate, Minutes: e.DurationMinutes)))
            .GroupBy(x => x.LocalDate)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Minutes));

        if (dayTotals.Values.Any(total => total > TimeEntryLimits.ImplausibleDayMinutes))
        {
            return Fail("More than 16 hours in one day is not plausible.", 400, TimeEntryReasonCodes.DayImplausible, request);
        }

        // ── Claim the week (compare-and-set), then write the rows ────────────────────────────────────────────────
        var week = open;
        if (week is null)
        {
            if (request.Request.ExpectedVersion != 0)
            {
                return Fail("The week changed meanwhile; reload and retry.", 409, TimeEntryReasonCodes.ConcurrencyConflict, request);
            }

            week = TimesheetRules.NewRevision(context, _tenantContext.TenantId, 1);
            week.TotalMinutes = dayTotals.Values.Sum();
            week.FlaggedDates = TimesheetRules.FlaggedDates(dayTotals);
            if (!await _weeks.TryCreateAsync(week, ct))
            {
                return Fail("The week changed meanwhile; reload and retry.", 409, TimeEntryReasonCodes.ConcurrencyConflict, request);
            }
        }
        else
        {
            if (request.Request.ExpectedVersion != week.Version)
            {
                return Fail("The week changed meanwhile; reload and retry.", 409, TimeEntryReasonCodes.ConcurrencyConflict, request);
            }

            week.TotalMinutes = dayTotals.Values.Sum();
            week.FlaggedDates = TimesheetRules.FlaggedDates(dayTotals);
            if (!await _weeks.UpdateAsync(week, request.Request.ExpectedVersion, ct))
            {
                return Fail("The week changed meanwhile; reload and retry.", 409, TimeEntryReasonCodes.ConcurrencyConflict, request);
            }
        }

        var manual = existing.Where(e => e.Source == TimeEntrySource.Manual)
            .ToDictionary(e => (e.LocalDate, e.TaskItemId, e.CategoryCode));
        var kept = new HashSet<Guid>();

        foreach (var row in rows)
        {
            var key = (row.LocalDate, row.TaskItemId, row.CategoryCode);
            if (manual.TryGetValue(key, out var stored))
            {
                kept.Add(stored.Id);
                if (stored.DurationMinutes != row.DurationMinutes || stored.Note != row.Note)
                {
                    stored.DurationMinutes = row.DurationMinutes;
                    stored.Note = row.Note;
                    stored.UpdatedBy = userId.ToString();
                    await _entries.UpdateAsync(stored, ct);
                }

                continue;
            }

            await _entries.CreateAsync(new TimeEntryRow
            {
                TenantId = _tenantContext.TenantId,
                TimesheetWeekId = week.Id,
                UserId = userId,
                WeekKey = week.WeekKey,
                LocalDate = row.LocalDate,
                DurationMinutes = row.DurationMinutes,
                TaskItemId = row.TaskItemId,
                CategoryCode = row.CategoryCode,
                Source = TimeEntrySource.Manual,
                Note = row.Note,
                CreatedBy = userId.ToString()
            }, ct);
        }

        await _entries.SoftDeleteAsync(manual.Values.Where(e => !kept.Contains(e.Id)).Select(e => e.Id).ToList(), ct);

        return Response<TimesheetWeekMutationDto>.Success(TimesheetRules.ToMutation(week), correlationId: request.CorrelationId);
    }

    private static Response<TimesheetWeekMutationDto> Fail(string message, int status, string code, SaveTimeEntriesCommand request)
        => Response<TimesheetWeekMutationDto>.Fail(message, status, code, request.CorrelationId);
}
