using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.WorkingCalendar.Queries;

/// <summary>
/// <paramref name="CountryLayer"/> true = the platform surface (country rows); false = the tenant surface, which
/// returns the caller's own overrides plus ACTIVE country rows explicitly marked read-only. It never includes
/// another tenant's rows, nor draft/archived country rows.
/// </summary>
public sealed record ListWorkingCalendarsQuery(
    bool CountryLayer,
    string? CountryCode = null,
    int? CalendarYear = null,
    string? ScopeType = null,
    string? CalendarStatus = null,
    Guid? OrganizationUnitId = null,
    bool IncludeArchived = false) : IRequest<Response<WorkingCalendarListDto>>;
