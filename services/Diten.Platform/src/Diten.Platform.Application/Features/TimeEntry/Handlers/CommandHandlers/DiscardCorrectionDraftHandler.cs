using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.TimeEntry.Commands;
using Diten.Platform.Application.Features.TimeEntry.Services;
using Diten.Platform.Domain.Enums.TimeEntry;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.TimeEntry.Handlers.CommandHandlers;

/// <summary>
/// MOD-0280-FU01 §3.2 — drop an unsubmitted correction draft. Soft delete of the draft revision and its rows; the
/// approved revision in force is not read-modified at all, so it simply stays what it was.
/// </summary>
public sealed class DiscardCorrectionDraftHandler : IRequestHandler<DiscardCorrectionDraftCommand, Response<NoContent>>
{
    private readonly ITimesheetWeekReader _reader;
    private readonly ITimesheetWeekRepository _weeks;
    private readonly ITimeEntryRepository _entries;
    private readonly ICurrentUserContext _currentUser;

    public DiscardCorrectionDraftHandler(
        ITimesheetWeekReader reader,
        ITimesheetWeekRepository weeks,
        ITimeEntryRepository entries,
        ICurrentUserContext currentUser)
    {
        _reader = reader;
        _weeks = weeks;
        _entries = entries;
        _currentUser = currentUser;
    }

    public async Task<Response<NoContent>> Handle(DiscardCorrectionDraftCommand request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!WeekCalendar.TryParse(request.WeekKey, out var monday))
        {
            return Response<NoContent>.Fail("Week key is not an ISO week.", 400, TimeEntryReasonCodes.WeekKeyInvalid, request.CorrelationId);
        }

        var context = await _reader.LoadAsync(_currentUser.UserId, monday, ct);
        var draft = context.Open;
        if (draft is null || draft.Status != TimesheetWeekStatus.Draft || draft.CorrectionOfRevision is null)
        {
            return Response<NoContent>.Fail(
                "There is no correction draft to discard.", 404, TimeEntryReasonCodes.CorrectionDraftNotFound, request.CorrelationId);
        }

        var rows = await _entries.ListByWeekAsync(draft.Id, ct);

        draft.IsDeleted = true;
        draft.IsOpen = false;
        draft.UpdatedBy = _currentUser.UserId.ToString();
        if (!await _weeks.UpdateAsync(draft, draft.Version, ct))
        {
            return Response<NoContent>.Fail(
                "The week changed meanwhile; reload and retry.", 409, TimeEntryReasonCodes.ConcurrencyConflict, request.CorrelationId);
        }

        await _entries.SoftDeleteAsync(rows.Select(r => r.Id).ToList(), ct);
        return Response<NoContent>.Success(204, request.CorrelationId);
    }
}
