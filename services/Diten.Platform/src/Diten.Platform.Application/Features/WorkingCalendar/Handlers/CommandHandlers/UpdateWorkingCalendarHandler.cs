using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.WorkingCalendar.Commands;
using Diten.Platform.Application.Features.WorkingCalendar.Services;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.WorkingCalendar.Handlers.CommandHandlers;

public sealed class UpdateWorkingCalendarHandler
    : IRequestHandler<UpdateWorkingCalendarCommand, Response<NoContent>>
{
    private readonly IWorkingCalendarRepository _repository;
    private readonly ICurrentUserContext _currentUser;
    private readonly IWorkingCalendarLegalEntityValidator _legalEntities;

    public UpdateWorkingCalendarHandler(
        IWorkingCalendarRepository repository,
        ICurrentUserContext currentUser,
        IWorkingCalendarLegalEntityValidator legalEntities)
    {
        _repository = repository;
        _currentUser = currentUser;
        _legalEntities = legalEntities;
    }

    public async Task<Response<NoContent>> Handle(UpdateWorkingCalendarCommand request, CancellationToken ct)
    {
        var (calendar, error, status) = await WorkingCalendarWriteGuard.LoadWritableAsync(
            _repository, request.Id, request.IsPlatformActor, ct);
        if (calendar is null)
        {
            return Response<NoContent>.Fail(error!, status);
        }

        var countryCode = (request.CountryCode ?? string.Empty).Trim().ToUpperInvariant();
        var scopeType = (request.ScopeType ?? string.Empty).Trim();

        // Identity freezes on activation, content does not: official holidays get declared and shifted mid-year, so a
        // fully frozen active calendar would be unable to follow reality.
        var frozen = WorkingCalendarValidation.ValidateIdentityNotFrozen(
            calendar, countryCode, request.CalendarYear, scopeType, request.OrganizationUnitId, request.LegalEntityId);
        if (!frozen.Ok)
        {
            return Response<NoContent>.Fail(frozen.Message!, frozen.StatusCode);
        }

        // Scope can never be re-pointed: promoting a tenant row to the country layer would publish it to every tenant.
        if (!string.Equals(calendar.ScopeType, scopeType, StringComparison.Ordinal))
        {
            return Response<NoContent>.Fail(
                "ScopeType cannot be changed after creation; create a calendar in the target scope instead.", 409);
        }

        var scopeGuard = WorkingCalendarValidation.ValidateScope(
            scopeType, _repository.CurrentTenantId, request.IsPlatformActor,
            request.OrganizationUnitId, request.LegalEntityId);
        if (!scopeGuard.Ok)
        {
            return Response<NoContent>.Fail(scopeGuard.Message!, scopeGuard.StatusCode, scopeGuard.ReasonCode);
        }

        // Both scope foreign keys are immutable. This also blocks stale/hidden form values from re-pointing a row.
        if (calendar.OrganizationUnitId != request.OrganizationUnitId || calendar.LegalEntityId != request.LegalEntityId)
        {
            return Response<NoContent>.Fail(
                "Scope references cannot be changed after creation; create a calendar in the target scope instead.",
                409, "calendar_scope_reference_immutable");
        }

        if (request.LegalEntityId is { } legalEntityId && legalEntityId != Guid.Empty)
        {
            var validation = await _legalEntities.ValidateAsync(legalEntityId, ct);
            if (validation.DependencyUnavailable)
            {
                return Response<NoContent>.Fail(
                    "Legal entity validation is unavailable.", 503, "legal_entity_validation_unavailable");
            }
            if (!validation.IsReferenceable)
            {
                return Response<NoContent>.Fail(
                    "The referenced legal entity is not referenceable.", 400, "legal_entity_not_referenceable");
            }
        }

        var weekendGuard = WorkingCalendarValidation.ValidateWeekendDays(request.WeekendDays, scopeType);
        if (!weekendGuard.Ok)
        {
            return Response<NoContent>.Fail(weekendGuard.Message!, weekendGuard.StatusCode);
        }

        calendar.CalendarName = (request.CalendarName ?? string.Empty).Trim();
        calendar.Description = request.Description?.Trim();
        calendar.WeekendDays = request.WeekendDays?.ToList();
        calendar.Notes = request.Notes?.Trim();
        calendar.UpdatedBy = _currentUser.ActorName;

        return await WorkingCalendarWriteGuard.ReplaceAsync(_repository, calendar, request.ExpectedVersion, ct);
    }
}
