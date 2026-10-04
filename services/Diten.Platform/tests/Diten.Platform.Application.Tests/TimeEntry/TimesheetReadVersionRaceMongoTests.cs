using System.Net;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.TimeEntry;
using Diten.Platform.Application.Features.TimeEntry.Services;
using Diten.Platform.Application.Tests.Persistence;
using Diten.Platform.Common.Persistence;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.TimeEntry;
using Diten.Platform.Domain.Enums.TimeEntry;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Diten.Platform.Infrastructure.Persistence.Schema;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using Xunit;
using TimeEntryRow = Diten.Platform.Domain.Entities.TimeEntry.TimeEntry;

namespace Diten.Platform.Application.Tests.TimeEntry;

/// <summary>
/// BL-533 — the timesheet stores replace the WHOLE document, so a write must be conditional on the version the document
/// in hand was READ at, and the weekly save writes its claim and its rows in ONE Platform transaction. Each race is staged
/// deterministically: a <see cref="ReadBarrier"/> holds a handler right after its read, or a <see cref="CallHook"/> runs the
/// competing write at the exact call where it must land. Disposable single-node replica set
/// (<see cref="TimeEntryMongoFixture"/>), a fresh tenant per test class.
/// </summary>
[Collection(TimeEntryMongoCollection.Name)]
public sealed class TimesheetReadVersionRaceMongoTests : TimerScenario
{
    private static readonly DateOnly Tuesday = new(2026, 10, 6);
    private static readonly DateOnly Today = new(2026, 10, 7);

    private readonly ReadBarrier _barrier = new(armed: false);
    private readonly CallHook _rows = new();
    private readonly CallHook _weeks = new();

    public TimesheetReadVersionRaceMongoTests(TimeEntryMongoFixture fixture) : base(fixture)
    {
    }

    protected override void ConfigureHost(IServiceCollection services)
    {
        services.AddScoped<IWorkCategoryRepository>(sp => _barrier.Wrap<IWorkCategoryRepository>(
            ActivatorUtilities.CreateInstance<WorkCategoryRepository>(sp), nameof(IWorkCategoryRepository.GetByIdAsync)));
        services.AddScoped<ITimeEntryRepository>(sp => _rows.Wrap<ITimeEntryRepository>(
            ActivatorUtilities.CreateInstance<TimeEntryRepository>(sp)));
        services.AddScoped<ITimesheetWeekRepository>(sp => _weeks.Wrap<ITimesheetWeekRepository>(
            ActivatorUtilities.CreateInstance<TimesheetWeekRepository>(sp)));
    }

    private static TenantContext ScopeOf(Guid tenant)
    {
        var context = new TenantContext();
        context.SetTenant(tenant);
        return context;
    }

    private async Task<T> RawAsync<T>(string collection, Guid id) where T : BaseEntity
        => await Collection<T>(collection).Find(Builders<T>.Filter.Eq(x => x.Id, id)).SingleAsync();

    private IMongoCollection<TimeEntryRow> Rows => Collection<TimeEntryRow>(PlatformCollections.TimeEntryEntries);

    private Task<TimeEntryRow> RowAsync(Guid id) => RawAsync<TimeEntryRow>(PlatformCollections.TimeEntryEntries, id);

    /// <summary>Another writer's write to a row, straight to the store: only its version moves.</summary>
    private Task BumpAsync(Guid rowId)
        => Rows.UpdateOneAsync(e => e.Id == rowId, Builders<TimeEntryRow>.Update.Inc(e => e.Version, 1));

    private static bool InTransaction(object?[] args) => args.Any(a => a is IPlatformTransactionSession);

    private static Func<object?[], bool> RowCall(Guid rowId, bool? inTransaction = null)
        => args => CallHook.Arg<TimeEntryRow>(args)?.Id == rowId && (inTransaction is null || InTransaction(args) == inTransaction);

    private TimeEntryRow NewRow() => new()
    {
        TenantId = Tenant, TimesheetWeekId = Guid.NewGuid(), UserId = Person, WeekKey = CurrentWeek, LocalDate = Monday,
        DurationMinutes = 60, CategoryCode = Category
    };

    /// <summary>10:00–11:00 today on TaskA by the timer: one 60-minute timer row in the open draft.</summary>
    private async Task<TimeEntryRow> TimerHourAsync()
    {
        await EnableTimerAsync();
        Host.Clock.UtcNow = IstanbulLocal(2026, 10, 7, 10, 0);
        Ok(await StartTimerAsync(TaskA));
        Host.Clock.UtcNow = IstanbulLocal(2026, 10, 7, 11, 0);
        Ok(await StopTimerAsync());
        var week = (await StoredWeeksAsync()).Single(w => w.IsOpen);
        var row = (await StoredEntriesAsync(week.Id)).Single(e => e.Source == TimeEntrySource.Timer);
        Assert.Equal(60, row.DurationMinutes);
        return row;
    }

    private async Task<TimerDraftOutcome> ApplyTimerDraftAsync(DateOnly day, Guid task)
    {
        using var scope = Host.Services.CreateScope();
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        using (TenantScope.Begin(tenantContext, Tenant))
        {
            return await scope.ServiceProvider.GetRequiredService<ITimerDraftWriter>().ApplyAsync(Person, day, task, null);
        }
    }

    // ── The weekly save: one transaction, every row write's answer read ──────────────────────────────────────────

    /// <summary>Items 1 and 3: the second of three row changes is refused (another writer moved that row). The save says
    /// 409 — and it is true: the week is at the version the person read, the first row is unchanged, the new row was never
    /// created and the left-out row was never removed.</summary>
    [Fact]
    public async Task A_save_whose_middle_row_write_is_refused_answers_409_and_writes_nothing_at_all()
    {
        Ok(await SaveFreshAsync(CurrentWeek,
            Row(Monday, 60, TaskA), Row(Tuesday, 60, TaskA), Row(Today, 60, TaskA), Row(Monday, 30, TaskB)));
        var before = (await StoredWeeksAsync()).Single();
        var rows = await StoredEntriesAsync(before.Id);
        var monday = rows.Single(r => r.LocalDate == Monday && r.TaskItemId == TaskA);
        var tuesday = rows.Single(r => r.LocalDate == Tuesday);
        _rows.Before(nameof(ITimeEntryRepository.UpdateAsync), RowCall(tuesday.Id), () => BumpAsync(tuesday.Id));

        var refused = await SaveAsync(before.Version, CurrentWeek, null,
            Row(Monday, 90, TaskA), Row(Tuesday, 90, TaskA), Row(Today, 90, TaskA), Row(Tuesday, 45, TaskB));

        Assert.Equal(HttpStatusCode.Conflict, refused.Status);
        Assert.Equal(TimeEntryReasonCodes.ConcurrencyConflict, refused.ReasonCode);
        var week = await StoredWeekAsync(before.Id);
        Assert.Equal(before.Version, week.Version);
        Assert.Equal(before.TotalMinutes, week.TotalMinutes);
        var after = await StoredEntriesAsync(before.Id);
        Assert.Equal(4, after.Count);
        Assert.All(after.Where(r => r.TaskItemId == TaskA), r => Assert.Equal(60, r.DurationMinutes));
        Assert.Equal(monday.Version, after.Single(r => r.Id == monday.Id).Version);
        Assert.Contains(after, r => r.LocalDate == Monday && r.TaskItemId == TaskB && r.DurationMinutes == 30);
        Assert.DoesNotContain(after, r => r.LocalDate == Tuesday && r.TaskItemId == TaskB);
    }

    /// <summary>Item 1: the correction's own row write is refused (the row moved between the correction's read and its
    /// write). The save says 409 and no correction is recorded.</summary>
    [Fact]
    public async Task A_correction_whose_row_write_is_refused_answers_409_and_records_no_correction()
    {
        var timer = await TimerHourAsync();
        var before = await StoredWeekAsync(timer.TimesheetWeekId);
        _rows.Before(nameof(ITimeEntryRepository.UpdateAsync), RowCall(timer.Id, inTransaction: true), () => BumpAsync(timer.Id));

        var refused = await SaveAsync(before.Version, CurrentWeek, null, Row(Today, 45, TaskA, source: "Timer"));

        Assert.Equal(HttpStatusCode.Conflict, refused.Status);
        Assert.Equal(TimeEntryReasonCodes.ConcurrencyConflict, refused.ReasonCode);
        var row = await RowAsync(timer.Id);
        Assert.Equal(60, row.DurationMinutes);
        Assert.Null(row.CapturedMinutes);
        Assert.False(row.EditedFromTimer);
        Assert.Equal(before.Version, (await StoredWeekAsync(before.Id)).Version);
    }

    /// <summary>
    /// Item 2: the person corrects the 60-minute timer row to 45. After the save read the row and before its transaction
    /// began, another write made it 75 (the latest point such a write can still be seen by the correction's own read —
    /// once the save's transaction holds the week, a timer close waits on the week's claim). The correction is written
    /// only on the version the SAVE read: 409, and the 75 is not recorded as the captured value the person never saw.
    /// </summary>
    [Fact]
    public async Task A_row_write_between_the_saves_read_and_the_corrections_read_refuses_the_correction()
    {
        var timer = await TimerHourAsync();
        var before = await StoredWeekAsync(timer.TimesheetWeekId);
        _weeks.Before(nameof(ITimesheetWeekRepository.UpdateAsync), InTransaction, () => Rows.UpdateOneAsync(
            e => e.Id == timer.Id,
            Builders<TimeEntryRow>.Update.Set(e => e.DurationMinutes, 75).Inc(e => e.Version, 1)));

        var refused = await SaveAsync(before.Version, CurrentWeek, null, Row(Today, 45, TaskA, source: "Timer"));

        Assert.Equal(HttpStatusCode.Conflict, refused.Status);
        Assert.Equal(TimeEntryReasonCodes.ConcurrencyConflict, refused.ReasonCode);
        var row = await RowAsync(timer.Id);
        Assert.Equal(75, row.DurationMinutes);
        Assert.Null(row.CapturedMinutes);
        Assert.False(row.EditedFromTimer);
        Assert.Equal(before.Version, (await StoredWeekAsync(before.Id)).Version);
    }

    // ── The timer draft writer: a refused row write is read again, never taken as written ────────────────────────

    [Fact]
    public async Task The_timer_draft_writer_reads_the_week_again_when_its_row_write_is_refused_and_lands_the_new_minutes()
    {
        var timer = await TimerHourAsync();
        await SeedClosedSegmentAsync(Today, 1800, task: TaskA); // a second run: the row must become 90
        _rows.Before(nameof(ITimeEntryRepository.UpdateAsync), RowCall(timer.Id), () => BumpAsync(timer.Id));

        var outcome = await ApplyTimerDraftAsync(Today, TaskA);

        Assert.Equal(TimerDraftOutcome.Written, outcome);
        var row = await RowAsync(timer.Id);
        Assert.Equal(90, row.DurationMinutes);
        Assert.Equal(timer.Version + 2, row.Version); // the competing write, then the writer's own
    }

    [Fact]
    public async Task The_timer_draft_writer_gives_up_with_an_error_when_every_attempt_loses_its_row()
    {
        var timer = await TimerHourAsync();
        await SeedClosedSegmentAsync(Today, 1800, task: TaskA);
        _rows.Before(nameof(ITimeEntryRepository.UpdateAsync), RowCall(timer.Id), () => BumpAsync(timer.Id), once: false);

        await Assert.ThrowsAsync<InvalidOperationException>(() => ApplyTimerDraftAsync(Today, TaskA));

        Assert.Equal(60, (await RowAsync(timer.Id)).DurationMinutes);
    }

    // ── A handler that hands the store the CLIENT's version: the store guard itself ──────────────────────────────

    /// <summary>
    /// Item 5: the category edit passes the client's version straight to the store (no pre-check in the handler), so this
    /// race reaches the store's own rule: the loser read the category at N, the winner moved it to N+1, and the loser's
    /// client names N+1 — the version stored NOW. Its copy is still the N copy: refused, the winner's label stays.
    /// (The weekly save cannot reach this rule: it compares the client's version with the week it read before it writes,
    /// SaveTimeEntriesHandler's pre-check.)
    /// </summary>
    [Fact]
    public async Task A_category_edit_that_read_before_another_edit_is_refused_even_under_the_version_stored_now()
    {
        var code = "RACE" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        await SeedCategoryAsync(Tenant, code);
        var category = await Collection<WorkCategory>(PlatformCollections.TimeEntryWorkCategories)
            .Find(c => c.TenantId == Tenant && c.Code == code).SingleAsync();
        var path = $"/api/v1/time-entry/categories/{category.Id}";
        object Edit(string label, int expectedVersion) => new
        {
            expectedVersion, code = (string?)null, labelText = label, description = (string?)null, countsAsWork = true, sortOrder = 10
        };

        _barrier.Arm();
        var loser = Task.Run(() => Host.PutAsync(path, AdminToken(), Edit("Eski kopya", category.Version + 1)));
        await _barrier.Reached.WaitAsync(TimeSpan.FromSeconds(30));
        var winner = await Host.PutAsync(path, AdminToken(), Edit("Kazanan", category.Version));
        _barrier.Release();
        var refused = await loser;

        Assert.Equal(HttpStatusCode.OK, winner.Status);
        Assert.Equal(HttpStatusCode.Conflict, refused.Status);
        Assert.Equal(TimeEntryReasonCodes.CategoryConcurrencyConflict, refused.ReasonCode);
        var stored = await RawAsync<WorkCategory>(PlatformCollections.TimeEntryWorkCategories, category.Id);
        Assert.Equal("Kazanan", stored.LabelText);
        Assert.Equal(category.Version + 1, stored.Version);
    }

    // ── Each store, directly ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_week_copy_read_before_another_write_is_refused_even_under_the_stored_version()
    {
        var weeks = new TimesheetWeekRepository(Fixture.DbContext, ScopeOf(Tenant));
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
        var entries = new TimeEntryRepository(Fixture.DbContext, ScopeOf(Tenant));
        var row = await entries.CreateAsync(NewRow());
        var stale = (await entries.GetByIdAsync(row.Id))!;
        var fresh = (await entries.GetByIdAsync(row.Id))!;
        fresh.DurationMinutes = 90;
        Assert.True(await entries.UpdateAsync(fresh, fresh.Version));

        stale.DurationMinutes = 5;
        Assert.False(await entries.UpdateAsync(stale, 2));
        Assert.False(await entries.UpdateAsync(stale, stale.Version));

        Assert.Equal(1, stale.Version);
        var stored = await RowAsync(row.Id);
        Assert.Equal(90, stored.DurationMinutes);
        Assert.Equal(2, stored.Version);
    }

    [Fact]
    public async Task A_work_category_copy_read_before_another_write_is_refused_even_under_the_stored_version()
    {
        var categories = new WorkCategoryRepository(Fixture.DbContext, ScopeOf(Tenant));
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
        var settings = new TimeEntrySettingsRepository(Fixture.DbContext, ScopeOf(Tenant));
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
        var switches = new LegalEntityTimeSettingRepository(Fixture.DbContext, ScopeOf(Tenant));
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

    // ── The shared helper: tenant scope (item 6) and what a refusal puts back (item 8) ───────────────────────────

    [Fact]
    public async Task A_write_through_another_tenants_store_is_refused_and_the_document_stays_as_it_was()
    {
        var mine = new TimeEntryRepository(Fixture.DbContext, ScopeOf(Tenant));
        var theirs = new TimeEntryRepository(Fixture.DbContext, ScopeOf(OtherTenant));
        var row = await mine.CreateAsync(NewRow());
        var copy = await RowAsync(row.Id); // the document itself, at the version it is stored at
        copy.DurationMinutes = 5;

        Assert.False(await theirs.UpdateAsync(copy, copy.Version));

        var stored = await RowAsync(row.Id);
        Assert.Equal(60, stored.DurationMinutes);
        Assert.Equal(1, stored.Version);
        Assert.Equal(Tenant, stored.TenantId);
    }

    [Fact]
    public async Task A_write_to_a_removed_document_is_refused_and_it_stays_removed()
    {
        var entries = new TimeEntryRepository(Fixture.DbContext, ScopeOf(Tenant));
        var row = await entries.CreateAsync(NewRow());
        var copy = (await entries.GetByIdAsync(row.Id))!;
        await entries.SoftDeleteAsync([row.Id]);
        copy.DurationMinutes = 5;

        Assert.False(await entries.UpdateAsync(copy, copy.Version));

        var stored = await RowAsync(row.Id);
        Assert.True(stored.IsDeleted);
        Assert.Equal(60, stored.DurationMinutes);
        Assert.Equal(1, stored.Version);
    }

    [Fact]
    public async Task A_write_the_store_refuses_puts_back_the_version_and_the_update_time_the_copy_was_read_with()
    {
        var entries = new TimeEntryRepository(Fixture.DbContext, ScopeOf(Tenant));
        var row = await entries.CreateAsync(NewRow());
        var first = (await entries.GetByIdAsync(row.Id))!;
        first.DurationMinutes = 75;
        Assert.True(await entries.UpdateAsync(first, first.Version)); // the row now carries an update time
        var stale = (await entries.GetByIdAsync(row.Id))!;
        var fresh = (await entries.GetByIdAsync(row.Id))!;
        fresh.DurationMinutes = 90;
        Assert.True(await entries.UpdateAsync(fresh, fresh.Version));
        var readVersion = stale.Version;
        var readAt = stale.UpdatedAt;
        Assert.NotNull(readAt);

        stale.DurationMinutes = 5;
        Assert.False(await entries.UpdateAsync(stale, stale.Version)); // read at N, stored at N+1: the filter refuses

        Assert.Equal(readVersion, stale.Version);
        Assert.Equal(readAt, stale.UpdatedAt);
    }

    /// <summary>The week's duplicate-key path: a second revision "in force" is refused by the unique index, the store
    /// answers false — and the copy in hand is back at the version and update time it was read with.</summary>
    [Fact]
    public async Task A_week_write_a_unique_index_refuses_answers_false_and_puts_back_the_version_and_the_update_time()
    {
        var weeks = new TimesheetWeekRepository(Fixture.DbContext, ScopeOf(Tenant));
        var user = Guid.NewGuid();
        await weeks.CreateAsync(new TimesheetWeek
        {
            TenantId = Tenant, UserId = user, WeekKey = "2026-W40", WeekStartDate = new DateOnly(2026, 9, 28), TimeZoneId = Zone,
            RevisionNumber = 1, Status = TimesheetWeekStatus.Approved, InForce = true, IsOpen = false
        });
        var correction = await weeks.CreateAsync(new TimesheetWeek
        {
            TenantId = Tenant, UserId = user, WeekKey = "2026-W40", WeekStartDate = new DateOnly(2026, 9, 28), TimeZoneId = Zone,
            RevisionNumber = 2
        });
        var first = (await weeks.GetByIdAsync(correction.Id))!;
        first.TotalMinutes = 60;
        Assert.True(await weeks.UpdateAsync(first, first.Version)); // the revision now carries an update time
        var copy = (await weeks.GetByIdAsync(correction.Id))!;
        var readVersion = copy.Version;
        var readAt = copy.UpdatedAt;
        Assert.NotNull(readAt);

        copy.InForce = true; // a second revision in force for the same week
        Assert.False(await weeks.UpdateAsync(copy, copy.Version));

        Assert.Equal(readVersion, copy.Version);
        Assert.Equal(readAt, copy.UpdatedAt);
        var stored = await RawAsync<TimesheetWeek>(PlatformCollections.TimeEntryTimesheetWeeks, correction.Id);
        Assert.False(stored.InForce);
        Assert.Equal(readVersion, stored.Version);
    }
}
