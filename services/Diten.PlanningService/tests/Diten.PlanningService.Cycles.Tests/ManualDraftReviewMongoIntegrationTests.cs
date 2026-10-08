using System.Security.Cryptography;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Diten.PlanningService.Application.Features.DemandPlanning;
using Diten.PlanningService.Domain.Features.DemandPlanning;
using Diten.PlanningService.Persistence.Features.DemandPlanning;
using Microsoft.Extensions.Configuration;
using MongoDB.Driver;
using Xunit;

// Mongo tests share one explicit test database and some tests change its indexes.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Diten.PlanningService.Cycles.Tests;

internal static class ManualDraftMongoTestBootstrap
{
    // BSON-only tests may otherwise register an unspecified Guid serializer first.
    // Constructing the context initializes V3 serialization without opening a socket.
    [ModuleInitializer]
    internal static void Initialize()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Mongo:ConnectionString"] = "mongodb://127.0.0.1:39994",
                ["Mongo:SupplyChainDatabaseName"] = "mod0188_tests"
            }).Build();
        _ = new DemandPlanningMongoContext(configuration);
    }
}

public sealed class ManualDraftReviewMongoIntegrationTests
{
    private static readonly DateOnly FirstMonday = new(2026, 10, 5);
    private static readonly DateTimeOffset Now = new(2026, 10, 6, 10, 0, 0, TimeSpan.Zero);

    private static (ManualDraftMongoStore Store, DemandPlanningMongoContext Context) Open()
    {
        var uri = Environment.GetEnvironmentVariable("MOD0188_TEST_MONGO_URI")
            ?? throw new InvalidOperationException("An explicit test Mongo URI is required.");
        var url = new MongoUrl(uri);
        var servers = url.Servers.ToArray();
        if (servers.Length != 1 || servers[0].Host != "127.0.0.1" ||
            servers[0].Port is < 31994 or > 39994 || url.Username is not null ||
            url.Password is not null || url.ReplicaSetName != "rs-mod0188")
            throw new InvalidOperationException("Only the explicit local rs-mod0188 test replica set is allowed.");
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Mongo:ConnectionString"] = uri,
                ["Mongo:SupplyChainDatabaseName"] = "mod0188_tests"
            }).Build();
        var context = new DemandPlanningMongoContext(configuration);
        return (new ManualDraftMongoStore(context), context);
    }

    private static DemandRevisionDraft Draft(Guid tenant, Guid legalEntity,
        Guid cycleId, Guid creator, int? incompleteWeek = null,
        DraftWeekValueKind incompleteKind = DraftWeekValueKind.Missing,
        bool secondSeries = false, bool zero = false)
    {
        var cycle = new PlanningCycle
        {
            Id = cycleId, TenantId = tenant, LegalEntityId = legalEntity,
            AsOfDate = new DateOnly(2026, 10, 2), CalendarId = "ISO-8601",
            CalendarVersion = "1", TimeZoneId = "Asia/Baku",
            HorizonStart = FirstMonday, HorizonEnd = FirstMonday.AddDays(363),
            PlanningPeriodKey = "2026-10-05",
            Weeks = Enumerable.Range(0, 52).Select(index => new PlanningWeek
            {
                Number = index + 1, WeekStart = FirstMonday.AddDays(index * 7),
                WeekEnd = FirstMonday.AddDays(index * 7 + 6)
            }).ToList()
        };
        var series = new VerifiedDraftSeries(Guid.NewGuid(), "WH-1", "EA",
            Enumerable.Range(1, 52).Select(number => new ManualDraftWeekInput(
                number, FirstMonday.AddDays((number - 1) * 7),
                FirstMonday.AddDays((number - 1) * 7 + 6),
                !secondSeries && number == incompleteWeek ? incompleteKind : DraftWeekValueKind.Known,
                !secondSeries && number == incompleteWeek ? null : zero ? 0m : 10m)).ToArray());
        var selected = new List<VerifiedDraftSeries> { series };
        if (secondSeries)
            selected.Add(new VerifiedDraftSeries(Guid.NewGuid(), "WH-2", "EA",
                Enumerable.Range(1, 52).Select(number => new ManualDraftWeekInput(
                    number, FirstMonday.AddDays((number - 1) * 7),
                    FirstMonday.AddDays((number - 1) * 7 + 6),
                    number == incompleteWeek ? incompleteKind : DraftWeekValueKind.Known,
                    number == incompleteWeek ? null : 10m)).ToArray()));
        return Assert.IsType<DemandRevisionDraft>(ManualDraftFactory.Create(cycle,
            tenant, legalEntity, creator, "Verified manual fixture", Now, selected).Data);
    }

    [ManualDraftMongoFact]
    public async Task IncompleteManualDraft_SubmitHasNoStateSlotOrAuditEffect()
    {
        var (store, context) = Open();
        var tenant = Guid.NewGuid();
        var legalEntity = Guid.NewGuid();
        var creator = Guid.NewGuid();
        var draft = Draft(tenant, legalEntity, Guid.NewGuid(), creator, 19,
            DraftWeekValueKind.Missing, secondSeries: true);
        Assert.Equal(ManualDraftStoreOutcome.Created,
            (await store.CreateAsync(draft, "create-incomplete", default)).Outcome);
        Assert.Equal(ManualDraftStoreOutcome.Invalid,
            (await Transition(store, draft, creator, DraftReviewAction.Submitted,
                "submit-incomplete", 0, 0)).Outcome);
        var restored = Assert.IsType<DemandRevisionDraft>(await store.ReadAsync(
            tenant, legalEntity, draft.Id, default));
        Assert.Equal(DemandRevisionState.Draft, restored.State);
        Assert.Equal(0, restored.Version);
        Assert.Equal(0, restored.StateVersion);
        var incompleteSeries = restored.Series.Single(series => series.WarehouseId == "WH-2");
        Assert.Equal(DraftWeekValueKind.Missing, incompleteSeries.Weeks[18].ValueKind);
        Assert.Null(incompleteSeries.Weeks[18].Quantity);
        Assert.Equal(0, await context.ManualDraftReviewSlots.CountDocumentsAsync(x =>
            x.TenantId == tenant && x.LegalEntityId == legalEntity &&
            x.PlanningCycleId == draft.PlanningCycleId));
        Assert.Equal(0, await context.ManualDraftReviewAudit.CountDocumentsAsync(x =>
            x.TenantId == tenant && x.LegalEntityId == legalEntity &&
            x.RevisionId == draft.Id));
        Assert.Equal(ManualDraftStoreOutcome.Changed,
            (await store.EditWeekAsync(tenant, legalEntity, draft.Id, creator,
                incompleteSeries.SkuId, "WH-2", 19, DraftWeekValueKind.Known, 0m,
                "Completed missing week", "complete-week", 0, Now.AddMinutes(2),
                default)).Outcome);
        Assert.Equal(ManualDraftStoreOutcome.Changed,
            (await Transition(store, draft, creator, DraftReviewAction.Submitted,
                "submit-complete", 1, 0)).Outcome);
    }

    [ManualDraftMongoFact]
    public async Task UnknownWeekCannotSubmit_ButFiftyTwoExplicitZerosCan()
    {
        var (store, context) = Open();
        var tenant = Guid.NewGuid();
        var legalEntity = Guid.NewGuid();
        var creator = Guid.NewGuid();
        var unknown = Draft(tenant, legalEntity, Guid.NewGuid(), creator, 1,
            DraftWeekValueKind.Unknown);
        var zeros = Draft(tenant, legalEntity, Guid.NewGuid(), creator, zero: true);
        await store.CreateAsync(unknown, "create-unknown", default);
        await store.CreateAsync(zeros, "create-zeros", default);
        Assert.Equal(ManualDraftStoreOutcome.Invalid,
            (await Transition(store, unknown, creator, DraftReviewAction.Submitted,
                "submit-unknown", 0, 0)).Outcome);
        var unchanged = Assert.IsType<DemandRevisionDraft>(await store.ReadAsync(
            tenant, legalEntity, unknown.Id, default));
        Assert.Equal(DraftWeekValueKind.Unknown, unchanged.Series[0].Weeks[0].ValueKind);
        Assert.Null(unchanged.Series[0].Weeks[0].Quantity);
        Assert.Equal(ManualDraftStoreOutcome.Changed,
            (await Transition(store, zeros, creator, DraftReviewAction.Submitted,
                "submit-zeros", 0, 0)).Outcome);
        var restoredZeros = Assert.IsType<DemandRevisionDraft>(await store.ReadAsync(
            tenant, legalEntity, zeros.Id, default));
        Assert.All(restoredZeros.Series[0].Weeks, week => Assert.Equal(0m, week.Quantity));
        Assert.Equal(1, await context.ManualDraftReviewSlots.CountDocumentsAsync(x =>
            x.TenantId == tenant && x.LegalEntityId == legalEntity &&
            x.RevisionId == zeros.Id));
    }

    [ManualDraftMongoFact]
    public async Task LegacyIncompleteInReview_CannotBeApprovedOrChangeAudit()
    {
        var (store, context) = Open();
        var tenant = Guid.NewGuid();
        var legalEntity = Guid.NewGuid();
        var creator = Guid.NewGuid();
        var draft = Draft(tenant, legalEntity, Guid.NewGuid(), creator, 1);
        Assert.Equal(ManualDraftStoreOutcome.Created,
            (await store.CreateAsync(draft, "legacy-create", default)).Outcome);

        // Fixture represents a decision persisted before the new completeness rule.
        var occurredAt = Now.AddMinutes(1);
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(new
            {
                actorId = creator, action = DraftReviewAction.Submitted,
                reason = (string?)null, expectedContentVersion = 0,
                expectedStateVersion = 0
            }))));
        await context.ManualDraftReviewAudit.InsertOneAsync(new ManualDraftReviewAuditRecord
        {
            TenantId = tenant, LegalEntityId = legalEntity, RevisionId = draft.Id,
            RequestKey = "legacy-submit", Fingerprint = fingerprint,
            Action = DraftReviewAction.Submitted, ContentVersion = 0,
            StateVersionAfter = 1, ActorId = creator, OccurredAt = occurredAt,
            CreatedAt = occurredAt
        });
        await context.ManualDraftReviewSlots.InsertOneAsync(new ManualDraftReviewSlot
        {
            TenantId = tenant, LegalEntityId = legalEntity,
            PlanningCycleId = draft.PlanningCycleId, RevisionId = draft.Id,
            CreatedAt = occurredAt
        });
        var update = Builders<ManualDraftManifest>.Update
            .Set(x => x.State, DemandRevisionState.InReview)
            .Set(x => x.StateVersion, 1);
        await context.ManualDraftManifests.UpdateOneAsync(x => x.TenantId == tenant &&
            x.LegalEntityId == legalEntity && x.Id == draft.Id, update);

        var legacy = Assert.IsType<DemandRevisionDraft>(await store.ReadAsync(
            tenant, legalEntity, draft.Id, default));
        Assert.Equal(DemandRevisionState.InReview, legacy.State);
        Assert.Equal(DraftWeekValueKind.Missing, legacy.Series[0].Weeks[0].ValueKind);
        Assert.Equal(ManualDraftStoreOutcome.Invalid,
            (await Transition(store, draft, Guid.NewGuid(), DraftReviewAction.Approved,
                "legacy-approve-denied", 0, 1)).Outcome);
        var after = Assert.IsType<DemandRevisionDraft>(await store.ReadAsync(
            tenant, legalEntity, draft.Id, default));
        Assert.Equal(DemandRevisionState.InReview, after.State);
        Assert.Equal(1, after.StateVersion);
        Assert.Equal(0, after.Version);
        Assert.Equal(1, await context.ManualDraftReviewAudit.CountDocumentsAsync(x =>
            x.TenantId == tenant && x.LegalEntityId == legalEntity &&
            x.RevisionId == draft.Id));
        Assert.Equal(1, await context.ManualDraftReviewSlots.CountDocumentsAsync(x =>
            x.TenantId == tenant && x.LegalEntityId == legalEntity &&
            x.RevisionId == draft.Id));
    }

    private static Task<ManualDraftStoreResult> Transition(ManualDraftMongoStore store,
        DemandRevisionDraft draft, Guid actor, DraftReviewAction action,
        string key, int contentVersion, int stateVersion, string? reason = "Review decision") =>
        store.TransitionReviewAsync(draft.TenantId, draft.LegalEntityId, draft.Id,
            actor, action, reason, key, contentVersion, stateVersion,
            Now.AddMinutes(stateVersion + 1), default);

    [ManualDraftMongoFact]
    public async Task TwoDraftsSameCycle_ConcurrentSubmit_OnlyOneHoldsCandidateSlot()
    {
        var (store, context) = Open();
        var tenant = Guid.NewGuid();
        var legalEntity = Guid.NewGuid();
        var cycle = Guid.NewGuid();
        var creator = Guid.NewGuid();
        var first = Draft(tenant, legalEntity, cycle, creator);
        var second = Draft(tenant, legalEntity, cycle, creator);
        Assert.Equal(ManualDraftStoreOutcome.Created,
            (await store.CreateAsync(first, "first-create", default)).Outcome);
        Assert.Equal(ManualDraftStoreOutcome.Created,
            (await store.CreateAsync(second, "second-create", default)).Outcome);

        var results = await Task.WhenAll(
            Transition(store, first, creator, DraftReviewAction.Submitted, "first-submit", 0, 0),
            Transition(store, second, creator, DraftReviewAction.Submitted, "second-submit", 0, 0));

        Assert.Single(results, result => result.Outcome == ManualDraftStoreOutcome.Changed);
        Assert.Single(results, result => result.Outcome == ManualDraftStoreOutcome.Conflict);
        Assert.Equal(1, await context.ManualDraftReviewSlots.CountDocumentsAsync(x =>
            x.TenantId == tenant && x.LegalEntityId == legalEntity &&
            x.PlanningCycleId == cycle));
        Assert.Equal(1, await context.ManualDraftReviewAudit.CountDocumentsAsync(x =>
            x.TenantId == tenant && x.LegalEntityId == legalEntity));
    }

    [ManualDraftMongoFact]
    public async Task CreatorAndEverySignificantEditor_CannotApprove()
    {
        var (store, context) = Open();
        var tenant = Guid.NewGuid();
        var legalEntity = Guid.NewGuid();
        var creator = Guid.NewGuid();
        var firstEditor = Guid.NewGuid();
        var lastEditor = Guid.NewGuid();
        var reviewer = Guid.NewGuid();
        var draft = Draft(tenant, legalEntity, Guid.NewGuid(), creator);
        await store.CreateAsync(draft, "create", default);
        var sku = draft.Series[0].SkuId;
        Assert.Equal(ManualDraftStoreOutcome.Changed,
            (await store.EditWeekAsync(tenant, legalEntity, draft.Id, firstEditor,
                sku, "WH-1", 1, DraftWeekValueKind.Known, 11m,
                "First significant edit", "edit-one", 0, Now.AddMinutes(1), default)).Outcome);
        Assert.Equal(ManualDraftStoreOutcome.Changed,
            (await store.EditWeekAsync(tenant, legalEntity, draft.Id, lastEditor,
                sku, "WH-1", 2, DraftWeekValueKind.Known, 12m,
                "Second significant edit", "edit-two", 1, Now.AddMinutes(2), default)).Outcome);
        Assert.Equal(ManualDraftStoreOutcome.Changed,
            (await Transition(store, draft, creator, DraftReviewAction.Submitted,
                "submit", 2, 0)).Outcome);

        foreach (var actor in new[] { creator, firstEditor, lastEditor })
        {
            Assert.Equal(ManualDraftStoreOutcome.SeparationDenied,
                (await Transition(store, draft, actor, DraftReviewAction.Approved,
                    $"denied-{actor}", 2, 1)).Outcome);
            Assert.Equal(ManualDraftStoreOutcome.SeparationDenied,
                (await Transition(store, draft, actor, DraftReviewAction.Rejected,
                    $"denied-reject-{actor}", 2, 1)).Outcome);
        }
        Assert.Equal(ManualDraftStoreOutcome.Changed,
            (await Transition(store, draft, reviewer, DraftReviewAction.Approved,
                "approve", 2, 1)).Outcome);
        var restored = Assert.IsType<DemandRevisionDraft>(await store.ReadAsync(
            tenant, legalEntity, draft.Id, default));
        Assert.Equal(DemandRevisionState.Approved, restored.State);
        Assert.Equal(2, restored.Version);
        Assert.Equal(2, restored.StateVersion);
        Assert.Equal(11m, restored.Series[0].Weeks[0].Quantity);
        Assert.Equal(12m, restored.Series[0].Weeks[1].Quantity);
        Assert.Equal(2, await context.ManualDraftReviewAudit.CountDocumentsAsync(x =>
            x.TenantId == tenant && x.LegalEntityId == legalEntity &&
            x.RevisionId == draft.Id));
    }

    [ManualDraftMongoFact]
    public async Task StaleVersionAndRetry_DoNotCreateSecondDecision()
    {
        var (store, context) = Open();
        var tenant = Guid.NewGuid();
        var legalEntity = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var draft = Draft(tenant, legalEntity, Guid.NewGuid(), actor);
        await store.CreateAsync(draft, "create", default);
        Assert.Equal(ManualDraftStoreOutcome.Conflict,
            (await Transition(store, draft, actor, DraftReviewAction.Submitted,
                "stale-content", 1, 0)).Outcome);
        Assert.Equal(ManualDraftStoreOutcome.Changed,
            (await Transition(store, draft, actor, DraftReviewAction.Submitted,
                "submit", 0, 0)).Outcome);
        Assert.Equal(ManualDraftStoreOutcome.Replayed,
            (await Transition(store, draft, actor, DraftReviewAction.Submitted,
                "submit", 0, 0)).Outcome);
        Assert.Equal(ManualDraftStoreOutcome.Conflict,
            (await Transition(store, draft, actor, DraftReviewAction.Submitted,
                "submit", 0, 0, "Different content")).Outcome);
        Assert.Equal(ManualDraftStoreOutcome.Conflict,
            (await Transition(store, draft, actor, DraftReviewAction.Submitted,
                "stale-state", 0, 0)).Outcome);
        Assert.Equal(1, await context.ManualDraftReviewAudit.CountDocumentsAsync(x =>
            x.TenantId == tenant && x.LegalEntityId == legalEntity &&
            x.RevisionId == draft.Id));
    }

    [ManualDraftMongoFact]
    public async Task CompetingReviewDecisions_OnlyOneCommitsWithAudit()
    {
        var (store, context) = Open();
        var tenant = Guid.NewGuid();
        var legalEntity = Guid.NewGuid();
        var creator = Guid.NewGuid();
        var draft = Draft(tenant, legalEntity, Guid.NewGuid(), creator);
        await store.CreateAsync(draft, "create", default);
        Assert.Equal(ManualDraftStoreOutcome.Changed,
            (await Transition(store, draft, creator, DraftReviewAction.Submitted,
                "submit", 0, 0)).Outcome);

        var results = await Task.WhenAll(
            Transition(store, draft, Guid.NewGuid(), DraftReviewAction.Approved,
                "approve", 0, 1),
            Transition(store, draft, Guid.NewGuid(), DraftReviewAction.Rejected,
                "reject", 0, 1, "Needs rework"));
        Assert.Single(results, result => result.Outcome == ManualDraftStoreOutcome.Changed);
        Assert.Single(results, result => result.Outcome == ManualDraftStoreOutcome.Conflict);
        Assert.Equal(2, await context.ManualDraftReviewAudit.CountDocumentsAsync(x =>
            x.TenantId == tenant && x.LegalEntityId == legalEntity &&
            x.RevisionId == draft.Id));
        var restored = Assert.IsType<DemandRevisionDraft>(await store.ReadAsync(
            tenant, legalEntity, draft.Id, default));
        Assert.Contains(restored.State, new[] { DemandRevisionState.Approved,
            DemandRevisionState.Draft });
        Assert.Equal(restored.State == DemandRevisionState.Approved ? 1 : 0,
            await context.ManualDraftReviewSlots.CountDocumentsAsync(x =>
                x.TenantId == tenant && x.LegalEntityId == legalEntity &&
                x.RevisionId == draft.Id));
    }

    [ManualDraftMongoFact]
    public async Task RejectionReleasesSlot_AndPreservesDecisionAudit()
    {
        var (store, context) = Open();
        var tenant = Guid.NewGuid();
        var legalEntity = Guid.NewGuid();
        var cycle = Guid.NewGuid();
        var creator = Guid.NewGuid();
        var reviewer = Guid.NewGuid();
        var first = Draft(tenant, legalEntity, cycle, creator);
        var second = Draft(tenant, legalEntity, cycle, creator);
        await store.CreateAsync(first, "first-create", default);
        await store.CreateAsync(second, "second-create", default);
        Assert.Equal(ManualDraftStoreOutcome.Changed,
            (await Transition(store, first, creator, DraftReviewAction.Submitted,
                "first-submit", 0, 0)).Outcome);
        Assert.Equal(ManualDraftStoreOutcome.Changed,
            (await Transition(store, first, reviewer, DraftReviewAction.Rejected,
                "reject", 0, 1, "Incorrect forecast scope")).Outcome);
        Assert.Equal(ManualDraftStoreOutcome.Changed,
            (await Transition(store, second, creator, DraftReviewAction.Submitted,
                "second-submit", 0, 0)).Outcome);
        Assert.Equal(DemandRevisionState.Draft,
            (await store.ReadAsync(tenant, legalEntity, first.Id, default))?.State);
        Assert.Equal(DemandRevisionState.InReview,
            (await store.ReadAsync(tenant, legalEntity, second.Id, default))?.State);
        Assert.Equal(1, await context.ManualDraftReviewSlots.CountDocumentsAsync(x =>
            x.TenantId == tenant && x.LegalEntityId == legalEntity &&
            x.PlanningCycleId == cycle));
        Assert.Equal(2, await context.ManualDraftReviewAudit.CountDocumentsAsync(x =>
            x.TenantId == tenant && x.LegalEntityId == legalEntity &&
            x.RevisionId == first.Id));
    }

    [ManualDraftMongoFact]
    public async Task ApprovedDraft_RequiresAuditedReopenBeforeManualEdit()
    {
        var (store, context) = Open();
        var tenant = Guid.NewGuid();
        var legalEntity = Guid.NewGuid();
        var creator = Guid.NewGuid();
        var reviewer = Guid.NewGuid();
        var draft = Draft(tenant, legalEntity, Guid.NewGuid(), creator);
        await store.CreateAsync(draft, "create", default);
        await Transition(store, draft, creator, DraftReviewAction.Submitted, "submit", 0, 0);
        await Transition(store, draft, reviewer, DraftReviewAction.Approved, "approve", 0, 1);
        var sku = draft.Series[0].SkuId;
        Assert.Equal(ManualDraftStoreOutcome.Conflict,
            (await store.EditWeekAsync(tenant, legalEntity, draft.Id, creator,
                sku, "WH-1", 1, DraftWeekValueKind.Known, 99m,
                "Would silently edit approval", "blocked-edit", 0,
                Now.AddMinutes(4), default)).Outcome);
        Assert.Equal(ManualDraftStoreOutcome.Changed,
            (await Transition(store, draft, creator, DraftReviewAction.Reopened,
                "reopen", 0, 2, "Important content change required")).Outcome);
        Assert.Equal(ManualDraftStoreOutcome.Changed,
            (await store.EditWeekAsync(tenant, legalEntity, draft.Id, creator,
                sku, "WH-1", 1, DraftWeekValueKind.Known, 99m,
                "Important content change", "allowed-edit", 0,
                Now.AddMinutes(5), default)).Outcome);
        var restored = Assert.IsType<DemandRevisionDraft>(await store.ReadAsync(
            tenant, legalEntity, draft.Id, default));
        Assert.Equal(DemandRevisionState.Draft, restored.State);
        Assert.Equal(3, restored.StateVersion);
        Assert.Equal(99m, restored.Series[0].Weeks[0].Quantity);
        Assert.Equal(3, await context.ManualDraftReviewAudit.CountDocumentsAsync(x =>
            x.TenantId == tenant && x.LegalEntityId == legalEntity &&
            x.RevisionId == draft.Id));
        Assert.Equal(0, await context.ManualDraftReviewSlots.CountDocumentsAsync(x =>
            x.TenantId == tenant && x.LegalEntityId == legalEntity &&
            x.RevisionId == draft.Id));
    }

    [ManualDraftMongoFact]
    public async Task TenantAndLegalEntityScopes_DenyReviewAndAuditAccess()
    {
        var (store, context) = Open();
        var tenant = Guid.NewGuid();
        var legalEntity = Guid.NewGuid();
        var creator = Guid.NewGuid();
        var draft = Draft(tenant, legalEntity, Guid.NewGuid(), creator);
        await store.CreateAsync(draft, "create", default);
        Assert.Equal(ManualDraftStoreOutcome.ScopeDenied,
            (await store.TransitionReviewAsync(Guid.NewGuid(), legalEntity,
                draft.Id, creator, DraftReviewAction.Submitted, null,
                "wrong-tenant", 0, 0, Now, default)).Outcome);
        Assert.Equal(ManualDraftStoreOutcome.ScopeDenied,
            (await store.TransitionReviewAsync(tenant, Guid.NewGuid(),
                draft.Id, creator, DraftReviewAction.Submitted, null,
                "wrong-company", 0, 0, Now, default)).Outcome);
        Assert.Null(await store.ReadAsync(Guid.NewGuid(), legalEntity, draft.Id, default));
        Assert.Null(await store.ReadAsync(tenant, Guid.NewGuid(), draft.Id, default));
        Assert.Equal(0, await context.ManualDraftReviewAudit.CountDocumentsAsync(x =>
            x.TenantId == tenant && x.LegalEntityId == legalEntity &&
            x.RevisionId == draft.Id));
    }

    [ManualDraftMongoFact]
    public async Task ReviewAuditInsertFailure_RollsBackStateAndCandidateSlot()
    {
        var (store, context) = Open();
        var tenant = Guid.NewGuid();
        var legalEntity = Guid.NewGuid();
        var creator = Guid.NewGuid();
        var draft = Draft(tenant, legalEntity, Guid.NewGuid(), creator);
        await store.CreateAsync(draft, "create", default);
        var keys = Builders<ManualDraftReviewAuditRecord>.IndexKeys;
        await context.ManualDraftReviewAudit.Indexes.CreateOneAsync(
            new CreateIndexModel<ManualDraftReviewAuditRecord>(
                keys.Ascending(x => x.RequestKey), new CreateIndexOptions<ManualDraftReviewAuditRecord>
                {
                    Unique = true, Name = "mod0188_test_review_audit_failure",
                    PartialFilterExpression = Builders<ManualDraftReviewAuditRecord>.Filter
                        .Eq(x => x.TenantId, tenant)
                }));
        try
        {
            await context.ManualDraftReviewAudit.InsertOneAsync(new ManualDraftReviewAuditRecord
            {
                TenantId = tenant, LegalEntityId = legalEntity,
                RevisionId = Guid.NewGuid(), RequestKey = "blocked-review",
                Fingerprint = "test", Action = DraftReviewAction.Submitted,
                ActorId = Guid.NewGuid(), OccurredAt = Now, CreatedAt = Now
            });
            Assert.Equal(ManualDraftStoreOutcome.Conflict,
                (await Transition(store, draft, creator, DraftReviewAction.Submitted,
                    "blocked-review", 0, 0)).Outcome);
            var restored = Assert.IsType<DemandRevisionDraft>(await store.ReadAsync(
                tenant, legalEntity, draft.Id, default));
            Assert.Equal(DemandRevisionState.Draft, restored.State);
            Assert.Equal(0, restored.StateVersion);
            Assert.Equal(0, await context.ManualDraftReviewSlots.CountDocumentsAsync(x =>
                x.TenantId == tenant && x.LegalEntityId == legalEntity &&
                x.PlanningCycleId == draft.PlanningCycleId));
            Assert.Equal(0, await context.ManualDraftReviewAudit.CountDocumentsAsync(x =>
                x.TenantId == tenant && x.LegalEntityId == legalEntity &&
                x.RevisionId == draft.Id));
        }
        finally
        {
            await context.ManualDraftReviewAudit.Indexes.DropOneAsync(
                "mod0188_test_review_audit_failure");
        }
    }
}
