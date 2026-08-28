using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.WorkingCalendar.Commands;
using Diten.Platform.Domain.Entities.WorkingCalendar;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.WorkingCalendar.Handlers.CommandHandlers;

/// <summary>Archive is how a calendar is closed. There is no delete endpoint anywhere in this module, so a
/// superseded calendar stays readable as history rather than disappearing from the audit trail.</summary>
public sealed class ArchiveWorkingCalendarHandler
    : IRequestHandler<ArchiveWorkingCalendarCommand, Response<NoContent>>
{
    private readonly IWorkingCalendarRepository _repository;
    private readonly ICurrentUserContext _currentUser;

    public ArchiveWorkingCalendarHandler(IWorkingCalendarRepository repository, ICurrentUserContext currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<Response<NoContent>> Handle(ArchiveWorkingCalendarCommand request, CancellationToken ct)
    {
        var (calendar, error, status) = await WorkingCalendarWriteGuard.LoadWritableAsync(
            _repository, request.Id, request.IsPlatformActor, ct);
        if (calendar is null)
        {
            return Response<NoContent>.Fail(error!, status);
        }

        calendar.CalendarStatus = WorkingCalendarStatus.Archived;
        calendar.ArchivedAt = DateTimeOffset.UtcNow;
        calendar.ArchivedBy = _currentUser.ActorName;
        calendar.UpdatedBy = _currentUser.ActorName;

        return await WorkingCalendarWriteGuard.ReplaceAsync(_repository, calendar, request.ExpectedVersion, ct);
    }
}
