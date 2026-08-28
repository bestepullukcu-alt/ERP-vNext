using Diten.Platform.Application.Features.WorkingCalendar.Provider;
using Diten.Platform.Domain.Entities.WorkingCalendar;
using Xunit;
using Wc = Diten.Platform.Domain.Entities.WorkingCalendar.WorkingCalendar;

namespace Diten.Platform.Application.Tests.WorkingCalendar;

/// <summary>
/// The resolution order is the whole product. These tests pin each rung of the ladder and, just as importantly, the
/// cases where the engine must REFUSE to answer instead of guessing.
/// </summary>
public sealed class WorkingCalendarResolveEngineTests
{
    private const string Country = "TR";
    private static readonly DateOnly Saturday = new(2026, 8, 29);
    private static readonly DateOnly Wednesday = new(2026, 8, 26);

    private static Wc CountryCalendar(params WorkingCalendarDay[] days) => new()
    {
        TenantId = null,
        CalendarCode = "TR-2026",
        CountryCode = Country,
        CalendarYear = 2026,
        ScopeType = WorkingCalendarScopeType.Country,
        CalendarStatus = WorkingCalendarStatus.Active,
        WeekendDays = new List<string> { WorkingCalendarDayOfWeek.Saturday, WorkingCalendarDayOfWeek.Sunday },
        Days = days.ToList()
    };

    private static Wc OverrideCalendar(List<string>? weekend, params WorkingCalendarDay[] days) => new()
    {
        TenantId = Guid.NewGuid(),
        CalendarCode = "ACME-2026",
        CountryCode = Country,
        CalendarYear = 2026,
        ScopeType = WorkingCalendarScopeType.Tenant,
        CalendarStatus = WorkingCalendarStatus.Active,
        WeekendDays = weekend,
        Days = days.ToList()
    };

    private static WorkingCalendarDay Day(
        DateOnly date, string type, bool halfDay = false, DateOnly? observed = null, string code = "D1") => new()
    {
        DayCode = code,
        DayName = $"{type} {date:yyyy-MM-dd}",
        Date = date,
        ObservedDate = observed,
        DayType = type,
        Recurrence = WorkingCalendarRecurrence.None,
        IsHalfDay = halfDay,
        DayStatus = WorkingCalendarDayStatus.Active
    };

    [Fact]
    public void No_calendar_returns_unresolved_and_never_guesses()
    {
        var result = WorkingCalendarResolveEngine.ResolveWorkingDay(Wednesday, Country, null, null);

        Assert.Equal(WorkingCalendarResolution.CalendarMissing, result.Resolution);
        Assert.Null(result.IsWorkingDay);
        Assert.Contains(WorkingCalendarReasonCodes.CalendarMissing, result.ReasonCodes);
    }

    [Fact]
    public void Wrong_year_returns_year_missing_rather_than_a_neighbouring_year()
    {
        var result = WorkingCalendarResolveEngine.ResolveWorkingDay(
            new DateOnly(2027, 3, 3), Country, CountryCalendar(), null);

        Assert.Equal(WorkingCalendarResolution.YearMissing, result.Resolution);
        Assert.Null(result.IsWorkingDay);
    }

    [Fact]
    public void Ordinary_weekday_is_a_working_day()
    {
        var result = WorkingCalendarResolveEngine.ResolveWorkingDay(Wednesday, Country, CountryCalendar(), null);

        Assert.True(result.IsWorkingDay);
        Assert.Contains(WorkingCalendarReasonCodes.WorkingDay, result.ReasonCodes);
    }

    [Fact]
    public void Weekend_day_is_not_a_working_day()
    {
        var result = WorkingCalendarResolveEngine.ResolveWorkingDay(Saturday, Country, CountryCalendar(), null);

        Assert.False(result.IsWorkingDay);
        Assert.Contains(WorkingCalendarReasonCodes.WeekendDay, result.ReasonCodes);
    }

    [Fact]
    public void Country_holiday_beats_an_ordinary_weekday()
    {
        var calendar = CountryCalendar(Day(Wednesday, WorkingCalendarDayType.PublicHoliday));

        var result = WorkingCalendarResolveEngine.ResolveWorkingDay(Wednesday, Country, calendar, null);

        Assert.False(result.IsWorkingDay);
        Assert.Contains(WorkingCalendarReasonCodes.PublicHoliday, result.ReasonCodes);
        Assert.NotNull(result.Holiday);
    }

    [Fact]
    public void Tenant_working_day_override_beats_a_country_holiday()
    {
        var country = CountryCalendar(Day(Wednesday, WorkingCalendarDayType.PublicHoliday));
        var tenant = OverrideCalendar(null, Day(Wednesday, WorkingCalendarDayType.WorkingDayOverride, code: "COMP"));

        var result = WorkingCalendarResolveEngine.ResolveWorkingDay(Wednesday, Country, country, tenant);

        Assert.True(result.IsWorkingDay);
        Assert.Contains(WorkingCalendarReasonCodes.WorkingDayOverrideApplied, result.ReasonCodes);
        Assert.Contains(WorkingCalendarReasonCodes.TenantOverrideApplied, result.ReasonCodes);
    }

    [Fact]
    public void Compensation_day_beats_the_weekend_too()
    {
        var tenant = OverrideCalendar(null, Day(Saturday, WorkingCalendarDayType.WorkingDayOverride, code: "BRIDGE"));

        var result = WorkingCalendarResolveEngine.ResolveWorkingDay(Saturday, Country, CountryCalendar(), tenant);

        Assert.True(result.IsWorkingDay);
        Assert.Contains(WorkingCalendarReasonCodes.WorkingDayOverrideApplied, result.ReasonCodes);
    }

    [Fact]
    public void Company_closure_makes_an_ordinary_weekday_non_working()
    {
        var tenant = OverrideCalendar(null, Day(Wednesday, WorkingCalendarDayType.CompanyClosure));

        var result = WorkingCalendarResolveEngine.ResolveWorkingDay(Wednesday, Country, CountryCalendar(), tenant);

        Assert.False(result.IsWorkingDay);
        Assert.Contains(WorkingCalendarReasonCodes.CompanyClosure, result.ReasonCodes);
    }

    [Fact]
    public void Half_day_counts_as_working_and_says_so_out_loud()
    {
        var calendar = CountryCalendar(Day(Wednesday, WorkingCalendarDayType.PublicHoliday, halfDay: true));

        var result = WorkingCalendarResolveEngine.ResolveWorkingDay(Wednesday, Country, calendar, null);

        Assert.True(result.IsWorkingDay);
        Assert.Contains(WorkingCalendarReasonCodes.HalfDayTreatedAsWorking, result.ReasonCodes);
    }

    [Fact]
    public void Observed_date_governs_when_a_holiday_is_shifted()
    {
        var nominal = new DateOnly(2026, 8, 30);   // Sunday
        var observed = new DateOnly(2026, 8, 31);  // Monday
        var calendar = CountryCalendar(Day(nominal, WorkingCalendarDayType.PublicHoliday, observed: observed));

        var shifted = WorkingCalendarResolveEngine.ResolveWorkingDay(observed, Country, calendar, null);
        Assert.False(shifted.IsWorkingDay);

        // The nominal date is only a Sunday now — non-working because of the weekend, not because of the holiday.
        var original = WorkingCalendarResolveEngine.ResolveWorkingDay(nominal, Country, calendar, null);
        Assert.False(original.IsWorkingDay);
        Assert.Contains(WorkingCalendarReasonCodes.WeekendDay, original.ReasonCodes);
    }

    [Fact]
    public void Archived_day_is_ignored_by_resolution()
    {
        var day = Day(Wednesday, WorkingCalendarDayType.PublicHoliday);
        day.DayStatus = WorkingCalendarDayStatus.Archived;

        var result = WorkingCalendarResolveEngine.ResolveWorkingDay(Wednesday, Country, CountryCalendar(day), null);

        Assert.True(result.IsWorkingDay);
    }

    // ── D3: null inherits, empty does not ────────────────────────────────────

    [Fact]
    public void Null_weekend_on_the_override_inherits_the_country_weekend()
    {
        var tenant = OverrideCalendar(weekend: null);

        var result = WorkingCalendarResolveEngine.ResolveWorkingDay(Saturday, Country, CountryCalendar(), tenant);

        Assert.False(result.IsWorkingDay);
        Assert.Contains(WorkingCalendarReasonCodes.WeekendInheritedFromCountry, result.ReasonCodes);
    }

    [Fact]
    public void Empty_weekend_on_the_override_means_no_weekend_and_is_not_inheritance()
    {
        // This is the distinction that would disappear if null and empty were collapsed: a seven-day organization
        // would silently get the country's Saturday/Sunday back.
        var tenant = OverrideCalendar(weekend: new List<string>());

        var result = WorkingCalendarResolveEngine.ResolveWorkingDay(Saturday, Country, CountryCalendar(), tenant);

        Assert.True(result.IsWorkingDay);
        Assert.Contains(WorkingCalendarReasonCodes.WeekendFromTenantOverride, result.ReasonCodes);
    }

    [Fact]
    public void Declared_override_weekend_wins_over_the_country_weekend()
    {
        var friday = new DateOnly(2026, 8, 28);
        var tenant = OverrideCalendar(new List<string>
        {
            WorkingCalendarDayOfWeek.Friday, WorkingCalendarDayOfWeek.Saturday
        });

        var fridayResult = WorkingCalendarResolveEngine.ResolveWorkingDay(friday, Country, CountryCalendar(), tenant);
        Assert.False(fridayResult.IsWorkingDay);

        // Sunday is a country weekend day but NOT in the override's set, so it becomes a working day.
        var sunday = new DateOnly(2026, 8, 30);
        var sundayResult = WorkingCalendarResolveEngine.ResolveWorkingDay(sunday, Country, CountryCalendar(), tenant);
        Assert.True(sundayResult.IsWorkingDay);
    }

    [Fact]
    public void Effective_weekend_reports_which_layer_it_came_from()
    {
        WorkingCalendarResolveEngine.EffectiveWeekend(CountryCalendar(), OverrideCalendar(null), out var inherited);
        Assert.True(inherited);

        WorkingCalendarResolveEngine.EffectiveWeekend(
            CountryCalendar(), OverrideCalendar(new List<string> { WorkingCalendarDayOfWeek.Friday }), out var declared);
        Assert.False(declared);
    }
}
