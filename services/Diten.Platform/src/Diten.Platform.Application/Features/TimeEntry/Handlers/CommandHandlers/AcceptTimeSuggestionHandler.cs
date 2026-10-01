using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.TimeEntry.Commands;
using Diten.Platform.Application.Features.TimeEntry.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.TimeEntry;
using Diten.Platform.Domain.Enums.TimeEntry;
using Diten.Platform.Domain.Repositories;
using MediatR;
using TimeEntryRow = Diten.Platform.Domain.Entities.TimeEntry.TimeEntry;

namespace Diten.Platform.Application.Features.TimeEntry.Handlers.CommandHandlers;

/// <summary>
/// MOD-0280-FU01 D8 — "I attended". The only way a meeting becomes time: the person's own act (no entry is created from a
/// meeting without it). The proposed minutes land as a <see cref="TimeEntrySource.Meeting"/> draft row in the week's open
/// draft, under the same write rules as a manual save (open Draft, edit window, 16-hour day, expected version).
///
/// <para><b>Order.</b> The week is claimed first (compare-and-set), then the decision (the (meeting, person) unique index
/// refuses a second one), then the row. A loser at either step writes no time.</para>
///
/// <para><b>One row per meeting</b> (v2 F8): <see cref="TimeEntryRow.SourceRef"/> is the meeting id and part of the entries'
/// unique key, so two meetings on one day are two rows, and a minutes conflict flags only the meeting it concerns.</para>
/// </summary>
public sealed class AcceptTimeSuggestionHandler : IRequestHandler<AcceptTimeSuggestionCommand, Response<TimeSuggestionMutationDto>>
{
    private readonly ITimesheetWeekReader _reader;
    private readonly ITimeSuggestionReader _suggestions;
    private readonly ITimeSuggestionRepository _decisions;
    private readonly ITimesheetWeekRepository _weeks;
    private readonly ITimeEntryRepository _entries;
    private readonly IWorkCategoryRepository _categories;
    private readonly ITimeEntryTaskGateway _tasks;
    private readonly ICurrentUserContext _currentUser;
    private readonly ITenantContext _tenantContext;
    private readonly TimeProvider _clock;

    public AcceptTimeSuggestionHandler(
        ITimesheetWeekReader reader,
        ITimeSuggestionReader suggestions,
        ITimeSuggestionRepository decisions,
        ITimesheetWeekRepository weeks,
        ITimeEntryRepository entries,
        IWorkCategoryRepository categories,
        ITimeEntryTaskGateway tasks,
        ICurrentUserContext currentUser,
        ITenantContext tenantContext,
        TimeProvider clock)
    {
        _reader = reader;
        _suggestions = suggestions;
        _decisions = decisions;
        _weeks = weeks;
        _entries = entries;
        _categories = categories;
        _tasks = tasks;
        _currentUser = currentUser;
        _tenantContext = tenantContext;
        _clock = clock;
    }

    public async Task<Response<TimeSuggestionMutationDto>> Handle(AcceptTimeSuggestionCommand request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!WeekCalendar.TryParse(request.WeekKey, out var monday))
        {
            return Fail("Week key is not an ISO week.", 400, TimeEntryReasonCodes.WeekKeyInvalid, request);
        }

        var userId = _currentUser.UserId;
        var context = await _reader.LoadAsync(userId, monday, ct);
        var suggestion = (await _suggestions.ListForWeekAsync(context, ct)).FirstOrDefault(s => s.Id == request.SuggestionId);
        if (suggestion is null)
        {
            return Fail("Suggestion not found.", 404, TimeEntryReasonCodes.SuggestionNotFound, request);
        }

        if (suggestion.Decision is not null)
        {
            return Fail("This suggestion was already decided.", 409, TimeEntryReasonCodes.SuggestionAlreadyDecided, request);
        }

        if (suggestion.MinutesStatus == TimeSuggestionMinutesStatus.Withdrawn)
        {
            return Fail("The minutes record you as not present.", 409, TimeEntryReasonCodes.SuggestionWithdrawn, request);
        }

        var refusal = TimesheetRules.WriteRefusal(context);
        if (refusal is not null)
        {
            return Fail("This week is not open for changes.", 409, refusal, request);
        }

        // ── Target: the named task (readable) or category (active); by default the meeting category ──────────────
        Guid? taskId = request.Request.TaskItemId is { } t && t != Guid.Empty ? t : null;
        string? category = null;
        if (taskId is { } task)
        {
            if (!(await _tasks.ReadableTaskIdsAsync(userId, [task], ct)).Contains(task))
            {
                return Fail("Unknown task.", 400, TimeEntryReasonCodes.TargetInvalid, request);
            }
        }
        else
        {
            category = (string.IsNullOrWhiteSpace(request.Request.CategoryCode)
                ? AcceptTimeSuggestionRequest.DefaultCategoryCode
                : request.Request.CategoryCode).Trim().ToUpperInvariant();
            var stored = await _categories.GetByCodeAsync(category, ct);
            if (stored is null)
            {
                return Fail("Unknown work category.", 400, TimeEntryReasonCodes.TargetInvalid, request);
            }

            if (!stored.IsActive)
            {
                return Fail("This work category is no longer active.", 400, TimeEntryReasonCodes.CategoryInactive, request);
            }
        }

        var week = context.Open;
        var rows = week is null ? [] : (await _entries.ListByWeekAsync(week.Id, ct)).ToList();
        var dayTotal = rows.Where(r => r.LocalDate == suggestion.LocalDate).Sum(r => r.DurationMinutes) + suggestion.ProposedMinutes;
        if (dayTotal > TimeEntryLimits.ImplausibleDayMinutes)
        {
            return Fail("More than 16 hours in one day is not plausible.", 400, TimeEntryReasonCodes.DayImplausible, request);
        }

        // ── Claim the week (compare-and-set, the T1a save pattern): a new week is created WITH its totals; an existing
        //    one is replaced conditionally on the version the person read ─────────────────────────────────────────
        var entryId = Guid.NewGuid();
        var after = rows.Append(new TimeEntryRow
        {
            TenantId = _tenantContext.TenantId, TimesheetWeekId = week?.Id ?? Guid.Empty, UserId = userId,
            WeekKey = context.WeekKey, LocalDate = suggestion.LocalDate, DurationMinutes = suggestion.ProposedMinutes
        });
        var dayTotals = TimesheetRules.DayTotals(after);

        if (week is null)
        {
            if (request.Request.ExpectedVersion != 0)
            {
                return Conflict(request);
            }

            week = TimesheetRules.NewRevision(context, _tenantContext.TenantId, 1);
            week.TotalMinutes = dayTotals.Values.Sum();
            week.FlaggedDates = TimesheetRules.FlaggedDates(dayTotals);
            if (!await _weeks.TryCreateAsync(week, ct))
            {
                return Conflict(request);
            }
        }
        else
        {
            if (request.Request.ExpectedVersion != week.Version)
            {
                return Conflict(request);
            }

            week.TotalMinutes = dayTotals.Values.Sum();
            week.FlaggedDates = TimesheetRules.FlaggedDates(dayTotals);
            if (!await _weeks.UpdateAsync(week, request.Request.ExpectedVersion, ct))
            {
                return Conflict(request);
            }
        }

        // ── The decision (unique per meeting + person), then the row — ONE row per meeting (v2 F8): its SourceRef is
        //    the meeting id and is part of the entries' unique key, so each meeting keeps its own minutes and flag ──
        if (!await _decisions.TryCreateAsync(new TimeSuggestion
            {
                Id = suggestion.Id,
                TenantId = _tenantContext.TenantId,
                UserId = userId,
                MeetingId = suggestion.Invitation.MeetingId,
                LocalDate = suggestion.LocalDate,
                ProposedMinutes = suggestion.ProposedMinutes,
                State = TimeSuggestionState.Accepted,
                AcceptedEntryId = entryId,
                DecidedAtUtc = _clock.GetUtcNow(),
                CreatedBy = userId.ToString()
            }, ct))
        {
            return Fail("This suggestion was already decided.", 409, TimeEntryReasonCodes.SuggestionAlreadyDecided, request);
        }

        await _entries.CreateAsync(new TimeEntryRow
        {
            Id = entryId,
            TenantId = _tenantContext.TenantId,
            TimesheetWeekId = week.Id,
            UserId = userId,
            WeekKey = week.WeekKey,
            LocalDate = suggestion.LocalDate,
            DurationMinutes = suggestion.ProposedMinutes,
            TaskItemId = taskId,
            CategoryCode = category,
            Source = TimeEntrySource.Meeting,
            SourceRef = suggestion.Invitation.MeetingId.ToString(),
            CreatedBy = userId.ToString()
        }, ct);

        return Response<TimeSuggestionMutationDto>.Success(
            new TimeSuggestionMutationDto(suggestion.Id, nameof(TimeSuggestionState.Accepted), entryId, week.Version),
            correlationId: request.CorrelationId);
    }

    private static Response<TimeSuggestionMutationDto> Conflict(AcceptTimeSuggestionCommand request)
        => Fail("The week changed meanwhile; reload and retry.", 409, TimeEntryReasonCodes.ConcurrencyConflict, request);

    private static Response<TimeSuggestionMutationDto> Fail(string message, int status, string code, AcceptTimeSuggestionCommand request)
        => Response<TimeSuggestionMutationDto>.Fail(message, status, code, request.CorrelationId);
}
