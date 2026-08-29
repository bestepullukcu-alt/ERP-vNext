using Diten.Platform.Application.Features.WorkingCalendar;
using Diten.Platform.Domain.Entities.WorkingCalendar;
using Xunit;
using Wc = Diten.Platform.Domain.Entities.WorkingCalendar.WorkingCalendar;

namespace Diten.Platform.Application.Tests.WorkingCalendar;

/// <summary>
/// The scope guard is the tenant boundary. These tests exist because the two UI surfaces dispatch the SAME commands:
/// if the guard weakened, one surface would keep working and the other would quietly gain a capability it must not
/// have, with no visible symptom.
/// </summary>
public sealed class WorkingCalendarValidationTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    [Fact]
    public void Country_scope_requires_a_platform_actor()
    {
        var result = WorkingCalendarValidation.ValidateScope(
            WorkingCalendarScopeType.Country, TenantId, isPlatformActor: false, organizationUnitId: null, legalEntityId: null);

        Assert.False(result.Ok);
        Assert.Equal(403, result.StatusCode);
        Assert.Equal("country_scope_requires_platform_actor", result.ReasonCode);
    }

    [Fact]
    public void Country_scope_is_allowed_for_a_platform_actor()
    {
        var result = WorkingCalendarValidation.ValidateScope(
            WorkingCalendarScopeType.Country, null, isPlatformActor: true, organizationUnitId: null, legalEntityId: null);

        Assert.True(result.Ok);
    }

    [Fact]
    public void Country_scope_cannot_carry_an_organization_unit()
    {
        var result = WorkingCalendarValidation.ValidateScope(
            WorkingCalendarScopeType.Country, null, isPlatformActor: true, organizationUnitId: Guid.NewGuid(), legalEntityId: null);

        Assert.False(result.Ok);
    }

    [Fact]
    public void Tenant_scope_without_an_ambient_tenant_is_rejected()
    {
        // A tenant-surface caller whose token carried no tenant would otherwise create a row owned by nobody.
        var result = WorkingCalendarValidation.ValidateScope(
            WorkingCalendarScopeType.Tenant, null, isPlatformActor: false, organizationUnitId: null, legalEntityId: null);

        Assert.False(result.Ok);
        Assert.Equal("tenant_scope_requires_tenant", result.ReasonCode);
    }

    [Theory]
    [InlineData(WorkingCalendarScopeType.Tenant)]
    [InlineData(WorkingCalendarScopeType.OrganizationUnit)]
    public void Platform_actor_can_author_the_country_layer_only(string scopeType)
    {
        // The platform surface has no scope selector, so this can only arrive from a tampered payload. It must fail
        // as "wrong surface", not as "missing tenant" — the old reason code read like a missing X-Tenant-Id header.
        var result = WorkingCalendarValidation.ValidateScope(
            scopeType, null, isPlatformActor: true, organizationUnitId: null, legalEntityId: null);

        Assert.False(result.Ok);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal("platform_surface_is_country_only", result.ReasonCode);
    }

    [Fact]
    public void Organization_unit_scope_requires_the_unit_id()
    {
        var result = WorkingCalendarValidation.ValidateScope(
            WorkingCalendarScopeType.OrganizationUnit, TenantId, isPlatformActor: false, organizationUnitId: null, legalEntityId: null);

        Assert.False(result.Ok);
        Assert.Equal("org_scope_requires_organization_unit", result.ReasonCode);
    }

    [Fact]
    public void Legal_entity_scope_requires_exactly_the_legal_entity_fk()
    {
        var missing = WorkingCalendarValidation.ValidateScope(
            WorkingCalendarScopeType.LegalEntity, TenantId, false, null, null);
        var valid = WorkingCalendarValidation.ValidateScope(
            WorkingCalendarScopeType.LegalEntity, TenantId, false, null, Guid.NewGuid());
        var leakedOrg = WorkingCalendarValidation.ValidateScope(
            WorkingCalendarScopeType.LegalEntity, TenantId, false, Guid.NewGuid(), Guid.NewGuid());

        Assert.False(missing.Ok);
        Assert.Equal("legal_entity_scope_requires_legal_entity", missing.ReasonCode);
        Assert.True(valid.Ok);
        Assert.False(leakedOrg.Ok);
        Assert.Equal("organization_unit_forbidden_for_scope", leakedOrg.ReasonCode);
    }

    [Theory]
    [InlineData(WorkingCalendarScopeType.Tenant)]
    [InlineData(WorkingCalendarScopeType.OrganizationUnit)]
    public void Legal_entity_fk_is_forbidden_outside_legal_entity_scope(string scopeType)
    {
        Guid? organizationUnitId = scopeType == WorkingCalendarScopeType.OrganizationUnit ? Guid.NewGuid() : null;
        var result = WorkingCalendarValidation.ValidateScope(
            scopeType, TenantId, isPlatformActor: false, organizationUnitId, Guid.NewGuid());

        Assert.False(result.Ok);
        Assert.Equal("legal_entity_forbidden_for_scope", result.ReasonCode);
    }

    [Fact]
    public void Unknown_scope_is_rejected()
    {
        var result = WorkingCalendarValidation.ValidateScope("global", TenantId, false, null, null);
        Assert.False(result.Ok);
    }

    // ── Day-type boundary ────────────────────────────────────────────────────

    [Theory]
    [InlineData(WorkingCalendarDayType.PublicHoliday)]
    [InlineData(WorkingCalendarDayType.ReligiousHoliday)]
    [InlineData(WorkingCalendarDayType.MoveableHoliday)]
    public void Country_layer_day_types_are_rejected_on_a_tenant_override(string dayType)
    {
        var result = WorkingCalendarValidation.ValidateDayType(dayType, isCountryLayer: false);

        Assert.False(result.Ok);
        Assert.Equal("day_type_reserved_for_country_layer", result.ReasonCode);
    }

    [Theory]
    [InlineData(WorkingCalendarDayType.CompanyHoliday)]
    [InlineData(WorkingCalendarDayType.CompanyClosure)]
    [InlineData(WorkingCalendarDayType.WorkingDayOverride)]
    public void Override_authorable_day_types_are_accepted_on_a_tenant_override(string dayType)
    {
        Assert.True(WorkingCalendarValidation.ValidateDayType(dayType, isCountryLayer: false).Ok);
    }

    [Fact]
    public void Country_layer_may_use_every_day_type()
    {
        foreach (var dayType in WorkingCalendarDayType.All)
        {
            Assert.True(WorkingCalendarValidation.ValidateDayType(dayType, isCountryLayer: true).Ok);
        }
    }

    // ── Weekend + source + freeze ────────────────────────────────────────────

    [Fact]
    public void Country_calendar_must_declare_a_weekend()
    {
        var result = WorkingCalendarValidation.ValidateWeekendDays(null, WorkingCalendarScopeType.Country);

        Assert.False(result.Ok);
        Assert.Equal("weekend_days_required", result.ReasonCode);
    }

    [Fact]
    public void Override_may_omit_the_weekend_to_inherit_it()
    {
        Assert.True(WorkingCalendarValidation.ValidateWeekendDays(null, WorkingCalendarScopeType.Tenant).Ok);
    }

    [Fact]
    public void Duplicate_weekend_days_are_rejected()
    {
        var result = WorkingCalendarValidation.ValidateWeekendDays(
            new[] { WorkingCalendarDayOfWeek.Saturday, WorkingCalendarDayOfWeek.Saturday },
            WorkingCalendarScopeType.Country);

        Assert.False(result.Ok);
    }

    [Fact]
    public void Provider_fetch_source_is_rejected_until_the_review_flow_exists()
    {
        var result = WorkingCalendarValidation.ValidateSource(WorkingCalendarSource.ProviderFetch);

        Assert.False(result.Ok);
        Assert.Equal("source_reserved", result.ReasonCode);
    }

    [Fact]
    public void Manual_source_is_writable()
    {
        Assert.True(WorkingCalendarValidation.ValidateSource(WorkingCalendarSource.Manual).Ok);
    }

    [Fact]
    public void Active_calendar_freezes_identity_but_not_content()
    {
        var calendar = new Wc
        {
            CountryCode = "TR",
            CalendarYear = 2026,
            ScopeType = WorkingCalendarScopeType.Country,
            CalendarStatus = WorkingCalendarStatus.Active
        };

        var changedCountry = WorkingCalendarValidation.ValidateIdentityNotFrozen(
            calendar, "DE", 2026, WorkingCalendarScopeType.Country, null, null);
        Assert.False(changedCountry.Ok);
        Assert.Equal(409, changedCountry.StatusCode);

        // Same identity → allowed, so weekend days and day entries stay editable on an active calendar.
        var unchanged = WorkingCalendarValidation.ValidateIdentityNotFrozen(
            calendar, "TR", 2026, WorkingCalendarScopeType.Country, null, null);
        Assert.True(unchanged.Ok);
    }

    [Fact]
    public void Draft_calendar_identity_is_not_frozen()
    {
        var calendar = new Wc
        {
            CountryCode = "TR",
            CalendarYear = 2026,
            ScopeType = WorkingCalendarScopeType.Country,
            CalendarStatus = WorkingCalendarStatus.Draft
        };

        Assert.True(WorkingCalendarValidation.ValidateIdentityNotFrozen(
            calendar, "DE", 2027, WorkingCalendarScopeType.Country, null, null).Ok);
    }

    [Fact]
    public void Archived_calendar_is_not_writable()
    {
        var calendar = new Wc { CalendarStatus = WorkingCalendarStatus.Archived };

        var result = WorkingCalendarValidation.ValidateWritable(calendar);

        Assert.False(result.Ok);
        Assert.Equal(409, result.StatusCode);
    }

    // ── Day input ────────────────────────────────────────────────────────────

    private static Wc CountryCal() => new()
    {
        TenantId = null,
        CountryCode = "TR",
        CalendarYear = 2026,
        ScopeType = WorkingCalendarScopeType.Country,
        CalendarStatus = WorkingCalendarStatus.Draft
    };

    private static WorkingCalendarDayInput DayInput(
        DateOnly date, string code = "NEW", string type = WorkingCalendarDayType.PublicHoliday, bool halfDay = false)
        => new(null, code, "Test day", date, null, type, WorkingCalendarRecurrence.None, halfDay, null);

    [Fact]
    public void Day_outside_the_calendar_year_is_rejected()
    {
        var result = WorkingCalendarValidation.ValidateDayInput(
            CountryCal(), DayInput(new DateOnly(2027, 1, 1)), null);

        Assert.False(result.Ok);
        Assert.Equal("day_year_mismatch", result.ReasonCode);
    }

    [Fact]
    public void Duplicate_day_code_is_rejected()
    {
        var calendar = CountryCal();
        calendar.Days.Add(new WorkingCalendarDay
        {
            DayCode = "NEW",
            DayName = "Existing",
            Date = new DateOnly(2026, 5, 1),
            DayType = WorkingCalendarDayType.PublicHoliday,
            DayStatus = WorkingCalendarDayStatus.Active
        });

        var result = WorkingCalendarValidation.ValidateDayInput(
            calendar, DayInput(new DateOnly(2026, 6, 1), code: "NEW"), null);

        Assert.False(result.Ok);
        Assert.Equal("duplicate_day_code", result.ReasonCode);
    }

    [Fact]
    public void Two_active_days_cannot_govern_the_same_effective_date()
    {
        var calendar = CountryCal();
        calendar.Days.Add(new WorkingCalendarDay
        {
            DayCode = "A",
            DayName = "Existing",
            Date = new DateOnly(2026, 5, 1),
            DayType = WorkingCalendarDayType.PublicHoliday,
            DayStatus = WorkingCalendarDayStatus.Active
        });

        var result = WorkingCalendarValidation.ValidateDayInput(
            calendar, DayInput(new DateOnly(2026, 5, 1), code: "B"), null);

        Assert.False(result.Ok);
        Assert.Equal("duplicate_day_date", result.ReasonCode);
    }

    [Fact]
    public void A_working_day_override_cannot_also_be_a_half_day()
    {
        var result = WorkingCalendarValidation.ValidateDayInput(
            CountryCal(),
            DayInput(new DateOnly(2026, 5, 1), type: WorkingCalendarDayType.WorkingDayOverride, halfDay: true),
            null);

        Assert.False(result.Ok);
        Assert.Equal("half_day_on_override", result.ReasonCode);
    }

    [Fact]
    public void Tenant_contract_slice_hides_country_scope_and_country_day_types()
    {
        var contract = WorkingCalendarValidation.BuildOverrideContract();

        Assert.DoesNotContain(WorkingCalendarScopeType.Country, contract.ScopeTypes);
        Assert.DoesNotContain(WorkingCalendarDayType.PublicHoliday, contract.DayTypes);
        Assert.Contains(WorkingCalendarDayType.WorkingDayOverride, contract.DayTypes);
        Assert.DoesNotContain(WorkingCalendarPermissions.Manage, contract.Permissions);
    }

    [Fact]
    public void Full_contract_exposes_every_vocabulary_and_no_hardcoded_gap()
    {
        var contract = WorkingCalendarValidation.BuildContract();

        // The PLATFORM slice offers the country layer only — it is not the union of both layers. The tenant slice
        // still carries tenant/organization-unit; see Override_contract_hides_the_country_layer.
        Assert.Equal(WorkingCalendarScopeType.PlatformAuthorable, contract.ScopeTypes);
        Assert.Equal(new[] { WorkingCalendarScopeType.Country }, contract.ScopeTypes);
        Assert.Equal(WorkingCalendarDayType.All, contract.DayTypes);
        Assert.Equal(WorkingCalendarDayOfWeek.All, contract.DayOfWeek);
        Assert.DoesNotContain(WorkingCalendarSource.ProviderFetch, contract.WritableSources);
    }
}
