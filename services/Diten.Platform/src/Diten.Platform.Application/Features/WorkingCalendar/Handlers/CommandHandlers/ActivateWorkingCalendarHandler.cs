using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.WorkingCalendar.Commands;
using Diten.Platform.Domain.Entities.WorkingCalendar;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.WorkingCalendar.Handlers.CommandHandlers;

public sealed class ActivateWorkingCalendarHandler
    : IRequestHandler<ActivateWorkingCalendarCommand, Response<NoContent>>
{
    private readonly IWorkingCalendarRepository _repository;
    private readonly ICurrentUserContext _currentUser;

    public ActivateWorkingCalendarHandler(IWorkingCalendarRepository repository, ICurrentUserContext currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<Response<NoContent>> Handle(ActivateWorkingCalendarCommand request, CancellationToken ct)
    {
        var (calendar, error, status) = await WorkingCalendarWriteGuard.LoadWritableAsync(
            _repository, request.Id, request.IsPlatformActor, ct);
        if (calendar is null)
        {
            return Response<NoContent>.Fail(error!, status);
        }

        if (calendar.IsActive())
        {
            return Response<NoContent>.Success(204);
        }

        // A country calendar with no weekend would silently turn every day into a working day.
        if (calendar.IsCountryLayer && (calendar.WeekendDays is null || calendar.WeekendDays.Count == 0))
        {
            return Response<NoContent>.Fail("A country calendar must declare its weekend days before activation.", 400);
        }

        // Exactly one active calendar per scope is what keeps the provider deterministic.
        if (await _repository.ExistsActiveAsync(
                calendar.TenantId, calendar.CountryCode, calendar.CalendarYear, calendar.OrganizationUnitId,
                calendar.Id, ct, calendar.LegalEntityId))
        {
            return Response<NoContent>.Fail(
                $"An active calendar already exists for {calendar.CountryCode} {calendar.CalendarYear} in this scope. " +
                "Archive it before activating another.", 409);
        }

        calendar.CalendarStatus = WorkingCalendarStatus.Active;
        calendar.ActivatedAt = DateTimeOffset.UtcNow;
        calendar.ActivatedBy = _currentUser.ActorName;
        calendar.UpdatedBy = _currentUser.ActorName;

        return await WorkingCalendarWriteGuard.ReplaceAsync(_repository, calendar, request.ExpectedVersion, ct);
    }
}
