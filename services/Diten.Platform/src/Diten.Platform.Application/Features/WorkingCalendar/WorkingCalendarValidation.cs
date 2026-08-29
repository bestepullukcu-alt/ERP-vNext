using Diten.Platform.Application.Features.WorkingCalendar.Provider;
using Diten.Platform.Domain.Entities.WorkingCalendar;
using Wc = Diten.Platform.Domain.Entities.WorkingCalendar.WorkingCalendar;

namespace Diten.Platform.Application.Features.WorkingCalendar;

/// <summary>
/// Every scope/day guard lives HERE and nowhere else. Both controllers call the same commands, so if these rules were
/// duplicated per surface the two layers would drift and the tenant boundary would eventually be enforced in only one
/// of them. A guard returns a message + status instead of throwing, so handlers can answer 400/403/409 precisely.
/// </summary>
public static class WorkingCalendarValidation
{
    public const int MaxDaysPerCalendar = 400;
    public const int MinYear = 1900;
    public const int MaxYear = 2200;

    public sealed record GuardResult(bool Ok, string? Message = null, int StatusCode = 400, string? ReasonCode = null)
    {
        public static readonly GuardResult Success = new(true);
        public static GuardResult Fail(string message, int status = 400, string? reason = null)
            => new(false, message, status, reason);
    }

    /// <summary>
    /// The single most important rule in the module: which layer a row belongs to, and who may write it.
    /// A country row must have no tenant AND a platform actor; a tenant/org row must carry the ambient tenant.
    /// <c>TenantId</c> is never taken from the request payload.
    /// </summary>
    public static GuardResult ValidateScope(
        string scopeType,
        Guid? ambientTenantId,
        bool isPlatformActor,
        Guid? organizationUnitId,
        Guid? legalEntityId)
    {
        if (!WorkingCalendarScopeType.IsValid(scopeType))
        {
            return GuardResult.Fail(
                $"Unsupported scope type '{scopeType}'. Supported: {string.Join(", ", WorkingCalendarScopeType.All)}.",
                400, "unsupported_vocabulary_value");
        }

        // The platform surface owns the COUNTRY layer and nothing else, so a platform actor may author only
        // `country`. Without this the request would still fail — but as `tenant_scope_requires_tenant`, which reads
        // like a missing tenant header rather than "this surface cannot author that layer". Fail with the honest
        // reason instead. The tenant override surface is unaffected: it never sets isPlatformActor.
        if (isPlatformActor && scopeType != WorkingCalendarScopeType.Country)
        {
            return GuardResult.Fail(
                $"The platform surface authors the country layer only; scope '{scopeType}' belongs to the tenant "
                + "override surface.",
                400, "platform_surface_is_country_only");
        }

        if (scopeType == WorkingCalendarScopeType.Country)
        {
            if (!isPlatformActor)
            {
                return GuardResult.Fail(
                    "The country layer is platform-owned; a tenant actor cannot author it.",
                    403, "country_scope_requires_platform_actor");
            }

            if (organizationUnitId is not null)
            {
                return GuardResult.Fail(
                    "A country calendar cannot be bound to an organization unit.",
                    400, "organization_unit_not_allowed_for_country_scope");
            }

            if (legalEntityId is not null)
            {
                return GuardResult.Fail(
                    "A country calendar cannot be bound to a legal entity.",
                    400, "legal_entity_forbidden_for_scope");
            }

            return GuardResult.Success;
        }

        // tenant / organization-unit
        if (ambientTenantId is null || ambientTenantId == Guid.Empty)
        {
            return GuardResult.Fail(
                "A tenant-scoped calendar requires an ambient tenant context; none was resolved from the token.",
                400, "tenant_scope_requires_tenant");
        }

        if (scopeType == WorkingCalendarScopeType.OrganizationUnit && (organizationUnitId is null || organizationUnitId == Guid.Empty))
        {
            return GuardResult.Fail(
                "An organization-unit scoped calendar requires OrganizationUnitId.",
                400, "org_scope_requires_organization_unit");
        }

        if (scopeType != WorkingCalendarScopeType.OrganizationUnit && organizationUnitId is not null)
        {
            return GuardResult.Fail(
                "OrganizationUnitId is allowed only for scope 'organization-unit'.",
                400, "organization_unit_forbidden_for_scope");
        }

        if (scopeType == WorkingCalendarScopeType.LegalEntity && (legalEntityId is null || legalEntityId == Guid.Empty))
        {
            return GuardResult.Fail(
                "A legal-entity scoped calendar requires LegalEntityId.",
                400, "legal_entity_scope_requires_legal_entity");
        }

        if (scopeType != WorkingCalendarScopeType.LegalEntity && legalEntityId is not null)
        {
            return GuardResult.Fail(
                "LegalEntityId is allowed only for scope 'legal-entity'.",
                400, "legal_entity_forbidden_for_scope");
        }

        return GuardResult.Success;
    }

    /// <summary>
    /// A tenant may not restate an official/religious holiday inside its own layer — that would create two competing
    /// "official holiday" truths for the same date. Company holidays, closures and compensation days are its own.
    /// Enforced in the backend, not merely hidden in the UI.
    /// </summary>
    public static GuardResult ValidateDayType(string dayType, bool isCountryLayer)
    {
        if (!WorkingCalendarDayType.IsValid(dayType))
        {
            return GuardResult.Fail(
                $"Unsupported day type '{dayType}'. Supported: {string.Join(", ", WorkingCalendarDayType.All)}.",
                400, "unsupported_vocabulary_value");
        }

        if (!isCountryLayer && WorkingCalendarDayType.IsCountryLayerOnly(dayType))
        {
            return GuardResult.Fail(
                $"Day type '{dayType}' belongs to the country layer and cannot be authored on a tenant override. " +
                $"Use one of: {string.Join(", ", WorkingCalendarDayType.OverrideAuthorable)}.",
                400, "day_type_reserved_for_country_layer");
        }

        return GuardResult.Success;
    }

    public static GuardResult ValidateYear(int year)
        => year is >= MinYear and <= MaxYear
            ? GuardResult.Success
            : GuardResult.Fail($"CalendarYear must be between {MinYear} and {MaxYear}.", 400, "invalid_calendar_year");

    public static GuardResult ValidateWeekendDays(IReadOnlyList<string>? weekendDays, string scopeType)
    {
        if (weekendDays is null)
        {
            // Only an override may inherit. A country calendar has nothing to inherit from.
            return scopeType == WorkingCalendarScopeType.Country
                ? GuardResult.Fail(
                    "A country calendar must declare its weekend days; there is no layer to inherit them from.",
                    400, "weekend_days_required")
                : GuardResult.Success;
        }

        foreach (var day in weekendDays)
        {
            if (!WorkingCalendarDayOfWeek.IsValid(day))
            {
                return GuardResult.Fail(
                    $"Unsupported weekday '{day}'. Supported: {string.Join(", ", WorkingCalendarDayOfWeek.All)}.",
                    400, "unsupported_vocabulary_value");
            }
        }

        if (weekendDays.Distinct(StringComparer.Ordinal).Count() != weekendDays.Count)
        {
            return GuardResult.Fail("WeekendDays contains duplicates.", 400, "duplicate_weekend_day");
        }

        return GuardResult.Success;
    }

    /// <summary>A calendar's identity freezes on activation, but its content does not: official holidays get declared
    /// and shifted mid-year, and the calendar has to be able to follow that.</summary>
    public static GuardResult ValidateIdentityNotFrozen(
        Wc existing,
        string countryCode,
        int year,
        string scopeType,
        Guid? organizationUnitId,
        Guid? legalEntityId)
    {
        if (!existing.IsActive())
        {
            return GuardResult.Success;
        }

        var changed =
            !string.Equals(existing.CountryCode, countryCode, StringComparison.OrdinalIgnoreCase)
            || existing.CalendarYear != year
            || !string.Equals(existing.ScopeType, scopeType, StringComparison.Ordinal)
            || existing.OrganizationUnitId != organizationUnitId
            || existing.LegalEntityId != legalEntityId;

        return changed
            ? GuardResult.Fail(
                "CountryCode, CalendarYear, ScopeType, OrganizationUnitId and LegalEntityId are frozen once a calendar is active. " +
                "Weekend days, day entries, name, description and notes remain editable.",
                409, "calendar_identity_frozen")
            : GuardResult.Success;
    }

    public static GuardResult ValidateWritable(Wc calendar)
        => calendar.IsArchived()
            ? GuardResult.Fail("An archived calendar cannot be modified.", 409, "calendar_archived")
            : GuardResult.Success;

    public static GuardResult ValidateSource(string source)
    {
        if (!WorkingCalendarSource.IsValid(source))
        {
            return GuardResult.Fail(
                $"Unsupported source '{source}'. Supported: {string.Join(", ", WorkingCalendarSource.All)}.",
                400, "unsupported_vocabulary_value");
        }

        return WorkingCalendarSource.IsWritable(source)
            ? GuardResult.Success
            : GuardResult.Fail(
                $"Source '{source}' is reserved for the automated holiday-fetch flow, which requires a human review " +
                "step that does not exist yet. It cannot be written directly.",
                400, "source_reserved");
    }

    /// <summary>Day uniqueness has no DB backstop (an in-array unique index is not expressible), so the handler and
    /// validator are the only defence. Both code and effective date are checked.</summary>
    public static GuardResult ValidateDayUniqueness(Wc calendar, WorkingCalendarDayInput input, Guid? excludeDayId)
    {
        var active = calendar.ActiveDays().Where(d => d.DayId != excludeDayId).ToList();

        if (active.Any(d => string.Equals(d.DayCode, input.DayCode, StringComparison.OrdinalIgnoreCase)))
        {
            return GuardResult.Fail($"Day code '{input.DayCode}' already exists in this calendar.", 409, "duplicate_day_code");
        }

        var effective = input.ObservedDate ?? input.Date;
        if (active.Any(d => d.EffectiveDate == effective))
        {
            return GuardResult.Fail(
                $"Another active day already governs {effective:yyyy-MM-dd} in this calendar.", 409, "duplicate_day_date");
        }

        return GuardResult.Success;
    }

    public static GuardResult ValidateDayInput(Wc calendar, WorkingCalendarDayInput input, Guid? excludeDayId)
    {
        if (string.IsNullOrWhiteSpace(input.DayCode) || input.DayCode.Trim().Length > 64)
        {
            return GuardResult.Fail("DayCode is required and must be at most 64 characters.", 400, "invalid_day_code");
        }

        if (string.IsNullOrWhiteSpace(input.DayName) || input.DayName.Trim().Length > 200)
        {
            return GuardResult.Fail("DayName is required and must be at most 200 characters.", 400, "invalid_day_name");
        }

        var dayTypeGuard = ValidateDayType(input.DayType, calendar.IsCountryLayer);
        if (!dayTypeGuard.Ok)
        {
            return dayTypeGuard;
        }

        if (!WorkingCalendarRecurrence.IsValid(input.Recurrence))
        {
            return GuardResult.Fail(
                $"Unsupported recurrence '{input.Recurrence}'. Supported: {string.Join(", ", WorkingCalendarRecurrence.All)}.",
                400, "unsupported_vocabulary_value");
        }

        if (input.Date.Year != calendar.CalendarYear)
        {
            return GuardResult.Fail(
                $"Day date {input.Date:yyyy-MM-dd} is not in the calendar year {calendar.CalendarYear}.",
                400, "day_year_mismatch");
        }

        if (input.ObservedDate is { } observed && observed.Year != calendar.CalendarYear)
        {
            return GuardResult.Fail(
                $"Observed date {observed:yyyy-MM-dd} is not in the calendar year {calendar.CalendarYear}.",
                400, "day_year_mismatch");
        }

        // A compensation day that is also "half" is contradictory: it both forces work and reduces it.
        if (input.IsHalfDay && string.Equals(input.DayType, WorkingCalendarDayType.WorkingDayOverride, StringComparison.Ordinal))
        {
            return GuardResult.Fail(
                "A working-day override cannot also be a half day.", 400, "half_day_on_override");
        }

        if (excludeDayId is null && calendar.ActiveDays().Count() >= MaxDaysPerCalendar)
        {
            return GuardResult.Fail(
                $"A calendar may hold at most {MaxDaysPerCalendar} active days.", 400, "day_limit_exceeded");
        }

        return ValidateDayUniqueness(calendar, input, excludeDayId);
    }

    /// <summary>The tenant-facing contract slice — country scope and country-layer day types are structurally absent.</summary>
    public static WorkingCalendarOverrideContractDto BuildOverrideContract() => new(
        Capability: "working-calendar",
        ContractVersion: "working-calendar.v1",
        ScopeTypes: WorkingCalendarScopeType.TenantAuthorable,
        DayOfWeek: WorkingCalendarDayOfWeek.All,
        DayTypes: WorkingCalendarDayType.OverrideAuthorable,
        Recurrences: WorkingCalendarRecurrence.All,
        Statuses: WorkingCalendarStatus.All,
        DayStatuses: WorkingCalendarDayStatus.All,
        Resolutions: AllResolutions,
        ReasonCodes: AllReasonCodes,
        Permissions: new[] { WorkingCalendarPermissions.OverrideRead, WorkingCalendarPermissions.OverrideManage },
        MaxDaysPerCalendar: MaxDaysPerCalendar,
        Limitations: new[]
        {
            "A tenant override cannot declare public, religious or moveable holidays — those belong to the country layer.",
            "A tenant cannot see or edit the country layer; only the resolved outcome for a date is visible.",
            "Recurrence is a declaration only; no day is generated automatically.",
            "Day granularity only — no working hours, shifts or leave."
        });

    public static WorkingCalendarContractDto BuildContract() => new(
        Capability: "working-calendar",
        ContractVersion: "working-calendar.v1",
        ScopeTypes: WorkingCalendarScopeType.PlatformAuthorable,
        DayOfWeek: WorkingCalendarDayOfWeek.All,
        DayTypes: WorkingCalendarDayType.All,
        Recurrences: WorkingCalendarRecurrence.All,
        Statuses: WorkingCalendarStatus.All,
        DayStatuses: WorkingCalendarDayStatus.All,
        Sources: WorkingCalendarSource.All,
        WritableSources: WorkingCalendarSource.Writable,
        Resolutions: AllResolutions,
        ReasonCodes: AllReasonCodes,
        Permissions: WorkingCalendarPermissions.All,
        MaxDaysPerCalendar: MaxDaysPerCalendar,
        Limitations: new[]
        {
            "No scheduling, capacity, reservation or optimisation engine.",
            "Recurrence is a declaration only; no day is generated automatically and next year never appears by itself.",
            "Day granularity only — no working hours, shifts or leave.",
            "A half day resolves to a working day and is flagged with half_day_treated_as_working.",
            "When no active calendar exists the answer is unresolved and null — a default is never invented.",
            "External holiday auto-fetch is not implemented; source 'provider-fetch' is rejected."
        });

    private static readonly IReadOnlyList<string> AllResolutions = new[]
    {
        WorkingCalendarResolution.Resolved,
        WorkingCalendarResolution.CalendarMissing,
        WorkingCalendarResolution.YearMissing,
        WorkingCalendarResolution.CountryUnknown,
        WorkingCalendarResolution.InvalidRange
    };

    private static readonly IReadOnlyList<string> AllReasonCodes = new[]
    {
        WorkingCalendarReasonCodes.WorkingDay,
        WorkingCalendarReasonCodes.WeekendDay,
        WorkingCalendarReasonCodes.PublicHoliday,
        WorkingCalendarReasonCodes.CompanyClosure,
        WorkingCalendarReasonCodes.WorkingDayOverrideApplied,
        WorkingCalendarReasonCodes.HalfDayTreatedAsWorking,
        WorkingCalendarReasonCodes.TenantOverrideApplied,
        WorkingCalendarReasonCodes.WeekendInheritedFromCountry,
        WorkingCalendarReasonCodes.WeekendFromTenantOverride,
        WorkingCalendarReasonCodes.CalendarMissing,
        WorkingCalendarReasonCodes.YearMissing,
        WorkingCalendarReasonCodes.CountryUnknown,
        WorkingCalendarReasonCodes.CalendarNotActive,
        WorkingCalendarReasonCodes.InvalidDateRange
    };
}
