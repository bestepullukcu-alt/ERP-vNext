using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.TimeEntry.Commands;
using Diten.Platform.Application.Features.TimeEntry.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Enums.TimeEntry;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;
using TimeEntryRow = Diten.Platform.Domain.Entities.TimeEntry.TimeEntry;

namespace Diten.Platform.Application.Features.TimeEntry.Handlers.CommandHandlers;

/// <summary>
/// MOD-0280-FU01 §3.2 <c>SaveTimeEntries</c> — the caller's own rows of one week's open draft.
///
/// <para><b>Every row names its source</b> (v2 F1): a row without one is refused, so a timer row sent back without its
/// source can never be counted a second time as a Manual row.</para>
/// <list type="bullet">
/// <item><b>Manual and Plan</b> rows are the person's own set: sent = kept (created or changed), left out = removed (soft
/// delete). A row accepted from "fill from plan" (D9) keeps <c>Plan</c>.</item>
/// <item><b>Timer and Meeting</b> rows are captured, and the person CORRECTS them (Z-2: "the timer's output is raw, the
/// person confirms it"): a sent row must match one the draft already holds (same day, target and — for a meeting — the
/// meeting) and may change its minutes and note; its source stays, and a changed duration marks it
/// <c>EditedFromTimer</c> so the timer recomputation leaves the person's number alone. A captured row cannot be created
/// here, and one left out stays as it is.</item>
/// </list>
///
/// <para><b>Order matters.</b> The week and the rows are checked first; then the week's timer drafts are recomputed from
/// the segments (v2 F5 — a draft a failed close never wrote is written now; v3 G4 — a failure there is logged, never a
/// 500); then the captured rows are matched against the fresh draft, and the week row is claimed with a compare-and-set
/// on its version: two saves of one week cannot both pass (§13 "Concurrency"). Nothing is written before the claim.</para>
///
/// <para><b>Day limits (A3), on the days this save CHANGES</b> (v2 F1): above 960 minutes is refused, above 660 flagged. A
/// day the save does not touch is never the reason a save fails — a forgotten 17-hour timer day blocks the SUBMIT (which
/// checks the whole week) until the person corrects it, not every unrelated save.</para>
/// </summary>
public sealed class SaveTimeEntriesHandler : IRequestHandler<SaveTimeEntriesCommand, Response<TimesheetWeekMutationDto>>
{
    private readonly ITimesheetWeekReader _reader;
    private readonly ITimesheetWeekRepository _weeks;
    private readonly ITimeEntryRepository _entries;
    private readonly IWorkCategoryRepository _categories;
    private readonly ITimeEntryTaskGateway _tasks;
    private readonly ITimerDraftWriter _drafts;
    private readonly ICurrentUserContext _currentUser;
    private readonly ITenantContext _tenantContext;
    private readonly IMediator _mediator;
    private readonly ILogger<SaveTimeEntriesHandler> _logger;

    public SaveTimeEntriesHandler(
        ITimesheetWeekReader reader,
        ITimesheetWeekRepository weeks,
        ITimeEntryRepository entries,
        IWorkCategoryRepository categories,
        ITimeEntryTaskGateway tasks,
        ITimerDraftWriter drafts,
        ICurrentUserContext currentUser,
        ITenantContext tenantContext,
        IMediator mediator,
        ILogger<SaveTimeEntriesHandler> logger)
    {
        _mediator = mediator;
        _logger = logger;
        _reader = reader;
        _weeks = weeks;
        _entries = entries;
        _categories = categories;
        _tasks = tasks;
        _drafts = drafts;
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
            .Select(r => r with { CategoryCode = r.CategoryCode?.Trim().ToUpperInvariant(), Note = NormalizeNote(r.Note) })
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

        var typed = rows.Where(r => IsPersonTyped(SourceOf(r))).ToList();
        var captured = rows.Where(r => !IsPersonTyped(SourceOf(r))).ToList();

        // ── Targets of the person's own rows: the category exists and is active — a row the draft ALREADY holds (same
        //    day, same category) may keep a since-retired category, but it cannot spread to a new day; the task is one
        //    the person can read ─────────────────────────────────────────────────────────────────────────────────
        var categoryRows = typed.Where(r => r.CategoryCode is not null).ToList();
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

        var taskIds = typed.Where(r => r.TaskItemId is not null).Select(r => r.TaskItemId!.Value).Distinct().ToList();
        if (taskIds.Count > 0)
        {
            // "Does not exist" and "you cannot read it" are ONE answer (F5): no existence leak through a time row.
            var readable = await _tasks.ReadableTaskIdsAsync(userId, taskIds, ct);
            if (taskIds.Any(id => !readable.Contains(id)))
            {
                return Fail("Unknown task.", 400, TimeEntryReasonCodes.TargetInvalid, request);
            }
        }

        // ── v2 F5 / v3 G4 — the timer drafts, recomputed from the segments AFTER the week and the rows passed their
        //    checks. A failure here is logged and the save goes on: the recomputation is idempotent, and the next save,
        //    submit or midnight run writes what this one could not. If it moved the week, the version the person read is
        //    stale and the claim below answers 409 — they reload and see the timer rows they were missing ──────────
        try
        {
            await _drafts.ApplyWeekAsync(userId, context.WeekKey, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "time-entry.save.draft_recompute_failed WeekKey={WeekKey}; the save goes on.", context.WeekKey);
        }

        context = await _reader.LoadAsync(userId, monday, ct);
        refusal = TimesheetRules.WriteRefusal(context);
        if (refusal is not null)
        {
            return Fail("This week is not open for changes.", 409, refusal, request);
        }

        open = context.Open;
        existing = open is null ? [] : (await _entries.ListByWeekAsync(open.Id, ct)).ToList();

        // ── Captured rows (Timer, Meeting): only ever a correction of a row the draft already holds. One sent back
        //    exactly as stored (minutes and note) is not a change and is left out entirely (v3 G3) — a 17-hour timer
        //    row the person did not touch never fails the save ─────────────────────────────────────────────────────
        var capturedMatches = new List<(TimeEntryRowRequest Row, TimeEntryRow Stored)>();
        foreach (var row in captured)
        {
            var source = SourceOf(row);
            var stored = existing.FirstOrDefault(e => e.Source == source && e.LocalDate == row.LocalDate
                                                      && e.TaskItemId == row.TaskItemId && e.CategoryCode == row.CategoryCode
                                                      && (source != TimeEntrySource.Meeting || e.SourceRef == row.SourceRef));
            if (stored is null)
            {
                return Fail("A timer or meeting row can only be corrected, not created.", 400,
                    TimeEntryReasonCodes.CapturedRowNotFound, request);
            }

            if (stored.DurationMinutes == row.DurationMinutes && NormalizeNote(stored.Note) == row.Note)
            {
                continue; // untouched
            }

            if (!IsValidStep(row.DurationMinutes))
            {
                return Fail("Durations are whole 15-minute steps between 15 and 960 minutes.", 400,
                    TimeEntryReasonCodes.StepInvalid, request);
            }

            capturedMatches.Add((row, stored));
        }

        // ── The week as it will be, and the days this save changes ───────────────────────────────────────────────
        var personTyped = existing.Where(e => IsPersonTyped(e.Source))
            .GroupBy(e => (e.LocalDate, e.TaskItemId, e.CategoryCode))
            .ToDictionary(g => g.Key, g => g.First());
        var typedKeys = typed.Select(r => (r.LocalDate, r.TaskItemId, r.CategoryCode)).ToHashSet();
        var capturedEdits = capturedMatches.ToDictionary(m => m.Stored.Id, m => m.Row);

        var after = new List<(DateOnly Date, int Minutes)>();
        var changedDays = new HashSet<DateOnly>();
        foreach (var stored in existing)
        {
            if (IsPersonTyped(stored.Source))
            {
                if (!typedKeys.Contains((stored.LocalDate, stored.TaskItemId, stored.CategoryCode)))
                {
                    changedDays.Add(stored.LocalDate); // removed
                }

                continue; // its replacement (if any) is counted from the request below
            }

            if (capturedEdits.TryGetValue(stored.Id, out var edit))
            {
                after.Add((stored.LocalDate, edit.DurationMinutes));
                changedDays.Add(stored.LocalDate); // only real changes are in capturedEdits (G3)
            }
            else
            {
                after.Add((stored.LocalDate, stored.DurationMinutes));
            }
        }

        foreach (var row in typed)
        {
            after.Add((row.LocalDate, row.DurationMinutes));
            if (!personTyped.TryGetValue((row.LocalDate, row.TaskItemId, row.CategoryCode), out var stored)
                || stored.DurationMinutes != row.DurationMinutes || NormalizeNote(stored.Note) != row.Note
                || stored.Source != SourceOf(row))
            {
                changedDays.Add(row.LocalDate);
            }
        }

        var dayTotals = after.GroupBy(x => x.Date).ToDictionary(g => g.Key, g => g.Sum(x => x.Minutes));
        if (changedDays.Any(day => dayTotals.GetValueOrDefault(day) > TimeEntryLimits.ImplausibleDayMinutes))
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

        // Captured rows: the person's correction, one audited command each (v3 G2 — before/after on the week's trail).
        // The source stays; a changed duration marks the row as theirs and records the timer baseline (v3 G1).
        foreach (var (row, stored) in capturedMatches)
        {
            var corrected = await _mediator.Send(new CorrectCapturedTimeEntryCommand(
                week.Id, stored.Id, stored.Source.ToString(), stored.DurationMinutes, row.DurationMinutes,
                NormalizeNote(stored.Note) != row.Note, row.Note, request.CorrelationId), ct);
            if (!corrected.IsSuccessful)
            {
                return Fail("The week changed meanwhile; reload and retry.", 409, TimeEntryReasonCodes.ConcurrencyConflict, request);
            }
        }

        // The person's own rows: the whole set.
        var kept = new HashSet<Guid>();
        foreach (var row in typed)
        {
            var key = (row.LocalDate, row.TaskItemId, row.CategoryCode);
            var source = SourceOf(row);
            if (personTyped.TryGetValue(key, out var stored))
            {
                kept.Add(stored.Id);
                if (stored.DurationMinutes != row.DurationMinutes || stored.Note != row.Note || stored.Source != source)
                {
                    stored.DurationMinutes = row.DurationMinutes;
                    stored.Note = row.Note;
                    stored.Source = source;
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
                Source = source,
                Note = row.Note,
                CreatedBy = userId.ToString()
            }, ct);
        }

        await _entries.SoftDeleteAsync(personTyped.Values.Where(e => !kept.Contains(e.Id)).Select(e => e.Id).ToList(), ct);

        return Response<TimesheetWeekMutationDto>.Success(TimesheetRules.ToMutation(week), correlationId: request.CorrelationId);
    }

    /// <summary>A note as stored and compared: trimmed, and blank is no note (v3 G3: <c>""</c> = null).</summary>
    private static string? NormalizeNote(string? note) => string.IsNullOrWhiteSpace(note) ? null : note.Trim();

    private static bool IsValidStep(int minutes)
        => minutes >= TimeEntryLimits.StepMinutes && minutes <= TimeEntryLimits.MaxRowMinutes
           && minutes % TimeEntryLimits.StepMinutes == 0;

    /// <summary>The rows the person writes themselves — typed, or accepted from the plan (D9).</summary>
    private static bool IsPersonTyped(TimeEntrySource source) => source is TimeEntrySource.Manual or TimeEntrySource.Plan;

    /// <summary>The row's declared source. The validator guarantees one of the four names is present.</summary>
    private static TimeEntrySource SourceOf(TimeEntryRowRequest row)
        => Enum.Parse<TimeEntrySource>(row.Source!.Trim(), ignoreCase: true);

    private static Response<TimesheetWeekMutationDto> Fail(string message, int status, string code, SaveTimeEntriesCommand request)
        => Response<TimesheetWeekMutationDto>.Fail(message, status, code, request.CorrelationId);
}
