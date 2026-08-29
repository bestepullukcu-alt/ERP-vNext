using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.WorkingCalendar.Commands;

/// <summary>
/// Creates a calendar in ONE layer. There is no TenantId here on purpose: the layer is decided by
/// <paramref name="ScopeType"/> plus the ambient token context, never by a value the caller can supply.
/// </summary>
public sealed record CreateWorkingCalendarCommand(
    string CalendarCode,
    string CalendarName,
    string? Description,
    string CountryCode,
    int CalendarYear,
    string ScopeType,
    Guid? OrganizationUnitId,
    Guid? LegalEntityId,
    IReadOnlyList<string>? WeekendDays,
    string CalendarStatus,
    string Source,
    string? Notes,
    bool IsPlatformActor) : IRequest<Response<Guid>>;
