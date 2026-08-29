using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.WorkingCalendar.Commands;

/// <summary><paramref name="ExpectedVersion"/> is required: a blind update would silently overwrite a concurrent edit.</summary>
public sealed record UpdateWorkingCalendarCommand(
    Guid Id,
    string CalendarName,
    string? Description,
    string CountryCode,
    int CalendarYear,
    string ScopeType,
    Guid? OrganizationUnitId,
    Guid? LegalEntityId,
    IReadOnlyList<string>? WeekendDays,
    string? Notes,
    int ExpectedVersion,
    bool IsPlatformActor) : IRequest<Response<NoContent>>;
