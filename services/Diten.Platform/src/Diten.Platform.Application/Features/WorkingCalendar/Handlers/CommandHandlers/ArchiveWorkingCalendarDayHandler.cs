using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.WorkingCalendar.Commands;
using Diten.Platform.Domain.Entities.WorkingCalendar;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.WorkingCalendar.Handlers.CommandHandlers;

/// <summary>Archives one day. The provider stops considering it immediately, but it stays in the document so the
/// history of "this used to be a holiday" is not silently rewritten.</summary>
public sealed class ArchiveWorkingCalendarDayHandler
    : IRequestHandler<ArchiveWorkingCalendarDayCommand, Response<NoContent>>
{
    private readonly IWorkingCalendarRepository _repository;
    private readonly ICurrentUserContext _currentUser;

    public ArchiveWorkingCalendarDayHandler(IWorkingCalendarRepository repository, ICurrentUserContext currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<Response<NoContent>> Handle(ArchiveWorkingCalendarDayCommand request, CancellationToken ct)
    {
        var (calendar, error, status) = await WorkingCalendarWriteGuard.LoadWritableAsync(
            _repository, request.CalendarId, request.IsPlatformActor, ct);
        if (calendar is null)
        {
            return Response<NoContent>.Fail(error!, status);
        }

        var day = calendar.Days.FirstOrDefault(d => d.DayId == request.DayId);
        if (day is null)
        {
            return Response<NoContent>.Fail("The referenced day was not found in this calendar.", 404);
        }

        if (string.Equals(day.DayStatus, WorkingCalendarDayStatus.Archived, StringComparison.Ordinal))
        {
            return Response<NoContent>.Success(204);
        }

        day.DayStatus = WorkingCalendarDayStatus.Archived;
        day.ArchivedAt = DateTimeOffset.UtcNow;
        day.ArchivedBy = _currentUser.ActorName;
        calendar.UpdatedBy = _currentUser.ActorName;

        return await WorkingCalendarWriteGuard.ReplaceAsync(_repository, calendar, request.ExpectedVersion, ct);
    }
}
