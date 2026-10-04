using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.TimeEntry.Commands;
using Diten.Platform.Domain.Enums.TimeEntry;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.TimeEntry.Handlers.CommandHandlers;

/// <summary>
/// MOD-0280-FU01 v3 G1/G2 — writes one captured row's correction. The first correction keeps the captured value
/// (<c>CapturedMinutes</c>) for the approver; a timer row also records the person's value and the day's timer seconds at
/// this moment (the baseline), so timer time that arrives later is added on top instead of being lost.
/// </summary>
public sealed class CorrectCapturedTimeEntryHandler : IRequestHandler<CorrectCapturedTimeEntryCommand, Response<Guid>>
{
    private readonly ITimeEntryRepository _entries;
    private readonly ITimerSegmentRepository _segments;

    public CorrectCapturedTimeEntryHandler(ITimeEntryRepository entries, ITimerSegmentRepository segments)
    {
        _entries = entries;
        _segments = segments;
    }

    public async Task<Response<Guid>> Handle(CorrectCapturedTimeEntryCommand request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var row = (await _entries.ListByWeekAsync(request.WeekId, ct)).FirstOrDefault(e => e.Id == request.EntryId);
        if (row is null)
        {
            return Response<Guid>.Fail("Row not found.", 404, TimeEntryReasonCodes.CapturedRowNotFound, request.CorrelationId);
        }

        if (row.DurationMinutes != request.AfterMinutes)
        {
            row.CapturedMinutes ??= row.DurationMinutes;
            row.EditedFromTimer = true;
            if (row.Source == TimeEntrySource.Timer)
            {
                row.CorrectedMinutes = request.AfterMinutes;
                row.CorrectionBaselineSeconds = (await _segments.ListClosedForDayAsync(row.UserId, row.LocalDate, ct))
                    .Where(s => s.TaskItemId == row.TaskItemId && s.CategoryCode == row.CategoryCode)
                    .Sum(s => (long)s.DurationSeconds);
            }

            row.DurationMinutes = request.AfterMinutes;
        }

        row.Note = request.Note;
        row.UpdatedBy = row.UserId.ToString();
        if (!await _entries.UpdateAsync(row, row.Version, ct))
        {
            return Response<Guid>.Fail(
                "The week changed meanwhile; reload and retry.", 409, TimeEntryReasonCodes.ConcurrencyConflict, request.CorrelationId);
        }

        return Response<Guid>.Success(row.Id, correlationId: request.CorrelationId);
    }
}
