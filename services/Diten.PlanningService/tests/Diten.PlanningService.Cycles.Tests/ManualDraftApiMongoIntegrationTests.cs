using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using Diten.PlanningService.Api.Features.DemandPlanning;
using Diten.PlanningService.Application.Features.DemandPlanning;
using Diten.PlanningService.Application.Features.DemandPlanning.Cycles;
using Diten.PlanningService.Application.Features.DemandPlanning.ManualDrafts;
using Diten.PlanningService.Domain.Features.DemandPlanning;
using Diten.PlanningService.Infrastructure.Features.DemandPlanning;
using Diten.PlanningService.Persistence.Features.DemandPlanning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using MediatR;
using Microsoft.Extensions.Configuration;
using MongoDB.Driver;
using Xunit;

namespace Diten.PlanningService.Cycles.Tests;

// All tests here require the explicit local replica set. They use a fresh Tenant
// per test in mod0188_tests, never an operational database or live authority.
public sealed class ManualDraftApiMongoIntegrationTests
{
    private static readonly DateOnly FirstMonday = new(2026, 10, 5);

    private static (DemandPlanningMongoContext Context, PlanningCycleMongoStore Cycles,
        ManualDraftMongoStore Drafts) Open()
    {
        var uri = Environment.GetEnvironmentVariable("MOD0188_TEST_MONGO_URI")
            ?? throw new InvalidOperationException("Explicit test URI required.");
        var url = new MongoUrl(uri);
        var servers = url.Servers.ToArray();
        if (servers.Length != 1 || servers[0].Host != "127.0.0.1" ||
            servers[0].Port is < 31994 or > 39994 ||
            url.ReplicaSetName != "rs-mod0188" ||
            url.Username is not null || url.Password is not null)
            throw new InvalidOperationException("Only local rs-mod0188 is allowed.");
        var config = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Mongo:ConnectionString"] = uri,
                ["Mongo:SupplyChainDatabaseName"] = "mod0188_tests"
            }).Build();
        var context = new DemandPlanningMongoContext(config);
        return (context, new PlanningCycleMongoStore(context),
            new ManualDraftMongoStore(context));
    }

    private static PlanningCycle Cycle(Guid tenant, Guid legalEntity, Guid creator) =>
        new()
        {
            TenantId = tenant, LegalEntityId = legalEntity,
            CreatedByActorId = creator, AsOfDate = new DateOnly(2026, 10, 2),
            CalendarId = "ISO-8601", CalendarVersion = "1",
            TimeZoneId = "Asia/Baku", HorizonStart = FirstMonday,
            HorizonEnd = FirstMonday.AddDays(363),
            PlanningPeriodKey = "2026-10-05",
            IdempotencyKey = Guid.NewGuid().ToString("N"),
            RequestFingerprint = "fixture",
            Weeks = Enumerable.Range(0, 52).Select(index => new PlanningWeek
            {
                Number = index + 1, WeekStart = FirstMonday.AddDays(index * 7),
                WeekEnd = FirstMonday.AddDays(index * 7 + 6)
            }).ToList()
        };

    private static ManualDraftSeriesInput Series(Guid sku, string warehouse = "WH-1",
        int? incompleteWeek = null) =>
        new(sku, warehouse, Enumerable.Range(1, 52).Select(number =>
            new ManualDraftWeekValue(number,
                number == incompleteWeek ? DraftWeekValueKind.Unknown :
                    DraftWeekValueKind.Known,
                number == incompleteWeek ? null : 0m)).ToArray());

    private sealed class FixtureAuthority(Guid legalEntity) : IManualDraftAuthority
    {
        public bool Assigned { get; set; } = true;
        public bool Scope { get; set; } = true;
        public bool Outage { get; set; }
        public Guid? OverrideResolution { get; set; }

        public Task<Guid?> ResolveSelectedAsync(Guid tenantId, Guid actorId,
            Guid selectedLegalEntityHint, CancellationToken cancellationToken)
        {
            if (Outage) throw new InvalidOperationException("fixture outage");
            return Task.FromResult(Assigned && selectedLegalEntityHint == legalEntity
                ? OverrideResolution ?? legalEntity : (Guid?)null);
        }

        public Task<IReadOnlyList<VerifiedDraftSeriesReference>?> VerifyCreateSeriesAsync(
            Guid tenantId, Guid actorId, Guid legalEntityId,
            IReadOnlyList<DraftSeriesKey> requested, CancellationToken cancellationToken)
        {
            if (Outage) throw new InvalidOperationException("fixture outage");
            IReadOnlyList<VerifiedDraftSeriesReference>? result = Scope
                ? requested.Select(x => new VerifiedDraftSeriesReference(
                    x.SkuId, x.WarehouseId, "EA")).ToArray() : null;
            return Task.FromResult(result);
        }

        public Task<bool?> CanAccessAsync(Guid tenantId, Guid actorId,
            Guid legalEntityId, IReadOnlyList<DraftSeriesKey> series,
            CancellationToken cancellationToken)
        {
            if (Outage) throw new InvalidOperationException("fixture outage");
            return Task.FromResult<bool?>(Scope);
        }
    }

    [ManualDraftMongoFact]
    public async Task ApiWorkflow_CreateReadEditSubmitIndependentApprove_AndScopeLoss()
    {
        var (context, cycles, drafts) = Open();
        var tenant = Guid.NewGuid();
        var legalEntity = Guid.NewGuid();
        var creator = Guid.NewGuid();
        var editor = Guid.NewGuid();
        var reviewer = Guid.NewGuid();
        var sku = Guid.NewGuid();
        var cycle = Cycle(tenant, legalEntity, creator);
        Assert.Equal(CycleInsertOutcome.Created,
            (await cycles.InsertOrGetAsync(cycle, default)).Outcome);
        var authority = new FixtureAuthority(legalEntity);
        var workflow = new ManualDraftWorkflow(authority, cycles, drafts);

        var created = await new CreateManualDraftHandler(workflow).Handle(
            new CreateManualDraftCommand(tenant, creator, legalEntity, true,
                cycle.Id, "No reliable history",
                [Series(sku, incompleteWeek: 9)], "create-api"), default);
        Assert.Equal(201, created.StatusCode);
        var id = Assert.IsType<ManualDraftView>(created.Data).RevisionId;
        Assert.Equal("EA", created.Data.Series[0].BaseUomId);
        Assert.Equal(52, created.Data.Series[0].Weeks.Count);
        Assert.Equal(DraftWeekValueKind.Unknown,
            created.Data.Series[0].Weeks[8].ValueKind);
        Assert.Equal(422, (await workflow.TransitionAsync(tenant, creator,
            legalEntity, true, id, DraftReviewAction.Submitted, null,
            "submit-too-early", 0, 0, default)).StatusCode);
        Assert.Equal(0, await context.ManualDraftReviewAudit.CountDocumentsAsync(x =>
            x.TenantId == tenant && x.RevisionId == id));

        var edited = await new EditManualDraftWeekHandler(workflow).Handle(
            new EditManualDraftWeekCommand(tenant, editor, legalEntity,
                true, id, sku, "WH-1", 9, DraftWeekValueKind.Known, 0m,
                "Week explicitly set to zero", "edit-api", 0), default);
        Assert.Equal(200, edited.StatusCode);
        Assert.Equal(1, edited.Data!.ContentVersion);
        var submitted = await new TransitionManualDraftHandler(workflow).Handle(
            new TransitionManualDraftCommand(tenant, creator,
                legalEntity, true, id, DraftReviewAction.Submitted, null,
                "submit-api", 1, 0), default);
        Assert.Equal(DemandRevisionState.InReview, submitted.Data!.State);
        Assert.Equal(403, (await workflow.TransitionAsync(tenant, creator,
            legalEntity, true, id, DraftReviewAction.Approved, "approve",
            "self-approve", 1, 1, default)).StatusCode);
        Assert.Equal(403, (await workflow.TransitionAsync(tenant, editor,
            legalEntity, true, id, DraftReviewAction.Approved, "approve",
            "editor-approve", 1, 1, default)).StatusCode);
        var approved = await workflow.TransitionAsync(tenant, reviewer,
            legalEntity, true, id, DraftReviewAction.Approved,
            "Reviewed all manual weeks", "approve-api", 1, 1, default);
        Assert.Equal(200, approved.StatusCode);
        Assert.Equal(DemandRevisionState.Approved, approved.Data!.State);
        Assert.Equal(reviewer, approved.Data.ReviewedBy);
        Assert.Single(approved.Data.Changes);
        Assert.Equal(editor, approved.Data.Changes[0].ActorId);

        authority.Scope = false;
        Assert.Equal(404, (await new GetManualDraftHandler(workflow).Handle(
            new GetManualDraftQuery(tenant, creator, legalEntity,
                true, id), default)).StatusCode);
        authority.Scope = true;
        authority.Assigned = false;
        Assert.Equal(503, (await workflow.ReadAsync(tenant, creator, legalEntity,
            true, id, default)).StatusCode);
        authority.Assigned = true;
        Assert.Equal(404, (await workflow.ReadAsync(Guid.NewGuid(), creator,
            legalEntity, true, id, default)).StatusCode);
        Assert.Equal(503, (await workflow.ReadAsync(tenant, creator,
            Guid.NewGuid(), true, id, default)).StatusCode);
    }

    [ManualDraftMongoFact]
    public async Task ApiWorkflow_IdempotencyConflictStaleVersionAndReasonedRejection()
    {
        var (context, cycles, drafts) = Open();
        var tenant = Guid.NewGuid();
        var legalEntity = Guid.NewGuid();
        var creator = Guid.NewGuid();
        var reviewer = Guid.NewGuid();
        var sku = Guid.NewGuid();
        var cycle = Cycle(tenant, legalEntity, creator);
        await cycles.InsertOrGetAsync(cycle, default);
        var workflow = new ManualDraftWorkflow(new FixtureAuthority(legalEntity),
            cycles, drafts);
        var input = new[] { Series(sku) };
        var first = await workflow.CreateAsync(tenant, creator, legalEntity, true,
            cycle.Id, "Manual fixture", input, "same-create", default);
        var replay = await workflow.CreateAsync(tenant, creator, legalEntity, true,
            cycle.Id, "Manual fixture", input, "same-create", default);
        Assert.Equal(first.Data!.RevisionId, replay.Data!.RevisionId);
        Assert.True(replay.Data.Replayed);
        Assert.Equal(409, (await workflow.CreateAsync(tenant, creator,
            legalEntity, true, cycle.Id, "Changed reason", input,
            "same-create", default)).StatusCode);
        var id = first.Data.RevisionId;
        Assert.Equal(1, await context.ManualDraftManifests.CountDocumentsAsync(x =>
            x.TenantId == tenant && x.PlanningCycleId == cycle.Id));
        Assert.Equal(1, await context.ManualDraftAudit.CountDocumentsAsync(x =>
            x.TenantId == tenant && x.RevisionId == id));
        Assert.Equal(403, (await workflow.EditWeekAsync(tenant, creator,
            legalEntity, false, id, sku, "WH-1", 1, DraftWeekValueKind.Known,
            1m, "Denied", "denied", 0, default)).StatusCode);
        var edit = await workflow.EditWeekAsync(tenant, creator, legalEntity, true,
            id, sku, "WH-1", 1, DraftWeekValueKind.Known, 1m,
            "Manual correction", "edit-once", 0, default);
        Assert.Equal(200, edit.StatusCode);
        Assert.True((await workflow.EditWeekAsync(tenant, creator,
            legalEntity, true, id, sku, "WH-1", 1, DraftWeekValueKind.Known,
            1m, "Manual correction", "edit-once", 0, default)).Data!.Replayed);
        Assert.Equal(409, (await workflow.EditWeekAsync(tenant, creator,
            legalEntity, true, id, sku, "WH-1", 1, DraftWeekValueKind.Known,
            2m, "Different", "edit-once", 0, default)).StatusCode);
        Assert.Equal(409, (await workflow.EditWeekAsync(tenant, creator,
            legalEntity, true, id, sku, "WH-1", 1, DraftWeekValueKind.Known,
            2m, "Stale", "stale", 0, default)).StatusCode);
        var submit = await workflow.TransitionAsync(tenant, creator, legalEntity,
            true, id, DraftReviewAction.Submitted, null, "submit", 1, 0, default);
        Assert.Equal(200, submit.StatusCode);
        var rejected = await workflow.TransitionAsync(tenant, reviewer, legalEntity,
            true, id, DraftReviewAction.Rejected, "Needs correction",
            "reject", 1, 1, default);
        Assert.Equal(DemandRevisionState.Draft, rejected.Data!.State);
        Assert.Equal("Needs correction", rejected.Data.ReviewReason);
        Assert.True((await workflow.TransitionAsync(tenant, reviewer, legalEntity,
            true, id, DraftReviewAction.Rejected, "Needs correction",
            "reject", 1, 1, default)).Data!.Replayed);
        Assert.Equal(409, (await workflow.TransitionAsync(tenant, reviewer,
            legalEntity, true, id, DraftReviewAction.Rejected,
            "Other reason", "reject", 1, 1, default)).StatusCode);
        Assert.Equal(2, await context.ManualDraftReviewAudit.CountDocumentsAsync(x =>
            x.TenantId == tenant && x.RevisionId == id));
    }

    [ManualDraftMongoFact]
    public async Task ApiWorkflow_CompetingCandidatesLeaveOneSlotAndOneReviewAudit()
    {
        var (context, cycles, drafts) = Open();
        var tenant = Guid.NewGuid();
        var legalEntity = Guid.NewGuid();
        var creator = Guid.NewGuid();
        var cycle = Cycle(tenant, legalEntity, creator);
        await cycles.InsertOrGetAsync(cycle, default);
        var workflow = new ManualDraftWorkflow(new FixtureAuthority(legalEntity),
            cycles, drafts);
        var one = (await workflow.CreateAsync(tenant, creator, legalEntity, true,
            cycle.Id, "Manual one", [Series(Guid.NewGuid())],
            "create-one", default)).Data!;
        var two = (await workflow.CreateAsync(tenant, creator, legalEntity, true,
            cycle.Id, "Manual two", [Series(Guid.NewGuid())],
            "create-two", default)).Data!;
        var race = await Task.WhenAll(
            workflow.TransitionAsync(tenant, creator, legalEntity, true,
                one.RevisionId, DraftReviewAction.Submitted, null,
                "submit-one", 0, 0, default),
            workflow.TransitionAsync(tenant, creator, legalEntity, true,
                two.RevisionId, DraftReviewAction.Submitted, null,
                "submit-two", 0, 0, default));
        Assert.Single(race, x => x.StatusCode == 200);
        Assert.Single(race, x => x.StatusCode == 409);
        Assert.Equal(1, await context.ManualDraftReviewSlots.CountDocumentsAsync(x =>
            x.TenantId == tenant && x.PlanningCycleId == cycle.Id));
        Assert.Equal(1, await context.ManualDraftReviewAudit.CountDocumentsAsync(x =>
            x.TenantId == tenant &&
            (x.RevisionId == one.RevisionId || x.RevisionId == two.RevisionId)));

        var third = (await workflow.CreateAsync(tenant, creator, legalEntity, true,
            Cycle(tenant, legalEntity, creator).Id, "Invalid cycle",
            [Series(Guid.NewGuid())], "create-three", default));
        Assert.Equal(404, third.StatusCode);
    }

    [ManualDraftMongoFact]
    public async Task ApiWorkflow_UnconfiguredAuthorityFailsClosedBeforeAnyWrite()
    {
        var (context, cycles, drafts) = Open();
        var tenant = Guid.NewGuid();
        var legalEntity = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var cycle = Cycle(tenant, legalEntity, actor);
        await cycles.InsertOrGetAsync(cycle, default);
        var workflow = new ManualDraftWorkflow(new UnconfiguredManualDraftAuthority(),
            cycles, drafts);
        Assert.Equal(503, (await workflow.CreateAsync(tenant, actor, legalEntity,
            true, cycle.Id, "Manual", [Series(Guid.NewGuid())],
            "closed", default)).StatusCode);
        Assert.Equal(0, await context.ManualDraftManifests.CountDocumentsAsync(x =>
            x.TenantId == tenant && x.LegalEntityId == legalEntity));
    }

    [ManualDraftMongoFact]
    public async Task ApiWorkflow_ReviewAuditWriteFailureRollsBackStateAndCandidateSlot()
    {
        var (context, cycles, drafts) = Open();
        var tenant = Guid.NewGuid();
        var legalEntity = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var cycle = Cycle(tenant, legalEntity, actor);
        await cycles.InsertOrGetAsync(cycle, default);
        var workflow = new ManualDraftWorkflow(new FixtureAuthority(legalEntity),
            cycles, drafts);
        var created = (await workflow.CreateAsync(tenant, actor, legalEntity,
            true, cycle.Id, "Manual", [Series(Guid.NewGuid())],
            "create-audit", default)).Data!;
        var keys = Builders<ManualDraftReviewAuditRecord>.IndexKeys;
        await context.ManualDraftReviewAudit.Indexes.CreateOneAsync(
            new CreateIndexModel<ManualDraftReviewAuditRecord>(
                keys.Ascending(x => x.RequestKey),
                new CreateIndexOptions<ManualDraftReviewAuditRecord>
                {
                    Unique = true, Name = "mod0188_api_test_review_audit_failure",
                    PartialFilterExpression =
                        Builders<ManualDraftReviewAuditRecord>.Filter
                            .Eq(x => x.TenantId, tenant)
                }));
        try
        {
            await context.ManualDraftReviewAudit.InsertOneAsync(
                new ManualDraftReviewAuditRecord
                {
                    TenantId = tenant, LegalEntityId = legalEntity,
                    RevisionId = Guid.NewGuid(), RequestKey = "blocked-api",
                    Fingerprint = "fixture", Action = DraftReviewAction.Submitted,
                    ActorId = Guid.NewGuid(), OccurredAt = DateTimeOffset.UtcNow,
                    CreatedAt = DateTimeOffset.UtcNow
                });
            var attempted = await workflow.TransitionAsync(tenant, actor,
                legalEntity, true, created.RevisionId, DraftReviewAction.Submitted,
                null, "blocked-api", 0, 0, default);
            Assert.False(attempted.IsSuccessful);
            var persisted = await drafts.ReadAsync(tenant, legalEntity,
                created.RevisionId, default);
            Assert.Equal(DemandRevisionState.Draft, persisted!.State);
            Assert.Equal(0, persisted.StateVersion);
            Assert.Equal(0, await context.ManualDraftReviewSlots.CountDocumentsAsync(x =>
                x.TenantId == tenant && x.PlanningCycleId == cycle.Id));
            Assert.Equal(0, await context.ManualDraftReviewAudit.CountDocumentsAsync(x =>
                x.TenantId == tenant && x.RevisionId == created.RevisionId));
        }
        finally
        {
            await context.ManualDraftReviewAudit.Indexes.DropOneAsync(
                "mod0188_api_test_review_audit_failure");
        }
    }

    [ManualDraftMongoFact]
    public async Task ExcludeDuringReview_ReopensAtomicallyAndPreservesSeries()
    {
        var (context, cycles, drafts) = Open();
        var tenant = Guid.NewGuid();
        var legalEntity = Guid.NewGuid();
        var planner = Guid.NewGuid();
        var sku1 = Guid.NewGuid();
        var sku2 = Guid.NewGuid();
        var cycle = Cycle(tenant, legalEntity, planner);
        await cycles.InsertOrGetAsync(cycle, default);
        var authority = new FixtureAuthority(legalEntity);
        var workflow = new ManualDraftWorkflow(authority, cycles, drafts);
        var created = (await workflow.CreateAsync(tenant, planner, legalEntity,
            true, cycle.Id, "Manual plan", [Series(sku1), Series(sku2, "WH-2")],
            "create-exclusion", default)).Data!;
        var submitted = await workflow.TransitionAsync(tenant, planner,
            legalEntity, true, created.RevisionId, DraftReviewAction.Submitted,
            null, "submit-exclusion", 0, 0, default);
        Assert.Equal(DemandRevisionState.InReview, submitted.Data!.State);
        var excluded = await new ExcludeManualDraftSeriesHandler(workflow).Handle(
            new ExcludeManualDraftSeriesCommand(tenant, planner, legalEntity,
                true, created.RevisionId, sku2, "WH-2", "Unverified warehouse",
                "exclude-once", 0, 1), default);
        Assert.Equal(200, excluded.StatusCode);
        Assert.Equal(DemandRevisionState.Draft, excluded.Data!.State);
        Assert.Equal(1, excluded.Data.ContentVersion);
        Assert.Equal(2, excluded.Data.StateVersion);
        Assert.Equal(2, excluded.Data.Series.Count);
        Assert.Equal(52, excluded.Data.Series.Single(x => x.SkuId == sku2).Weeks.Count);
        Assert.Equal("Unverified warehouse", excluded.Data.Series.Single(x =>
            x.SkuId == sku2).Exclusion!.Reason);
        Assert.Single(excluded.Data.Exclusions);
        Assert.Equal(DemandRevisionState.InReview,
            excluded.Data.Exclusions[0].PreviousState);
        Assert.Equal(0, await context.ManualDraftReviewSlots.CountDocumentsAsync(x =>
            x.TenantId == tenant && x.PlanningCycleId == cycle.Id));
        Assert.Equal(2, await context.ManualDraftReviewAudit.CountDocumentsAsync(x =>
            x.TenantId == tenant && x.RevisionId == created.RevisionId));
        Assert.Equal(2, await context.ManualDraftAudit.CountDocumentsAsync(x =>
            x.TenantId == tenant && x.RevisionId == created.RevisionId));
        var reread = await workflow.ReadAsync(tenant, planner, legalEntity,
            true, created.RevisionId, default);
        Assert.Equal(DemandRevisionState.Draft, reread.Data!.State);
        Assert.NotNull(reread.Data.Series.Single(x => x.SkuId == sku2).Exclusion);
        var replay = await workflow.ExcludeSeriesAsync(tenant, planner, legalEntity,
            true, created.RevisionId, sku2, "WH-2", " Unverified warehouse ",
            "exclude-once", 0, 1, default);
        Assert.True(replay.Data!.Replayed);
        Assert.Equal(409, (await workflow.ExcludeSeriesAsync(tenant, planner,
            legalEntity, true, created.RevisionId, sku2, "WH-2", "Different",
            "exclude-once", 0, 1, default)).StatusCode);
        Assert.Equal(2, await context.ManualDraftReviewAudit.CountDocumentsAsync(x =>
            x.TenantId == tenant && x.RevisionId == created.RevisionId));
    }

    [ManualDraftMongoFact]
    public async Task ApprovedRevision_RequiresExplicitAuditedReopenBeforeExclusion()
    {
        var (context, cycles, drafts) = Open();
        var tenant = Guid.NewGuid();
        var legalEntity = Guid.NewGuid();
        var planner = Guid.NewGuid();
        var reviewer = Guid.NewGuid();
        var sku1 = Guid.NewGuid();
        var sku2 = Guid.NewGuid();
        var cycle = Cycle(tenant, legalEntity, planner);
        await cycles.InsertOrGetAsync(cycle, default);
        var workflow = new ManualDraftWorkflow(new FixtureAuthority(legalEntity),
            cycles, drafts);
        var created = (await workflow.CreateAsync(tenant, planner, legalEntity,
            true, cycle.Id, "Manual plan", [Series(sku1), Series(sku2, "WH-2")],
            "create-approved", default)).Data!;
        await workflow.TransitionAsync(tenant, planner, legalEntity, true,
            created.RevisionId, DraftReviewAction.Submitted, null,
            "submit-approved", 0, 0, default);
        var approved = await workflow.TransitionAsync(tenant, reviewer, legalEntity,
            true, created.RevisionId, DraftReviewAction.Approved,
            "Reviewed", "approve-once", 0, 1, default);
        Assert.Equal(DemandRevisionState.Approved, approved.Data!.State);
        Assert.Equal(409, (await workflow.ExcludeSeriesAsync(tenant, planner,
            legalEntity, true, created.RevisionId, sku2, "WH-2", "Exclude",
            "exclude-too-soon", 0, 2, default)).StatusCode);
        Assert.Equal(1, await context.ManualDraftReviewSlots.CountDocumentsAsync(x =>
            x.TenantId == tenant && x.PlanningCycleId == cycle.Id));
        var reopened = await workflow.TransitionAsync(tenant, planner, legalEntity,
            true, created.RevisionId, DraftReviewAction.Reopened,
            "Changed scope", "reopen-approved", 0, 2, default);
        Assert.Equal(DemandRevisionState.Draft, reopened.Data!.State);
        Assert.Null(reopened.Data.ReviewedBy);
        Assert.Equal(3, reopened.Data.StateVersion);
        Assert.Equal(0, await context.ManualDraftReviewSlots.CountDocumentsAsync(x =>
            x.TenantId == tenant && x.PlanningCycleId == cycle.Id));
        var excluded = await workflow.ExcludeSeriesAsync(tenant, planner,
            legalEntity, true, created.RevisionId, sku2, "WH-2", "Exclude",
            "exclude-after-reopen", 0, 3, default);
        Assert.Equal(200, excluded.StatusCode);
        Assert.NotNull(excluded.Data!.Series.Single(x => x.SkuId == sku2).Exclusion);
        Assert.Equal(3, await context.ManualDraftReviewAudit.CountDocumentsAsync(x =>
            x.TenantId == tenant && x.RevisionId == created.RevisionId));
    }

    [ManualDraftMongoFact]
    public async Task Exclusion_RequiresCurrentPermissionScopeAndVersions()
    {
        var (context, cycles, drafts) = Open();
        var tenant = Guid.NewGuid();
        var legalEntity = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var sku1 = Guid.NewGuid();
        var sku2 = Guid.NewGuid();
        var cycle = Cycle(tenant, legalEntity, actor);
        await cycles.InsertOrGetAsync(cycle, default);
        var authority = new FixtureAuthority(legalEntity);
        var workflow = new ManualDraftWorkflow(authority, cycles, drafts);
        var created = (await workflow.CreateAsync(tenant, actor, legalEntity,
            true, cycle.Id, "Manual plan", [Series(sku1), Series(sku2, "WH-2")],
            "create-scope", default)).Data!;
        var id = created.RevisionId;
        Assert.Equal(403, (await workflow.ExcludeSeriesAsync(tenant, actor,
            legalEntity, false, id, sku2, "WH-2", "Denied", "no-permission",
            0, 0, default)).StatusCode);
        authority.Scope = false;
        Assert.Equal(404, (await workflow.ExcludeSeriesAsync(tenant, actor,
            legalEntity, true, id, sku2, "WH-2", "Denied", "lost-scope",
            0, 0, default)).StatusCode);
        authority.Scope = true;
        authority.Outage = true;
        Assert.Equal(503, (await workflow.ExcludeSeriesAsync(tenant, actor,
            legalEntity, true, id, sku2, "WH-2", "Denied", "scope-outage",
            0, 0, default)).StatusCode);
        authority.Outage = false;
        Assert.Equal(404, (await workflow.ExcludeSeriesAsync(Guid.NewGuid(), actor,
            legalEntity, true, id, sku2, "WH-2", "Denied", "other-tenant",
            0, 0, default)).StatusCode);
        Assert.Equal(409, (await workflow.ExcludeSeriesAsync(tenant, actor,
            legalEntity, true, id, sku2, "WH-2", "Stale", "stale-exclude",
            1, 0, default)).StatusCode);
        Assert.Equal(0, await context.ManualDraftAudit.CountDocumentsAsync(x =>
            x.TenantId == tenant && x.RevisionId == id && x.Action == "Excluded"));
        Assert.Equal(0, await context.ManualDraftReviewAudit.CountDocumentsAsync(x =>
            x.TenantId == tenant && x.RevisionId == id));
    }

    [ManualDraftMongoFact]
    public async Task ConcurrentExclusions_AtMostOneCommits()
    {
        var (context, cycles, drafts) = Open();
        var tenant = Guid.NewGuid();
        var legalEntity = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var firstSku = Guid.NewGuid();
        var secondSku = Guid.NewGuid();
        var thirdSku = Guid.NewGuid();
        var cycle = Cycle(tenant, legalEntity, actor);
        await cycles.InsertOrGetAsync(cycle, default);
        var workflow = new ManualDraftWorkflow(new FixtureAuthority(legalEntity),
            cycles, drafts);
        var id = (await workflow.CreateAsync(tenant, actor, legalEntity,
            true, cycle.Id, "Manual plan",
            [Series(firstSku), Series(secondSku, "WH-2"),
                Series(thirdSku, "WH-3")], "create-race", default)).Data!.RevisionId;
        var race = await Task.WhenAll(
            workflow.ExcludeSeriesAsync(tenant, actor, legalEntity, true,
                id, secondSku, "WH-2", "First exclusion", "race-one", 0, 0, default),
            workflow.ExcludeSeriesAsync(tenant, actor, legalEntity, true,
                id, thirdSku, "WH-3", "Second exclusion", "race-two", 0, 0, default));
        Assert.Single(race, x => x.StatusCode == 200);
        Assert.Single(race, x => x.StatusCode == 409);
        var saved = await drafts.ReadAsync(tenant, legalEntity, id, default);
        Assert.Single(saved!.Exclusions);
        Assert.Equal(1, saved.Version);
        Assert.Equal(2, await context.ManualDraftAudit.CountDocumentsAsync(x =>
            x.TenantId == tenant && x.RevisionId == id));
    }

    [ManualDraftMongoFact]
    public async Task ExclusionAuditFailure_RollsBackReopenSlotAndContent()
    {
        var (context, cycles, drafts) = Open();
        var tenant = Guid.NewGuid();
        var legalEntity = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var sku1 = Guid.NewGuid();
        var sku2 = Guid.NewGuid();
        var cycle = Cycle(tenant, legalEntity, actor);
        await cycles.InsertOrGetAsync(cycle, default);
        var workflow = new ManualDraftWorkflow(new FixtureAuthority(legalEntity),
            cycles, drafts);
        var id = (await workflow.CreateAsync(tenant, actor, legalEntity,
            true, cycle.Id, "Manual plan", [Series(sku1), Series(sku2, "WH-2")],
            "create-rollback", default)).Data!.RevisionId;
        await workflow.TransitionAsync(tenant, actor, legalEntity, true,
            id, DraftReviewAction.Submitted, null, "submit-rollback",
            0, 0, default);
        var keys = Builders<ManualDraftAuditRecord>.IndexKeys;
        await context.ManualDraftAudit.Indexes.CreateOneAsync(
            new CreateIndexModel<ManualDraftAuditRecord>(
                keys.Ascending(x => x.RequestKey),
                new CreateIndexOptions<ManualDraftAuditRecord>
                {
                    Unique = true, Name = "mod0188_api_test_exclusion_audit_failure",
                    PartialFilterExpression =
                        Builders<ManualDraftAuditRecord>.Filter.Eq(x => x.TenantId, tenant)
                }));
        try
        {
            await context.ManualDraftAudit.InsertOneAsync(new ManualDraftAuditRecord
            {
                TenantId = tenant, LegalEntityId = legalEntity,
                RevisionId = Guid.NewGuid(), RequestKey = "blocked-exclusion",
                Fingerprint = "fixture", Action = "fixture", ActorId = actor,
                OccurredAt = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow
            });
            var response = await workflow.ExcludeSeriesAsync(tenant, actor,
                legalEntity, true, id, sku2, "WH-2", "Audit must persist",
                "blocked-exclusion", 0, 1, default);
            Assert.False(response.IsSuccessful);
            var saved = await drafts.ReadAsync(tenant, legalEntity, id, default);
            Assert.Equal(DemandRevisionState.InReview, saved!.State);
            Assert.Equal(0, saved.Version);
            Assert.Equal(1, saved.StateVersion);
            Assert.Empty(saved.Exclusions);
            Assert.Equal(1, await context.ManualDraftReviewSlots.CountDocumentsAsync(x =>
                x.TenantId == tenant && x.PlanningCycleId == cycle.Id));
            Assert.Equal(1, await context.ManualDraftReviewAudit.CountDocumentsAsync(x =>
                x.TenantId == tenant && x.RevisionId == id));
            Assert.Equal(1, await context.ManualDraftAudit.CountDocumentsAsync(x =>
                x.TenantId == tenant && x.RevisionId == id));
        }
        finally
        {
            await context.ManualDraftAudit.Indexes.DropOneAsync(
                "mod0188_api_test_exclusion_audit_failure");
        }
    }

    [Fact]
    public void DraftApi_RoutesAndPermissionsAreExplicit()
    {
        var type = typeof(ManualDraftRevisionsController);
        Assert.Equal("api/v2/demand/revisions",
            Assert.IsType<RouteAttribute>(Attribute.GetCustomAttribute(
                type, typeof(RouteAttribute))).Template);
        static string Permission(string action) =>
            Assert.IsType<HasPermissionAttribute>(
                Attribute.GetCustomAttribute(typeof(ManualDraftRevisionsController)
                    .GetMethod(action)!, typeof(HasPermissionAttribute))).Policy!;
        Assert.Equal("demand.drafts.create", Permission("Create"));
        Assert.Equal("demand.plans.read", Permission("Get"));
        Assert.Equal("demand.drafts.update", Permission("EditWeek"));
        Assert.Equal("demand.drafts.update", Permission("Submit"));
        Assert.Equal("demand.plans.review", Permission("Approve"));
        Assert.Equal("demand.plans.review", Permission("Reject"));
        Assert.Equal("demand.drafts.update", Permission("Reopen"));
        Assert.Equal("demand.drafts.update", Permission("ExcludeSeries"));
    }

    [Fact]
    public async Task DraftApi_UsesServerActorTenantAndUntrustedHeaderHint()
    {
        var tenant = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var hint = Guid.NewGuid();
        var proxy = DispatchProxy.Create<ISender, CapturingSenderProxy>();
        var capture = (CapturingSenderProxy)proxy;
        var http = new DefaultHttpContext();
        http.Items["DemandPlanning.TenantId"] = tenant;
        http.User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, actor.ToString("D")),
            new Claim("permission", "demand.drafts.create")
        ], "fixture"));
        var controller = new ManualDraftRevisionsController(proxy)
        {
            ControllerContext = new ControllerContext { HttpContext = http }
        };
        var cycleId = Guid.NewGuid();
        await controller.Create(new CreateManualDraftRequest(cycleId,
            "Explicit manual plan", [Series(Guid.NewGuid())]), hint, "api-key",
            default);
        var command = Assert.IsType<CreateManualDraftCommand>(capture.LastRequest);
        Assert.Equal(tenant, command.TenantId);
        Assert.Equal(actor, command.ActorId);
        Assert.Equal(hint, command.SelectedLegalEntityHint);
        Assert.True(command.HasPermission);
        Assert.Equal(cycleId, command.PlanningCycleId);
        Assert.Equal("api-key", command.IdempotencyKey);
    }

    [Fact]
    public async Task ReopenAndExclusionApi_UseServerActorAndUpdatePermission()
    {
        var tenant = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var hint = Guid.NewGuid();
        var revision = Guid.NewGuid();
        var sku = Guid.NewGuid();
        var proxy = DispatchProxy.Create<ISender, CapturingSenderProxy>();
        var capture = (CapturingSenderProxy)proxy;
        var http = new DefaultHttpContext();
        http.Items["DemandPlanning.TenantId"] = tenant;
        http.User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, actor.ToString("D")),
            new Claim("permission", "demand.drafts.update")
        ], "fixture"));
        var controller = new ManualDraftRevisionsController(proxy)
        {
            ControllerContext = new ControllerContext { HttpContext = http }
        };
        await controller.Reopen(revision, new DraftTransitionRequest(2, 3,
            "Changed scope"), hint, "reopen-api", default);
        var reopened = Assert.IsType<TransitionManualDraftCommand>(capture.LastRequest);
        Assert.Equal(DraftReviewAction.Reopened, reopened.Action);
        Assert.Equal(tenant, reopened.TenantId);
        Assert.Equal(actor, reopened.ActorId);
        Assert.Equal(hint, reopened.SelectedLegalEntityHint);
        Assert.True(reopened.HasPermission);
        await controller.ExcludeSeries(revision, sku, "WH-2",
            new ExcludeManualDraftSeriesRequest("Reason", 2, 4),
            hint, "exclude-api", default);
        var excluded = Assert.IsType<ExcludeManualDraftSeriesCommand>(capture.LastRequest);
        Assert.Equal(tenant, excluded.TenantId);
        Assert.Equal(actor, excluded.ActorId);
        Assert.Equal(hint, excluded.SelectedLegalEntityHint);
        Assert.True(excluded.HasPermission);
        Assert.Equal(sku, excluded.SkuId);
        Assert.Equal("WH-2", excluded.WarehouseId);
        Assert.Equal("exclude-api", excluded.IdempotencyKey);
    }

    [Fact]
    public void DraftApi_RejectsClientActorTenantLegalEntityAndBaseUomPayload()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<CreateManualDraftRequest>(
                """{"planningCycleId":"11111111-1111-1111-1111-111111111111","reason":"Manual","series":[],"tenantId":"22222222-2222-2222-2222-222222222222"}""",
                options));
        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<CreateManualDraftRequest>(
                """{"planningCycleId":"11111111-1111-1111-1111-111111111111","reason":"Manual","series":[{"skuId":"33333333-3333-3333-3333-333333333333","warehouseId":"WH-1","baseUomId":"EA","weeks":[]}]}""",
                options));
        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<DraftTransitionRequest>(
                """{"expectedContentVersion":0,"expectedStateVersion":0,"actorId":"44444444-4444-4444-4444-444444444444"}""",
                options));
        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<ExcludeManualDraftSeriesRequest>(
                """{"reason":"Exclude","expectedContentVersion":0,"expectedStateVersion":0,"legalEntityId":"44444444-4444-4444-4444-444444444444"}""",
                options));
    }

    public class CapturingSenderProxy : DispatchProxy
    {
        public object? LastRequest { get; private set; }
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            LastRequest = args?[0];
            return Task.FromResult(Response<ManualDraftView>.Fail(
                "Fixture sender did not persist.", 503));
        }
    }
}
