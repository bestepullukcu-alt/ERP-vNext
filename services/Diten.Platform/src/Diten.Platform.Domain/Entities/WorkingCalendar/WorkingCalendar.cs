using Diten.Platform.Common.Persistence;

namespace Diten.Platform.Domain.Entities.WorkingCalendar;

/// <summary>
/// Working Calendar &amp; Public Holidays — the shared foundation answering ONE question: "in this country / this
/// organization, is this date a working day?" plus the four derived calculations (next working day, add N working
/// days, working days between, which holiday a date falls on).
/// <para>
/// <b>Hybrid by design (pack D2).</b> A row with <c>TenantId == null</c> is the COUNTRY layer (platform system of
/// record: weekend definition + official/religious holidays). A row with <c>TenantId</c> set is that tenant's
/// OVERRIDE layer (company holidays/closures, compensation working days, optional weekend override). Both live in
/// ONE collection so <c>HybridRepository</c>'s "global default + tenant override" execution filter resolves the
/// layering at the infrastructure level instead of every query re-implementing it.
/// </para>
/// <para>
/// <b>Not a scheduler (pack D6).</b> It stores calendar facts and computes deterministic working-day arithmetic over
/// them. It allocates no resources, sequences no work, models no shift/working hours, and knows nothing about people
/// or leave. <see cref="WorkingCalendarDay.Recurrence"/> is a DECLARATION only — no day is ever auto-generated and
/// next year's calendar never appears by itself.
/// </para>
/// <para>
/// <b>Year is the only time axis (pack D1).</b> There is deliberately no policy-style EffectiveFrom/EffectiveTo pair:
/// a calendar is already scoped to <see cref="CalendarYear"/>, and a second axis would produce two conflicting
/// answers to "which calendar applies?" (and two <c>DateTimeOffset</c> fields that must never be sorted together).
/// </para>
/// </summary>
public sealed class WorkingCalendar : HybridEntity
{
    /// <summary>Stable business key. Unique per scope + country + year among non-deleted rows. Never renamed —
    /// display renaming goes through <see cref="CalendarName"/>.</summary>
    public string CalendarCode { get; set; } = string.Empty;

    public string CalendarName { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>ISO-3166-1 alpha-2, upper-cased. Validated against the MOD-0048 <c>countries</c> reference set via
    /// <c>IPlatformLookupProvider</c> — never free text, never a hardcoded fallback list.</summary>
    public string CountryCode { get; set; } = string.Empty;

    /// <summary>The calendar's year. This is the aggregate's ONLY time axis (D1).</summary>
    public int CalendarYear { get; set; }

    /// <summary><see cref="WorkingCalendarScopeType"/>. MUST stay consistent with <see cref="HybridEntity.TenantId"/>:
    /// <c>country</c> ⇒ TenantId null + platform actor; tenant-authorable scopes ⇒ TenantId from JWT.</summary>
    public string ScopeType { get; set; } = string.Empty;

    /// <summary>REAL, verified foreign key to <c>OrganizationUnit</c> (MOD-0288, same service). Required when
    /// <see cref="ScopeType"/> is <c>organization-unit</c>, and forbidden otherwise. No fake FK is ever opened here:
    /// there is deliberately no person/employee field, because a calendar knows nothing about people.</summary>
    public Guid? OrganizationUnitId { get; set; }

    /// <summary>REAL, cross-service verified FK to the tenant's active MDM Legal Entity. Required only when
    /// <see cref="ScopeType"/> is <c>legal-entity</c>, forbidden for every other scope.</summary>
    public Guid? LegalEntityId { get; set; }

    /// <summary>Which weekdays are non-working, from <see cref="WorkingCalendarDayOfWeek"/>.
    /// <para><b>null vs empty is meaningful (D3).</b> On a country row this is REQUIRED and non-empty. On an override
    /// row <c>null</c> means "inherit the country layer's weekend" while an empty list means "this organization has no
    /// weekend at all". The two must never be collapsed.</para></summary>
    public List<string>? WeekendDays { get; set; }

    /// <summary>Embedded day list (holidays / closures / compensation working days). Embedded so the calendar keeps
    /// ONE concurrency token and needs no second repository.</summary>
    public List<WorkingCalendarDay> Days { get; set; } = new();

    /// <summary><see cref="WorkingCalendarStatus"/>. Only <c>active</c> rows are visible to the provider.</summary>
    public string CalendarStatus { get; set; } = WorkingCalendarStatus.Draft;

    /// <summary><see cref="WorkingCalendarSource"/>. Only <c>manual</c> is writable in v1; <c>provider-fetch</c> is
    /// reserved for the auto-fetch follow-up and is rejected until then.</summary>
    public string Source { get; set; } = WorkingCalendarSource.Manual;

    public string? Notes { get; set; }

    public DateTimeOffset? ActivatedAt { get; set; }
    public string? ActivatedBy { get; set; }

    public DateTimeOffset? ArchivedAt { get; set; }
    public string? ArchivedBy { get; set; }

    /// <summary>True for the country layer (the platform-owned, cross-tenant row).</summary>
    public bool IsCountryLayer => TenantId is null;

    public bool IsActive() => string.Equals(CalendarStatus, WorkingCalendarStatus.Active, StringComparison.Ordinal);

    public bool IsArchived() => string.Equals(CalendarStatus, WorkingCalendarStatus.Archived, StringComparison.Ordinal);

    /// <summary>Effective-dating expressed over the single time axis (D1): the calendar governs its own year.</summary>
    public bool IsEffectiveOn(DateOnly date) => date.Year == CalendarYear;

    /// <summary>Active, non-archived days only — the provider never considers an archived day.</summary>
    public IEnumerable<WorkingCalendarDay> ActiveDays()
        => Days.Where(d => string.Equals(d.DayStatus, WorkingCalendarDayStatus.Active, StringComparison.Ordinal));
}

/// <summary>
/// One dated entry inside a calendar: a holiday, a company closure, or a compensation day that turns an otherwise
/// non-working day INTO a working day. Embedded in <see cref="WorkingCalendar"/> — never its own collection, never
/// its own page.
/// </summary>
public sealed class WorkingCalendarDay
{
    public Guid DayId { get; set; } = Guid.NewGuid();

    /// <summary>Unique within the owning calendar. Enforced by handler + validator only — an in-array unique index
    /// is not expressible in MongoDB, so there is no DB-level backstop here.</summary>
    public string DayCode { get; set; } = string.Empty;

    public string DayName { get; set; } = string.Empty;

    /// <summary>The nominal date. Its year MUST equal the calendar's year. <c>DateOnly</c> (not DateTimeOffset) so a
    /// timezone shift can never move a holiday to the wrong day, and so two date fields never become a parallel-array
    /// sort hazard.</summary>
    public DateOnly Date { get; set; }

    /// <summary>The date the holiday is actually observed when a country shifts it (e.g. to the following Monday).
    /// The provider matches on <c>ObservedDate ?? Date</c>.</summary>
    public DateOnly? ObservedDate { get; set; }

    /// <summary><see cref="WorkingCalendarDayType"/>. Country-layer types (public/religious/moveable) are reserved
    /// for the country layer; a tenant override may only use company/closure/compensation types.</summary>
    public string DayType { get; set; } = string.Empty;

    /// <summary><see cref="WorkingCalendarRecurrence"/>. A DECLARATION ONLY — nothing is auto-generated from it (D6).</summary>
    public string Recurrence { get; set; } = WorkingCalendarRecurrence.None;

    /// <summary>Metadata label. The v1 provider treats a half day as a WORKING day and says so explicitly through the
    /// <c>half_day_treated_as_working</c> reason code — it is never silently swallowed. Hour-level modelling of the
    /// half day is deliberately out of scope.</summary>
    public bool IsHalfDay { get; set; }

    public string DayStatus { get; set; } = WorkingCalendarDayStatus.Active;

    public string? Notes { get; set; }

    /// <summary>Day-level provenance. Existing and manually authored rows default to manual.</summary>
    public string Source { get; set; } = WorkingCalendarSource.Manual;

    /// <summary>The reviewed staging batch that introduced this provider day.</summary>
    public Guid? ProviderBatchId { get; set; }

    /// <summary>Stable provider-side reference used for idempotent comparison.</summary>
    public string? ProviderRef { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? ArchivedAt { get; set; }
    public string? ArchivedBy { get; set; }

    /// <summary>The date this entry actually governs.</summary>
    public DateOnly EffectiveDate => ObservedDate ?? Date;

    /// <summary>True when this entry FORCES a working day (compensation / bridge day), overriding both the weekend
    /// definition and any holiday on the same date.</summary>
    public bool IsWorkingDayOverride
        => string.Equals(DayType, WorkingCalendarDayType.WorkingDayOverride, StringComparison.Ordinal);
}

// ─────────────────────────────────────────────────────────────────────────────
// In-domain vocabulary (pack D7). These are STRUCTURAL: each value changes what
// the provider does, so a tenant cannot extend them freely. Out-of-set values are
// rejected with 400. Country codes are the exception — those are real reference
// data and come from MOD-0048. Hardcoded fallback lists are forbidden everywhere.
// ─────────────────────────────────────────────────────────────────────────────

public static class WorkingCalendarScopeType
{
    public const string Country = "country";
    public const string Tenant = "tenant";
    public const string OrganizationUnit = "organization-unit";
    public const string LegalEntity = "legal-entity";

    public static readonly IReadOnlyList<string> All = new[] { Country, Tenant, OrganizationUnit, LegalEntity };

    /// <summary>The subset a tenant actor may author. <c>country</c> is deliberately absent.</summary>
    public static readonly IReadOnlyList<string> TenantAuthorable = new[] { Tenant, OrganizationUnit, LegalEntity };

    /// <summary>
    /// The subset a PLATFORM actor may author. The platform surface owns the country layer and nothing else, so
    /// this is a single value — the platform form carries no scope selector at all. <c>All</c> stays as the union
    /// of both layers for validation messages; it is NOT what either surface offers.
    /// </summary>
    public static readonly IReadOnlyList<string> PlatformAuthorable = new[] { Country };

    public static bool IsValid(string? value) => value is not null && All.Contains(value, StringComparer.Ordinal);
}

public static class WorkingCalendarDayOfWeek
{
    public const string Monday = "monday";
    public const string Tuesday = "tuesday";
    public const string Wednesday = "wednesday";
    public const string Thursday = "thursday";
    public const string Friday = "friday";
    public const string Saturday = "saturday";
    public const string Sunday = "sunday";

    public static readonly IReadOnlyList<string> All =
        new[] { Monday, Tuesday, Wednesday, Thursday, Friday, Saturday, Sunday };

    public static bool IsValid(string? value) => value is not null && All.Contains(value, StringComparer.Ordinal);

    /// <summary>ISO weekday name for a date — the stable machine key, never a localized label.</summary>
    public static string FromDate(DateOnly date) => date.DayOfWeek switch
    {
        System.DayOfWeek.Monday => Monday,
        System.DayOfWeek.Tuesday => Tuesday,
        System.DayOfWeek.Wednesday => Wednesday,
        System.DayOfWeek.Thursday => Thursday,
        System.DayOfWeek.Friday => Friday,
        System.DayOfWeek.Saturday => Saturday,
        _ => Sunday
    };
}

public static class WorkingCalendarDayType
{
    public const string PublicHoliday = "public-holiday";
    public const string ReligiousHoliday = "religious-holiday";
    public const string MoveableHoliday = "moveable-holiday";
    public const string CompanyHoliday = "company-holiday";
    public const string CompanyClosure = "company-closure";
    public const string WorkingDayOverride = "working-day-override";

    public static readonly IReadOnlyList<string> All = new[]
    {
        PublicHoliday, ReligiousHoliday, MoveableHoliday, CompanyHoliday, CompanyClosure, WorkingDayOverride
    };

    /// <summary>Reserved for the country layer — an official/religious holiday is a country-level fact and a tenant
    /// may not redefine it inside its own layer (that would create two competing "official holiday" truths).</summary>
    public static readonly IReadOnlyList<string> CountryLayerOnly = new[]
    {
        PublicHoliday, ReligiousHoliday, MoveableHoliday
    };

    /// <summary>What a tenant override row may contain.</summary>
    public static readonly IReadOnlyList<string> OverrideAuthorable = new[]
    {
        CompanyHoliday, CompanyClosure, WorkingDayOverride
    };

    public static bool IsValid(string? value) => value is not null && All.Contains(value, StringComparer.Ordinal);

    public static bool IsCountryLayerOnly(string? value)
        => value is not null && CountryLayerOnly.Contains(value, StringComparer.Ordinal);
}

public static class WorkingCalendarRecurrence
{
    public const string None = "none";
    public const string AnnualFixed = "annual-fixed";
    public const string AnnualMoveable = "annual-moveable";

    public static readonly IReadOnlyList<string> All = new[] { None, AnnualFixed, AnnualMoveable };

    public static bool IsValid(string? value) => value is not null && All.Contains(value, StringComparer.Ordinal);
}

public static class WorkingCalendarStatus
{
    public const string Draft = "draft";
    public const string Active = "active";
    public const string Archived = "archived";

    public static readonly IReadOnlyList<string> All = new[] { Draft, Active, Archived };

    /// <summary>
    /// The statuses that still HOLD a calendar code. Archiving releases the code, because codes are year-and-country
    /// shaped (<c>TR-2026</c>) and re-entering a year after archiving a mistake is the normal path — there is no
    /// delete endpoint, so a code counted while archived would stay burned forever.
    /// <para>
    /// This list is the single source for BOTH the repository's duplicate-code guard and the unique index's partial
    /// filter. They must agree: a guard that is looser than the index turns a friendly 409 into an E11000 500, which
    /// is exactly the defect this list exists to prevent. MongoDB's <c>partialFilterExpression</c> cannot express
    /// "not archived" (<c>$ne</c> is unsupported there), so both sides state the live set positively.
    /// </para>
    /// <para>Adding a new status? Decide explicitly whether it holds a code, and add it here if so.</para>
    /// </summary>
    public static readonly IReadOnlyList<string> CodeHolding = new[] { Draft, Active };

    public static bool IsValid(string? value) => value is not null && All.Contains(value, StringComparer.Ordinal);
}

public static class WorkingCalendarDayStatus
{
    public const string Active = "active";
    public const string Archived = "archived";

    public static readonly IReadOnlyList<string> All = new[] { Active, Archived };

    public static bool IsValid(string? value) => value is not null && All.Contains(value, StringComparer.Ordinal);
}

public static class WorkingCalendarSource
{
    public const string Manual = "manual";
    public const string Imported = "imported";

    /// <summary>Reserved for the auto-fetch follow-up (external provider → staging → review → activate). There is no
    /// producer in v1 and an attempt to write it is rejected — so the value can never appear without the human
    /// review step that justifies trusting an external holiday feed.</summary>
    public const string ProviderFetch = "provider-fetch";

    public static readonly IReadOnlyList<string> All = new[] { Manual, Imported, ProviderFetch };

    /// <summary>What a caller may actually write today.</summary>
    // ProviderFetch is reserved for the reviewed import apply path; public create/edit inputs remain manual/imported.
    public static readonly IReadOnlyList<string> Writable = new[] { Manual, Imported };

    public static bool IsValid(string? value) => value is not null && All.Contains(value, StringComparer.Ordinal);

    public static bool IsWritable(string? value) => value is not null && Writable.Contains(value, StringComparer.Ordinal);
}
