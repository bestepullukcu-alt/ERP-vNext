using Diten.PlanningService.Application.Features.DemandPlanning;
using Diten.PlanningService.Domain.Features.DemandPlanning;
using Diten.PlanningService.Persistence.Features.DemandPlanning;
using Microsoft.Extensions.Configuration;
using MongoDB.Driver;
using Xunit;

namespace Diten.PlanningService.Cycles.Tests;

public sealed class ForecastRunMongoIntegrationTests
{
    [ManualDraftMongoFact]
    public async Task RunDraftAndBothAudits_AreAtomicAndReplayOnce()
    {
        var (store, context) = Open();
        var tenant = Guid.NewGuid();
        var company = Guid.NewGuid();
        var cycle = Cycle(tenant, company);
        var actor = Guid.NewGuid();
        var first = Input(cycle, actor, "run-one", "fingerprint-one");
        var created = await store.CreateForecastAsync(first.Draft,
            "run-one", first.Run, first.Audit, default);
        Assert.Equal(ManualDraftStoreOutcome.Created, created.Outcome);
        var replay = await store.CreateForecastAsync(first.Draft,
            "run-one", first.Run, first.Audit, default);
        Assert.Equal(ManualDraftStoreOutcome.Replayed, replay.Outcome);
        Assert.Equal(first.Draft.Id, replay.Draft!.Id);
        var durable = await store.FindByRequestAsync(tenant, company, cycle.Id,
            actor, "run-one", default);
        Assert.Equal(first.Run.Id, durable!.Run.Id);
        Assert.All(durable.Draft.Series[0].Weeks,
            week => Assert.Equal(first.Run.Id,
                week.MethodProvenance!.ForecastRunId));
        Assert.Single(await context.ForecastRuns.Find(x => x.TenantId == tenant)
            .ToListAsync());
        Assert.Single(await context.ForecastSelectionAudit.Find(x =>
            x.TenantId == tenant).ToListAsync());

        var changedRequest = Input(cycle, actor, "run-one", "different-content");
        var conflict = await store.CreateForecastAsync(changedRequest.Draft,
            "run-one", changedRequest.Run, changedRequest.Audit, default);
        Assert.Equal(ManualDraftStoreOutcome.Conflict, conflict.Outcome);
        Assert.Null(await store.ReadAsync(tenant, company,
            changedRequest.Draft.Id, default));

        var second = Input(cycle, actor, "run-two", "fingerprint-two");
        second.Audit.Id = first.Audit.Id; // Force the final insert to fail.
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            store.CreateForecastAsync(second.Draft,
                "run-two", second.Run, second.Audit, default));
        Assert.Null(await store.ReadAsync(tenant, company, second.Draft.Id, default));
        Assert.Empty(await context.ForecastRuns.Find(x =>
            x.TenantId == tenant && x.Id == second.Run.Id).ToListAsync());
        Assert.Empty(await context.ManualDraftAudit.Find(x =>
            x.TenantId == tenant && x.RevisionId == second.Draft.Id).ToListAsync());
    }

    private static (ManualDraftMongoStore Store, DemandPlanningMongoContext Context) Open()
    {
        var uri = Environment.GetEnvironmentVariable("MOD0188_TEST_MONGO_URI")
            ?? throw new InvalidOperationException("Explicit loopback Mongo URI is required.");
        var url = new MongoUrl(uri);
        var servers = url.Servers.ToArray();
        if (servers.Length != 1 || servers[0].Host != "127.0.0.1" ||
            servers[0].Port is < 31994 or > 39994 ||
            url.Username is not null || url.Password is not null ||
            url.ReplicaSetName != "rs-mod0188")
            throw new InvalidOperationException("Unsafe test Mongo URI.");
        var config = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Mongo:ConnectionString"] = uri,
                ["Mongo:SupplyChainDatabaseName"] = "mod0188_tests"
            }).Build();
        var context = new DemandPlanningMongoContext(config);
        return (new ManualDraftMongoStore(context), context);
    }

    private static PlanningCycle Cycle(Guid tenant, Guid company)
    {
        var start = new DateOnly(2026, 10, 5);
        return new PlanningCycle
        {
            Id = Guid.NewGuid(), TenantId = tenant, LegalEntityId = company,
            AsOfDate = new DateOnly(2026, 10, 2), CalendarId = "ISO-8601",
            CalendarVersion = "1", TimeZoneId = "Asia/Baku",
            PlanningPeriodKey = "2026-10-05", HorizonStart = start,
            HorizonEnd = start.AddDays(363),
            Weeks = Enumerable.Range(0, 52).Select(i => new PlanningWeek
            {
                Number = i + 1, WeekStart = start.AddDays(7 * i),
                WeekEnd = start.AddDays(7 * i + 6)
            }).ToList()
        };
    }

    private static (DemandRevisionDraft Draft, ForecastRun Run,
        ForecastSelectionAudit Audit) Input(PlanningCycle cycle, Guid actor,
        string key, string fingerprint)
    {
        var runId = Guid.NewGuid();
        var sku = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);
        var provenance = new MethodWeekProvenance(runId, "Naive", "last-value",
            "approved-test-policy", cycle.HorizonStart.AddDays(-7),
            new string('A', 64), new Dictionary<string, string>(), false);
        var series = new VerifiedDraftSeries(sku, "WH-1", "EA",
            cycle.Weeks.Select(week => new ManualDraftWeekInput(week.Number,
                week.WeekStart, week.WeekEnd, DraftWeekValueKind.Known, 5m))
                .ToArray(), provenance);
        var draft = Assert.IsType<DemandRevisionDraft>(ManualDraftFactory.Create(
            cycle, cycle.TenantId, cycle.LegalEntityId, actor, "Explicit choice",
            now, [series]).Data);
        var run = new ForecastRun
        {
            Id = runId, TenantId = cycle.TenantId,
            LegalEntityId = cycle.LegalEntityId, PlanningCycleId = cycle.Id,
            RevisionId = draft.Id, SkuId = sku, WarehouseId = "WH-1",
            BaseUomId = "EA", Method = "Naive", Variant = "last-value",
            PolicyVersion = "approved-test-policy", SelectedBy = actor,
            SelectedAt = now, CreatedAt = now, Reason = "Explicit choice",
            RequestKey = key, RequestFingerprint = fingerprint
        };
        var audit = new ForecastSelectionAudit
        {
            TenantId = cycle.TenantId, LegalEntityId = cycle.LegalEntityId,
            PlanningCycleId = cycle.Id, RevisionId = draft.Id,
            ForecastRunId = runId, SkuId = sku, WarehouseId = "WH-1",
            NewMethod = "Naive", PolicyVersion = "approved-test-policy",
            ActorId = actor, OccurredAt = now, CreatedAt = now,
            Reason = "Explicit choice", RequestKey = key,
            RequestFingerprint = fingerprint
        };
        return (draft, run, audit);
    }
}
