using System.Net;
using Diten.Platform.Application.Features.TimeEntry;
using Diten.Platform.Application.Features.TimeEntry.Services;
using Diten.Platform.Application.Tests.Persistence;
using Diten.Platform.Common.Persistence;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.TimeEntry;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Diten.Platform.Infrastructure.Persistence.Schema;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.TimeEntry;

/// <summary>
/// BL-533 — the timesheet stores replace the WHOLE document, so a write must be conditional on the version the document
/// in hand was READ at. The weekly save is raced on the real wire; every repository is also held to the rule directly:
/// a copy read at N is refused even when the caller names the version stored NOW, and nothing it carries is written.
/// Disposable mongod (<see cref="TimeEntryMongoFixture"/>), a fresh tenant per test class.
/// </summary>
[Collection(TimeEntryMongoCollection.Name)]
public sealed class TimesheetReadVersionRaceMongoTests : TimeEntryScenario
{
    private readonly ReadBarrier _barrier = new(armed: false);

    public TimesheetReadVersionRaceMongoTests(TimeEntryMongoFixture fixture) : base(fixture)
    {
    }

    protected override void ConfigureHost(IServiceCollection services)
        => services.AddScoped<ITimesheetWeekReader>(sp => _barrier.Wrap<ITimesheetWeekReader>(
            ActivatorUtilities.CreateInstance<TimesheetWeekReader>(sp), nameof(ITimesheetWeekReader.LoadAsync)));

    private TenantContext TenantScope()
    {
        var context = new TenantContext();
        context.SetTenant(Tenant);
        return context;
    }

    private async Task<T> RawAsync<T>(string collection, Guid id) where T : BaseEntity
        => await Collection<T>(collection).Find(Builders<T>.Filter.Eq(x => x.Id, id)).SingleAsync();

    // ── The weekly save, on the wire ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Two_saves_of_one_week_that_read_the_same_version_one_wins_and_the_other_is_refused()
    {
        Assert.Equal(HttpStatusCode.OK, (await SaveFreshAsync(CurrentWeek, Row(Monday, 120, TaskA))).Status);
        var version = await VersionAsync();

        _barrier.Arm();
        var loser = Task.Run(() => SaveAsync(version, CurrentWeek, null, Row(Monday, 60, TaskA)));
        await _barrier.Reached.WaitAsync(TimeSpan.FromSeconds(30));
        var winner = await SaveAsync(version, CurrentWeek, null, Row(Monday, 240, TaskA));
        _barrier.Release();
        var refused = await loser;

        Assert.Equal(HttpStatusCode.OK, winner.Status);
        Assert.Equal(HttpStatusCode.Conflict, refused.Status);
        Assert.Equal(TimeEntryReasonCodes.ConcurrencyConflict, refused.ReasonCode);
        var weekId = winner.Data.GetProperty("weekId").GetGuid();
        var rows = await Collection<Domain.Entities.TimeEntry.TimeEntry>(PlatformCollections.TimeEntryEntries)
            .Find(e => e.TimesheetWeekId == weekId && !e.IsDeleted).ToListAsync();
        Assert.Equal(240, Assert.Single(rows).DurationMinutes);
        Assert.Equal(240, (await RawAsync<TimesheetWeek>(PlatformCollections.TimeEntryTimesheetWeeks, weekId)).TotalMinutes);
    }

    // ── Each store, directly ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_week_copy_read_before_another_write_is_refused_even_under_the_stored_version()
    {
        var weeks = new TimesheetWeekRepository(Fixture.DbContext, TenantScope());
        var week = await weeks.CreateAsync(new TimesheetWeek
        {
            TenantId = Tenant, UserId = Guid.NewGuid(), WeekKey = "2026-W40", WeekStartDate = new DateOnly(2026, 9, 28),
            TimeZoneId = Zone, RevisionNumber = 1
        });
        var stale = (await weeks.GetByIdAsync(week.Id))!;
        var fresh = (await weeks.GetByIdAsync(week.Id))!;
        fresh.TotalMinutes = 480;
        Assert.True(await weeks.UpdateAsync(fresh, 1));

        stale.TotalMinutes = 15;
        Assert.False(await weeks.UpdateAsync(stale, 2));

        Assert.Equal(1, stale.Version);
        var stored = await RawAsync<TimesheetWeek>(PlatformCollections.TimeEntryTimesheetWeeks, week.Id);
        Assert.Equal(480, stored.TotalMinutes);
        Assert.Equal(2, stored.Version);
    }

    [Fact]
    public async Task A_time_row_copy_read_before_another_write_is_refused_even_under_the_stored_version()
    {
        var entries = new TimeEntryRepository(Fixture.DbContext, TenantScope());
        var row = await entries.CreateAsync(new Domain.Entities.TimeEntry.TimeEntry
        {
            TenantId = Tenant, TimesheetWeekId = Guid.NewGuid(), UserId = Person, WeekKey = CurrentWeek, LocalDate = Monday,
            DurationMinutes = 60, CategoryCode = Category
        });
        var stale = (await entries.GetByIdAsync(row.Id))!;
        var fresh = (await entries.GetByIdAsync(row.Id))!;
        fresh.DurationMinutes = 90;
        Assert.True(await entries.UpdateAsync(fresh, fresh.Version));

        stale.DurationMinutes = 5;
        Assert.False(await entries.UpdateAsync(stale, 2));
        Assert.False(await entries.UpdateAsync(stale, stale.Version));

        Assert.Equal(1, stale.Version);
        var stored = await RawAsync<Domain.Entities.TimeEntry.TimeEntry>(PlatformCollections.TimeEntryEntries, row.Id);
        Assert.Equal(90, stored.DurationMinutes);
        Assert.Equal(2, stored.Version);
    }

    [Fact]
    public async Task A_work_category_copy_read_before_another_write_is_refused_even_under_the_stored_version()
    {
        var categories = new WorkCategoryRepository(Fixture.DbContext, TenantScope());
        var category = await categories.CreateAsync(new WorkCategory
        {
            TenantId = Tenant, Code = "RACE" + Guid.NewGuid().ToString("N")[..6], LabelText = "Yarış"
        });
        var stale = (await categories.GetByIdAsync(category.Id))!;
        var fresh = (await categories.GetByIdAsync(category.Id))!;
        fresh.IsActive = false;
        Assert.True(await categories.UpdateAsync(fresh, 1));

        stale.LabelText = "Eski kopya";
        Assert.False(await categories.UpdateAsync(stale, 2));

        Assert.Equal(1, stale.Version);
        var stored = await RawAsync<WorkCategory>(PlatformCollections.TimeEntryWorkCategories, category.Id);
        Assert.False(stored.IsActive);
        Assert.Equal("Yarış", stored.LabelText);
    }

    [Fact]
    public async Task A_time_settings_copy_read_before_another_write_is_refused_even_under_the_stored_version()
    {
        var settings = new TimeEntrySettingsRepository(Fixture.DbContext, TenantScope());
        var row = new TimeEntrySettings { TenantId = Tenant, WeeklyReminderEnabled = false };
        Assert.True(await settings.TryCreateAsync(row));
        var stale = (await settings.GetByIdAsync(row.Id))!;
        var fresh = (await settings.GetByIdAsync(row.Id))!;
        fresh.WeeklyReminderEnabled = true;
        Assert.True(await settings.UpdateAsync(fresh, 1));

        stale.TimeAdminPoolPositionId = Guid.NewGuid();
        Assert.False(await settings.UpdateAsync(stale, 2));

        Assert.Equal(1, stale.Version);
        var stored = await RawAsync<TimeEntrySettings>(PlatformCollections.TimeEntrySettings, row.Id);
        Assert.True(stored.WeeklyReminderEnabled);
        Assert.Null(stored.TimeAdminPoolPositionId);
    }

    [Fact]
    public async Task A_legal_entity_timer_switch_copy_read_before_another_write_is_refused_even_under_the_stored_version()
    {
        var switches = new LegalEntityTimeSettingRepository(Fixture.DbContext, TenantScope());
        var row = new LegalEntityTimeSetting { TenantId = Tenant, LegalEntityId = Guid.NewGuid(), TimerEnabled = false };
        Assert.True(await switches.TryCreateAsync(row));
        var stale = (await switches.GetByIdAsync(row.Id))!;
        var fresh = (await switches.GetByIdAsync(row.Id))!;
        fresh.TimerEnabled = true;
        fresh.Reason = "Pilot";
        Assert.True(await switches.UpdateAsync(fresh, 1));

        stale.Reason = "Eski kopya";
        Assert.False(await switches.UpdateAsync(stale, 2));

        Assert.Equal(1, stale.Version);
        var stored = await RawAsync<LegalEntityTimeSetting>(PlatformCollections.TimeEntryLegalEntitySettings, row.Id);
        Assert.True(stored.TimerEnabled);
        Assert.Equal("Pilot", stored.Reason);
    }
}
