using Diten.Platform.Application.Features.Lookups;
using Diten.Platform.Application.Features.Lookups.Services;
using Diten.Platform.Application.Features.WorkingCalendar.Provider;
using Diten.Platform.Domain.Entities.WorkingCalendar;
using Diten.Platform.Domain.Repositories;
using Moq;
using Xunit;
using Wc = Diten.Platform.Domain.Entities.WorkingCalendar.WorkingCalendar;

namespace Diten.Platform.Application.Tests.WorkingCalendar;

public sealed class WorkingCalendarProviderScopePrecedenceTests
{
    private static readonly Guid OrganizationUnitId = Guid.NewGuid();
    private static readonly Guid LegalEntityId = Guid.NewGuid();
    private static readonly DateOnly ProbeDate = new(2026, 8, 26);

    [Theory]
    [InlineData(true, true, true, WorkingCalendarScopeType.OrganizationUnit)]
    [InlineData(false, true, true, WorkingCalendarScopeType.LegalEntity)]
    [InlineData(false, false, true, WorkingCalendarScopeType.Tenant)]
    [InlineData(false, false, false, WorkingCalendarScopeType.Country)]
    public async Task Ac_chain_selects_the_most_specific_existing_active_scope(
        bool includeOrganizationUnit, bool includeLegalEntity, bool includeTenant, string expectedScope)
    {
        var country = Calendar(WorkingCalendarScopeType.Country, "COUNTRY");
        var candidates = new List<Wc>();
        if (includeTenant) candidates.Add(Calendar(WorkingCalendarScopeType.Tenant, "TENANT"));
        if (includeLegalEntity) candidates.Add(Calendar(WorkingCalendarScopeType.LegalEntity, "LEGAL"));
        if (includeOrganizationUnit) candidates.Add(Calendar(WorkingCalendarScopeType.OrganizationUnit, "ORG"));

        var provider = BuildProvider(country, candidates);
        var result = await provider.IsWorkingDayAsync(
            ProbeDate, new WorkingCalendarScope("TR", OrganizationUnitId, LegalEntityId));

        Assert.Equal(WorkingCalendarResolution.Resolved, result.Resolution);
        if (expectedScope == WorkingCalendarScopeType.Country)
        {
            Assert.Null(result.ResolvedOverrideCalendarId);
            Assert.Equal(country.Id, result.ResolvedCalendarId);
        }
        else
        {
            Assert.Equal(candidates.Single(x => x.ScopeType == expectedScope).Id, result.ResolvedOverrideCalendarId);
        }
    }

    [Fact]
    public async Task Ac_chain_3_passes_one_override_to_the_engine_and_never_merges_scope_rows()
    {
        var country = Calendar(WorkingCalendarScopeType.Country, "COUNTRY");
        var legal = Calendar(WorkingCalendarScopeType.LegalEntity, "LEGAL");
        legal.Days.Add(Day(WorkingCalendarDayType.WorkingDayOverride, "LEGAL-WORK"));

        var organizationUnit = Calendar(WorkingCalendarScopeType.OrganizationUnit, "ORG");
        organizationUnit.Days.Add(Day(WorkingCalendarDayType.CompanyClosure, "ORG-CLOSE"));

        var provider = BuildProvider(country, new[] { legal, organizationUnit });
        var result = await provider.IsWorkingDayAsync(
            ProbeDate, new WorkingCalendarScope("TR", OrganizationUnitId, LegalEntityId));

        Assert.Equal(organizationUnit.Id, result.ResolvedOverrideCalendarId);
        Assert.False(result.IsWorkingDay);
        Assert.Contains(WorkingCalendarReasonCodes.CompanyClosure, result.ReasonCodes);
        Assert.DoesNotContain(WorkingCalendarReasonCodes.WorkingDayOverrideApplied, result.ReasonCodes);
    }

    private static WorkingCalendarProvider BuildProvider(Wc country, IReadOnlyList<Wc> overrides)
    {
        var repository = new Mock<IWorkingCalendarRepository>(MockBehavior.Strict);
        repository.Setup(x => x.GetCountryLayerAsync("TR", 2026, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { country });
        repository.Setup(x => x.GetTenantOverridesAsync(
                "TR", 2026, OrganizationUnitId, LegalEntityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(overrides);

        var lookups = new Mock<IPlatformLookupProvider>(MockBehavior.Strict);
        lookups.Setup(x => x.GetLookupOptionsAsync(PlatformLookupKeys.Countries, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new LookupOptionDto("TR", "Türkiye", "TR") });

        return new WorkingCalendarProvider(repository.Object, lookups.Object);
    }

    private static Wc Calendar(string scopeType, string code) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = scopeType == WorkingCalendarScopeType.Country ? null : Guid.NewGuid(),
        CalendarCode = code,
        CalendarName = code,
        CountryCode = "TR",
        CalendarYear = 2026,
        ScopeType = scopeType,
        OrganizationUnitId = scopeType == WorkingCalendarScopeType.OrganizationUnit ? OrganizationUnitId : null,
        LegalEntityId = scopeType == WorkingCalendarScopeType.LegalEntity ? LegalEntityId : null,
        CalendarStatus = WorkingCalendarStatus.Active,
        WeekendDays = new List<string> { WorkingCalendarDayOfWeek.Saturday, WorkingCalendarDayOfWeek.Sunday }
    };

    private static WorkingCalendarDay Day(string type, string code) => new()
    {
        DayCode = code,
        DayName = code,
        Date = ProbeDate,
        DayType = type,
        DayStatus = WorkingCalendarDayStatus.Active,
        Recurrence = WorkingCalendarRecurrence.None
    };
}
