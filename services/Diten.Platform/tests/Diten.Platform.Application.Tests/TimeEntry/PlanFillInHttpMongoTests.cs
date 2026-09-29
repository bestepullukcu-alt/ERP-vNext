using System.Text.Json;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Entities.TimeEntry;
using Diten.Platform.Infrastructure.Persistence.Schema;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;
using TimeEntryRow = Diten.Platform.Domain.Entities.TimeEntry.TimeEntry;

namespace Diten.Platform.Application.Tests.TimeEntry;

/// <summary>
/// MOD-0280-FU01 T1b D9 / T-17 — "fill from plan": ghost values from the person's plan blocks, for (day, task) cells
/// with no timer time and days up to local today. The read writes NOTHING — no collection grows, no audit entry; a row
/// the person accepts is saved with <c>Source = Plan</c>.
/// </summary>
[Collection(TimeEntryMongoCollection.Name)]
public sealed class PlanFillInHttpMongoTests : TimerScenario
{
    public PlanFillInHttpMongoTests(TimeEntryMongoFixture fixture) : base(fixture)
    {
    }

    private Task PlanAsync(Guid task, DateTimeOffset startUtc, int minutes)
        => Collection<TaskItem>(PlatformCollections.TaskItems).UpdateOneAsync(
            t => t.Id == task,
            Builders<TaskItem>.Update.Set(t => t.PlannedStartAt, startUtc).Set(t => t.PlannedDurationMinutes, minutes));

    private async Task<List<JsonElement>> FillInAsync()
    {
        var result = await Host.GetAsync($"/api/v1/time-entry/weeks/{CurrentWeek}/plan-fill-in", PersonToken());
        Ok(result);
        return result.Data.GetProperty("rows").EnumerateArray().ToList();
    }

    private async Task<long> DocumentCountAsync()
    {
        long total = 0;
        foreach (var name in new[]
                 {
                     PlatformCollections.TimeEntryTimesheetWeeks, PlatformCollections.TimeEntryEntries,
                     PlatformCollections.TimeEntryTimerSegments, PlatformCollections.TimeEntrySuggestions,
                     PlatformCollections.TimeEntryTaskTotals
                 })
        {
            total += await Fixture.Database.GetCollection<BsonDocument>(name).CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty);
        }

        return total;
    }

    [Fact]
    public async Task Fill_in_returns_ghost_values_and_writes_nothing()
    {
        await PlanAsync(TaskA, IstanbulLocal(2026, 10, 5, 10, 0), 100);  // Monday, 100 min → 105
        await PlanAsync(TaskB, IstanbulLocal(2026, 10, 9, 10, 0), 60);   // Friday — after local today
        var documents = await DocumentCountAsync();
        var audits = Host.Audit.Requests.Count;

        var rows = await FillInAsync();

        var row = Assert.Single(rows);
        Assert.Equal(TaskA, row.GetProperty("taskItemId").GetGuid());
        Assert.Equal("2026-10-05", row.GetProperty("localDate").GetString());
        Assert.Equal(105, row.GetProperty("durationMinutes").GetInt32());
        Assert.Equal(documents, await DocumentCountAsync());
        Assert.Equal(audits, Host.Audit.Requests.Count);
    }

    [Fact]
    public async Task A_cell_the_timer_already_covers_gets_no_ghost_value()
    {
        await PlanAsync(TaskA, IstanbulLocal(2026, 10, 5, 10, 0), 60);
        await SeedClosedSegmentAsync(new DateOnly(2026, 10, 5), 600, task: TaskA);

        Assert.Empty(await FillInAsync());
    }

    [Fact]
    public async Task An_accepted_ghost_row_is_saved_with_source_plan()
    {
        await PlanAsync(TaskA, IstanbulLocal(2026, 10, 5, 10, 0), 60);
        var ghost = Assert.Single(await FillInAsync());

        var saved = await SaveAsync(0, CurrentWeek, null, new
        {
            localDate = ghost.GetProperty("localDate").GetString(),
            taskItemId = TaskA,
            categoryCode = (string?)null,
            durationMinutes = ghost.GetProperty("durationMinutes").GetInt32(),
            note = (string?)null,
            source = "Plan"
        });
        Ok(saved);

        var entry = Assert.Single(await StoredEntriesAsync(saved.Data.GetProperty("weekId").GetGuid()));
        Assert.Equal(Domain.Enums.TimeEntry.TimeEntrySource.Plan, entry.Source);

        var version = await VersionAsync();
        var unknown = await SaveAsync(version, CurrentWeek, null, new
        {
            localDate = "2026-10-05", taskItemId = TaskA, categoryCode = (string?)null, durationMinutes = 60, note = (string?)null,
            source = "Clock"
        });
        Assert.Equal(Application.Features.TimeEntry.TimeEntryReasonCodes.SourceInvalid, unknown.ReasonCode);

        // v2 F1 — no source at all is refused: a timer row sent back without one must not become a Manual copy.
        var missing = await SaveAsync(version, CurrentWeek, null, new
        {
            localDate = "2026-10-05", taskItemId = TaskA, categoryCode = (string?)null, durationMinutes = 60, note = (string?)null
        });
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, missing.Status);
        Assert.Equal(Application.Features.TimeEntry.TimeEntryReasonCodes.SourceRequired, missing.ReasonCode);
    }
}
