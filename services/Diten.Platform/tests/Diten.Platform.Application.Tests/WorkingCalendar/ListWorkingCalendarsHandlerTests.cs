using Diten.Platform.Application.Features.WorkingCalendar.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.WorkingCalendar.Queries;
using Diten.Platform.Domain.Entities.WorkingCalendar;
using Diten.Platform.Domain.Repositories;
using Moq;
using Xunit;
using Wc = Diten.Platform.Domain.Entities.WorkingCalendar.WorkingCalendar;

namespace Diten.Platform.Application.Tests.WorkingCalendar;

/// <summary>
/// Locks the 2026-08-27 visibility decision: the tenant override surface shows the ACTIVE country layer read-only
/// alongside the tenant's own rows, while the country surface stays single-layer and no cross-tenant row is ever
/// reachable from either.
/// </summary>
public sealed class ListWorkingCalendarsHandlerTests
{
    private static readonly Guid TenantId = Guid.Parse("97c59330-dbc4-4665-b29c-0c26dbb5cc93");

    private static Wc Row(
        string code,
        Guid? tenantId,
        string status = WorkingCalendarStatus.Active,
        string countryCode = "TR",
        int year = 2026,
        string? scopeType = null)
        => new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CalendarCode = code,
            CalendarName = code,
            CountryCode = countryCode,
            CalendarYear = year,
            ScopeType = scopeType ?? (tenantId is null
                ? WorkingCalendarScopeType.Country
                : WorkingCalendarScopeType.Tenant),
            CalendarStatus = status,
            WeekendDays = tenantId is null ? ["saturday", "sunday"] : null
        };

    private static ListWorkingCalendarsHandler Handler(
        IReadOnlyList<Wc> countryRows,
        IReadOnlyList<Wc> tenantRows,
        out Mock<IWorkingCalendarRepository> repository)
    {
        repository = new Mock<IWorkingCalendarRepository>();
        repository.Setup(x => x.ListCountryLayerAsync(It.IsAny<CancellationToken>())).ReturnsAsync(countryRows);
        repository.Setup(x => x.ListTenantOverridesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(tenantRows);
        return new ListWorkingCalendarsHandler(repository.Object);
    }

    private static ListWorkingCalendarsQuery TenantQuery(bool includeArchived = false)
        => new(CountryLayer: false, null, null, null, null, null, includeArchived);

    private static ListWorkingCalendarsQuery CountryQuery(bool includeArchived = false)
        => new(CountryLayer: true, null, null, null, null, null, includeArchived);

    [Fact]
    public async Task Tenant_surface_shows_active_country_rows_as_read_only_beside_its_own()
    {
        var country = Row("TR-2026", tenantId: null);
        var own = Row("ACME-TR-2026", TenantId);
        var handler = Handler([country], [own], out _);

        var response = await handler.Handle(TenantQuery(), CancellationToken.None);
        var items = response.Data!.Items;

        Assert.Equal(2, items.Count);

        var inherited = Assert.Single(items, x => x.Id == country.Id);
        Assert.True(inherited.IsReadOnly);
        Assert.True(inherited.IsCountryLayer);

        var mine = Assert.Single(items, x => x.Id == own.Id);
        Assert.False(mine.IsReadOnly);
        Assert.False(mine.IsCountryLayer);
    }

    [Fact]
    public async Task Inherited_country_row_sorts_above_the_overrides_that_layer_on_it()
    {
        var country = Row("ZZ-COUNTRY", tenantId: null);
        var own = Row("AAA-OVERRIDE", TenantId);
        var handler = Handler([country], [own], out _);

        var items = (await handler.Handle(TenantQuery(), CancellationToken.None)).Data!.Items;

        // Alphabetically AAA-OVERRIDE would win; the country row is pulled up deliberately so the group reads
        // "here is the country calendar, and here is what I changed".
        Assert.Equal(country.Id, items[0].Id);
        Assert.Equal(own.Id, items[1].Id);
    }

    [Theory]
    [InlineData(WorkingCalendarStatus.Draft)]
    [InlineData(WorkingCalendarStatus.Archived)]
    public async Task Only_ACTIVE_country_rows_are_inherited(string status)
    {
        // A draft or archived country calendar resolves to nothing, so advertising it on the tenant surface would
        // promise an inheritance that does not exist. includeArchived must not resurrect it either.
        var country = Row("TR-2026", tenantId: null, status: status);
        var handler = Handler([country], [], out _);

        var items = (await handler.Handle(TenantQuery(includeArchived: true), CancellationToken.None)).Data!.Items;

        Assert.Empty(items);
    }

    [Fact]
    public async Task Tenant_with_no_overrides_still_sees_the_country_layer()
    {
        var country = Row("TR-2026", tenantId: null);
        var handler = Handler([country], [], out _);

        var items = (await handler.Handle(TenantQuery(), CancellationToken.None)).Data!.Items;

        var only = Assert.Single(items);
        Assert.True(only.IsReadOnly);
    }

    [Fact]
    public async Task Country_surface_stays_single_layer_and_never_marks_rows_read_only()
    {
        var country = Row("TR-2026", tenantId: null);
        var handler = Handler([country], [Row("ACME-TR-2026", TenantId)], out var repository);

        var items = (await handler.Handle(CountryQuery(), CancellationToken.None)).Data!.Items;

        var only = Assert.Single(items);
        Assert.Equal(country.Id, only.Id);
        Assert.False(only.IsReadOnly);

        // The operator surface must not even ask for tenant rows.
        repository.Verify(x => x.ListTenantOverridesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Tenant_surface_reads_only_the_country_layer_never_another_tenants_rows()
    {
        // AC-SEC-3/9 stay intact: the handler's only extra source is ListCountryLayerAsync, which filters
        // TenantId == null. There is no code path here that can widen to another tenant.
        var handler = Handler([Row("TR-2026", tenantId: null)], [Row("ACME-TR-2026", TenantId)], out var repository);

        await handler.Handle(TenantQuery(), CancellationToken.None);

        repository.Verify(x => x.ListCountryLayerAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        repository.Verify(x => x.ListTenantOverridesAsync(It.IsAny<CancellationToken>()), Times.Once);
        repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Country_filter_narrows_the_inherited_rows_too()
    {
        var tr = Row("TR-2026", tenantId: null, countryCode: "TR");
        var by = Row("BY-2026", tenantId: null, countryCode: "BY");
        var handler = Handler([tr, by], [], out _);

        var query = new ListWorkingCalendarsQuery(
            CountryLayer: false, "BY", null, null, null, null, false);
        var items = (await handler.Handle(query, CancellationToken.None)).Data!.Items;

        var only = Assert.Single(items);
        Assert.Equal(by.Id, only.Id);
    }
}
