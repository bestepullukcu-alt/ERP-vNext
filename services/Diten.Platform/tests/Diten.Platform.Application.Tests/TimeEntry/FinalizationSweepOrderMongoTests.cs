using Diten.Platform.Application.Contracts;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.TimeEntry;
using Diten.Platform.Domain.Enums.TimeEntry;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Persistence.Schema;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.TimeEntry;

/// <summary>
/// BL-479 — the decision sweep's work list: approved weeks whose task totals never landed come FIRST (their own query,
/// their own partial index), so a backlog of undecided submitted weeks larger than the limit can never starve them.
/// </summary>
[Collection(TimeEntryMongoCollection.Name)]
public sealed class FinalizationSweepOrderMongoTests : TimeEntryScenario
{
    public FinalizationSweepOrderMongoTests(TimeEntryMongoFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task Approved_weeks_missing_their_totals_are_listed_before_a_larger_backlog_of_pending_ones()
    {
        var weeks = Collection<TimesheetWeek>(PlatformCollections.TimeEntryTimesheetWeeks);
        for (var i = 0; i < 5; i++)
        {
            await weeks.InsertOneAsync(Week($"2026-W{30 + i}", TimesheetWeekStatus.Submitted, ticks: 1_000 + i, instance: Guid.NewGuid()));
        }

        var starving = Week("2026-W40", TimesheetWeekStatus.Approved, ticks: 9_000, instance: null);
        starving.InForce = true;
        starving.IsOpen = false;
        await weeks.InsertOneAsync(starving);
        await weeks.InsertOneAsync(Week("2026-W39", TimesheetWeekStatus.Approved, ticks: 8_000, instance: null, totalsApplied: true));

        using var scope = Host.Services.CreateScope();
        using (TenantScope.Begin(scope.ServiceProvider.GetRequiredService<ITenantContext>(), Tenant))
        {
            var repository = scope.ServiceProvider.GetRequiredService<ITimesheetWeekRepository>();

            var three = await repository.ListNeedingFinalizationAsync(3);
            Assert.Equal(starving.Id, three[0].Id);
            Assert.Equal(3, three.Count);
            Assert.All(three.Skip(1), w => Assert.Equal(TimesheetWeekStatus.Submitted, w.Status));

            var all = await repository.ListNeedingFinalizationAsync(100);
            Assert.Equal(6, all.Count);                                            // the applied one is not work
            Assert.Equal(all.Skip(1).Select(w => w.SubmittedAtUtcTicks).Order(), all.Skip(1).Select(w => w.SubmittedAtUtcTicks));
        }
    }

    [Fact]
    public async Task The_outstanding_totals_query_has_its_own_index()
    {
        var names = (await (await Collection<TimesheetWeek>(PlatformCollections.TimeEntryTimesheetWeeks).Indexes.ListAsync()).ToListAsync())
            .Select(i => i["name"].AsString)
            .ToList();

        Assert.Contains("ix_time_entry_weeks_tenant_totals_outstanding", names);
    }

    private TimesheetWeek Week(string weekKey, TimesheetWeekStatus status, long ticks, Guid? instance, bool totalsApplied = false)
    {
        Application.Features.TimeEntry.Services.WeekCalendar.TryParse(weekKey, out var monday);
        return new TimesheetWeek
        {
            TenantId = Tenant,
            UserId = Guid.NewGuid(),
            WeekKey = weekKey,
            WeekStartDate = monday,
            TimeZoneId = Zone,
            RevisionNumber = 1,
            Status = status,
            IsOpen = status == TimesheetWeekStatus.Submitted,
            WorkflowInstanceId = instance,
            SubmittedAtUtcTicks = ticks,
            TotalsAppliedAtUtc = totalsApplied ? Wednesday : null
        };
    }
}
