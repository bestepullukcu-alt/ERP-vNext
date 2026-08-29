using System.ComponentModel.DataAnnotations;

namespace Diten.Web.Models.WorkingCalendar;

/// <summary>
/// Create/Edit form model shared by both surfaces. It carries NO TenantId: which layer a calendar belongs to is
/// decided server-side from the scope plus the caller's token, so the browser cannot aim a write at another layer.
/// <para>
/// Optional numeric fields are nullable on purpose — a non-nullable value type would register as "required" in the
/// shared required-fields tracker and show the user a red asterisk the backend never asked for.
/// </para>
/// </summary>
public sealed class WorkingCalendarEditViewModel
{
    public Guid? Id { get; set; }

    [Required]
    [StringLength(64)]
    public string CalendarCode { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string CalendarName { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    [Required]
    [StringLength(2, MinimumLength = 2)]
    public string CountryCode { get; set; } = string.Empty;

    [Required]
    [Range(1900, 2200)]
    public int CalendarYear { get; set; } = DateTime.UtcNow.Year;

    [Required]
    public string ScopeType { get; set; } = string.Empty;

    /// <summary>Only used when the scope is organization-unit; a real, server-verified reference.</summary>
    public Guid? OrganizationUnitId { get; set; }

    /// <summary>Only used when the scope is legal-entity; verified against tenant-scoped MDM before persistence.</summary>
    public Guid? LegalEntityId { get; set; }

    /// <summary>
    /// Empty means different things per layer and the UI must not blur them: on a country calendar it is invalid,
    /// on an override it means "inherit the country weekend". The form shows the inherited value explicitly.
    /// </summary>
    public List<string> WeekendDays { get; set; } = new();

    [Required]
    public string CalendarStatus { get; set; } = "draft";

    /// <summary>Server-set on the tenant surface (only 'manual' is legal there), authored on the platform surface.</summary>
    public string Source { get; set; } = "manual";

    [StringLength(2000)]
    public string? Notes { get; set; }

    /// <summary>Optimistic concurrency token echoed back on save.</summary>
    public int ExpectedVersion { get; set; }

    // ── Read-only context rendered by the form (never posted back) ────────────

    /// <summary>The weekend actually in force once inheritance is applied, so an inheriting override can show
    /// "Ülke takviminden devralınıyor: Cts, Paz" instead of an empty control that reads as "no weekend".</summary>
    public List<string> EffectiveWeekendDays { get; set; } = new();

    public bool WeekendInherited { get; set; }

    public bool IsCountryLayer { get; set; }
}
