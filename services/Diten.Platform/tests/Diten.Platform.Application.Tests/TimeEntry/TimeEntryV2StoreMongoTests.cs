using System.Net;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Meetings;
using Diten.Platform.Domain.Entities.TimeEntry;
using Diten.Platform.Domain.Enums.TimeEntry;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Diten.Platform.Infrastructure.Persistence.Schema;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;
using TimeEntryRow = Diten.Platform.Domain.Entities.TimeEntry.TimeEntry;

namespace Diten.Platform.Application.Tests.TimeEntry;

/// <summary>
/// MOD-0280-FU01 T1b, CT acceptance round (v2) — the store side: a correction's pending and draft minutes count only its
/// change (F7), the entries' unique key tells two meetings apart and the old index is retired on startup (F8), the
/// meeting window is read in the database (F9), and the sweep's two queries each keep their share (F9 / BL-479).
/// </summary>
[Collection(TimeEntryMongoCollection.Name)]
public sealed class TimeEntryV2StoreMongoTests : TimerScenario
{
    public TimeEntryV2StoreMongoTests(TimeEntryMongoFixture fixture) : base(fixture)
    {
    }

    private async Task<T> InTenantAsync<T>(Func<IServiceProvider, Task<T>> read)
    {
        using var scope = Host.Services.CreateScope();
        using (TenantScope.Begin(scope.ServiceProvider.GetRequiredService<ITenantContext>(), Tenant))
        {
            return await read(scope.ServiceProvider);
        }
    }

    // ── F7 ──────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_corrections_pending_and_draft_minutes_are_its_change_not_the_approved_figure_again()
    {
        var weekId = await SubmittedWeekAsync(Row(Monday, 120, TaskA));
        Ok(await DecideAsync(Manager, weekId, approve: true));
        Ok(await GetWeekAsync());
        Assert.Equal(HttpStatusCode.Created, (await Host.PostAsync($"/api/v1/time-entry/weeks/{CurrentWeek}/corrections",
            PersonToken(), new { reason = "Monday was two and a half hours" })).Status);
        Ok(await SaveFreshAsync(CurrentWeek, Row(Monday, 150, TaskA)));

        var draft = await InTenantAsync(sp => sp.GetRequiredService<ITaskSpentTimeSource>().ReaderTimeAsync(Person, [TaskA]));
        Assert.Equal(30, draft.DraftMinutes[TaskA]);

        Ok(await SubmitAsync());
        var spent = await InTenantAsync(sp => sp.GetRequiredService<ITaskSpentTimeSource>().SpentTimeAsync([TaskA]));
        Assert.Equal(new TaskSpentTime(120, 30), spent[TaskA]);
    }

    // ── F8 ──────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Startup_retires_the_old_entries_index_and_the_new_key_tells_meetings_apart_but_not_timer_rows()
    {
        var db = Fixture.Database.Client.GetDatabase("diten_platform_standalone_time_entry_migration");
        await db.DropCollectionAsync(PlatformCollections.TimeEntryEntries);
        var entries = db.GetCollection<TimeEntryRow>(PlatformCollections.TimeEntryEntries);
        // The T1a index as a live database carries it.
        await entries.Indexes.CreateOneAsync(new CreateIndexModel<TimeEntryRow>(
            Builders<TimeEntryRow>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.TimesheetWeekId).Ascending(x => x.LocalDate)
                .Ascending(x => x.TaskItemId).Ascending(x => x.CategoryCode).Ascending(x => x.Source),
            new CreateIndexOptions<TimeEntryRow>
            {
                Unique = true, Name = PlatformSchemaMigrations.RetiredTimeEntryEntriesIndex,
                PartialFilterExpression = Builders<TimeEntryRow>.Filter.Eq(x => x.IsDeleted, false)
            }));

        await PlatformSchemaMigrations.RunAsync(db);
        await PlatformSchemaManifest.ApplyAsync(db, [SchemaProfile.TimeEntry]);

        var names = (await (await entries.Indexes.ListAsync()).ToListAsync()).Select(i => i["name"].AsString).ToList();
        Assert.DoesNotContain(PlatformSchemaMigrations.RetiredTimeEntryEntriesIndex, names);
        Assert.Contains("ux_time_entry_entries_tenant_week_date_target_source_ref", names);

        var week = Guid.NewGuid();
        TimeEntryRow Entry(TimeEntrySource source, string? sourceRef) => new()
        {
            TenantId = Tenant, TimesheetWeekId = week, UserId = Person, WeekKey = CurrentWeek, LocalDate = Monday,
            DurationMinutes = 30, CategoryCode = "INTERNAL_MEETING", Source = source, SourceRef = sourceRef
        };
        await entries.InsertOneAsync(Entry(TimeEntrySource.Meeting, Guid.NewGuid().ToString()));
        await entries.InsertOneAsync(Entry(TimeEntrySource.Meeting, Guid.NewGuid().ToString()));   // a second meeting: fine
        await entries.InsertOneAsync(Entry(TimeEntrySource.Timer, null));
        await Assert.ThrowsAsync<MongoWriteException>(() => entries.InsertOneAsync(Entry(TimeEntrySource.Timer, null))); // still one per cell
    }

    // ── F9 — the meeting window is read in the database ─────────────────────────────────────────────────────────

    [Fact]
    public async Task The_meeting_window_is_filtered_in_the_database_whatever_offset_a_meeting_was_saved_with()
    {
        Meeting At(DateTimeOffset start) => new()
        {
            TenantId = Tenant, Title = "m", MeetingTypeId = Guid.NewGuid(), StartAt = start, EndAt = start.AddHours(1),
            OrganizerUserId = Manager, IdempotencyKey = Guid.NewGuid().ToString("N")
        };

        // Monday 00:30 Istanbul, saved WITH its +03:00 offset: 2026-10-04 21:30 UTC — inside a window that starts at
        // 2026-10-04 21:00 UTC, although its local wall clock ticks alone would say otherwise.
        var inside = At(new DateTimeOffset(2026, 10, 5, 0, 30, 0, TimeSpan.FromHours(3)));
        var before = At(new DateTimeOffset(2026, 10, 4, 23, 30, 0, TimeSpan.FromHours(3)));  // Sunday — the week before
        var after = At(new DateTimeOffset(2026, 10, 12, 0, 30, 0, TimeSpan.FromHours(3)));   // next Monday
        await Collection<Meeting>(PlatformCollections.MeetingMeetings).InsertManyAsync([inside, before, after]);

        var found = await InTenantAsync(sp => new MeetingRepository(Fixture.DbContext, sp.GetRequiredService<ITenantContext>())
            .ListByIdsStartingBetweenAsync([inside.Id, before.Id, after.Id],
                new DateTimeOffset(2026, 10, 4, 21, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 10, 11, 21, 0, 0, TimeSpan.Zero)));

        Assert.Equal(inside.Id, Assert.Single(found).Id);
    }

    // ── F9 / BL-479 — each query keeps its share ───────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(3, 5, 4, 2, 2)] // both sides full: half each
    [InlineData(5, 0, 4, 4, 0)] // nothing pending: approved weeks get the unused share
    [InlineData(1, 5, 4, 1, 3)] // few approved: pending gets the rest
    public async Task The_sweeps_two_queries_each_keep_their_share_of_the_limit(
        int approvedMissing, int pending, int limit, int expectedApproved, int expectedPending)
    {
        var weeks = Collection<TimesheetWeek>(PlatformCollections.TimeEntryTimesheetWeeks);
        for (var i = 0; i < approvedMissing; i++)
        {
            await weeks.InsertOneAsync(Week(TimesheetWeekStatus.Approved, 100 + i, null));
        }

        for (var i = 0; i < pending; i++)
        {
            await weeks.InsertOneAsync(Week(TimesheetWeekStatus.Submitted, 10 + i, Guid.NewGuid()));
        }

        var listed = await InTenantAsync(sp => sp.GetRequiredService<ITimesheetWeekRepository>().ListNeedingFinalizationAsync(limit));

        Assert.Equal(expectedApproved, listed.Count(w => w.Status == TimesheetWeekStatus.Approved));
        Assert.Equal(expectedPending, listed.Count(w => w.Status == TimesheetWeekStatus.Submitted));
    }

    private TimesheetWeek Week(TimesheetWeekStatus status, long ticks, Guid? instance) => new()
    {
        TenantId = Tenant, UserId = Guid.NewGuid(), WeekKey = "2026-W30", WeekStartDate = new DateOnly(2026, 7, 20),
        TimeZoneId = Zone, RevisionNumber = 1, Status = status, IsOpen = status == TimesheetWeekStatus.Submitted,
        InForce = status == TimesheetWeekStatus.Approved, WorkflowInstanceId = instance, SubmittedAtUtcTicks = ticks
    };
}
