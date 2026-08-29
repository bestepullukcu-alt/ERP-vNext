using Diten.Platform.Application.Features.WorkingCalendar.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.WorkingCalendar.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.WorkingCalendar.Queries;
using Diten.Platform.Domain.Entities.WorkingCalendar;
using Diten.Platform.Domain.Repositories;
using Moq;
using Xunit;
using Wc = Diten.Platform.Domain.Entities.WorkingCalendar.WorkingCalendar;

namespace Diten.Platform.Application.Tests.WorkingCalendar;

public sealed class GetWorkingCalendarByIdHandlerTests
{
    private static Wc Row(Guid? tenantId, string status = WorkingCalendarStatus.Active) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        CalendarCode = tenantId is null ? "TR-2026" : "ACME-TR-2026",
        CalendarName = tenantId is null ? "Türkiye 2026" : "ACME Türkiye 2026",
        CountryCode = "TR",
        CalendarYear = 2026,
        ScopeType = tenantId is null ? WorkingCalendarScopeType.Country : WorkingCalendarScopeType.Tenant,
        CalendarStatus = status,
        WeekendDays = tenantId is null ? [WorkingCalendarDayOfWeek.Saturday, WorkingCalendarDayOfWeek.Sunday] : null
    };

    [Fact]
    public async Task Tenant_by_id_falls_back_to_ACTIVE_country_row_as_read_only()
    {
        var country = Row(tenantId: null);
        var repository = new Mock<IWorkingCalendarRepository>();
        repository.Setup(x => x.GetOwnOverrideByIdAsync(country.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Wc?)null);
        repository.Setup(x => x.GetCountryLayerByIdAsync(country.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(country);

        var response = await new GetWorkingCalendarByIdHandler(repository.Object)
            .Handle(new GetWorkingCalendarByIdQuery(country.Id, CountryLayer: false), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(200, response.StatusCode);
        Assert.True(response.Data!.IsCountryLayer);
        Assert.True(response.Data.IsReadOnly);
    }

    [Theory]
    [InlineData(WorkingCalendarStatus.Draft)]
    [InlineData(WorkingCalendarStatus.Archived)]
    public async Task Tenant_by_id_does_not_expose_non_active_country_rows(string status)
    {
        var country = Row(tenantId: null, status);
        var repository = new Mock<IWorkingCalendarRepository>();
        repository.Setup(x => x.GetOwnOverrideByIdAsync(country.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Wc?)null);
        repository.Setup(x => x.GetCountryLayerByIdAsync(country.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(country);

        var response = await new GetWorkingCalendarByIdHandler(repository.Object)
            .Handle(new GetWorkingCalendarByIdQuery(country.Id, CountryLayer: false), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
        Assert.Null(response.Data);
    }

    [Fact]
    public async Task Tenant_own_override_detail_stays_writable()
    {
        var own = Row(Guid.NewGuid());
        var repository = new Mock<IWorkingCalendarRepository>();
        repository.Setup(x => x.GetOwnOverrideByIdAsync(own.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(own);
        repository.Setup(x => x.GetCountryLayerAsync(own.CountryCode, own.CalendarYear, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Wc>());

        var response = await new GetWorkingCalendarByIdHandler(repository.Object)
            .Handle(new GetWorkingCalendarByIdQuery(own.Id, CountryLayer: false), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.False(response.Data!.IsCountryLayer);
        Assert.False(response.Data.IsReadOnly);
        repository.Verify(
            x => x.GetCountryLayerByIdAsync(own.Id, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Platform_by_id_branch_is_unchanged()
    {
        var draftCountry = Row(tenantId: null, WorkingCalendarStatus.Draft);
        var repository = new Mock<IWorkingCalendarRepository>();
        repository.Setup(x => x.GetCountryLayerByIdAsync(draftCountry.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(draftCountry);

        var response = await new GetWorkingCalendarByIdHandler(repository.Object)
            .Handle(new GetWorkingCalendarByIdQuery(draftCountry.Id, CountryLayer: true), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.False(response.Data!.IsReadOnly);
        repository.Verify(
            x => x.GetOwnOverrideByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Tenant_write_guard_still_returns_404_for_country_id()
    {
        var country = Row(tenantId: null);
        var repository = new Mock<IWorkingCalendarRepository>();
        repository.Setup(x => x.GetOwnOverrideByIdAsync(country.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Wc?)null);

        var result = await WorkingCalendarWriteGuard.LoadWritableAsync(
            repository.Object, country.Id, isPlatformActor: false, CancellationToken.None);

        Assert.Null(result.Calendar);
        Assert.Equal(404, result.Status);
        repository.Verify(
            x => x.GetCountryLayerByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
