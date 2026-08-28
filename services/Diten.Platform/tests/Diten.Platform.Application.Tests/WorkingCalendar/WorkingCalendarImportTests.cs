using Diten.Platform.Application.Features.WorkingCalendarImport;
using Diten.Platform.Domain.Entities.WorkingCalendar;
using Diten.Platform.Domain.Repositories;
using Moq;
using Microsoft.Extensions.Options;
using Xunit;
using Wc = Diten.Platform.Domain.Entities.WorkingCalendar.WorkingCalendar;

namespace Diten.Platform.Application.Tests.WorkingCalendar;

public sealed class WorkingCalendarImportTests
{
    [Fact]
    public async Task Fetch_creates_staging_and_never_writes_live_calendar()
    {
        var target = Calendar();
        var calendars = new Mock<IWorkingCalendarRepository>();
        calendars.Setup(x => x.GetCountryLayerByIdAsync(target.Id, It.IsAny<CancellationToken>())).ReturnsAsync(target);
        var batches = new Mock<IWorkingCalendarImportBatchRepository>();
        batches.Setup(x => x.HasOpenBatchAsync(target.Id, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        batches.Setup(x => x.CreateAsync(It.IsAny<WorkingCalendarImportBatch>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkingCalendarImportBatch x, CancellationToken _) => x);
        batches.Setup(x => x.ReplaceAsync(It.IsAny<WorkingCalendarImportBatch>(), 1, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        WorkingCalendarImportBatch? staged = null;
        batches.Setup(x => x.ReplaceAsync(It.IsAny<WorkingCalendarImportBatch>(), 1, It.IsAny<CancellationToken>()))
            .Callback<WorkingCalendarImportBatch, int, CancellationToken>((x, _, _) => staged = x).ReturnsAsync(true);
        var provider = new Mock<IHolidayProvider>(); provider.SetupGet(x => x.ProviderKey).Returns("test");
        provider.Setup(x => x.FetchAsync("TR", 2026, It.IsAny<CancellationToken>())).ReturnsAsync(new HolidayFetchResult(
            HolidayProviderOutcome.Succeeded,
            new[] { new ProviderHoliday(new DateOnly(2026, 1, 1), "New Year", "Yılbaşı", new[] { "Public" }, true, null, "ref-1") },
            "test", "test://holidays", DateTimeOffset.UtcNow, "hash"));

        var result = await new StartWorkingCalendarImportHandler(calendars.Object, batches.Object, provider.Object,
                Options.Create(new WorkingCalendarImportOptions { Enabled = true }))
            .Handle(new(target.Id, false, null, WorkingCalendarImportTriggerSource.Manual, "maker"), default);

        Assert.True(result.IsSuccessful); Assert.NotNull(staged);
        Assert.Equal(WorkingCalendarImportStatus.PendingReview, staged!.ImportStatus);
        Assert.Single(staged.Candidates); Assert.Null(staged.Candidates[0].AppliedDayId);
        calendars.Verify(x => x.ReplaceAsync(It.IsAny<Wc>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Apply_adds_all_approved_days_with_one_calendar_replace()
    {
        var calendar = Calendar();
        var batch = Batch(calendar, "maker", 2);
        batch.Candidates.Add(Candidate(new DateOnly(2026, 1, 1), "NY"));
        batch.Candidates.Add(Candidate(new DateOnly(2026, 4, 23), "NS"));
        batch.RecalculateCounts();
        var calendars = new Mock<IWorkingCalendarRepository>();
        calendars.Setup(x => x.GetCountryLayerByIdAsync(calendar.Id, It.IsAny<CancellationToken>())).ReturnsAsync(calendar);
        calendars.Setup(x => x.ReplaceAsync(calendar, 1, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var batches = new Mock<IWorkingCalendarImportBatchRepository>();
        batches.Setup(x => x.GetByIdAsync(batch.Id, It.IsAny<CancellationToken>())).ReturnsAsync(batch);
        batches.Setup(x => x.ReplaceAsync(batch, 2, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await new ApplyWorkingCalendarImportHandler(calendars.Object, batches.Object)
            .Handle(new(batch.Id, 2, 1, "checker", true, false), default);

        Assert.True(result.IsSuccessful); Assert.Equal(2, calendar.Days.Count);
        Assert.All(calendar.Days, x => { Assert.Equal(WorkingCalendarSource.ProviderFetch, x.Source); Assert.Equal(batch.Id, x.ProviderBatchId); Assert.Null(x.ObservedDate); });
        calendars.Verify(x => x.ReplaceAsync(calendar, 1, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("maker")]
    [InlineData(WorkingCalendarImportActors.Scheduler)]
    public async Task Apply_enforces_segregation_of_duties_and_system_checker_forbidden(string checker)
    {
        var calendar = Calendar(); var batch = Batch(calendar, "maker", 1); batch.RecalculateCounts();
        var batches = new Mock<IWorkingCalendarImportBatchRepository>();
        batches.Setup(x => x.GetByIdAsync(batch.Id, It.IsAny<CancellationToken>())).ReturnsAsync(batch);
        var result = await new ApplyWorkingCalendarImportHandler(new Mock<IWorkingCalendarRepository>().Object, batches.Object)
            .Handle(new(batch.Id, 1, 1, checker, true, false), default);
        Assert.Equal(403, result.StatusCode);
    }

    private static Wc Calendar() => new() { CalendarCode = "TR-2026", CalendarName = "TR", CountryCode = "TR",
        CalendarYear = 2026, ScopeType = WorkingCalendarScopeType.Country, WeekendDays = new() { "saturday", "sunday" },
        CalendarStatus = WorkingCalendarStatus.Draft, Source = WorkingCalendarSource.Manual, CreatedBy = "seed" };
    private static WorkingCalendarImportBatch Batch(Wc target, string maker, int version) => new() { TargetCalendarId = target.Id,
        TargetCalendarCodeSnapshot = target.CalendarCode, CountryCode = target.CountryCode, CalendarYear = target.CalendarYear,
        BatchCode = "B", ProviderKey = "test", ImportStatus = WorkingCalendarImportStatus.InReview,
        RequestedBy = maker, RequestedAt = DateTimeOffset.UtcNow, Version = version, CreatedBy = maker };
    private static WorkingCalendarImportCandidate Candidate(DateOnly date, string code) => new() { ProviderDayKey = code,
        Date = date, ProviderName = code, MappedDayType = WorkingCalendarDayType.PublicHoliday, MappedDayCode = code,
        MappedDayName = code, Decision = WorkingCalendarImportDecision.Approved };
}
