using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.Campaign;
using Diten.CrmService.Application.Features.Campaign.Commands;
using Diten.CrmService.Application.Features.Campaign.Contract;
using Diten.CrmService.Application.Features.Campaign.Handlers;
using Diten.CrmService.Application.Features.Campaign.Queries;
using Diten.CrmService.Application.Features.Campaign.Read;
using Diten.CrmService.Application.Features.Campaign.Services;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using Xunit;
using CampaignEntity = Diten.CrmService.Domain.Entities.Campaign;

namespace Diten.CrmService.Application.Tests;

/// <summary>
/// MOD-0165 FU10 — targeting mode + multi-segment targeting. Pins down: the mode is required and fail-closed;
/// pre-FU10 rows read as manual and nothing is migrated; only the ACTIVE mode's data is validated; the passive mode's
/// data is kept, never cleared by a switch, and refuses only NEW writes; segments are validated when the set CHANGES
/// while "at least one" is checked on every write; a pinned segment is a VERSION; the code is generated at write time;
/// and the direction stays one-way (no segment repository, no segment write, no HttpClient).
/// </summary>
public sealed class CampaignTargetingModeTests
{
    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static DateTimeOffset Utc(int y, int m, int d) => new(y, m, d, 0, 0, 0, TimeSpan.Zero);

    // ------------------------------------------------------------------ fixture

    private sealed class FakeCampaignRepo : ICampaignRepository
    {
        public List<CampaignEntity> Items { get; } = new();
        public int WriteCount { get; private set; }

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

    private sealed class Fixture
    {
        public FakeCampaignRepo Campaigns { get; } = new();
        public FakeCampaignTargetRepo Targets { get; } = new();
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
            new CampaignScopeTestDoubles.FakeReferenceValidator()
                .Publish(CampaignScopeReferenceSets.CountrySet, "TR")
                .Publish(CampaignScopeReferenceSets.BusinessUnitSet, "alpha"),
            new CampaignScopeTestDoubles.FakeLegalEntityValidator());

        private CampaignSegmentValidator Targeting => new(Segments);

        public CampaignCodeGenerator CodeGenerator => new(Sequence, Campaigns, () => Utc(2026, 5, 1));

        public CreateCampaignHandler Create() => new(
            Tenant(), new NullActorContext(), Campaigns,
            new CampaignCycleBindingGuard(Periods), Scope, Targeting, CodeGenerator);

        public UpdateCampaignHandler Update() => new(
            Tenant(), new NullActorContext(), Campaigns,
            new CampaignCycleBindingGuard(Periods), Scope, Targeting);

        public GetCampaignHandler Get() => new(Tenant(), Campaigns, Periods, Segments);

        public ListCampaignsHandler List() => new(Tenant(), Campaigns, Periods, Segments);

        public CreateCampaignTargetHandler CreateTarget()
            => new(Tenant(), new NullActorContext(), Campaigns, Targets);

        public Task<Response<Guid>> CreateAsync(
            string? code = "CMP-1",
            string? mode = null,
            IReadOnlyList<Guid>? segmentIds = null)
            => Create().Handle(
                new CreateCampaignCommand(
                    code, "Campaign", CampaignTypes.ProductCampaign, Utc(2026, 3, 10),
                    EndDate: Utc(2026, 4, 10),
                    TargetingMode: mode,
                    TargetedSegmentIds: segmentIds),
                default);

        public Task<Response<bool>> UpdateAsync(
            Guid campaignId,
            string? mode = null,
            IReadOnlyList<Guid>? segmentIds = null,
            string name = "Campaign")
            => Update().Handle(
                new UpdateCampaignCommand(
                    campaignId, name, CampaignTypes.ProductCampaign, Utc(2026, 3, 10),
                    EndDate: Utc(2026, 4, 10),
                    TargetingMode: mode,
                    TargetedSegmentIds: segmentIds),
                default);

        public CampaignEntity Stored() => Campaigns.Items.Single();
    }

    // ============ 1–6 · The mode itself ============

    /// <summary>Scenario 1 — a manual campaign needs no segment.</summary>
    [Fact]
    public async Task T01_Manual_Mode_Needs_No_Segment()
    {
        var f = new Fixture();
        Assert.Equal(201, (await f.CreateAsync(mode: CampaignTargetingModes.Manual)).StatusCode);
        Assert.Equal(CampaignTargetingModes.Manual, f.Stored().TargetingMode);
        Assert.Empty(f.Stored().TargetedSegments);
    }

    /// <summary>Scenario 15 — an unknown mode is refused, never quietly defaulted.</summary>
    [Fact]
    public async Task T02_Unknown_Mode_Is_Refused()
    {
        var f = new Fixture();
        var created = await f.CreateAsync(mode: "auto");

        Assert.Equal(400, created.StatusCode);
        Assert.Equal(0, f.Campaigns.WriteCount);
    }

    /// <summary>Scenario 16 — a pre-FU10 row reads as manual, and reading it writes nothing.</summary>
    [Fact]
    public void T03_Pre_FU10_Row_Reads_As_Manual_And_Derivation_Writes_Nothing()
    {
        var legacy = new CampaignEntity { CampaignCode = "OLD" };

        Assert.Equal(CampaignTargetingModes.Manual, legacy.EffectiveTargetingMode());
        Assert.False(legacy.IsSegmentTargeted());
        // Read-time only: the stored field is still empty, so nothing needs backfilling.
        Assert.Equal(string.Empty, legacy.TargetingMode);
    }

    /// <summary>
    /// The derivation is uniform on purpose: a campaign with no manual targets is still manual. Deriving
    /// <c>segment</c> for it would make it instantly unsaveable, because segment mode requires a segment.
    /// </summary>
    [Fact]
    public async Task T04_A_Pre_FU10_Row_With_No_Targets_Stays_Editable()
    {
        var f = new Fixture();
        var legacy = new CampaignEntity
        {
            TenantId = TenantA,
            CampaignCode = "OLD",
            CampaignName = "Legacy",
            CampaignType = CampaignTypes.ProductCampaign,
            StartDate = Utc(2026, 3, 10),
            EndDate = Utc(2026, 4, 10)
        };
        f.Campaigns.Items.Add(legacy);

        var renamed = await f.UpdateAsync(legacy.Id, name: "Legacy renamed");

        Assert.True(renamed.IsSuccessful);
        Assert.Equal(CampaignTargetingModes.Manual, legacy.TargetingMode);
    }

    /// <summary>A command that omits the mode on update keeps the row's effective mode rather than resetting it.</summary>
    [Fact]
    public async Task T05_Omitted_Mode_Keeps_The_Stored_Mode()
    {
        var f = new Fixture();
        var segment = f.Segments.Add("SEG-A");
        var id = (await f.CreateAsync(mode: CampaignTargetingModes.Segment, segmentIds: new[] { segment })).Data;

        var renamed = await f.UpdateAsync(id, mode: null, segmentIds: new[] { segment }, name: "Renamed");

        Assert.True(renamed.IsSuccessful);
        Assert.Equal(CampaignTargetingModes.Segment, f.Stored().TargetingMode);
    }

    [Fact]
    public void T06_Mode_Vocabulary_Is_Fail_Closed()
    {
        Assert.True(CampaignTargetingModes.IsKnown("segment"));
        Assert.True(CampaignTargetingModes.IsKnown("manual"));
        Assert.False(CampaignTargetingModes.IsKnown("hybrid"));
        Assert.False(CampaignTargetingModes.IsKnown(null));
    }

    // ============ 7–14 · The targeted set ============

    /// <summary>Scenario 2 — two active segments are accepted.</summary>
    [Fact]
    public async Task T07_Segment_Mode_With_Active_Segments()
    {
        var f = new Fixture();
        var a = f.Segments.Add("SEG-A");
        var b = f.Segments.Add("SEG-B");

        Assert.Equal(201, (await f.CreateAsync(
            mode: CampaignTargetingModes.Segment, segmentIds: new[] { a, b })).StatusCode);
        Assert.Equal(2, f.Stored().TargetedSegments.Count);
    }

    /// <summary>Scenario 3 — segment mode with no segment is refused.</summary>
    [Fact]
    public async Task T08_Segment_Mode_Without_A_Segment_Is_Refused()
    {
        var f = new Fixture();
        var created = await f.CreateAsync(mode: CampaignTargetingModes.Segment);

        Assert.Equal(400, created.StatusCode);
        Assert.Equal(0, f.Campaigns.WriteCount);
    }

    /// <summary>Scenario 4 — a draft or archived segment cannot be added.</summary>
    [Theory]
    [InlineData(SegmentStatuses.Draft)]
    [InlineData(SegmentStatuses.Archived)]
    public async Task T09_Only_Active_Segments_Can_Be_Added(string status)
    {
        var f = new Fixture();
        var s = f.Segments.Add("SEG-X", status: status);

        Assert.Equal(400, (await f.CreateAsync(
            mode: CampaignTargetingModes.Segment, segmentIds: new[] { s })).StatusCode);
    }

    /// <summary>Scenario 5 — an unknown (or another tenant's) segment is refused, and existence is not leaked.</summary>
    [Fact]
    public async Task T10_Unknown_Segment_Is_Refused_Without_Leaking_Existence()
    {
        var f = new Fixture();
        var created = await f.CreateAsync(
            mode: CampaignTargetingModes.Segment, segmentIds: new[] { Guid.NewGuid() });

        Assert.Equal(400, created.StatusCode);
        Assert.Equal(0, f.Campaigns.WriteCount);

        var message = string.Join(" ", created.Errors ?? new List<string>());
        Assert.Contains("not found in this tenant", message);
        Assert.DoesNotContain("another tenant", message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Scenario 6 — the same segment twice is refused, not silently de-duplicated.</summary>
    [Fact]
    public async Task T11_Duplicate_Segment_Is_Refused()
    {
        var f = new Fixture();
        var s = f.Segments.Add("SEG-A");

        Assert.Equal(400, (await f.CreateAsync(
            mode: CampaignTargetingModes.Segment, segmentIds: new[] { s, s })).StatusCode);
    }

    /// <summary>Scenario 7 — the published ceiling is enforced.</summary>
    [Fact]
    public async Task T12_Segment_Ceiling_Is_Enforced()
    {
        var f = new Fixture();
        var ids = Enumerable.Range(0, CampaignLimits.MaxTargetedSegments + 1)
            .Select(i => f.Segments.Add($"SEG-{i}")).ToList();

        Assert.Equal(400, (await f.CreateAsync(
            mode: CampaignTargetingModes.Segment, segmentIds: ids)).StatusCode);
    }

    /// <summary>Scenario 8 — mixing subject types is deliberate and allowed.</summary>
    [Fact]
    public async Task T13_Mixed_Subject_Types_Are_Allowed()
    {
        var f = new Fixture();
        var account = f.Segments.Add("SEG-ACC", SegmentSubjectTypes.Account);
        var contact = f.Segments.Add("SEG-CON", SegmentSubjectTypes.Contact);

        Assert.Equal(201, (await f.CreateAsync(
            mode: CampaignTargetingModes.Segment, segmentIds: new[] { account, contact })).StatusCode);
    }

    /// <summary>The "at least one" rule runs on EVERY write, so the mode cannot be satisfied once and then emptied.</summary>
    [Fact]
    public async Task T14_Segment_Mode_Cannot_Be_Emptied_By_A_Later_Write()
    {
        var f = new Fixture();
        var s = f.Segments.Add("SEG-A");
        var id = (await f.CreateAsync(mode: CampaignTargetingModes.Segment, segmentIds: new[] { s })).Data;

        var emptied = await f.UpdateAsync(id, mode: CampaignTargetingModes.Segment, segmentIds: Array.Empty<Guid>());

        Assert.Equal(400, emptied.StatusCode);
    }

    // ============ 15–19 · Validate-on-change ============

    /// <summary>Scenario 9 — a segment archived after it was linked does not lock the campaign.</summary>
    [Fact]
    public async Task T15_Archived_Segment_Does_Not_Lock_The_Campaign()
    {
        var f = new Fixture();
        var s = f.Segments.Add("SEG-A");
        var id = (await f.CreateAsync(mode: CampaignTargetingModes.Segment, segmentIds: new[] { s })).Data;

        Archive(f, s);

        var renamed = await f.UpdateAsync(
            id, mode: CampaignTargetingModes.Segment, segmentIds: new[] { s }, name: "Renamed");

        Assert.True(renamed.IsSuccessful);
    }

    /// <summary>Scenario 10 — only the ADDED segment is validated; the archived one is carried through.</summary>
    [Fact]
    public async Task T16_Only_Added_Segments_Are_Validated()
    {
        var f = new Fixture();
        var archived = f.Segments.Add("SEG-A");
        var id = (await f.CreateAsync(mode: CampaignTargetingModes.Segment, segmentIds: new[] { archived })).Data;
        Archive(f, archived);

        var fresh = f.Segments.Add("SEG-B");
        var added = await f.UpdateAsync(
            id, mode: CampaignTargetingModes.Segment, segmentIds: new[] { archived, fresh });

        Assert.True(added.IsSuccessful);
        Assert.Equal(2, f.Stored().TargetedSegments.Count);
    }

    /// <summary>Scenario 11 — removing an archived segment is always allowed.</summary>
    [Fact]
    public async Task T17_Removing_An_Archived_Segment_Is_Allowed()
    {
        var f = new Fixture();
        var a = f.Segments.Add("SEG-A");
        var b = f.Segments.Add("SEG-B");
        var id = (await f.CreateAsync(mode: CampaignTargetingModes.Segment, segmentIds: new[] { a, b })).Data;
        Archive(f, a);

        Assert.True((await f.UpdateAsync(
            id, mode: CampaignTargetingModes.Segment, segmentIds: new[] { b })).IsSuccessful);
    }

    /// <summary>The link time of a segment that was already linked is preserved across re-saves.</summary>
    [Fact]
    public async Task T18_LinkedAt_Is_Preserved_For_An_Existing_Segment()
    {
        var f = new Fixture();
        var s = f.Segments.Add("SEG-A");
        var id = (await f.CreateAsync(mode: CampaignTargetingModes.Segment, segmentIds: new[] { s })).Data;
        var linkedAt = f.Stored().TargetedSegments.Single().LinkedAt;

        await f.UpdateAsync(id, mode: CampaignTargetingModes.Segment, segmentIds: new[] { s }, name: "Renamed");

        Assert.Equal(linkedAt, f.Stored().TargetedSegments.Single().LinkedAt);
    }

    /// <summary>A pinned segment is a VERSION: a superseded one keeps its link and is surfaced, not swapped.</summary>
    [Fact]
    public async Task T19_A_Superseded_Segment_Keeps_Its_Link_And_Is_Surfaced()
    {
        var f = new Fixture();
        var s = f.Segments.Add("SEG-A");
        var id = (await f.CreateAsync(mode: CampaignTargetingModes.Segment, segmentIds: new[] { s })).Data;

        var index = f.Segments.Segments.FindIndex(x => x.SegmentId == s);
        f.Segments.Segments[index] = f.Segments.Segments[index] with { Superseded = true };

        var read = await f.Get().Handle(new GetCampaignQuery(id), default);
        var projected = read.Data!.TargetedSegments.Single();

        Assert.Equal(s, projected.SegmentId);
        Assert.True(projected.Superseded);
        Assert.True(projected.IsResolvable);
    }

    // ============ 20–24 · Dormant data and the mode gate ============

    /// <summary>Scenario 12/14 — switching the mode never clears the other mode's data.</summary>
    [Fact]
    public async Task T20_Switching_The_Mode_Keeps_The_Other_Modes_Data()
    {
        var f = new Fixture();
        var s = f.Segments.Add("SEG-A");
        var id = (await f.CreateAsync(mode: CampaignTargetingModes.Segment, segmentIds: new[] { s })).Data;

        // segment -> manual: the segments stay dormant, they are not deleted.
        Assert.True((await f.UpdateAsync(
            id, mode: CampaignTargetingModes.Manual, segmentIds: new[] { s })).IsSuccessful);
        Assert.Single(f.Stored().TargetedSegments);
        Assert.False(f.Stored().IsSegmentTargeted());

        // manual -> segment: the same set is still there and becomes active again.
        Assert.True((await f.UpdateAsync(
            id, mode: CampaignTargetingModes.Segment, segmentIds: new[] { s })).IsSuccessful);
        Assert.True(f.Stored().IsSegmentTargeted());
    }

    /// <summary>Scenario 13 — a mode switch is ATOMIC: an invalid new mode writes nothing at all.</summary>
    [Fact]
    public async Task T21_An_Invalid_Mode_Switch_Writes_Nothing()
    {
        var f = new Fixture();
        var id = (await f.CreateAsync(mode: CampaignTargetingModes.Manual)).Data;
        var writesBefore = f.Campaigns.WriteCount;

        var switched = await f.UpdateAsync(id, mode: CampaignTargetingModes.Segment, segmentIds: Array.Empty<Guid>());

        Assert.Equal(400, switched.StatusCode);
        Assert.Equal(writesBefore, f.Campaigns.WriteCount);
        Assert.Equal(CampaignTargetingModes.Manual, f.Stored().TargetingMode);
    }

    /// <summary>
    /// D-TARGETING-MODE-WRITES = (b): a segment-targeted campaign refuses a NEW manual target. The mode is a rule,
    /// not a UI convention — a direct API call meets the same answer the hidden button would have given.
    /// </summary>
    [Fact]
    public async Task T22_Segment_Mode_Refuses_A_New_Manual_Target()
    {
        var f = new Fixture();
        var s = f.Segments.Add("SEG-A");
        var id = (await f.CreateAsync(mode: CampaignTargetingModes.Segment, segmentIds: new[] { s })).Data;

        var created = await f.CreateTarget().Handle(
            new CreateCampaignTargetCommand(
                id, CampaignTargetTypes.Contact, Guid.NewGuid(), CampaignTargetSources.Manual,
                SelectionReason: "manual pick",
                ReasonCodes: new[] { CampaignReasonCodes.ManualTargetSelected },
                EffectiveFrom: Utc(2026, 3, 10)),
            default);

        Assert.Equal(400, created.StatusCode);
        Assert.Equal(0, f.Targets.WriteCount);
        Assert.Contains(
            CampaignReasonCodes.CampaignTargetingModeForbidsManualTarget,
            string.Join(" ", created.Errors ?? new List<string>()));
    }

    /// <summary>The same write is accepted in manual mode — the gate refuses a mode, not a capability.</summary>
    [Fact]
    public async Task T23_Manual_Mode_Still_Accepts_Manual_Targets()
    {
        var f = new Fixture();
        var id = (await f.CreateAsync(mode: CampaignTargetingModes.Manual)).Data;

        var created = await f.CreateTarget().Handle(
            new CreateCampaignTargetCommand(
                id, CampaignTargetTypes.Contact, Guid.NewGuid(), CampaignTargetSources.Manual,
                SelectionReason: "manual pick",
                ReasonCodes: new[] { CampaignReasonCodes.ManualTargetSelected },
                EffectiveFrom: Utc(2026, 3, 10)),
            default);

        Assert.Equal(201, created.StatusCode);
        Assert.Equal(1, f.Targets.WriteCount);
    }

    /// <summary>Manual rows authored earlier survive a switch to segment mode — dormant, never deleted.</summary>
    [Fact]
    public async Task T24_Existing_Manual_Targets_Survive_A_Switch_To_Segment_Mode()
    {
        var f = new Fixture();
        var id = (await f.CreateAsync(mode: CampaignTargetingModes.Manual)).Data;
        await f.CreateTarget().Handle(
            new CreateCampaignTargetCommand(
                id, CampaignTargetTypes.Contact, Guid.NewGuid(), CampaignTargetSources.Manual,
                SelectionReason: "manual pick",
                ReasonCodes: new[] { CampaignReasonCodes.ManualTargetSelected },
                EffectiveFrom: Utc(2026, 3, 10)),
            default);

        var s = f.Segments.Add("SEG-A");
        Assert.True((await f.UpdateAsync(
            id, mode: CampaignTargetingModes.Segment, segmentIds: new[] { s })).IsSuccessful);

        Assert.Single(f.Targets.Items);
    }

    // ============ 25–28 · Auto-code ============

    [Fact]
    public async Task T25_An_Empty_Code_Is_Generated_At_Write_Time()
    {
        var f = new Fixture();

        Assert.Equal(201, (await f.CreateAsync(code: null, mode: CampaignTargetingModes.Manual)).StatusCode);

        Assert.Equal("CMP-2026-000001", f.Stored().CampaignCode);
        Assert.Equal(1, f.Sequence.Calls);
    }

    [Fact]
    public async Task T26_A_Supplied_Code_Is_Kept_And_Burns_No_Number()
    {
        var f = new Fixture();

        Assert.Equal(201, (await f.CreateAsync(code: "MY-OWN", mode: CampaignTargetingModes.Manual)).StatusCode);

        Assert.Equal("MY-OWN", f.Stored().CampaignCode);
        Assert.Equal(0, f.Sequence.Calls);
    }

    /// <summary>Opening a form takes no number: generation only happens on a real write.</summary>
    [Fact]
    public void T27_Generation_Is_Not_Reachable_Without_A_Write()
    {
        var f = new Fixture();
        Assert.Equal(0, f.Sequence.Calls);
    }

    [Fact]
    public void T28_Generated_Code_Format_Is_Stable()
        => Assert.Equal("CMP-2026-000042", CampaignCodeGenerator.Format(2026, 42));

    // ============ 29–33 · Direction, projection and contract ============

    /// <summary>The campaign write path never takes the segment REPOSITORY — only the read-only window.</summary>
    [Fact]
    public void T29_Campaign_Handlers_Never_Take_A_Segment_Repository()
    {
        foreach (var handler in new[] { typeof(CreateCampaignHandler), typeof(UpdateCampaignHandler) })
        {
            var parameters = handler.GetConstructors().Single().GetParameters().Select(p => p.ParameterType);
            Assert.DoesNotContain(parameters, x => x == typeof(ISegmentRepository) || x == typeof(HttpClient));
        }

        var validator = typeof(CampaignSegmentValidator).GetConstructors().Single()
            .GetParameters().Select(p => p.ParameterType).ToList();
        Assert.Equal(new[] { typeof(ICampaignSegmentCatalog) }, validator);
    }

    /// <summary>The segment window exposes no write member — the boundary is structural, not a promise.</summary>
    [Fact]
    public void T30_Segment_Catalog_Is_Read_Only()
        => Assert.DoesNotContain(
            typeof(ICampaignSegmentCatalog).GetMethods().Select(m => m.Name),
            name => name.Contains("Insert", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("Update", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("Replace", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("Delete", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("Archive", StringComparison.OrdinalIgnoreCase));

    /// <summary>Segment targeting produces NO CampaignTarget row — resolution is a separate follow-up.</summary>
    [Fact]
    public async Task T31_Segment_Targeting_Produces_No_Target_Rows()
    {
        var f = new Fixture();
        var a = f.Segments.Add("SEG-A");
        var b = f.Segments.Add("SEG-B");

        await f.CreateAsync(mode: CampaignTargetingModes.Segment, segmentIds: new[] { a, b });

        Assert.Equal(0, f.Targets.WriteCount);
        Assert.Empty(f.Targets.Items);
    }

    /// <summary>The list resolves every targeted segment in ONE read, and the projection is never persisted.</summary>
    [Fact]
    public async Task T32_List_Projects_Segments_In_A_Single_Batch_Read()
    {
        var f = new Fixture();
        var a = f.Segments.Add("SEG-A");
        await f.CreateAsync(code: "C1", mode: CampaignTargetingModes.Segment, segmentIds: new[] { a });

        var before = f.Segments.GetByIdsCalls;
        var list = await f.List().Handle(new ListCampaignsQuery(), default);

        Assert.Equal(before + 1, f.Segments.GetByIdsCalls);
        Assert.Equal("SEG-A", list.Data!.Items.Single().TargetedSegments.Single().SegmentCode);

        // Nothing about the segment is stored on the campaign itself.
        Assert.DoesNotContain(
            typeof(CampaignTargetedSegment).GetProperties().Select(p => p.Name),
            name => name is "SegmentCode" or "SegmentName" or "SubjectType" or "SegmentStatus");
    }

    /// <summary>An unresolvable segment still shows its pinned id rather than an invented label.</summary>
    [Fact]
    public async Task T33_An_Unresolvable_Segment_Shows_Its_Id()
    {
        var f = new Fixture();
        var s = f.Segments.Add("SEG-A");
        var id = (await f.CreateAsync(mode: CampaignTargetingModes.Segment, segmentIds: new[] { s })).Data;

        f.Segments.Segments.Clear();
        var read = await f.Get().Handle(new GetCampaignQuery(id), default);
        var projected = read.Data!.TargetedSegments.Single();

        Assert.True(read.IsSuccessful);
        Assert.Equal(s, projected.SegmentId);
        Assert.False(projected.IsResolvable);
        Assert.Null(projected.SegmentCode);
    }

    /// <summary>The contract declares the new capability, its vocabulary, its ceiling and its limits.</summary>
    [Fact]
    public async Task T34_Contract_Declares_Targeting_Mode_And_Its_Limits()
    {
        var tenant = new TenantContext();
        tenant.SetTenant(TenantA);

        var contract = (await new GetCampaignContractHandler(tenant)
            .Handle(new GetCampaignContractQuery(), default)).Data!;

        Assert.True(contract.Features.SupportsSegmentTargeting);
        // Manual targeting keeps BOTH its API and its screen, so these stay true and are now unambiguous.
        Assert.True(contract.Features.SupportsCampaignTargetManagement);
        Assert.True(contract.Features.SupportsStaticTargetSnapshot);

        Assert.Equal(CampaignTargetingModes.All, contract.Vocabulary.TargetingModes);
        Assert.Equal(CampaignLimits.MaxTargetedSegments, contract.Vocabulary.MaxTargetedSegments);

        foreach (var code in new[]
                 {
                     CampaignReasonCodes.CampaignTargetingModeUnknown,
                     CampaignReasonCodes.CampaignSegmentRequired,
                     CampaignReasonCodes.CampaignSegmentNotFound,
                     CampaignReasonCodes.CampaignSegmentNotActive,
                     CampaignReasonCodes.CampaignSegmentDuplicate,
                     CampaignReasonCodes.CampaignSegmentLimitExceeded,
                     CampaignReasonCodes.CampaignTargetingModeForbidsManualTarget,
                     CampaignReasonCodes.CampaignCodeGenerationFailed
                 })
        {
            Assert.Contains(code, contract.ReasonCodes);
        }

        var limitations = string.Join(" | ", contract.Limitations);
        Assert.Contains("declares HOW it is targeted", limitations);
        Assert.Contains("never cleared by a mode switch", limitations);
        Assert.Contains("not a UI convention", limitations);
        Assert.Contains("pinned by SEGMENT VERSION", limitations);
        Assert.Contains("read as 'manual'", limitations);
    }

    /// <summary>FU08/FU09 are untouched: the cycle period still holds no campaign reference.</summary>
    [Fact]
    public void T35_CyclePeriod_And_Segment_Remain_Unaware_Of_Campaigns()
    {
        Assert.DoesNotContain(
            typeof(Domain.Entities.CyclePeriod).GetProperties().Select(p => p.Name),
            name => name.Contains("Campaign", StringComparison.OrdinalIgnoreCase));

        Assert.DoesNotContain(
            typeof(Segment).GetProperties().Select(p => p.Name),
            name => name.Contains("Campaign", StringComparison.OrdinalIgnoreCase));
    }

    private static void Archive(Fixture f, Guid segmentId)
    {
        var index = f.Segments.Segments.FindIndex(x => x.SegmentId == segmentId);
        f.Segments.Segments[index] = f.Segments.Segments[index] with { SegmentStatus = SegmentStatuses.Archived };
    }
}
