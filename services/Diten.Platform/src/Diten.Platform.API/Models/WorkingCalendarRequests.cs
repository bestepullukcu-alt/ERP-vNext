using Diten.Platform.Application.Features.WorkingCalendar;
using Diten.Platform.Application.Features.WorkingCalendar.Commands;
using Diten.Platform.Domain.Entities.WorkingCalendar;

namespace Diten.Platform.API.Models;

/// <summary>
/// API request shapes. Note what is ABSENT: there is no TenantId anywhere. The layer a row belongs to is derived from
/// the scope plus the caller's token, so a client cannot write into another tenant's layer (or into the country layer)
/// by crafting a payload.
/// </summary>
public sealed class CreateWorkingCalendarRequest
{
    public string CalendarCode { get; set; } = string.Empty;
    public string CalendarName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string CountryCode { get; set; } = string.Empty;
    public int CalendarYear { get; set; }
    public string ScopeType { get; set; } = string.Empty;
    public Guid? OrganizationUnitId { get; set; }
    public Guid? LegalEntityId { get; set; }

    /// <summary>Null on an override means "inherit the country weekend"; an empty list means "no weekend at all".
    /// The two are deliberately different and are not collapsed.</summary>
    public List<string>? WeekendDays { get; set; }

    public string CalendarStatus { get; set; } = WorkingCalendarStatus.Draft;
    public string Source { get; set; } = WorkingCalendarSource.Manual;
    public string? Notes { get; set; }

    public CreateWorkingCalendarCommand ToCommand(bool isPlatformActor) => new(
        CalendarCode, CalendarName, Description, CountryCode, CalendarYear, ScopeType, OrganizationUnitId, LegalEntityId,
        WeekendDays, CalendarStatus, Source, Notes, isPlatformActor);
}

public sealed class UpdateWorkingCalendarRequest
{
    public string CalendarName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string CountryCode { get; set; } = string.Empty;
    public int CalendarYear { get; set; }
    public string ScopeType { get; set; } = string.Empty;
    public Guid? OrganizationUnitId { get; set; }
    public Guid? LegalEntityId { get; set; }
    public List<string>? WeekendDays { get; set; }
    public string? Notes { get; set; }

    /// <summary>Required. Without it an update would silently overwrite a concurrent edit.</summary>
    public int ExpectedVersion { get; set; }

    public UpdateWorkingCalendarCommand ToCommand(Guid id, bool isPlatformActor) => new(
        id, CalendarName, Description, CountryCode, CalendarYear, ScopeType, OrganizationUnitId, LegalEntityId,
        WeekendDays, Notes, ExpectedVersion, isPlatformActor);
}

/// <summary>Body for the action endpoints, carrying only the concurrency token.</summary>
public sealed class VersionedActionRequest
{
    public int ExpectedVersion { get; set; }
}

public sealed class WorkingCalendarDayRequest
{
    public Guid? DayId { get; set; }
    public string DayCode { get; set; } = string.Empty;
    public string DayName { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public DateOnly? ObservedDate { get; set; }
    public string DayType { get; set; } = string.Empty;
    public string Recurrence { get; set; } = WorkingCalendarRecurrence.None;
    public bool IsHalfDay { get; set; }
    public string? Notes { get; set; }
    public int ExpectedVersion { get; set; }

    public WorkingCalendarDayInput ToInput() => new(
        DayId, DayCode, DayName, Date, ObservedDate, DayType, Recurrence, IsHalfDay, Notes);
}
