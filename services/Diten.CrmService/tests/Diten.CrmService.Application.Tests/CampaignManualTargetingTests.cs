using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.Campaign;
using Diten.CrmService.Application.Features.Campaign.Commands;
using Diten.CrmService.Application.Features.Campaign.Contract;
using Diten.CrmService.Application.Features.Campaign.Handlers;
using Diten.CrmService.Application.Features.Campaign.Queries;
using Diten.CrmService.Application.Features.Campaign.Services;
using Diten.CrmService.Application.Features.Campaign.Snapshot;
using Diten.CrmService.Application.Features.ConsentPreference.Evaluation;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using Xunit;
using CampaignEntity = Diten.CrmService.Domain.Entities.Campaign;

namespace Diten.CrmService.Application.Tests;

/// <summary>
/// MOD-0165 FU11 — manual targeting redesign. Pins down the four things that could quietly go wrong when a screen
/// stops asking for eight fields:
/// <list type="bullet">
/// <item>the priority BAND is a new field, so pre-FU11 integers still READ (nothing was migrated) and are shown under
/// the integer's own "smaller wins" meaning — 1 is high, not low;</item>
/// <item>FU04's "a target always states why it exists" invariant survived the removal of the reason box — the server
/// now writes a true sentence and flags that it did;</item>
/// <item>what the screen stopped sending is filled in, not lost, and an EDIT never erases what it did not mention;</item>
/// <item>the snapshot is untouched in BEHAVIOUR: it still accepts the integer, still writes excluded with a reason,
/// and is still additive and idempotent.</item>
/// </list>
/// </summary>
public sealed class CampaignManualTargetingTests
{
    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Account1 = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid Account2 = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private static DateTimeOffset Utc(int y, int m, int d) => new(y, m, d, 0, 0, 0, TimeSpan.Zero);

    // ------------------------------------------------------------------ fixture

    private sealed class FakeCampaignRepo : ICampaignRepository
    {
        public List<CampaignEntity> Items { get; } = new();

        public Task<CampaignEntity?> GetByIdAsync(Guid t, Guid id, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(c => c.TenantId == t && c.Id == id && !c.IsDeleted));

        public Task<IReadOnlyList<CampaignEntity>> ListAsync(Guid t, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<CampaignEntity>)Items.Where(c => c.TenantId == t).ToList());

        public Task<CampaignEntity?> GetActiveByCodeAsync(Guid t, string code, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(c => c.TenantId == t && c.CampaignCode == code && !c.IsArchived()));

        public Task<CampaignEntity?> FindByExternalReferenceAsync(Guid t, string s, string e, CancellationToken ct)
            => Task.FromResult<CampaignEntity?>(null);

        public Task InsertAsync(CampaignEntity campaign, CancellationToken ct)
        {
            Items.Add(campaign);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(CampaignEntity campaign, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeCampaignTargetRepo : ICampaignTargetRepository
    {
        public List<CampaignTarget> Items { get; } = new();

        public Task<CampaignTarget?> GetByIdAsync(Guid t, Guid id, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(x => x.TenantId == t && x.Id == id));

        public Task<IReadOnlyList<CampaignTarget>> ListByCampaignAsync(Guid t, Guid campaignId, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<CampaignTarget>)Items
                .Where(x => x.TenantId == t && x.CampaignId == campaignId).ToList());

        public Task<CampaignTarget?> FindActiveByTargetAsync(
            Guid t, Guid campaignId, string targetType, Guid targetId, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(x =>
                x.TenantId == t && x.CampaignId == campaignId && x.TargetType == targetType
                && x.TargetId == targetId && !x.IsArchived()));

        public Task InsertAsync(CampaignTarget target, CancellationToken ct)
        {
            Items.Add(target);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(CampaignTarget target, CancellationToken ct) => Task.CompletedTask;
    }

    /// <summary>An actor with a name, so the generated selection reason can be checked for the fact it claims.</summary>
    private sealed class NamedActor : IActorContext
    {
        public string? ActorName => "Dr. Ayse Yilmaz";
    }

    private sealed class FakeConsent : IConsentPreferenceEvaluator
    {
        public ConsentEvaluationResult Result { get; set; } = Verdict(
            ConsentEligibilityStatus.Allowed, ConsentDecision.ConsentGranted, ConsentReasonCodes.ConsentGranted);

        public Task<ConsentEvaluationResult> EvaluateAsync(ConsentEvaluationRequest r, CancellationToken ct)
            => Task.FromResult(Result);

        public static ConsentEvaluationResult Verdict(string eligibility, string decision, string reasonCode)
            => new(
                eligibility, decision, ConsentSubjectType.Contact, Account1,
                ConsentChannel.Visit, ConsentPurpose.MedicalVisit, ConsentScopeType.Campaign, Guid.NewGuid(),
                Utc(2026, 5, 1), null, Array.Empty<Guid>(), new[] { reasonCode }, "evaluated",
                Array.Empty<CandidateConsent>(), Array.Empty<CandidatePreference>(),
                ConsentEvaluationResult.CurrentEvaluatorVersion, Utc(2026, 5, 1));
    }

    private sealed class Fixture
    {
        public FakeCampaignRepo Campaigns { get; } = new();
        public FakeCampaignTargetRepo Targets { get; } = new();
        public FakeConsent Consent { get; } = new();
        public CampaignScopeTestDoubles.FakeSegmentCatalog Segments { get; } = new();
        public CampaignScopeTestDoubles.FakeCyclePeriodReader Periods { get; } = new();
        public CampaignScopeTestDoubles.FakeCampaignCodeSequence Sequence { get; } = new();

        private TenantContext Tenant()
        {
            var ctx = new TenantContext();
            ctx.SetTenant(TenantA);
            return ctx;
        }

        private CampaignScopeWriteValidator Scope => new(
            new CampaignScopeTestDoubles.FakeReferenceValidator(),
            new CampaignScopeTestDoubles.FakeLegalEntityValidator());

        public CreateCampaignHandler CreateCampaign() => new(
            Tenant(), new NamedActor(), Campaigns, new CampaignCycleBindingGuard(Periods), Scope,
            new CampaignSegmentValidator(Segments),
            new CampaignCodeGenerator(Sequence, Campaigns, () => Utc(2026, 5, 1)));

        public CreateCampaignTargetHandler CreateTarget()
            => new(Tenant(), new NamedActor(), Campaigns, Targets);

        public UpdateCampaignTargetHandler UpdateTarget()
            => new(Tenant(), new NamedActor(), Campaigns, Targets);

        public GetCampaignTargetHandler GetTarget() => new(Tenant(), Targets);

        public CreateCampaignTargetSnapshotHandler Snapshot()
            => new(Tenant(), new NamedActor(), Campaigns, Targets, Consent);

        public async Task<Guid> SeedCampaignAsync()
        {
            var response = await CreateCampaign().Handle(
                new CreateCampaignCommand(
                    "CMP-1", "Campaign", CampaignTypes.ProductCampaign, Utc(2026, 3, 10),
                    EndDate: Utc(2026, 4, 10)),
                default);
            Assert.Equal(201, response.StatusCode);
            return response.Data;
        }

        public CampaignTarget Stored() => Targets.Items.Single();
    }

    private static CreateCampaignTargetCommand TargetCmd(
        Guid campaignId,
        Guid? targetId = null,
        string targetType = CampaignTargetTypes.Account,
        string? priorityLevel = null,
        string? status = null,
        string? selectionReason = null,
        string? targetSource = null,
        DateTimeOffset? effectiveFrom = null,
        int? priority = null)
        => new(campaignId, targetType, targetId ?? Account1,
            TargetSource: targetSource, SelectionReason: selectionReason, EffectiveFrom: effectiveFrom,
            TargetDisplayName: "Grand Medical A.S.", TargetStatus: status,
            Priority: priority, PriorityLevel: priorityLevel);

    // ============ 1–7 · The priority band ============

    /// <summary>Scenario 1 — the three bands are accepted and stored as given.</summary>
    [Theory]
    [InlineData(CampaignTargetPriorityLevels.Low)]
    [InlineData(CampaignTargetPriorityLevels.Medium)]
    [InlineData(CampaignTargetPriorityLevels.High)]
    public async Task T01_Known_Bands_Are_Accepted(string band)
    {
        var f = new Fixture();
        var campaignId = await f.SeedCampaignAsync();

        Assert.Equal(201, (await f.CreateTarget().Handle(TargetCmd(campaignId, priorityLevel: band), default)).StatusCode);
        Assert.Equal(band, f.Stored().PriorityLevel);
    }

    /// <summary>Scenario 2 — an unknown band is REFUSED, never rounded to a neighbour. A target quietly demoted to
    /// "low" would be worked on last for a reason nobody chose.</summary>
    [Fact]
    public async Task T02_Unknown_Band_Is_Refused_Not_Rounded()
    {
        var f = new Fixture();
        var campaignId = await f.SeedCampaignAsync();

        var response = await f.CreateTarget().Handle(TargetCmd(campaignId, priorityLevel: "urgent"), default);
        Assert.Equal(400, response.StatusCode);
        Assert.Contains(CampaignReasonCodes.CampaignTargetPriorityLevelUnknown, string.Join(" ", response.Errors));
        Assert.Empty(f.Targets.Items);
    }

    /// <summary>Scenario 3 — no band is a real answer. It is never turned into "low".</summary>
    [Fact]
    public async Task T03_Absent_Band_Stays_Absent()
    {
        var f = new Fixture();
        var campaignId = await f.SeedCampaignAsync();

        Assert.Equal(201, (await f.CreateTarget().Handle(TargetCmd(campaignId), default)).StatusCode);
        Assert.Null(f.Stored().PriorityLevel);
        Assert.Null(f.Stored().DerivedPriorityLevel());
    }

    /// <summary>
    /// Scenario 4 — the migration that was NOT run. A pre-FU11 row carries an integer and no band; it must read as a
    /// band under the integer's own documented contract ("smaller wins"), which makes 1 the HIGHEST priority.
    /// Mapping 1 to "low" would relabel every top-priority row ever written.
    /// </summary>
    [Theory]
    [InlineData(1, CampaignTargetPriorityLevels.High)]
    [InlineData(2, CampaignTargetPriorityLevels.Medium)]
    [InlineData(3, CampaignTargetPriorityLevels.Low)]
    [InlineData(42, CampaignTargetPriorityLevels.Low)]
    public void T04_Legacy_Integer_Reads_As_A_Band_Smaller_Wins(int stored, string expected)
    {
        var target = new CampaignTarget { Priority = stored };
        Assert.Equal(expected, target.DerivedPriorityLevel());
    }

    /// <summary>Scenario 5 — deriving is a READ. It writes nothing back, so an old row keeps its integer forever and
    /// no backfill has to exist.</summary>
    [Fact]
    public void T05_Deriving_Never_Writes()
    {
        var target = new CampaignTarget { Priority = 1 };

        Assert.Equal(CampaignTargetPriorityLevels.High, target.DerivedPriorityLevel());
        Assert.Equal(1, target.Priority);
        Assert.Null(target.PriorityLevel);
    }

    /// <summary>Scenario 6 — a stated band always beats a derived one; the integer is not consulted once a band
    /// exists.</summary>
    [Fact]
    public void T06_Stated_Band_Wins_Over_The_Integer()
    {
        var target = new CampaignTarget { Priority = 1, PriorityLevel = CampaignTargetPriorityLevels.Low };
        Assert.Equal(CampaignTargetPriorityLevels.Low, target.DerivedPriorityLevel());
    }

    /// <summary>Scenario 7 — ordering, if a consumer ever wants it, stays deterministic and most-important-first. An
    /// unstated band sorts last because it makes no claim.</summary>
    [Fact]
    public void T07_Band_Weights_Are_Deterministic()
    {
        Assert.True(CampaignTargetPriorityLevels.Weight(CampaignTargetPriorityLevels.High)
                    < CampaignTargetPriorityLevels.Weight(CampaignTargetPriorityLevels.Medium));
        Assert.True(CampaignTargetPriorityLevels.Weight(CampaignTargetPriorityLevels.Medium)
                    < CampaignTargetPriorityLevels.Weight(CampaignTargetPriorityLevels.Low));
        Assert.True(CampaignTargetPriorityLevels.Weight(null)
                    > CampaignTargetPriorityLevels.Weight(CampaignTargetPriorityLevels.Low));
    }

    // ============ 8–14 · What the screen stopped sending ============

    /// <summary>Scenario 8 — the four defaults. The author sends none of them and the row still carries all four,
    /// stated as facts rather than guesses.</summary>
    [Fact]
    public async Task T08_Server_Fills_Source_Reason_Codes_And_Start()
    {
        var f = new Fixture();
        var campaignId = await f.SeedCampaignAsync();

        Assert.Equal(201, (await f.CreateTarget().Handle(TargetCmd(campaignId), default)).StatusCode);

        var row = f.Stored();
        Assert.Equal(CampaignTargetSources.Manual, row.TargetSource);
        Assert.Contains(CampaignReasonCodes.ManualTargetSelected, row.ReasonCodes);
        Assert.Contains(CampaignReasonCodes.CampaignTargetCreated, row.ReasonCodes);
        Assert.False(string.IsNullOrWhiteSpace(row.SelectionReason));
        Assert.NotEqual(default, row.EffectiveFrom);
    }

    /// <summary>
    /// Scenario 9 — FU04's invariant, intact. The reason box is gone from the screen but the target still states why
    /// it exists, and the statement names the two facts the server actually knows: who, and when.
    /// </summary>
    [Fact]
    public async Task T09_Generated_Reason_States_Who_And_When()
    {
        var f = new Fixture();
        var campaignId = await f.SeedCampaignAsync();

        Assert.Equal(201, (await f.CreateTarget().Handle(TargetCmd(campaignId), default)).StatusCode);

        var row = f.Stored();
        Assert.Contains("Dr. Ayse Yilmaz", row.SelectionReason);
        Assert.Contains(DateTimeOffset.UtcNow.ToString("yyyy-MM-dd"), row.SelectionReason);
    }

    /// <summary>Scenario 10 — a generated reason is DECLARED. An auditor can tell a reason someone stated from one
    /// the server filled in, without comparing wording.</summary>
    [Fact]
    public async Task T10_Generated_Reason_Is_Flagged_And_A_Stated_One_Is_Not()
    {
        var f = new Fixture();
        var campaignId = await f.SeedCampaignAsync();

        Assert.Equal(201, (await f.CreateTarget().Handle(TargetCmd(campaignId), default)).StatusCode);
        Assert.Contains(CampaignReasonCodes.CampaignTargetSelectionReasonGenerated, f.Stored().ReasonCodes);

        var g = new Fixture();
        var otherCampaign = await g.SeedCampaignAsync();
        Assert.Equal(201, (await g.CreateTarget().Handle(
            TargetCmd(otherCampaign, selectionReason: "Key opinion leader for Q2"), default)).StatusCode);

        Assert.Equal("Key opinion leader for Q2", g.Stored().SelectionReason);
        Assert.DoesNotContain(CampaignReasonCodes.CampaignTargetSelectionReasonGenerated, g.Stored().ReasonCodes);
    }

    /// <summary>Scenario 11 — an existing caller that still sends the three fields is honoured unchanged. The
    /// defaults fill gaps; they never overwrite.</summary>
    [Fact]
    public async Task T11_Explicit_Values_Are_Never_Overwritten_By_Defaults()
    {
        var f = new Fixture();
        var campaignId = await f.SeedCampaignAsync();

        Assert.Equal(201, (await f.CreateTarget().Handle(
            TargetCmd(campaignId,
                targetSource: CampaignTargetSources.Import,
                selectionReason: "Imported from the 2025 plan",
                effectiveFrom: Utc(2026, 1, 1)),
            default)).StatusCode);

        var row = f.Stored();
        Assert.Equal(CampaignTargetSources.Import, row.TargetSource);
        Assert.Equal("Imported from the 2025 plan", row.SelectionReason);
        Assert.Equal(Utc(2026, 1, 1), row.EffectiveFrom);
    }

    /// <summary>
    /// Scenario 12 — an EDIT that does not mention a field must not erase it. The screen stopped sending the reason
    /// and the start date, so without this an ordinary edit would silently rewrite both.
    /// </summary>
    [Fact]
    public async Task T12_Update_Preserves_What_It_Did_Not_Mention()
    {
        var f = new Fixture();
        var campaignId = await f.SeedCampaignAsync();
        var created = await f.CreateTarget().Handle(
            TargetCmd(campaignId, selectionReason: "Stated once, by hand", effectiveFrom: Utc(2026, 1, 1)), default);

        var updated = await f.UpdateTarget().Handle(
            new UpdateCampaignTargetCommand(campaignId, created.Data,
                PriorityLevel: CampaignTargetPriorityLevels.High),
            default);

        Assert.Equal(200, updated.StatusCode);
        var row = f.Stored();
        Assert.Equal("Stated once, by hand", row.SelectionReason);
        Assert.Equal(Utc(2026, 1, 1), row.EffectiveFrom);
        Assert.Equal(CampaignTargetPriorityLevels.High, row.PriorityLevel);
    }

    /// <summary>Scenario 13 — the deprecated integer survives an edit. The FU11 screen never sends it, and losing it
    /// would erase the only record that a pre-FU11 row was ever prioritised.</summary>
    [Fact]
    public async Task T13_Update_Preserves_The_Deprecated_Integer()
    {
        var f = new Fixture();
        var campaignId = await f.SeedCampaignAsync();
        var created = await f.CreateTarget().Handle(TargetCmd(campaignId, priority: 1), default);

        Assert.Equal(200, (await f.UpdateTarget().Handle(
            new UpdateCampaignTargetCommand(campaignId, created.Data,
                PriorityLevel: CampaignTargetPriorityLevels.Medium),
            default)).StatusCode);

        Assert.Equal(1, f.Stored().Priority);
        Assert.Equal(CampaignTargetPriorityLevels.Medium, f.Stored().PriorityLevel);
    }

    /// <summary>Scenario 14 — the label the picker showed travels with the row for audit, and is still explicitly not
    /// a source of truth (FU04).</summary>
    [Fact]
    public async Task T14_Picker_Label_Is_Stored_As_A_Label()
    {
        var f = new Fixture();
        var campaignId = await f.SeedCampaignAsync();

        Assert.Equal(201, (await f.CreateTarget().Handle(TargetCmd(campaignId), default)).StatusCode);
        Assert.Equal("Grand Medical A.S.", f.Stored().TargetDisplayName);
        Assert.Equal(Account1, f.Stored().TargetId);
    }

    // ============ 15–18 · Authorable status ============

    /// <summary>Scenario 15 — the four statuses a human may set.</summary>
    [Theory]
    [InlineData(CampaignTargetStatuses.Draft)]
    [InlineData(CampaignTargetStatuses.Active)]
    [InlineData(CampaignTargetStatuses.Inactive)]
    [InlineData(CampaignTargetStatuses.Completed)]
    public async Task T15_Authorable_Statuses_Are_Accepted(string status)
    {
        var f = new Fixture();
        var campaignId = await f.SeedCampaignAsync();
        Assert.Equal(201, (await f.CreateTarget().Handle(TargetCmd(campaignId, status: status), default)).StatusCode);
    }

    /// <summary>
    /// Scenario 16 — 'excluded' is refused WITH or WITHOUT a reason. It is the outcome of a consent evaluation, which
    /// writes it together with the reason it is required to carry; an author choosing it by hand was the only way to
    /// produce an excluded row nobody had evaluated.
    /// </summary>
    [Fact]
    public async Task T16_Excluded_Is_Not_Authorable_By_A_Human()
    {
        var f = new Fixture();
        var campaignId = await f.SeedCampaignAsync();

        var response = await f.CreateTarget().Handle(
            TargetCmd(campaignId, status: CampaignTargetStatuses.Excluded), default);

        Assert.Equal(400, response.StatusCode);
        Assert.Contains(CampaignReasonCodes.CampaignTargetStatusNotAuthorable, string.Join(" ", response.Errors));
        Assert.Empty(f.Targets.Items);
    }

    /// <summary>Scenario 17 — 'archived' is not authorable either; archiving is its own action.</summary>
    [Fact]
    public async Task T17_Archived_Is_Not_An_Authorable_Status()
    {
        var f = new Fixture();
        var campaignId = await f.SeedCampaignAsync();

        Assert.Equal(400, (await f.CreateTarget().Handle(
            TargetCmd(campaignId, status: CampaignTargetStatuses.Archived), default)).StatusCode);
    }

    /// <summary>Scenario 18 — the restriction is the SCREEN's, not the vocabulary's: both statuses stay valid on the
    /// aggregate, which is why the snapshot can still write one.</summary>
    [Fact]
    public void T18_Excluded_Stays_A_Valid_Status_Everywhere_Else()
    {
        Assert.True(CampaignTargetStatuses.IsValid(CampaignTargetStatuses.Excluded));
        Assert.False(CampaignTargetStatuses.IsAuthorable(CampaignTargetStatuses.Excluded));
        Assert.Contains(CampaignTargetStatuses.Excluded, CampaignTargetStatuses.All);
        Assert.DoesNotContain(CampaignTargetStatuses.Excluded, CampaignTargetStatuses.Authorable);
    }

    // ============ 19–23 · The snapshot is untouched in BEHAVIOUR ============

    /// <summary>
    /// Scenario 19 — a snapshot caller that still sends an INTEGER keeps working. Retyping the field would otherwise
    /// have rejected requests that were valid yesterday, for a reason the caller cannot see.
    /// </summary>
    [Fact]
    public async Task T19_Snapshot_Still_Accepts_The_Integer()
    {
        var f = new Fixture();
        var campaignId = await f.SeedCampaignAsync();

        var response = await f.Snapshot().Handle(
            new CreateCampaignTargetSnapshotCommand(
                campaignId, CampaignTargetSources.Manual,
                new[] { new CampaignSnapshotTargetItem(CampaignTargetTypes.Account, Account1, Priority: 1) },
                "snapshot run",
                ApplyConsentFilter: false),
            default);

        Assert.Equal(201, response.StatusCode);
        var row = f.Targets.Items.Single();
        Assert.Equal(1, row.Priority);
        Assert.Equal(CampaignTargetPriorityLevels.High, row.DerivedPriorityLevel());
    }

    /// <summary>Scenario 20 — a snapshot caller may also send a band, and it is stored as given.</summary>
    [Fact]
    public async Task T20_Snapshot_Accepts_The_Band()
    {
        var f = new Fixture();
        var campaignId = await f.SeedCampaignAsync();

        Assert.Equal(201, (await f.Snapshot().Handle(
            new CreateCampaignTargetSnapshotCommand(
                campaignId, CampaignTargetSources.Manual,
                new[] { new CampaignSnapshotTargetItem(CampaignTargetTypes.Account, Account1, PriorityLevel: CampaignTargetPriorityLevels.Medium) },
                "snapshot run",
                ApplyConsentFilter: false),
            default)).StatusCode);

        Assert.Equal(CampaignTargetPriorityLevels.Medium, f.Targets.Items.Single().PriorityLevel);
    }

    /// <summary>Scenario 21 — an unknown band is refused on the snapshot path too, per row.</summary>
    [Fact]
    public async Task T21_Snapshot_Refuses_An_Unknown_Band()
    {
        var f = new Fixture();
        var campaignId = await f.SeedCampaignAsync();

        Assert.Equal(400, (await f.Snapshot().Handle(
            new CreateCampaignTargetSnapshotCommand(
                campaignId, CampaignTargetSources.Manual,
                new[] { new CampaignSnapshotTargetItem(CampaignTargetTypes.Account, Account1, PriorityLevel: "urgent") },
                "snapshot run",
                ApplyConsentFilter: false),
            default)).StatusCode);

        Assert.Empty(f.Targets.Items);
    }

    /// <summary>
    /// Scenario 22 — the snapshot still writes 'excluded' WITH its reason when consent says no. FU11 took that status
    /// away from the author precisely so it keeps meaning what the evaluator said.
    /// </summary>
    [Fact]
    public async Task T22_Snapshot_Still_Writes_Excluded_With_A_Reason()
    {
        var f = new Fixture();
        f.Consent.Result = FakeConsent.Verdict(
            ConsentEligibilityStatus.Blocked, ConsentDecision.PreferenceRestricted,
            ConsentReasonCodes.PreferenceRestricted);

        var campaignId = await f.SeedCampaignAsync();

        Assert.Equal(201, (await f.Snapshot().Handle(
            new CreateCampaignTargetSnapshotCommand(
                campaignId, CampaignTargetSources.Manual,
                new[] { new CampaignSnapshotTargetItem(CampaignTargetTypes.Contact, Account1) },
                "snapshot run",
                ConsentChannel: ConsentChannel.Visit, ConsentPurpose: ConsentPurpose.MedicalVisit),
            default)).StatusCode);

        var row = f.Targets.Items.Single();
        Assert.Equal(CampaignTargetStatuses.Excluded, row.TargetStatus);
        Assert.False(string.IsNullOrWhiteSpace(row.ExclusionReason));
    }

    /// <summary>Scenario 23 — the snapshot is still ADDITIVE and idempotent per source: re-running reconciles the
    /// same row instead of duplicating or archiving it.</summary>
    [Fact]
    public async Task T23_Snapshot_Is_Still_Additive_And_Idempotent()
    {
        var f = new Fixture();
        var campaignId = await f.SeedCampaignAsync();
        var command = new CreateCampaignTargetSnapshotCommand(
            campaignId, CampaignTargetSources.Manual,
            new[] { new CampaignSnapshotTargetItem(CampaignTargetTypes.Account, Account1) },
            "snapshot run",
            ApplyConsentFilter: false);

        Assert.Equal(201, (await f.Snapshot().Handle(command, default)).StatusCode);
        var second = await f.Snapshot().Handle(command, default);

        Assert.Equal(201, second.StatusCode);
        Assert.Single(f.Targets.Items);
        Assert.Equal(1, second.Data.ReconciledCount);
        Assert.All(f.Targets.Items, row => Assert.False(row.IsArchived()));
    }

    // ============ 24–28 · Contract + the regressions FU11 must not cause ============

    /// <summary>Scenario 24 — both new vocabularies are PUBLISHED, so the screen needs no hardcoded list.</summary>
    [Fact]
    public async Task T24_Vocabulary_Publishes_Bands_And_Authorable_Statuses()
    {
        var vocabulary = (await ContractAsync()).Vocabulary;

        Assert.Equal(CampaignTargetPriorityLevels.All, vocabulary.TargetPriorityLevels);
        Assert.Equal(CampaignTargetStatuses.Authorable, vocabulary.AuthorableTargetStatuses);
        Assert.Equal(CampaignTargetStatuses.All, vocabulary.TargetStatuses);
    }

    private static async Task<CampaignContractDto> ContractAsync()
    {
        var tenant = new TenantContext();
        tenant.SetTenant(TenantA);
        return (await new GetCampaignContractHandler(tenant).Handle(new GetCampaignContractQuery(), default)).Data!;
    }

    /// <summary>
    /// Scenario 25 — the snapshot has no SCREEN, but its flag stays true because the API genuinely supports it.
    /// Flipping the flag would have told consumers a capability was withdrawn when only a button was.
    /// </summary>
    [Fact]
    public async Task T25_Snapshot_Capability_Is_Still_Declared_And_The_Gap_Is_Stated()
    {
        var contract = await ContractAsync();

        Assert.True(contract.Features.SupportsStaticTargetSnapshot);
        Assert.Contains("NO SCREEN", string.Join(" | ", contract.Limitations));
    }

    /// <summary>Scenario 26 — every FU11 promise a consumer might rely on is written down in limitations, not left to
    /// be discovered.</summary>
    [Theory]
    [InlineData("priorityLevel")]
    [InlineData("smaller wins")]
    [InlineData("authorableTargetStatuses")]
    [InlineData("account and contact")]
    public async Task T26_Limitations_State_The_FU11_Behaviour(string phrase)
    {
        var limitations = string.Join(" | ", (await ContractAsync()).Limitations);
        Assert.Contains(phrase, limitations, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Scenario 27 — FU10's mode gate still fires. A segment-targeted campaign refuses a new manual target,
    /// whatever FU11 did to the fields.</summary>
    [Fact]
    public async Task T27_Segment_Mode_Still_Refuses_A_Manual_Target()
    {
        var f = new Fixture();
        var segmentId = f.Segments.Add("SEG-1");

        var created = await f.CreateCampaign().Handle(
            new CreateCampaignCommand(
                "CMP-SEG", "Campaign", CampaignTypes.ProductCampaign, Utc(2026, 3, 10),
                EndDate: Utc(2026, 4, 10),
                TargetingMode: CampaignTargetingModes.Segment,
                TargetedSegmentIds: new[] { segmentId }),
            default);
        Assert.Equal(201, created.StatusCode);

        var response = await f.CreateTarget().Handle(TargetCmd(created.Data), default);
        Assert.Equal(400, response.StatusCode);
        Assert.Contains(CampaignReasonCodes.CampaignTargetingModeForbidsManualTarget, string.Join(" ", response.Errors));
    }

    /// <summary>Scenario 28 — FU04's strict manual duplicate guard is untouched: the same target twice by hand is a
    /// mistake, not an idempotent retry.</summary>
    [Fact]
    public async Task T28_Manual_Duplicate_Is_Still_409()
    {
        var f = new Fixture();
        var campaignId = await f.SeedCampaignAsync();

        Assert.Equal(201, (await f.CreateTarget().Handle(TargetCmd(campaignId), default)).StatusCode);
        Assert.Equal(409, (await f.CreateTarget().Handle(TargetCmd(campaignId), default)).StatusCode);
        Assert.Equal(201, (await f.CreateTarget().Handle(TargetCmd(campaignId, targetId: Account2), default)).StatusCode);
    }

    /// <summary>Scenario 29 — the read projection exposes the band so a grid can render an old row without knowing
    /// anything about the deprecated integer.</summary>
    [Fact]
    public async Task T29_Read_Projects_The_Band_For_A_Legacy_Row()
    {
        var f = new Fixture();
        var campaignId = await f.SeedCampaignAsync();
        var created = await f.CreateTarget().Handle(TargetCmd(campaignId, priority: 2), default);

        var read = await f.GetTarget().Handle(new GetCampaignTargetQuery(campaignId, created.Data), default);

        Assert.Equal(200, read.StatusCode);
        Assert.Equal(CampaignTargetPriorityLevels.Medium, read.Data.PriorityLevel);
        Assert.Equal(2, read.Data.Priority);
    }
}
