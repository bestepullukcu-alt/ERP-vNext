using System.Reflection;
using Diten.CrmService.Api.Controllers.CRM;
using Diten.CrmService.Api.Models.CRM;
using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Features.Campaign;
using Diten.CrmService.Application.Features.Campaign.Commands;
using Diten.CrmService.Application.Features.Campaign.Contract;
using Diten.CrmService.Application.Features.Campaign.Handlers;
using Diten.CrmService.Application.Features.Campaign.Queries;
using Diten.CrmService.Application.Features.Campaign.Services;
using Diten.CrmService.Application.Features.CyclePeriod.Read;
using Diten.CrmService.Application.Features.Campaign.Snapshot;
using Diten.CrmService.Application.Features.ConsentPreference.Evaluation;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;
using CampaignEntity = Diten.CrmService.Domain.Entities.Campaign;

namespace Diten.CrmService.Application.Tests;

/// <summary>
/// MOD-0165 FU04 — Campaign / Targeting runtime + static target snapshot. Pins down: campaign and target are their own
/// aggregates holding only references, TenantId is claim-only, code uniqueness, the archived-campaign target freeze,
/// the strict manual duplicate guard vs the idempotent snapshot reconcile, additive snapshots, segment provenance
/// without membership resolution, and — the core of this FU — that consent is consumed through the MOD-0164 provider
/// seam only: allowed ⇒ active, blocked/unknown ⇒ excluded WITH a reason, unknown is never allowed, no consent data is
/// copied, and a missing consent context is rejected rather than guessed.
/// </summary>
public sealed class CampaignTargetingRuntimeTests
{
    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TenantB = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Contact1 = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid Contact2 = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid SegmentId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid BrandId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly DateTimeOffset Jan1 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Jun1 = new(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);

    private static TenantContext Tenant(Guid id)
    {
        var ctx = new TenantContext();
        ctx.SetTenant(id);
        return ctx;
    }

    private sealed class Fixture
    {
        public FakeCampaignRepo Campaigns { get; } = new();
        public FakeCampaignTargetRepo Targets { get; } = new();
        public FakeConsentEvaluator Consent { get; } = new();
        public Guid TenantId { get; }

        public Fixture(Guid tenant) => TenantId = tenant;

        // MOD-0165 FU08 added a cycle-period dependency to the campaign read/write handlers. These fixtures pass an
        // EMPTY read seam on purpose: no FU04 scenario binds a cycle period, so the guard short-circuits on the null
        // binding and the projection stays null. FU04 behaviour is therefore unchanged, which is what these tests
        // assert. Only the wiring below moved; not one FU04 assertion did.
        public FakeEmptyCyclePeriodReader CyclePeriods { get; } = new();

        private CampaignCycleBindingGuard CycleBinding => new(CyclePeriods);

        // MOD-0165 FU09 added a scope gate to the campaign write path. FU04 scenarios author no scope, so every
        // command derives ScopeType=tenant and the gate short-circuits before touching a reference set. The published
        // sets below therefore never matter here - they exist only so the seam is non-null. Not one FU04 assertion
        // changed; only the wiring did.
        private CampaignScopeWriteValidator Scope => new(
            new CampaignScopeTestDoubles.FakeReferenceValidator(),
            new CampaignScopeTestDoubles.FakeLegalEntityValidator());

        // MOD-0165 FU10 added a targeting gate and a code generator to the campaign write path. FU04 scenarios author
        // no targeting mode, so every command derives `manual` — which is exactly the mode FU04's manual targets
        // belong to, and why not one FU04 assertion changed.
        public CampaignScopeTestDoubles.FakeSegmentCatalog SegmentCatalog { get; } = new();

        private CampaignSegmentValidator Targeting => new(SegmentCatalog);

        private ICampaignCodeGenerator CodeGenerator
            => new CampaignCodeGenerator(new CampaignScopeTestDoubles.FakeCampaignCodeSequence(), Campaigns);

        public CreateCampaignHandler CreateCampaign(Guid? tenant = null)
            => new(Tenant(tenant ?? TenantId), new NullActorContext(), Campaigns, CycleBinding, Scope, Targeting, CodeGenerator);

        public UpdateCampaignHandler UpdateCampaign()
            => new(Tenant(TenantId), new NullActorContext(), Campaigns, CycleBinding, Scope, Targeting);

        public ArchiveCampaignHandler ArchiveCampaign() => new(Tenant(TenantId), new NullActorContext(), Campaigns);

        public GetCampaignHandler GetCampaign(Guid? tenant = null)
            => new(Tenant(tenant ?? TenantId), Campaigns, CyclePeriods, SegmentCatalog);

        public ListCampaignsHandler ListCampaigns(Guid? tenant = null)
            => new(Tenant(tenant ?? TenantId), Campaigns, CyclePeriods, SegmentCatalog);

        public CreateCampaignTargetHandler CreateTarget()
            => new(Tenant(TenantId), new NullActorContext(), Campaigns, Targets);

        public UpdateCampaignTargetHandler UpdateTarget()
            => new(Tenant(TenantId), new NullActorContext(), Campaigns, Targets);

        public ArchiveCampaignTargetHandler ArchiveTarget()
            => new(Tenant(TenantId), new NullActorContext(), Targets);

        public ListCampaignTargetsHandler ListTargets(Guid? tenant = null)
            => new(Tenant(tenant ?? TenantId), Campaigns, Targets);

        public GetCampaignTargetHandler GetTarget() => new(Tenant(TenantId), Targets);

        public CreateCampaignTargetSnapshotHandler Snapshot()
            => new(Tenant(TenantId), new NullActorContext(), Campaigns, Targets, Consent);

        /// <summary>Creates a campaign and returns its id (fails the test if the create did not succeed).</summary>
        public async Task<Guid> SeedCampaignAsync(
            string code = "CMP-1",
            string? defaultChannel = null,
            string? defaultPurpose = null)
        {
            var response = await CreateCampaign().Handle(CampaignCmd(code, defaultChannel, defaultPurpose), default);
            Assert.Equal(201, response.StatusCode);
            return response.Data;
        }
    }

    private static CreateCampaignCommand CampaignCmd(
        string code = "CMP-1",
        string? defaultChannel = null,
        string? defaultPurpose = null,
        string campaignType = CampaignTypes.ProductCampaign,
        string? status = CampaignStatuses.Active,
        DateTimeOffset? start = null,
        DateTimeOffset? end = null,
        string? objectiveType = CampaignObjectiveTypes.Awareness)
        // FU10 removed the nine reference fields and the external-reference list from the command; FU04's assertions
        // never depended on them, so the helper simply stops passing what no longer exists.
        => new(code, "Campaign " + code, campaignType, start ?? Jan1, status, objectiveType, null,
            defaultChannel, defaultPurpose, end, null);

    private static CreateCampaignTargetCommand TargetCmd(
        Guid campaignId,
        Guid? targetId = null,
        string targetType = CampaignTargetTypes.AccountContactLink,
        string source = CampaignTargetSources.Manual,
        string selectionReason = "manual selection for smoke",
        string? status = CampaignTargetStatuses.Active,
        int? priority = 100,
        string? priorityLevel = null,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        string? exclusionReason = null,
        string? sourceReferenceType = null,
        Guid? sourceReferenceId = null)
        // FU11 - named from here on. The command grew a PriorityLevel argument and three of its leading parameters
        // became optional, so a positional call silently lines arguments up against the wrong parameters.
        => new(campaignId, targetType, targetId ?? Contact1,
            TargetSource: source, SelectionReason: selectionReason, EffectiveFrom: from ?? Jan1,
            TargetDisplayName: null, TargetStatus: status,
            SourceReferenceType: sourceReferenceType, SourceReferenceId: sourceReferenceId,
            Priority: priority, PriorityLevel: priorityLevel, EffectiveTo: to, ExclusionReason: exclusionReason);

    private static CreateCampaignTargetSnapshotCommand SnapshotCmd(
        Guid campaignId,
        IReadOnlyList<CampaignSnapshotTargetItem>? items = null,
        string sourceType = CampaignTargetSources.Manual,
        bool applyConsentFilter = true,
        string? channel = ConsentChannel.Visit,
        string? purpose = ConsentPurpose.MedicalVisit,
        string? sourceReferenceType = null,
        Guid? sourceReferenceId = null,
        DateTimeOffset? effectiveAt = null)
        => new(campaignId, sourceType,
            items ?? new[] { new CampaignSnapshotTargetItem(CampaignTargetTypes.AccountContactLink, Contact1) },
            "snapshot for FU04 test", applyConsentFilter, sourceReferenceType, sourceReferenceId, channel, purpose,
            effectiveAt ?? Jun1);

    // ============ 1–10 · Campaign lifecycle ============

    /// <summary>Test 1 — a valid campaign persists with the claim tenant and normalized vocabulary, holding only references.</summary>
    [Fact]
    public async Task T01_Create_Campaign_Valid_Returns_201()
    {
        var f = new Fixture(TenantA);
        var r = await f.CreateCampaign().Handle(CampaignCmd(), default);

        Assert.Equal(201, r.StatusCode);
        var row = Assert.Single(f.Campaigns.Items);
        Assert.Equal(TenantA, row.TenantId);
        Assert.Equal("CMP-1", row.CampaignCode);
        Assert.Equal("product-campaign", row.CampaignType);
        Assert.Equal("active", row.CampaignStatus);
        // FU10 removed BrandId from the write path; the aggregate field survives as deprecated, so the campaign is
        // simply created without one. The lifecycle assertions this test exists for are untouched.
        Assert.Null(row.BrandId);
        Assert.False(row.IsArchived());
    }

    /// <summary>Test 2 — TenantId can never arrive from a payload, and a handler without a tenant claim refuses to write.</summary>
    [Fact]
    public async Task T02_TenantId_Is_Never_Accepted_From_Payload()
    {
        Type[] writeContracts =
        {
            typeof(CreateCampaignRequest), typeof(UpdateCampaignRequest),
            typeof(CreateCampaignTargetRequest), typeof(UpdateCampaignTargetRequest),
            typeof(CreateCampaignTargetSnapshotRequest),
            typeof(CreateCampaignCommand), typeof(UpdateCampaignCommand),
            typeof(CreateCampaignTargetCommand), typeof(UpdateCampaignTargetCommand),
            typeof(CreateCampaignTargetSnapshotCommand)
        };

        foreach (var contract in writeContracts)
        {
            Assert.DoesNotContain(
                contract.GetProperties().Select(p => p.Name),
                name => name.Equals("TenantId", StringComparison.OrdinalIgnoreCase));
        }

        var noTenantRepo = new FakeCampaignRepo();
        var noTenant = new CreateCampaignHandler(
            new TenantContext(), new NullActorContext(), noTenantRepo,
            new CampaignCycleBindingGuard(new FakeEmptyCyclePeriodReader()),
            new CampaignScopeWriteValidator(
                new CampaignScopeTestDoubles.FakeReferenceValidator(),
                new CampaignScopeTestDoubles.FakeLegalEntityValidator()),
            new CampaignSegmentValidator(new CampaignScopeTestDoubles.FakeSegmentCatalog()),
            new CampaignCodeGenerator(new CampaignScopeTestDoubles.FakeCampaignCodeSequence(), noTenantRepo));
        Assert.Equal(400, (await noTenant.Handle(CampaignCmd(), default)).StatusCode);
    }

    /// <summary>Test 3 — a duplicate active CampaignCode is a 409; an archived code becomes reusable.</summary>
    [Fact]
    public async Task T03_Duplicate_Active_CampaignCode_Returns_409()
    {
        var f = new Fixture(TenantA);
        var first = await f.SeedCampaignAsync("CMP-DUP");
        Assert.Equal(409, (await f.CreateCampaign().Handle(CampaignCmd("CMP-DUP"), default)).StatusCode);

        await f.ArchiveCampaign().Handle(new ArchiveCampaignCommand(first), default);
        Assert.Equal(201, (await f.CreateCampaign().Handle(CampaignCmd("CMP-DUP"), default)).StatusCode);
    }

    /// <summary>Test 4 — StartDate later than EndDate ⇒ 400.</summary>
    [Fact]
    public async Task T04_StartDate_After_EndDate_Returns_400()
    {
        var f = new Fixture(TenantA);
        Assert.Equal(400, (await f.CreateCampaign().Handle(CampaignCmd(start: Jun1, end: Jan1), default)).StatusCode);
        Assert.Empty(f.Campaigns.Items);
    }

    /// <summary>Test 5 — unknown type / status / objective / consent default ⇒ 400 (a typo is rejected, never stored).</summary>
    [Fact]
    public async Task T05_Unknown_Campaign_Vocabulary_Returns_400()
    {
        var f = new Fixture(TenantA);
        Assert.Equal(400, (await f.CreateCampaign().Handle(CampaignCmd(campaignType: "telepathy-campaign"), default)).StatusCode);
        Assert.Equal(400, (await f.CreateCampaign().Handle(CampaignCmd(status: "maybe"), default)).StatusCode);
        Assert.Equal(400, (await f.CreateCampaign().Handle(CampaignCmd(objectiveType: "vibes"), default)).StatusCode);
        Assert.Equal(400, (await f.CreateCampaign().Handle(CampaignCmd(defaultChannel: "carrier-pigeon"), default)).StatusCode);
        Assert.Equal(400, (await f.CreateCampaign().Handle(CampaignCmd(defaultPurpose: "gossip"), default)).StatusCode);
        Assert.Empty(f.Campaigns.Items);
    }

    /// <summary>Test 6 — archive is a soft lifecycle: the campaign stays readable with its stamp.</summary>
    [Fact]
    public async Task T06_Campaign_Archive_Is_Soft_Lifecycle()
    {
        var f = new Fixture(TenantA);
        var campaignId = await f.SeedCampaignAsync();

        Assert.Equal(200, (await f.ArchiveCampaign().Handle(new ArchiveCampaignCommand(campaignId), default)).StatusCode);
        var read = await f.GetCampaign().Handle(new GetCampaignQuery(campaignId), default);
        Assert.Equal(200, read.StatusCode);
        Assert.True(read.Data!.IsArchived);
        Assert.NotNull(read.Data!.ArchivedAt);
        Assert.Equal("archived", read.Data!.CampaignStatus);

        // idempotent
        Assert.Equal(200, (await f.ArchiveCampaign().Handle(new ArchiveCampaignCommand(campaignId), default)).StatusCode);
        Assert.Single(f.Campaigns.Items);
    }

    /// <summary>Test 7 — updating an archived campaign ⇒ 409.</summary>
    [Fact]
    public async Task T07_Archived_Campaign_Update_Returns_409()
    {
        var f = new Fixture(TenantA);
        var campaignId = await f.SeedCampaignAsync();
        await f.ArchiveCampaign().Handle(new ArchiveCampaignCommand(campaignId), default);

        var update = await f.UpdateCampaign().Handle(
            new UpdateCampaignCommand(campaignId, "renamed", CampaignTypes.Other, Jan1), default);
        Assert.Equal(409, update.StatusCode);
    }

    /// <summary>Test 8 — an archived campaign freezes target mutation: create, update and snapshot all 409, and its
    /// targets stay readable.</summary>
    [Fact]
    public async Task T08_Archived_Campaign_Blocks_Target_Mutation()
    {
        var f = new Fixture(TenantA);
        var campaignId = await f.SeedCampaignAsync();
        var targetId = (await f.CreateTarget().Handle(TargetCmd(campaignId), default)).Data;
        await f.ArchiveCampaign().Handle(new ArchiveCampaignCommand(campaignId), default);

        var create = await f.CreateTarget().Handle(TargetCmd(campaignId, targetId: Contact2), default);
        Assert.Equal(409, create.StatusCode);
        Assert.Contains(CampaignReasonCodes.CampaignArchivedNoTargetMutation, create.Errors![0]);

        var update = await f.UpdateTarget().Handle(
            new UpdateCampaignTargetCommand(campaignId, targetId, CampaignTargetSources.Manual, "x", Jan1), default);
        Assert.Equal(409, update.StatusCode);

        var snapshot = await f.Snapshot().Handle(SnapshotCmd(campaignId), default);
        Assert.Equal(409, snapshot.StatusCode);

        // still readable
        var list = await f.ListTargets().Handle(new ListCampaignTargetsQuery(campaignId), default);
        Assert.Equal(200, list.StatusCode);
        Assert.Single(list.Data!.Items);
    }

    /// <summary>Test 9 — DELETE is structurally unsupported for both aggregates: no action, no command, no repository
    /// method can delete, so a hard delete is impossible rather than merely unrouted.</summary>
    [Fact]
    public void T09_Delete_Is_Structurally_Unsupported()
    {
        var deleteActions = typeof(CampaignsController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttributes<HttpDeleteAttribute>().Any())
            .ToList();
        Assert.Empty(deleteActions);

        foreach (var repository in new[] { typeof(ICampaignRepository), typeof(ICampaignTargetRepository) })
        {
            Assert.DoesNotContain(
                repository.GetMethods().Select(m => m.Name),
                name => name.Contains("Delete", StringComparison.OrdinalIgnoreCase)
                        || name.Contains("Remove", StringComparison.OrdinalIgnoreCase));
        }

        Assert.DoesNotContain(
            typeof(CreateCampaignCommand).Assembly.GetTypes()
                .Where(t => t.Namespace == typeof(CreateCampaignCommand).Namespace)
                .Select(t => t.Name),
            name => name.Contains("Delete", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Test 10 — campaign list/read is tenant isolated.</summary>
    [Fact]
    public async Task T10_Campaign_List_Is_Tenant_Isolated()
    {
        var f = new Fixture(TenantA);
        var campaignId = await f.SeedCampaignAsync();

        Assert.Single((await f.ListCampaigns().Handle(new ListCampaignsQuery(), default)).Data!.Items);
        Assert.Empty((await f.ListCampaigns(TenantB).Handle(new ListCampaignsQuery(), default)).Data!.Items);
        Assert.Equal(404, (await f.GetCampaign(TenantB).Handle(new GetCampaignQuery(campaignId), default)).StatusCode);
    }

    // ============ 11–17 · Target lifecycle ============

    /// <summary>Test 11 — a valid manual target persists with its mandatory reason and lifecycle codes.</summary>
    [Fact]
    public async Task T11_Create_Manual_Target_Valid_Returns_201()
    {
        var f = new Fixture(TenantA);
        var campaignId = await f.SeedCampaignAsync();

        var r = await f.CreateTarget().Handle(TargetCmd(campaignId), default);
        Assert.Equal(201, r.StatusCode);

        var row = Assert.Single(f.Targets.Items);
        Assert.Equal(TenantA, row.TenantId);
        Assert.Equal(campaignId, row.CampaignId);
        Assert.Equal("account-contact-link", row.TargetType);
        Assert.Equal("manual", row.TargetSource);
        Assert.Equal("active", row.TargetStatus);
        Assert.False(string.IsNullOrWhiteSpace(row.SelectionReason));
        Assert.Contains(CampaignReasonCodes.CampaignTargetCreated, row.ReasonCodes);
        Assert.Contains(CampaignReasonCodes.ManualTargetSelected, row.ReasonCodes);
        Assert.Null(row.SnapshotBatchId);          // manual authoring is not part of a batch
        Assert.Null(row.ConsentEvaluation);        // provenance is only ever written from a live evaluation

        // A target with no stated reason is STILL not storable - MOD-0165 FU11 moved where that is enforced.
        // Before FU11 the caller was rejected; now the server states the reason itself, because the two facts a
        // justification needs (who selected it, and when) are things the server knows better than the author does.
        // The assertion therefore checks the invariant rather than the old mechanism: the row that comes back must
        // still say why it exists, and must admit that the sentence was generated rather than stated.
        var blankReason = await f.CreateTarget().Handle(
            TargetCmd(campaignId, targetId: Contact2, selectionReason: " "), default);
        Assert.Equal(201, blankReason.StatusCode);

        var generated = (await f.Targets.GetByIdAsync(TenantA, blankReason.Data, default))!;
        Assert.False(string.IsNullOrWhiteSpace(generated.SelectionReason));
        Assert.Contains(CampaignReasonCodes.CampaignTargetSelectionReasonGenerated, generated.ReasonCodes);
    }

    /// <summary>Test 12 — the MANUAL path is strict: a duplicate active (campaign, targetType, targetId) ⇒ 409. The
    /// snapshot path deliberately differs (idempotent reconcile — see T21).</summary>
    [Fact]
    public async Task T12_Manual_Duplicate_Target_Returns_409_While_Snapshot_Reconciles()
    {
        var f = new Fixture(TenantA);
        var campaignId = await f.SeedCampaignAsync();
        Assert.Equal(201, (await f.CreateTarget().Handle(TargetCmd(campaignId), default)).StatusCode);

        var duplicate = await f.CreateTarget().Handle(TargetCmd(campaignId), default);
        Assert.Equal(409, duplicate.StatusCode);
        Assert.Contains(CampaignReasonCodes.CampaignTargetDuplicate, duplicate.Errors![0]);
        Assert.Single(f.Targets.Items);

        // Same target through the snapshot with the SAME source reconciles instead of 409-ing.
        var snapshot = await f.Snapshot().Handle(SnapshotCmd(campaignId), default);
        Assert.Equal(201, snapshot.StatusCode);
        Assert.Equal(1, snapshot.Data!.ReconciledCount);
        Assert.Equal(0, snapshot.Data!.CreatedCount);
        Assert.Single(f.Targets.Items);
    }

    /// <summary>Test 13 — unknown TargetType ⇒ 400, and <c>campaign-target</c> is explicitly NOT a valid campaign target
    /// type (self-referential loop), while the separate frequency set still contains it.</summary>
    [Fact]
    public async Task T13_Unknown_TargetType_Returns_400_And_CampaignTarget_Is_Not_A_Member()
    {
        var f = new Fixture(TenantA);
        var campaignId = await f.SeedCampaignAsync();

        Assert.Equal(400, (await f.CreateTarget().Handle(TargetCmd(campaignId, targetType: "planet"), default)).StatusCode);
        Assert.Equal(400, (await f.CreateTarget().Handle(TargetCmd(campaignId, targetType: "campaign-target"), default)).StatusCode);
        Assert.Equal(400, (await f.CreateTarget().Handle(TargetCmd(campaignId, targetId: Guid.Empty), default)).StatusCode);

        // The two vocabularies are deliberately NOT unified (MOD-0048 reconciliation F6).
        Assert.DoesNotContain("campaign-target", CampaignTargetTypes.All);
        Assert.Contains("campaign-target", FrequencyTargetType.All);
        Assert.Equal(7, CampaignTargetTypes.All.Count);
    }

    /// <summary>Test 14 — unknown TargetSource / status ⇒ 400, and an excluded target requires a reason.</summary>
    [Fact]
    public async Task T14_Unknown_TargetSource_And_Missing_Exclusion_Reason_Return_400()
    {
        var f = new Fixture(TenantA);
        var campaignId = await f.SeedCampaignAsync();

        Assert.Equal(400, (await f.CreateTarget().Handle(TargetCmd(campaignId, source: "telepathy"), default)).StatusCode);
        Assert.Equal(400, (await f.CreateTarget().Handle(TargetCmd(campaignId, status: "maybe"), default)).StatusCode);
        Assert.Equal(400, (await f.CreateTarget().Handle(TargetCmd(campaignId, priority: 0), default)).StatusCode);
        Assert.Equal(400, (await f.CreateTarget().Handle(TargetCmd(campaignId, from: Jun1, to: Jan1), default)).StatusCode);

        // MOD-0165 FU11 - 'excluded' is no longer a status a human may set, WITH or WITHOUT a reason. It is the
        // outcome of a consent evaluation, which writes it together with the reason it is required to carry; an author
        // choosing it by hand was the only way to produce an excluded row whose reason nobody had evaluated.
        // The FU04 rule it replaces ("silently dropping a target is forbidden") is not weakened here - it is enforced
        // one step earlier, and the snapshot path still writes excluded exactly as before (see T22 and the FU11
        // snapshot tests).
        Assert.Equal(400, (await f.CreateTarget().Handle(
            TargetCmd(campaignId, status: CampaignTargetStatuses.Excluded), default)).StatusCode);
        Assert.Equal(400, (await f.CreateTarget().Handle(
            TargetCmd(campaignId, status: CampaignTargetStatuses.Excluded, exclusionReason: "operator decision"), default)).StatusCode);
    }

    /// <summary>Test 15 — target archive is a soft lifecycle and stays readable.</summary>
    [Fact]
    public async Task T15_Target_Archive_Is_Soft_Lifecycle()
    {
        var f = new Fixture(TenantA);
        var campaignId = await f.SeedCampaignAsync();
        var targetId = (await f.CreateTarget().Handle(TargetCmd(campaignId), default)).Data;

        Assert.Equal(200, (await f.ArchiveTarget().Handle(new ArchiveCampaignTargetCommand(campaignId, targetId), default)).StatusCode);
        var read = await f.GetTarget().Handle(new GetCampaignTargetQuery(campaignId, targetId), default);
        Assert.True(read.Data!.IsArchived);
        Assert.NotNull(read.Data!.ArchivedAt);
        Assert.Equal("archived", read.Data!.TargetStatus);
        Assert.Contains(CampaignReasonCodes.CampaignTargetArchived, read.Data!.ReasonCodes);

        var withoutArchived = await f.ListTargets().Handle(
            new ListCampaignTargetsQuery(campaignId, IncludeArchived: false), default);
        Assert.Empty(withoutArchived.Data!.Items);
        var withArchived = await f.ListTargets().Handle(new ListCampaignTargetsQuery(campaignId), default);
        Assert.Single(withArchived.Data!.Items);
    }

    /// <summary>Test 16 — updating an archived target ⇒ 409.</summary>
    [Fact]
    public async Task T16_Archived_Target_Update_Returns_409()
    {
        var f = new Fixture(TenantA);
        var campaignId = await f.SeedCampaignAsync();
        var targetId = (await f.CreateTarget().Handle(TargetCmd(campaignId), default)).Data;
        await f.ArchiveTarget().Handle(new ArchiveCampaignTargetCommand(campaignId, targetId), default);

        var update = await f.UpdateTarget().Handle(
            new UpdateCampaignTargetCommand(campaignId, targetId, CampaignTargetSources.Manual, "x", Jan1), default);
        Assert.Equal(409, update.StatusCode);
    }

    /// <summary>Test 17 — an archived target no longer blocks re-targeting the same subject, and consent provenance is
    /// not settable through the update contract (a caller can never hand-craft a verdict).</summary>
    [Fact]
    public async Task T17_Consent_Provenance_Is_Not_Caller_Settable()
    {
        foreach (var contract in new[]
                 {
                     typeof(UpdateCampaignTargetCommand), typeof(UpdateCampaignTargetRequest),
                     typeof(CreateCampaignTargetCommand), typeof(CreateCampaignTargetRequest)
                 })
        {
            Assert.DoesNotContain(
                contract.GetProperties().Select(p => p.Name),
                name => name.Contains("Consent", StringComparison.OrdinalIgnoreCase));
        }

        // Immutable identity on update: a different target is a different record.
        var updateNames = typeof(UpdateCampaignTargetRequest).GetProperties().Select(p => p.Name).ToList();
        Assert.DoesNotContain("TargetType", updateNames);
        Assert.DoesNotContain("TargetId", updateNames);
        Assert.DoesNotContain("CampaignCode", typeof(UpdateCampaignRequest).GetProperties().Select(p => p.Name));

        var f = new Fixture(TenantA);
        var campaignId = await f.SeedCampaignAsync();
        var targetId = (await f.CreateTarget().Handle(TargetCmd(campaignId), default)).Data;
        await f.ArchiveTarget().Handle(new ArchiveCampaignTargetCommand(campaignId, targetId), default);
        Assert.Equal(201, (await f.CreateTarget().Handle(TargetCmd(campaignId), default)).StatusCode);
    }

    // ============ 18–22 · Static snapshot ============

    /// <summary>Test 18 — an empty (or structurally invalid) snapshot is rejected 400 and writes nothing.</summary>
    [Fact]
    public async Task T18_Snapshot_Empty_Or_Invalid_Items_Return_400()
    {
        var f = new Fixture(TenantA);
        var campaignId = await f.SeedCampaignAsync();

        Assert.Equal(400, (await f.Snapshot().Handle(
            SnapshotCmd(campaignId, Array.Empty<CampaignSnapshotTargetItem>()), default)).StatusCode);

        // one bad row rejects the WHOLE request — no partial snapshot is ever persisted
        var mixed = new[]
        {
            new CampaignSnapshotTargetItem(CampaignTargetTypes.Contact, Contact1),
            new CampaignSnapshotTargetItem("planet", Contact2)
        };
        Assert.Equal(400, (await f.Snapshot().Handle(SnapshotCmd(campaignId, mixed), default)).StatusCode);

        // a duplicate row inside one payload is also a caller error
        var duplicated = new[]
        {
            new CampaignSnapshotTargetItem(CampaignTargetTypes.Contact, Contact1),
            new CampaignSnapshotTargetItem(CampaignTargetTypes.Contact, Contact1)
        };
        Assert.Equal(400, (await f.Snapshot().Handle(SnapshotCmd(campaignId, duplicated), default)).StatusCode);

        Assert.Empty(f.Targets.Items);
        Assert.Equal(0, f.Targets.WriteCount);
    }

    /// <summary>Test 19 — a snapshot stamps one SnapshotBatchId on every row it produces, and returns it.</summary>
    [Fact]
    public async Task T19_Snapshot_Creates_A_SnapshotBatchId()
    {
        var f = new Fixture(TenantA);
        var campaignId = await f.SeedCampaignAsync();
        var items = new[]
        {
            new CampaignSnapshotTargetItem(CampaignTargetTypes.Contact, Contact1),
            new CampaignSnapshotTargetItem(CampaignTargetTypes.AccountContactLink, Contact2)
        };

        var r = await f.Snapshot().Handle(SnapshotCmd(campaignId, items), default);
        Assert.Equal(201, r.StatusCode);

        var batchId = r.Data!.SnapshotBatchId;
        Assert.NotEqual(Guid.Empty, batchId);
        Assert.Equal(2, r.Data!.CreatedCount);
        Assert.Equal(2, r.Data!.RequestedCount);
        Assert.Equal(2, f.Targets.Items.Count);
        Assert.All(f.Targets.Items, t => Assert.Equal(batchId, t.SnapshotBatchId));
        Assert.Contains(CampaignReasonCodes.CampaignTargetSnapshotCreated, r.Data!.ReasonCodes);

        // the batch is queryable as a unit
        var byBatch = await f.ListTargets().Handle(
            new ListCampaignTargetsQuery(campaignId, SnapshotBatchId: batchId), default);
        Assert.Equal(2, byBatch.Data!.Total);
    }

    /// <summary>Test 20 — a snapshot is ADDITIVE: it never deletes or archives an earlier target, including one from a
    /// previous batch that is absent from the new item list.</summary>
    [Fact]
    public async Task T20_Snapshot_Does_Not_Delete_Previous_Targets()
    {
        var f = new Fixture(TenantA);
        var campaignId = await f.SeedCampaignAsync();
        // The fake evaluator defaults to the fail-closed 'unknown' verdict, so consent-agnostic tests state the
        // allowed path explicitly rather than relying on a fail-open default.
        f.Consent.Result = Allowed();

        var first = await f.Snapshot().Handle(SnapshotCmd(
            campaignId, new[] { new CampaignSnapshotTargetItem(CampaignTargetTypes.Contact, Contact1) }), default);
        var firstBatch = first.Data!.SnapshotBatchId;

        // Second snapshot mentions a DIFFERENT target only.
        var second = await f.Snapshot().Handle(SnapshotCmd(
            campaignId, new[] { new CampaignSnapshotTargetItem(CampaignTargetTypes.Contact, Contact2) }), default);
        Assert.Equal(201, second.StatusCode);
        Assert.NotEqual(firstBatch, second.Data!.SnapshotBatchId);

        Assert.Equal(2, f.Targets.Items.Count);
        var survivor = f.Targets.Items.Single(t => t.TargetId == Contact1);
        Assert.False(survivor.IsArchived());
        Assert.Equal("active", survivor.TargetStatus);
        Assert.Equal(firstBatch, survivor.SnapshotBatchId); // untouched by the second batch
        Assert.Equal(0, f.Targets.DeleteAttempts);
    }

    /// <summary>Test 21 — re-running the same snapshot reconciles instead of duplicating; a target owned by a DIFFERENT
    /// source aborts the whole batch with 409 before any write.</summary>
    [Fact]
    public async Task T21_Snapshot_Rerun_Does_Not_Duplicate_And_Source_Conflict_Aborts()
    {
        var f = new Fixture(TenantA);
        var campaignId = await f.SeedCampaignAsync();
        var items = new[] { new CampaignSnapshotTargetItem(CampaignTargetTypes.Contact, Contact1) };

        var first = await f.Snapshot().Handle(SnapshotCmd(campaignId, items), default);
        Assert.Equal(1, first.Data!.CreatedCount);

        var second = await f.Snapshot().Handle(SnapshotCmd(campaignId, items), default);
        Assert.Equal(201, second.StatusCode);
        Assert.Equal(0, second.Data!.CreatedCount);
        Assert.Equal(1, second.Data!.ReconciledCount);
        Assert.Single(f.Targets.Items);
        Assert.Contains(CampaignReasonCodes.CampaignTargetSnapshotReconciled, second.Data!.ReasonCodes);
        Assert.Equal(second.Data!.SnapshotBatchId, f.Targets.Items[0].SnapshotBatchId);

        // A different source claiming the same target aborts the batch — nothing written.
        var writesBefore = f.Targets.WriteCount;
        var conflicting = await f.Snapshot().Handle(
            SnapshotCmd(campaignId, new[]
            {
                new CampaignSnapshotTargetItem(CampaignTargetTypes.Contact, Contact1),
                new CampaignSnapshotTargetItem(CampaignTargetTypes.Contact, Contact2)
            }, sourceType: CampaignTargetSources.Segment, sourceReferenceType: CampaignTargetTypes.Segment,
                sourceReferenceId: SegmentId),
            default);

        Assert.Equal(409, conflicting.StatusCode);
        Assert.Contains(conflicting.Errors!, e => e.Contains(CampaignReasonCodes.CampaignTargetSourceConflict));
        Assert.Equal(writesBefore, f.Targets.WriteCount); // all-or-nothing: Contact2 was NOT created either
        Assert.Single(f.Targets.Items);
    }

    /// <summary>Test 22 — a segment-sourced snapshot stores the segment id as provenance and resolves NO membership: the
    /// items are taken exactly as supplied.</summary>
    [Fact]
    public async Task T22_Segment_Snapshot_Stores_Provenance_Without_Resolving_Membership()
    {
        var f = new Fixture(TenantA);
        var campaignId = await f.SeedCampaignAsync();
        var items = new[]
        {
            new CampaignSnapshotTargetItem(CampaignTargetTypes.Contact, Contact1),
            new CampaignSnapshotTargetItem(CampaignTargetTypes.Contact, Contact2)
        };

        var r = await f.Snapshot().Handle(SnapshotCmd(
            campaignId, items, sourceType: CampaignTargetSources.Segment,
            sourceReferenceType: CampaignTargetTypes.Segment, sourceReferenceId: SegmentId), default);

        Assert.Equal(201, r.StatusCode);
        Assert.Equal(2, r.Data!.CreatedCount);        // exactly what was supplied — nothing expanded
        Assert.Equal(SegmentId, r.Data!.SourceReferenceId);
        Assert.Contains(CampaignReasonCodes.SegmentSourceSnapshot, r.Data!.ReasonCodes);
        Assert.Contains(CampaignReasonCodes.TargetSourceProvenanceStored, r.Data!.ReasonCodes);

        Assert.All(f.Targets.Items, t =>
        {
            Assert.Equal("segment", t.TargetSource);
            Assert.Equal("segment", t.SourceReferenceType);
            Assert.Equal(SegmentId, t.SourceReferenceId);
            Assert.Contains(CampaignReasonCodes.SegmentSourceSnapshot, t.ReasonCodes);
        });

        // No target was created FOR the segment itself, and no member lookup happened.
        Assert.DoesNotContain(f.Targets.Items, t => t.TargetId == SegmentId);
    }

    // ============ 23–30 · Consent integration ============

    /// <summary>Test 23 — consent allowed ⇒ target active, with provenance stored.</summary>
    [Fact]
    public async Task T23_Consent_Allowed_Target_Is_Active()
    {
        var f = new Fixture(TenantA);
        var campaignId = await f.SeedCampaignAsync();
        f.Consent.Result = Allowed();

        var r = await f.Snapshot().Handle(SnapshotCmd(campaignId), default);
        Assert.Equal(201, r.StatusCode);
        Assert.Equal(1, r.Data!.ActiveCount);
        Assert.Equal(0, r.Data!.ExcludedCount);

        var row = Assert.Single(f.Targets.Items);
        Assert.Equal("active", row.TargetStatus);
        Assert.Null(row.ExclusionReason);
        Assert.Contains(CampaignReasonCodes.ConsentAllowed, row.ReasonCodes);
        Assert.Contains(CampaignReasonCodes.ConsentProvenanceStored, row.ReasonCodes);
        Assert.NotNull(row.ConsentEvaluation);
        Assert.Equal(ConsentEligibilityStatus.Allowed, row.ConsentEvaluation!.EligibilityStatus);
        Assert.True(row.ConsentEvaluation!.FilterApplied);

        // The evaluator was asked the right question, scoped to this campaign.
        var asked = Assert.Single(f.Consent.Requests);
        Assert.Equal(ConsentSubjectType.AccountContactLink, asked.SubjectType);
        Assert.Equal(Contact1, asked.SubjectId);
        Assert.Equal(ConsentChannel.Visit, asked.Channel);
        Assert.Equal(ConsentPurpose.MedicalVisit, asked.Purpose);
        Assert.Equal(ConsentScopeType.Campaign, asked.ScopeType);
        Assert.Equal(campaignId, asked.ScopeId);
    }

    /// <summary>Test 24 — consent blocked ⇒ target created but EXCLUDED with a reason (kept, not dropped, so the
    /// exclusion is auditable).</summary>
    [Fact]
    public async Task T24_Consent_Blocked_Target_Is_Excluded_With_Reason()
    {
        var f = new Fixture(TenantA);
        var campaignId = await f.SeedCampaignAsync();
        f.Consent.Result = Blocked();

        var r = await f.Snapshot().Handle(SnapshotCmd(campaignId), default);
        Assert.Equal(201, r.StatusCode);
        Assert.Equal(0, r.Data!.ActiveCount);
        Assert.Equal(1, r.Data!.ExcludedCount);

        var row = Assert.Single(f.Targets.Items);
        Assert.Equal("excluded", row.TargetStatus);
        Assert.Equal(CampaignReasonCodes.ConsentBlocked, row.ExclusionReason);
        Assert.Contains(CampaignReasonCodes.ConsentBlocked, row.ReasonCodes);
        Assert.Contains(CampaignReasonCodes.CampaignTargetExcluded, row.ReasonCodes);
        Assert.Equal(ConsentEligibilityStatus.Blocked, row.ConsentEvaluation!.EligibilityStatus);
        Assert.Contains(CampaignReasonCodes.CampaignTargetExcluded, r.Data!.ReasonCodes);
    }

    /// <summary>Test 25 — consent unknown ⇒ target EXCLUDED with a reason. Unknown is never allowed, and an evaluator
    /// error degrades the same way (never to allowed).</summary>
    [Fact]
    public async Task T25_Consent_Unknown_Target_Is_Excluded_And_Never_Allowed()
    {
        var f = new Fixture(TenantA);
        var campaignId = await f.SeedCampaignAsync();
        f.Consent.Result = Unknown();

        var r = await f.Snapshot().Handle(SnapshotCmd(campaignId), default);
        var row = Assert.Single(f.Targets.Items);
        Assert.Equal("excluded", row.TargetStatus);
        Assert.NotEqual("active", row.TargetStatus);
        Assert.Equal(CampaignReasonCodes.ConsentUnknown, row.ExclusionReason);
        Assert.Contains(CampaignReasonCodes.ConsentUnknown, row.ReasonCodes);
        Assert.Equal(1, r.Data!.ExcludedCount);

        // A controlled evaluator error is treated as unknown, never as allowed.
        var errorFixture = new Fixture(TenantA);
        var errorCampaign = await errorFixture.SeedCampaignAsync();
        errorFixture.Consent.Result = Unknown(withError: true);
        await errorFixture.Snapshot().Handle(SnapshotCmd(errorCampaign), default);
        var errorRow = Assert.Single(errorFixture.Targets.Items);
        Assert.Equal("excluded", errorRow.TargetStatus);
        Assert.Contains(CampaignReasonCodes.ConsentEvaluationError, errorRow.ReasonCodes);
    }

    /// <summary>Test 26 — the selected behaviour for a missing consent context is REJECTION (400
    /// <c>campaign_consent_context_required</c>); an explicit opt-out still produces targets but every row carries
    /// <c>consent_filter_not_applied</c>. Campaign defaults satisfy the requirement.</summary>
    [Fact]
    public async Task T26_Missing_Consent_Context_Is_Rejected_And_OptOut_Is_Visible()
    {
        var f = new Fixture(TenantA);
        var campaignId = await f.SeedCampaignAsync();

        // filter on, no channel/purpose anywhere ⇒ 400, nothing written, and the evaluator was never called
        var rejected = await f.Snapshot().Handle(SnapshotCmd(campaignId, channel: null, purpose: null), default);
        Assert.Equal(400, rejected.StatusCode);
        Assert.Contains(CreateCampaignTargetSnapshotHandler.ConsentContextRequiredCode, rejected.Errors![0]);
        Assert.Empty(f.Targets.Items);
        Assert.Empty(f.Consent.Requests);

        // an invalid channel is rejected too (a typo must not become an unusable question)
        Assert.Equal(400, (await f.Snapshot().Handle(SnapshotCmd(campaignId, channel: "carrier-pigeon"), default)).StatusCode);

        // explicit opt-out ⇒ targets produced, visibly unfiltered, evaluator still never called
        var optOut = await f.Snapshot().Handle(
            SnapshotCmd(campaignId, applyConsentFilter: false, channel: null, purpose: null), default);
        Assert.Equal(201, optOut.StatusCode);
        Assert.False(optOut.Data!.ConsentFilterApplied);
        Assert.Contains(CampaignReasonCodes.ConsentFilterNotApplied, optOut.Data!.ReasonCodes);
        var row = Assert.Single(f.Targets.Items);
        Assert.Contains(CampaignReasonCodes.ConsentFilterNotApplied, row.ReasonCodes);
        Assert.False(row.ConsentEvaluation!.FilterApplied);
        Assert.Empty(f.Consent.Requests);

        // campaign defaults satisfy the requirement without the caller repeating them
        var withDefaults = new Fixture(TenantA);
        var defaulted = await withDefaults.SeedCampaignAsync(
            "CMP-DEF", ConsentChannel.Email, ConsentPurpose.Marketing);
        withDefaults.Consent.Result = Allowed();
        var fromDefaults = await withDefaults.Snapshot().Handle(
            SnapshotCmd(defaulted, channel: null, purpose: null), default);
        Assert.Equal(201, fromDefaults.StatusCode);
        Assert.Equal(ConsentChannel.Email, fromDefaults.Data!.ConsentChannel);
        Assert.Equal(ConsentPurpose.Marketing, fromDefaults.Data!.ConsentPurpose);
    }

    /// <summary>Test 27 — the stored provenance carries every required member.</summary>
    [Fact]
    public async Task T27_Consent_Provenance_Contains_Every_Required_Member()
    {
        var f = new Fixture(TenantA);
        var campaignId = await f.SeedCampaignAsync();
        f.Consent.Result = Allowed();

        await f.Snapshot().Handle(SnapshotCmd(campaignId), default);
        var evaluation = Assert.Single(f.Targets.Items).ConsentEvaluation!;

        Assert.Equal(ConsentDecision.ConsentGranted, evaluation.Decision);
        Assert.Equal(ConsentEligibilityStatus.Allowed, evaluation.EligibilityStatus);
        Assert.NotEmpty(evaluation.ReasonCodes);
        Assert.NotEqual(default, evaluation.EvaluatedAt);
        Assert.NotNull(evaluation.MatchedConsentId);
        Assert.NotEmpty(evaluation.MatchedPreferenceIds);
        Assert.Equal(ConsentEvaluationResult.CurrentEvaluatorVersion, evaluation.EvaluatorVersion);
        Assert.False(string.IsNullOrWhiteSpace(evaluation.SelectionReason));
        Assert.Equal(ConsentChannel.Visit, evaluation.Channel);
        Assert.Equal(ConsentPurpose.MedicalVisit, evaluation.Purpose);
        Assert.True(evaluation.FilterApplied);
    }

    /// <summary>Test 28 — consent DATA is not copied: the provenance type has no consent/preference status or payload
    /// member, on the entity and on the DTO alike.</summary>
    [Fact]
    public void T28_Consent_Data_Is_Not_Copied()
    {
        string[] forbidden =
        {
            "ConsentStatus", "PreferenceStatus", "ConsentRecord", "PreferenceRecord",
            "ConsentRecordPayload", "PreferenceRecordPayload", "LegalBasis", "WithdrawalReason", "EvidenceRef",
            "PreferenceValue", "PreferenceType"
        };

        foreach (var type in new[] { typeof(CampaignTargetConsentEvaluation), typeof(CampaignTargetConsentEvaluationDto) })
        {
            var names = type.GetProperties().Select(p => p.Name).ToList();
            foreach (var member in forbidden)
            {
                Assert.DoesNotContain(member, names);
            }
        }

        // The target itself carries no consent field beyond the single provenance object.
        var targetConsentMembers = typeof(CampaignTarget).GetProperties()
            .Where(p => p.Name.Contains("Consent", StringComparison.OrdinalIgnoreCase))
            .Select(p => p.Name)
            .ToList();
        Assert.Equal(new[] { nameof(CampaignTarget.ConsentEvaluation) }, targetConsentMembers);
    }

    /// <summary>Test 29 — FU04 never mutates the MOD-0164 aggregates, and its evaluate call is read-only.</summary>
    [Fact]
    public async Task T29_Consent_And_Preference_Aggregates_Are_Never_Mutated()
    {
        var consents = new ThrowingConsentRecordRepo();
        var preferences = new ThrowingPreferenceRecordRepo();

        var f = new Fixture(TenantA);
        var campaignId = await f.SeedCampaignAsync();
        f.Consent.Result = Allowed();
        await f.Snapshot().Handle(SnapshotCmd(campaignId), default);

        // The snapshot never touched a consent/preference repository (these would have thrown if injected+used), and the
        // provider itself was only ever asked to evaluate.
        Assert.Equal(0, consents.CallCount);
        Assert.Equal(0, preferences.CallCount);
        Assert.Single(f.Consent.Requests);
        Assert.Equal(1, f.Consent.EvaluateCallCount);

        // The evaluator seam exposes exactly one member, and it is a read (no write/create/update method exists).
        var seamMethods = typeof(IConsentPreferenceEvaluator).GetMethods().Select(m => m.Name).ToList();
        Assert.Equal(new[] { nameof(IConsentPreferenceEvaluator.EvaluateAsync) }, seamMethods);
    }

    /// <summary>Test 30 — the snapshot handler depends on the PROVIDER SEAM, not on a consent repository: consent logic
    /// is not reimplemented here.</summary>
    [Fact]
    public void T30_Snapshot_Uses_Provider_Seam_Not_Consent_Repository()
    {
        var constructor = Assert.Single(typeof(CreateCampaignTargetSnapshotHandler).GetConstructors());
        var parameterTypes = constructor.GetParameters().Select(p => p.ParameterType).ToList();

        Assert.Contains(typeof(IConsentPreferenceEvaluator), parameterTypes);
        Assert.DoesNotContain(typeof(IConsentRecordRepository), parameterTypes);
        Assert.DoesNotContain(typeof(IPreferenceRecordRepository), parameterTypes);

        // No FU04 type takes a consent/preference repository at all.
        var campaignTypes = typeof(CreateCampaignTargetSnapshotHandler).Assembly.GetTypes()
            .Where(t => t.Namespace is { } ns && ns.StartsWith("Diten.CrmService.Application.Features.Campaign",
                StringComparison.Ordinal));
        foreach (var type in campaignTypes)
        {
            foreach (var ctor in type.GetConstructors())
            {
                Assert.DoesNotContain(typeof(IConsentRecordRepository), ctor.GetParameters().Select(p => p.ParameterType));
                Assert.DoesNotContain(typeof(IPreferenceRecordRepository), ctor.GetParameters().Select(p => p.ParameterType));
            }
        }
    }

    // ============ 31–35 · Boundaries · contract · response shape ============

    /// <summary>Test 31 — no visit/route/due/last-visit/frequency field appears anywhere in the FU04 response surface.</summary>
    [Fact]
    public void T31_Response_Shape_Carries_No_Visit_Route_Frequency_Fields()
    {
        string[] forbiddenFragments =
        {
            "visitplan", "routeplan", "routeid", "duestatus", "overdue", "lastvisit", "requiredvisitcount",
            "periodtype", "frequencypolicy", "segmentmembership", "recommendation", "nextbestaction",
            "workflowapproval", "contentrenderurl", "consentrecordpayload", "preferencerecordpayload"
        };

        Type[] responseTypes =
        {
            typeof(CampaignDto), typeof(CampaignTargetDto), typeof(CampaignTargetConsentEvaluationDto),
            typeof(CampaignExternalReferenceDto), typeof(CampaignTargetSnapshotResultDto),
            typeof(CampaignSnapshotRowResultDto), typeof(CampaignListDto), typeof(CampaignTargetListDto)
        };

        foreach (var type in responseTypes)
        {
            foreach (var property in type.GetProperties())
            {
                var name = property.Name.ToLowerInvariant();
                Assert.DoesNotContain(forbiddenFragments, fragment => name.Contains(fragment));
            }
        }
    }

    /// <summary>Test 32/33 — FU04 writes only its own two aggregates: no knowledge, brand/product, frequency, contact,
    /// availability or territory repository is reachable from the feature.</summary>
    [Fact]
    public void T32_T33_No_Foreign_Aggregate_Is_Writable_From_Campaign()
    {
        Type[] forbiddenRepositories =
        {
            typeof(IConsentRecordRepository), typeof(IPreferenceRecordRepository),
            typeof(IVisitFrequencyPolicyRepository), typeof(IContactRepository), typeof(IAccountRepository),
            typeof(IAccountContactLinkRepository), typeof(IContactAvailabilityRepository),
            typeof(ITerritoryModelRepository), typeof(ITerritoryNodeRepository)
        };

        var campaignTypes = typeof(CreateCampaignTargetSnapshotHandler).Assembly.GetTypes()
            .Where(t => t.Namespace is { } ns && ns.StartsWith("Diten.CrmService.Application.Features.Campaign",
                StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(campaignTypes);
        foreach (var type in campaignTypes)
        {
            foreach (var ctor in type.GetConstructors())
            {
                var parameterTypes = ctor.GetParameters().Select(p => p.ParameterType).ToList();
                foreach (var forbidden in forbiddenRepositories)
                {
                    Assert.DoesNotContain(forbidden, parameterTypes);
                }
            }
        }
    }

    /// <summary>Test 34 — the six FU04 contract flags are present and true, with the vocabulary and consent-integration
    /// contract surfaced.</summary>
    [Fact]
    public async Task T34_Contract_Flags_Are_True()
    {
        var handler = new GetCampaignContractHandler(Tenant(TenantA));
        var response = await handler.Handle(new GetCampaignContractQuery(), default);
        var dto = response.Data!;

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("MOD-0165", dto.ModuleId);
        Assert.Equal(TenantA, dto.TenantId);
        Assert.True(dto.Features.SupportsCampaignManagement);
        Assert.True(dto.Features.SupportsCampaignTargetManagement);
        Assert.True(dto.Features.SupportsStaticTargetSnapshot);
        Assert.True(dto.Features.SupportsConsentEvaluationIntegration);
        Assert.True(dto.Features.SupportsTargetExclusionReason);
        Assert.True(dto.Features.SupportsTargetSourceProvenance);

        Assert.Equal(CampaignTargetTypes.All, dto.Vocabulary.TargetTypes);
        Assert.DoesNotContain("campaign-target", dto.Vocabulary.TargetTypes);
        Assert.Equal("MOD-0164", dto.ConsentIntegration.ProviderModule);
        Assert.Equal(nameof(IConsentPreferenceEvaluator), dto.ConsentIntegration.ProviderSeam);
        Assert.Equal(ConsentScopeType.Campaign, dto.ConsentIntegration.ScopeType);
        Assert.Contains(CreateCampaignTargetSnapshotHandler.ConsentContextRequiredCode,
            dto.ConsentIntegration.MissingContextBehavior);
        Assert.Contains(CampaignReasonCodes.ConsentAllowed, dto.ReasonCodes);
        Assert.Contains(CampaignReasonCodes.ConsentFilterNotApplied, dto.ReasonCodes);
        Assert.NotEmpty(dto.Limitations);
        Assert.Equal(CampaignPermissions.All, dto.Permissions);
    }

    /// <summary>Test 35 — the ten forbidden capability flags are ABSENT from the contract (not even emitted as false).</summary>
    [Fact]
    public void T35_Forbidden_Contract_Flags_Are_Absent()
    {
        string[] forbidden =
        {
            "SupportsSegmentationEngine", "SupportsDynamicCampaignRules", "SupportsVisitPlanning",
            "SupportsRoutePlanning", "SupportsDueOverdue", "SupportsLastVisitHistory", "SupportsFrequencyRuntime",
            "SupportsDigitalDetailing", "SupportsRecommendationEngine", "SupportsWorkflowApproval"
        };

        var flagNames = typeof(CampaignFeatureFlags)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .ToList();

        foreach (var flag in forbidden)
        {
            Assert.DoesNotContain(flag, flagNames);
        }

        // Exactly the shipped flags and nothing else — a capability is never advertised, not even as false, unless it
        // is genuinely open. FU08 added SupportsCyclePeriodBinding because FU08 genuinely opens that capability; the
        // set is asserted by NAME rather than by count, so the next addition has to be declared here deliberately.
        Assert.Equal(
            new[]
            {
                "SupportsCampaignManagement", "SupportsCampaignTargetManagement", "SupportsStaticTargetSnapshot",
                "SupportsConsentEvaluationIntegration", "SupportsTargetExclusionReason",
                "SupportsTargetSourceProvenance", "SupportsCyclePeriodBinding",
                // FU09 - added deliberately: the campaign scope model genuinely opens scope-aware binding.
                "SupportsScopeAwareCycleBinding",
                // FU10 - segment targeting genuinely opens too.
                "SupportsSegmentTargeting"
            }.OrderBy(x => x, StringComparer.Ordinal),
            flagNames.OrderBy(x => x, StringComparer.Ordinal));
    }

    // ============ 36–40 · Authorization · isolation · build ============

    /// <summary>Test 36/37 — the controller is [Authorize]-gated and every action carries a permission guard, so an
    /// unauthenticated or garbage-token Gateway call can only ever be 401.</summary>
    [Fact]
    public void T36_T37_Endpoints_Require_Authentication_And_Permission()
    {
        Assert.NotEmpty(typeof(CampaignsController).GetCustomAttributes<AuthorizeAttribute>());
        Assert.Empty(typeof(CampaignsController).GetCustomAttributes<AllowAnonymousAttribute>());

        var actions = typeof(CampaignsController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttributes<HttpMethodAttribute>().Any())
            .ToList();

        // contract + campaign list/get/create/update/archive + target list/get/create/update/archive + snapshot,
        // plus the two FU09 READ endpoints (scope-options, applicable-cycle-periods) and the FU11 non-committing
        // code peek (next-code). Asserted by NAME rather than by count: what matters is that every action is
        // permission-guarded and that a new endpoint has to be declared here deliberately, not that the number
        // happens to match.
        Assert.Equal(
            new[]
            {
                "ApplicableCyclePeriods", "Archive", "ArchiveTarget", "Create", "CreateTarget",
                "CreateTargetSnapshot", "Get", "GetContract", "GetTarget", "List", "ListTargets",
                "PeekNextCode", "ScopeOptions",
                "Update", "UpdateTarget"
            }.OrderBy(x => x, StringComparer.Ordinal),
            actions.Select(a => a.Name).OrderBy(x => x, StringComparer.Ordinal));
        foreach (var action in actions)
        {
            Assert.Empty(action.GetCustomAttributes<AllowAnonymousAttribute>());
            Assert.Contains(
                action.GetCustomAttributes(),
                a => a.GetType().Name.Contains("HasPermission", StringComparison.Ordinal));
        }
    }

    /// <summary>Test 38 — tenant isolation on target read, list, update, archive and snapshot.</summary>
    [Fact]
    public async Task T38_Target_Tenant_Isolation_Is_Enforced()
    {
        var f = new Fixture(TenantA);
        var campaignId = await f.SeedCampaignAsync();
        var targetId = (await f.CreateTarget().Handle(TargetCmd(campaignId), default)).Data;

        var otherRead = new GetCampaignTargetHandler(Tenant(TenantB), f.Targets);
        Assert.Equal(404, (await otherRead.Handle(new GetCampaignTargetQuery(campaignId, targetId), default)).StatusCode);

        var otherList = new ListCampaignTargetsHandler(Tenant(TenantB), f.Campaigns, f.Targets);
        Assert.Equal(404, (await otherList.Handle(new ListCampaignTargetsQuery(campaignId), default)).StatusCode);

        var otherUpdate = new UpdateCampaignTargetHandler(
            Tenant(TenantB), new NullActorContext(), f.Campaigns, f.Targets);
        Assert.Equal(404, (await otherUpdate.Handle(
            new UpdateCampaignTargetCommand(campaignId, targetId, CampaignTargetSources.Manual, "x", Jan1), default)).StatusCode);

        var otherArchive = new ArchiveCampaignTargetHandler(Tenant(TenantB), new NullActorContext(), f.Targets);
        Assert.Equal(404, (await otherArchive.Handle(
            new ArchiveCampaignTargetCommand(campaignId, targetId), default)).StatusCode);

        var otherSnapshot = new CreateCampaignTargetSnapshotHandler(
            Tenant(TenantB), new NullActorContext(), f.Campaigns, f.Targets, f.Consent);
        Assert.Equal(404, (await otherSnapshot.Handle(SnapshotCmd(campaignId), default)).StatusCode);
    }

    /// <summary>Test 39 — a group-shaped target is reported <c>consent_evaluation_not_applicable</c> instead of being
    /// silently treated as evaluated, and the evaluator is not called for it.</summary>
    [Fact]
    public async Task T39_Group_Target_Reports_Consent_Not_Applicable()
    {
        var f = new Fixture(TenantA);
        var campaignId = await f.SeedCampaignAsync();
        f.Consent.Result = Allowed();

        var r = await f.Snapshot().Handle(SnapshotCmd(campaignId, new[]
        {
            new CampaignSnapshotTargetItem(CampaignTargetTypes.Segment, SegmentId),
            new CampaignSnapshotTargetItem(CampaignTargetTypes.AccountContactLink, Contact1)
        }), default);

        Assert.Equal(201, r.StatusCode);
        Assert.Single(f.Consent.Requests); // only the person-shaped target was evaluated

        var segmentRow = f.Targets.Items.Single(t => t.TargetType == CampaignTargetTypes.Segment);
        Assert.Equal("active", segmentRow.TargetStatus);
        Assert.Contains(CampaignReasonCodes.ConsentEvaluationNotApplicable, segmentRow.ReasonCodes);
        Assert.Equal(ConsentEligibilityStatus.NotApplicable, segmentRow.ConsentEvaluation!.EligibilityStatus);
        Assert.True(segmentRow.ConsentEvaluation!.FilterApplied);

        Assert.False(CampaignTargetTypes.SupportsConsentEvaluation(CampaignTargetTypes.Segment));
        Assert.True(CampaignTargetTypes.SupportsConsentEvaluation(CampaignTargetTypes.Contact));
    }

    // MOD-0165 FU10 removed test 40 ("external references are stored with the full contract, and duplicate mappings
    // are reported"). External references are no longer authored on a campaign: the command, the request body and the
    // DTO no longer carry them, so there is no write path left for that test to exercise. It is deleted rather than
    // weakened, because a test that still passed while asserting nothing would be worse than its absence.
    //
    // The aggregate field and the duplicate-mapping guard are BOTH still there (Campaign.ExternalReferences is marked
    // deprecated, not dropped), so if an integration surface returns the behaviour and this test come back together.


    // ---------------- Consent result builders ----------------

    private static ConsentEvaluationResult Allowed() => Result(
        ConsentEligibilityStatus.Allowed, ConsentDecision.ConsentGranted,
        new[] { ConsentReasonCodes.ConsentGranted });

    private static ConsentEvaluationResult Blocked() => Result(
        ConsentEligibilityStatus.Blocked, ConsentDecision.PreferenceRestricted,
        new[] { ConsentReasonCodes.PreferenceDoNotVisit, ConsentReasonCodes.PreferenceRestricted });

    private static ConsentEvaluationResult Unknown(bool withError = false) => Result(
        ConsentEligibilityStatus.Unknown, ConsentDecision.ConsentUnknown,
        withError
            ? new[] { ConsentReasonCodes.ConsentUnknown, ConsentReasonCodes.ConsentEvaluationError }
            : new[] { ConsentReasonCodes.ConsentUnknown, ConsentReasonCodes.NoMatchingConsent });

    private static ConsentEvaluationResult Result(
        string eligibility, string decision, IReadOnlyList<string> reasonCodes)
        => new(
            eligibility,
            decision,
            ConsentSubjectType.AccountContactLink,
            Contact1,
            ConsentChannel.Visit,
            ConsentPurpose.MedicalVisit,
            ConsentScopeType.Campaign,
            Guid.NewGuid(),
            Jun1,
            MatchedConsentId: Guid.NewGuid(),
            MatchedPreferenceIds: new[] { Guid.NewGuid() },
            ReasonCodes: reasonCodes,
            SelectionReason: "fake evaluator verdict",
            CandidateConsents: Array.Empty<CandidateConsent>(),
            CandidatePreferences: Array.Empty<CandidatePreference>(),
            ConsentEvaluationResult.CurrentEvaluatorVersion,
            Jun1);

    // ---------------- Fakes ----------------

    /// <summary>Stands in for MOD-0164. It records every question asked, so the tests can prove FU04 asks the right
    /// question and asks it exactly once per evaluable target — and never for a group target.</summary>
    private sealed class FakeConsentEvaluator : IConsentPreferenceEvaluator
    {
        public List<ConsentEvaluationRequest> Requests { get; } = new();
        public int EvaluateCallCount { get; private set; }
        public ConsentEvaluationResult Result { get; set; } = Unknown();

        public Task<ConsentEvaluationResult> EvaluateAsync(
            ConsentEvaluationRequest request, CancellationToken cancellationToken)
        {
            EvaluateCallCount++;
            Requests.Add(request);
            return Task.FromResult(Result);
        }
    }

    /// <summary>
    /// A read seam that knows no periods. FU04 never binds one, so every method answers "nothing" — and the fact that
    /// it has no write member at all is itself part of the FU08 boundary.
    /// </summary>
    internal sealed class FakeEmptyCyclePeriodReader : ICyclePeriodReader
    {
        public Task<CyclePeriodResolution> ResolveActiveAsync(
            DateTimeOffset at, string? country, Guid? legalEntityId, string? businessUnitId, CancellationToken ct)
            => Task.FromResult(new CyclePeriodResolution(
                CyclePeriodResolutionOutcomes.None, null, Array.Empty<Guid>(), null, null));

        public Task<CyclePeriodSnapshot?> GetByIdAsync(Guid cyclePeriodId, CancellationToken ct)
            => Task.FromResult<CyclePeriodSnapshot?>(null);

        public Task<IReadOnlyList<CyclePeriodSnapshot>> ListByYearAsync(
            int year, string? scopeType, string? scopeRef, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<CyclePeriodSnapshot>>(Array.Empty<CyclePeriodSnapshot>());

        public Task<IReadOnlyList<CyclePeriodSnapshot>> GetByIdsAsync(
            IReadOnlyCollection<Guid> cyclePeriodIds, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<CyclePeriodSnapshot>>(Array.Empty<CyclePeriodSnapshot>());
    }

    private sealed class FakeCampaignRepo : ICampaignRepository
    {
        public List<CampaignEntity> Items { get; } = new();
        public int WriteCount { get; private set; }

        public Task<CampaignEntity?> GetByIdAsync(Guid t, Guid id, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(c => c.TenantId == t && c.Id == id && !c.IsDeleted));

        public Task<IReadOnlyList<CampaignEntity>> ListAsync(Guid t, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<CampaignEntity>)Items
                .Where(c => c.TenantId == t && !c.IsDeleted)
                .OrderByDescending(c => c.CreatedAt).ToList());

        public Task<CampaignEntity?> GetActiveByCodeAsync(Guid t, string code, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(c =>
                c.TenantId == t && !c.IsDeleted && c.CampaignCode == code && !c.IsArchived()));

        public Task<CampaignEntity?> FindByExternalReferenceAsync(
            Guid t, string sourceSystem, string externalId, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(c =>
                c.TenantId == t && !c.IsDeleted && !c.IsArchived()
                && c.ExternalReferences.Any(x =>
                    string.Equals(x.SourceSystem, sourceSystem, StringComparison.OrdinalIgnoreCase)
                    && x.ExternalId == externalId)));

        public Task InsertAsync(CampaignEntity campaign, CancellationToken ct)
        {
            WriteCount++;
            Items.Add(campaign);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(CampaignEntity campaign, CancellationToken ct)
        {
            WriteCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeCampaignTargetRepo : ICampaignTargetRepository
    {
        public List<CampaignTarget> Items { get; } = new();
        public int WriteCount { get; private set; }

        /// <summary>Always expected to stay 0 — there is no delete path anywhere in FU04.</summary>
        public int DeleteAttempts => 0;

        public Task<CampaignTarget?> GetByIdAsync(Guid t, Guid id, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(x => x.TenantId == t && x.Id == id && !x.IsDeleted));

        public Task<IReadOnlyList<CampaignTarget>> ListByCampaignAsync(Guid t, Guid campaignId, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<CampaignTarget>)Items
                .Where(x => x.TenantId == t && !x.IsDeleted && x.CampaignId == campaignId)
                .OrderByDescending(x => x.CreatedAt).ToList());

        public Task<CampaignTarget?> FindActiveByTargetAsync(
            Guid t, Guid campaignId, string targetType, Guid targetId, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(x =>
                x.TenantId == t && !x.IsDeleted && x.CampaignId == campaignId
                && x.TargetType == targetType && x.TargetId == targetId && !x.IsArchived()));

        public Task InsertAsync(CampaignTarget target, CancellationToken ct)
        {
            WriteCount++;
            Items.Add(target);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(CampaignTarget target, CancellationToken ct)
        {
            WriteCount++;
            return Task.CompletedTask;
        }
    }

    /// <summary>Any call is a boundary violation — FU04 must never reach the MOD-0164 store directly.</summary>
    private sealed class ThrowingConsentRecordRepo : IConsentRecordRepository
    {
        public int CallCount { get; private set; }

        public Task<ConsentRecord?> GetByIdAsync(Guid t, Guid id, CancellationToken ct) => Fail();

        public Task<IReadOnlyList<ConsentRecord>> ListAsync(Guid t, CancellationToken ct) => FailList();

        public Task<IReadOnlyList<ConsentRecord>> ListForEvaluationAsync(
            Guid t, string subjectType, Guid subjectId, string channel, CancellationToken ct) => FailList();

        public Task<ConsentRecord?> FindByExternalReferenceAsync(
            Guid t, string sourceSystem, string externalId, CancellationToken ct) => Fail();

        public Task InsertAsync(ConsentRecord record, CancellationToken ct) => Fail();

        public Task UpdateAsync(ConsentRecord record, CancellationToken ct) => Fail();

        private Task<ConsentRecord?> Fail()
        {
            CallCount++;
            throw new InvalidOperationException("MOD-0165 must not touch the MOD-0164 consent store.");
        }

        private Task<IReadOnlyList<ConsentRecord>> FailList()
        {
            CallCount++;
            throw new InvalidOperationException("MOD-0165 must not touch the MOD-0164 consent store.");
        }
    }

    private sealed class ThrowingPreferenceRecordRepo : IPreferenceRecordRepository
    {
        public int CallCount { get; private set; }

        public Task<PreferenceRecord?> GetByIdAsync(Guid t, Guid id, CancellationToken ct) => Fail();

        public Task<IReadOnlyList<PreferenceRecord>> ListAsync(Guid t, CancellationToken ct) => FailList();

        public Task<IReadOnlyList<PreferenceRecord>> ListForEvaluationAsync(
            Guid t, string subjectType, Guid subjectId, CancellationToken ct) => FailList();

        public Task<PreferenceRecord?> FindByExternalReferenceAsync(
            Guid t, string sourceSystem, string externalId, CancellationToken ct) => Fail();

        public Task InsertAsync(PreferenceRecord record, CancellationToken ct) => Fail();

        public Task UpdateAsync(PreferenceRecord record, CancellationToken ct) => Fail();

        private Task<PreferenceRecord?> Fail()
        {
            CallCount++;
            throw new InvalidOperationException("MOD-0165 must not touch the MOD-0164 preference store.");
        }

        private Task<IReadOnlyList<PreferenceRecord>> FailList()
        {
            CallCount++;
            throw new InvalidOperationException("MOD-0165 must not touch the MOD-0164 preference store.");
        }
    }
}
