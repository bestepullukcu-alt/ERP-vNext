using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums.TimeEntry;

namespace Diten.Platform.Domain.Entities.TimeEntry;

/// <summary>
/// MOD-0280-FU01 (pack §4.2, D4) — one row per (week revision, local date, task-or-category, source): minutes.
/// Date + duration, never start/end instants. Exactly one of <see cref="TaskItemId"/> / <see cref="CategoryCode"/>.
///
/// <para>A row removed from a Draft is soft-deleted; rows of a submitted or approved revision are never touched —
/// a change after approval is a correction revision with its own rows.</para>
/// </summary>
public sealed class TimeEntry : TenantScopedEntity
{
    public required Guid TimesheetWeekId { get; set; }

    public required Guid UserId { get; set; }

    public required string WeekKey { get; set; }

    /// <summary>Tenant-local day, inside the week, never after the person's local today.</summary>
    public required DateOnly LocalDate { get; set; }

    /// <summary>Multiple of 15, 15…960 (A3).</summary>
    public required int DurationMinutes { get; set; }

    /// <summary>MOD-0024 task id — a reference only; read through the task port, never joined.</summary>
    public Guid? TaskItemId { get; set; }

    /// <summary>Active <see cref="WorkCategory.Code"/>.</summary>
    public string? CategoryCode { get; set; }

    public TimeEntrySource Source { get; set; } = TimeEntrySource.Manual;

    /// <summary>Meeting id for <see cref="TimeEntrySource.Meeting"/>; null otherwise (T1b).</summary>
    public string? SourceRef { get; set; }

    public bool EditedFromTimer { get; set; }

    public int OutsideWorkingMinutes { get; set; }

    /// <summary>Trim, max 500.</summary>
    public string? Note { get; set; }
}
