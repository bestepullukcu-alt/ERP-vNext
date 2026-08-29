using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.WorkingCalendar.Commands;
using Diten.Platform.Domain.Entities.WorkingCalendar;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.WorkingCalendar.Handlers.CommandHandlers;

/// <summary>
/// Adds or edits one embedded day. The ENTIRE aggregate is replaced under its version token — deliberately not a
/// positional array update — so days and the calendar root can never end up with two different concurrency
/// behaviours, and there is only ever one write path to reason about.
/// </summary>
public sealed class UpsertWorkingCalendarDayHandler
    : IRequestHandler<UpsertWorkingCalendarDayCommand, Response<Guid>>
{
    private readonly IWorkingCalendarRepository _repository;
    private readonly ICurrentUserContext _currentUser;

    public UpsertWorkingCalendarDayHandler(IWorkingCalendarRepository repository, ICurrentUserContext currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<Response<Guid>> Handle(UpsertWorkingCalendarDayCommand request, CancellationToken ct)
    {
        var (calendar, error, status) = await WorkingCalendarWriteGuard.LoadWritableAsync(
            _repository, request.CalendarId, request.IsPlatformActor, ct);
        if (calendar is null)
        {
            return Response<Guid>.Fail(error!, status);
        }

        var input = request.Day;
        var existing = input.DayId is { } dayId && dayId != Guid.Empty
            ? calendar.Days.FirstOrDefault(d => d.DayId == dayId)
            : null;

        if (input.DayId is { } requestedId && requestedId != Guid.Empty && existing is null)
        {
            return Response<Guid>.Fail("The referenced day was not found in this calendar.", 404);
        }

        // Uniqueness of day code and effective date has no DB backstop (an in-array unique index is not expressible
        // in MongoDB), so this validator is the only line of defence.
        var guard = WorkingCalendarValidation.ValidateDayInput(calendar, input, existing?.DayId);
        if (!guard.Ok)
        {
            return Response<Guid>.Fail(guard.Message!, guard.StatusCode);
        }

        var now = DateTimeOffset.UtcNow;

        if (existing is null)
        {
            var day = new WorkingCalendarDay
            {
                DayCode = input.DayCode.Trim(),
                DayName = input.DayName.Trim(),
                Date = input.Date,
                ObservedDate = input.ObservedDate,
                DayType = input.DayType,
                Recurrence = input.Recurrence,
                IsHalfDay = input.IsHalfDay,
                Notes = input.Notes?.Trim(),
                CreatedBy = _currentUser.ActorName
            };

            calendar.Days.Add(day);
            calendar.UpdatedBy = _currentUser.ActorName;

            var created = await _repository.ReplaceAsync(calendar, request.ExpectedVersion, ct);
            return created
                ? Response<Guid>.Success(day.DayId, 201)
                : Response<Guid>.Fail("The calendar changed since it was loaded. Reload and reapply the change.", 409);
        }

        existing.DayCode = input.DayCode.Trim();
        existing.DayName = input.DayName.Trim();
        existing.Date = input.Date;
        existing.ObservedDate = input.ObservedDate;
        existing.DayType = input.DayType;
        existing.Recurrence = input.Recurrence;
        existing.IsHalfDay = input.IsHalfDay;
        existing.Notes = input.Notes?.Trim();
        existing.UpdatedAt = now;
        existing.UpdatedBy = _currentUser.ActorName;
        calendar.UpdatedBy = _currentUser.ActorName;

        var replaced = await _repository.ReplaceAsync(calendar, request.ExpectedVersion, ct);
        return replaced
            ? Response<Guid>.Success(existing.DayId, 200)
            : Response<Guid>.Fail("The calendar changed since it was loaded. Reload and reapply the change.", 409);
    }
}
