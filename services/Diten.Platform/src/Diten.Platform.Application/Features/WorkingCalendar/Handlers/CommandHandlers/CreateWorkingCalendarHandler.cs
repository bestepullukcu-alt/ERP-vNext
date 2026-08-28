using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Lookups;
using Diten.Platform.Application.Features.Lookups.Services;
using Diten.Platform.Application.Features.WorkingCalendar.Commands;
using Diten.Platform.Application.Features.WorkingCalendar.Services;
using Diten.Platform.Domain.Entities.WorkingCalendar;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Wc = Diten.Platform.Domain.Entities.WorkingCalendar.WorkingCalendar;

namespace Diten.Platform.Application.Features.WorkingCalendar.Handlers.CommandHandlers;

public sealed class CreateWorkingCalendarHandler
    : IRequestHandler<CreateWorkingCalendarCommand, Response<Guid>>
{
    private readonly IWorkingCalendarRepository _repository;
    private readonly IPlatformLookupProvider _lookups;
    private readonly IOrganizationUnitRepository _organizationUnits;
    private readonly IWorkingCalendarLegalEntityValidator _legalEntities;
    private readonly ICurrentUserContext _currentUser;

    public CreateWorkingCalendarHandler(
        IWorkingCalendarRepository repository,
        IPlatformLookupProvider lookups,
        IOrganizationUnitRepository organizationUnits,
        IWorkingCalendarLegalEntityValidator legalEntities,
        ICurrentUserContext currentUser)
    {
        _repository = repository;
        _lookups = lookups;
        _organizationUnits = organizationUnits;
        _legalEntities = legalEntities;
        _currentUser = currentUser;
    }

    public async Task<Response<Guid>> Handle(CreateWorkingCalendarCommand request, CancellationToken ct)
    {
        var scopeType = (request.ScopeType ?? string.Empty).Trim();
        var ambientTenant = _repository.CurrentTenantId;

        var scopeGuard = WorkingCalendarValidation.ValidateScope(
            scopeType, ambientTenant, request.IsPlatformActor, request.OrganizationUnitId, request.LegalEntityId);
        if (!scopeGuard.Ok)
        {
            return Response<Guid>.Fail(scopeGuard.Message!, scopeGuard.StatusCode, scopeGuard.ReasonCode);
        }

        var yearGuard = WorkingCalendarValidation.ValidateYear(request.CalendarYear);
        if (!yearGuard.Ok)
        {
            return Response<Guid>.Fail(yearGuard.Message!, yearGuard.StatusCode);
        }

        var weekendGuard = WorkingCalendarValidation.ValidateWeekendDays(request.WeekendDays, scopeType);
        if (!weekendGuard.Ok)
        {
            return Response<Guid>.Fail(weekendGuard.Message!, weekendGuard.StatusCode);
        }

        var sourceGuard = WorkingCalendarValidation.ValidateSource((request.Source ?? string.Empty).Trim());
        if (!sourceGuard.Ok)
        {
            return Response<Guid>.Fail(sourceGuard.Message!, sourceGuard.StatusCode);
        }

        if (!WorkingCalendarStatus.IsValid(request.CalendarStatus)
            || string.Equals(request.CalendarStatus, WorkingCalendarStatus.Archived, StringComparison.Ordinal))
        {
            return Response<Guid>.Fail(
                "CalendarStatus must be 'draft' or 'active' on create; 'archived' is reached through the archive action.", 400);
        }

        var countryCode = (request.CountryCode ?? string.Empty).Trim().ToUpperInvariant();
        if (!await IsKnownCountryAsync(countryCode, ct))
        {
            return Response<Guid>.Fail(
                $"'{countryCode}' is not a published country in the reference data set.", 400);
        }

        // A real, verifiable FK — the organization unit must exist in this tenant. No fake foreign keys are opened
        // anywhere in this aggregate (and there is deliberately no person/employee field at all).
        if (request.OrganizationUnitId is { } ouId && ouId != Guid.Empty)
        {
            var unit = await _organizationUnits.GetByIdAsync(ouId, ct);
            if (unit is null)
            {
                return Response<Guid>.Fail("The referenced organization unit was not found.", 400);
            }
        }

        if (request.LegalEntityId is { } legalEntityId && legalEntityId != Guid.Empty)
        {
            var validation = await _legalEntities.ValidateAsync(legalEntityId, ct);
            if (validation.DependencyUnavailable)
            {
                return Response<Guid>.Fail(
                    "Legal entity validation is unavailable.", 503, "legal_entity_validation_unavailable");
            }
            if (!validation.IsReferenceable)
            {
                return Response<Guid>.Fail(
                    "The referenced legal entity is not referenceable.", 400, "legal_entity_not_referenceable");
            }
        }

        // The layer is decided here, from the scope + ambient context — never from the payload.
        var tenantId = scopeType == WorkingCalendarScopeType.Country ? (Guid?)null : ambientTenant;

        var calendarCode = (request.CalendarCode ?? string.Empty).Trim();
        if (await _repository.ExistsByCodeAsync(
                tenantId, countryCode, request.CalendarYear, calendarCode, null, ct,
                request.OrganizationUnitId, request.LegalEntityId))
        {
            return Response<Guid>.Fail(
                $"Calendar code '{calendarCode}' already exists for {countryCode} {request.CalendarYear} in this scope.", 409);
        }

        if (string.Equals(request.CalendarStatus, WorkingCalendarStatus.Active, StringComparison.Ordinal)
            && await _repository.ExistsActiveAsync(
                tenantId, countryCode, request.CalendarYear, request.OrganizationUnitId, null, ct,
                request.LegalEntityId))
        {
            return Response<Guid>.Fail(
                $"An active calendar already exists for {countryCode} {request.CalendarYear} in this scope. " +
                "Exactly one active calendar per scope keeps resolution deterministic.", 409);
        }

        var now = DateTimeOffset.UtcNow;
        var calendar = new Wc
        {
            TenantId = tenantId,
            CalendarCode = calendarCode,
            CalendarName = (request.CalendarName ?? string.Empty).Trim(),
            Description = request.Description?.Trim(),
            CountryCode = countryCode,
            CalendarYear = request.CalendarYear,
            ScopeType = scopeType,
            OrganizationUnitId = request.OrganizationUnitId,
            LegalEntityId = request.LegalEntityId,
            WeekendDays = request.WeekendDays?.ToList(),
            CalendarStatus = request.CalendarStatus,
            Source = (request.Source ?? string.Empty).Trim(),
            Notes = request.Notes?.Trim(),
            CreatedBy = _currentUser.ActorName,
            ActivatedAt = string.Equals(request.CalendarStatus, WorkingCalendarStatus.Active, StringComparison.Ordinal) ? now : null,
            ActivatedBy = string.Equals(request.CalendarStatus, WorkingCalendarStatus.Active, StringComparison.Ordinal)
                ? _currentUser.ActorName
                : null
        };

        await _repository.CreateAsync(calendar, ct);
        return Response<Guid>.Success(calendar.Id, 201);
    }

    private async Task<bool> IsKnownCountryAsync(string countryCode, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(countryCode))
        {
            return false;
        }

        var options = await _lookups.GetLookupOptionsAsync(PlatformLookupKeys.Countries, ct);
        if (options is null || options.Count == 0)
        {
            return false;
        }

        return options.Any(o =>
            string.Equals(o.Code, countryCode, StringComparison.OrdinalIgnoreCase)
            || string.Equals(o.Value, countryCode, StringComparison.OrdinalIgnoreCase));
    }
}
