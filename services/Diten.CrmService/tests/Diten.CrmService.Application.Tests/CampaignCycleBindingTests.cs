using System.Reflection;
using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.Campaign;
using Diten.CrmService.Application.Features.Campaign.Commands;
using Diten.CrmService.Application.Features.Campaign.Contract;
using Diten.CrmService.Application.Features.Campaign.Handlers;
using Diten.CrmService.Application.Features.Campaign.Queries;
using Diten.CrmService.Application.Features.Campaign.Read;
using Diten.CrmService.Application.Features.Campaign.Services;
using Diten.CrmService.Application.Features.CyclePeriod.Read;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using Xunit;
using CampaignEntity = Diten.CrmService.Domain.Entities.Campaign;

namespace Diten.CrmService.Application.Tests;

/// <summary>
/// MOD-0165 FU08 — Campaign ↔ CyclePeriod binding. Pins down: the binding is optional and costs nothing when unused;
/// only an ACTIVE period may be bound; a period that closes afterwards keeps its bindings (and the campaign stays
/// editable); containment is inclusive on the canonical UTC day; an open-ended campaign cannot be bound; unbinding is
/// always allowed; a dangling/cross-tenant reference is refused fail-closed before any write; the projection is
/// read-time only; and the direction stays one-way (no CyclePeriod write, no HttpClient, no repository).
/// </summary>
public sealed class CampaignCycleBindingTests
{
    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TenantB = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static readonly DateTimeOffset PeriodStart = new(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset PeriodEnd = new(2026, 4, 30, 0, 0, 0, TimeSpan.Zero);

    private static DateTimeOffset Utc(int y, int m, int d, int h = 0) => new(y, m, d, h, 0, 0, TimeSpan.Zero);

    // ------------------------------------------------------------------ fixture

    private sealed class FakeCampaignRepo : ICampaignRepository
    {
        public List<CampaignEntity> Items { get; } = new();
        public int WriteCount { get; private set; }

        public Task<CampaignEntity?> GetByIdAsync(Guid t, Guid id, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(c => c.TenantId == t && c.Id == id && !c.IsDeleted));

        public Task<IReadOnlyList<CampaignEntity>> ListAsync(Guid t, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<CampaignEntity>)Items
                .Where(c => c.TenantId == t && !c.IsDeleted).ToList());

        public Task<CampaignEntity?> GetActiveByCodeAsync(Guid t, string code, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(c =>
                c.TenantId == t && !c.IsDeleted && c.CampaignCode == code && !c.IsArchived()));

        public Task<CampaignEntity?> FindByExternalReferenceAsync(
            Guid t, string sourceSystem, string externalId, CancellationToken ct)
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

    /// <summary>A read-only stand-in for the FU06/FU07 seam. It counts reads so a test can prove the unbound path
    /// never touches it, and it has no write member at all — mirroring the real interface.</summary>
    private sealed class FakeCyclePeriodReader : ICyclePeriodReader
    {
        public List<CyclePeriodSnapshot> Periods { get; } = new();
        public int GetByIdCalls { get; private set; }
        public int GetByIdsCalls { get; private set; }

        public Task<CyclePeriodResolution> ResolveActiveAsync(
            DateTimeOffset at, string? country, Guid? legalEntityId, string? businessUnitId, CancellationToken ct)
            => Task.FromResult(new CyclePeriodResolution(
                CyclePeriodResolutionOutcomes.None, null, Array.Empty<Guid>(), null, null));

        public Task<CyclePeriodSnapshot?> GetByIdAsync(Guid cyclePeriodId, CancellationToken ct)
        {
            GetByIdCalls++;
            return Task.FromResult(Periods.FirstOrDefault(p => p.CyclePeriodId == cyclePeriodId));
        }

        public Task<IReadOnlyList<CyclePeriodSnapshot>> ListByYearAsync(
            int year, string? scopeType, string? scopeRef, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<CyclePeriodSnapshot>>(Array.Empty<CyclePeriodSnapshot>());

        public Task<IReadOnlyList<CyclePeriodSnapshot>> GetByIdsAsync(
            IReadOnlyCollection<Guid> cyclePeriodIds, CancellationToken ct)
        {
            GetByIdsCalls++;
            return Task.FromResult<IReadOnlyList<CyclePeriodSnapshot>>(
                Periods.Where(p => cyclePeriodIds.Contains(p.CyclePeriodId)).ToList());
        }
    }

    private sealed class Fixture
    {
        public FakeCampaignRepo Campaigns { get; } = new();
        public FakeCyclePeriodReader Reader { get; } = new();
        private readonly Guid _tenantId;

        public Fixture(Guid tenantId) => _tenantId = tenantId;

        private TenantContext Tenant(Guid? id = null)
        {
            var ctx = new TenantContext();
            ctx.SetTenant(id ?? _tenantId);
            return ctx;
        }

        private CampaignCycleBindingGuard Guard => new(Reader);

        // FU09 - the scope gate. FU08 scenarios author no scope, so every command derives ScopeType=tenant and the
        // gate short-circuits before reading a set. The tenant-scoped periods these tests seed stay applicable, which
        // is exactly why the FU08 behaviour is unchanged.
        // The governed sets a healthy tenant has published. FU08 scenarios derive ScopeType=tenant and never reach
        // them; T34 does author a business unit, and publishing the set there is what makes the SCOPE rule - not a
        // missing-vocabulary error - the thing that refuses the binding.
        // FU10 - the targeting gate and code generator. These suites author no targeting mode, so every command
        // derives `manual` and the gate short-circuits before reading a segment: the FU08/FU09 behaviour is unchanged.
        public CampaignScopeTestDoubles.FakeSegmentCatalog SegmentCatalog { get; } = new();

        private CampaignSegmentValidator Targeting => new(SegmentCatalog);

        private ICampaignCodeGenerator CodeGenerator
            => new CampaignCodeGenerator(new CampaignScopeTestDoubles.FakeCampaignCodeSequence(), Campaigns);

        private CampaignScopeWriteValidator Scope => new(
            new CampaignScopeTestDoubles.FakeReferenceValidator()
                .Publish(CampaignScopeReferenceSets.CountrySet, "TR", "DE")
                .Publish(CampaignScopeReferenceSets.BusinessUnitSet, "alpha", "beta"),
            new CampaignScopeTestDoubles.FakeLegalEntityValidator());

        public CreateCampaignHandler Create(Guid? tenant = null)
            => new(Tenant(tenant), new NullActorContext(), Campaigns, Guard, Scope, Targeting, CodeGenerator);

        public UpdateCampaignHandler Update(Guid? tenant = null)
            => new(Tenant(tenant), new NullActorContext(), Campaigns, Guard, Scope, Targeting);

        public ListCampaignsHandler List(Guid? tenant = null) => new(Tenant(tenant), Campaigns, Reader, SegmentCatalog);

        public GetCampaignHandler Get(Guid? tenant = null) => new(Tenant(tenant), Campaigns, Reader, SegmentCatalog);

        /// <summary>Seeds a period into the read seam. Never inserts anything anywhere else — FU08 writes no period.</summary>
        public Guid SeedPeriod(string status, DateTimeOffset? start = null, DateTimeOffset? end = null, string code = "2026-C3")
        {
            var id = Guid.NewGuid();
            Reader.Periods.Add(new CyclePeriodSnapshot(
                id, code, "2026 / cycle 3", 2026, 3,
                start ?? PeriodStart, end ?? PeriodEnd, status,
                CyclePeriodScopeTypes.Tenant, null, null, null, null));
            return id;
        }

        public Task<Response<Guid>> CreateCampaignAsync(
            Guid? cyclePeriodId, DateTimeOffset start, DateTimeOffset? end, string code = "CMP-1")
            => Create().Handle(
                new CreateCampaignCommand(
                    code, "Campaign", CampaignTypes.ProductCampaign, start,
                    EndDate: end, CyclePeriodId: cyclePeriodId),
                default);

        public Task<Response<bool>> UpdateCampaignAsync(
            Guid campaignId, Guid? cyclePeriodId, DateTimeOffset start, DateTimeOffset? end,
            string name = "Campaign")
            => Update().Handle(
                new UpdateCampaignCommand(
                    campaignId, name, CampaignTypes.ProductCampaign, start,
                    EndDate: end, CyclePeriodId: cyclePeriodId),
                default);

        /// <summary>Flips a seeded period's status the way the FU06 close endpoint would — WITHOUT this feature ever
        /// writing one. It rewrites the seam's snapshot, which is exactly what a campaign would observe afterwards.</summary>
        public void ClosePeriod(Guid cyclePeriodId)
        {
            var index = Reader.Periods.FindIndex(p => p.CyclePeriodId == cyclePeriodId);
            Reader.Periods[index] = Reader.Periods[index] with { CycleStatus = CyclePeriodStatuses.Closed };
        }
    }

    // ============ 1–3 · Unbound costs nothing ============

    /// <summary>Test 1 — an unbound campaign is created and the cycle seam is never touched.</summary>
    [Fact]
    public async Task T01_Unbound_Campaign_Never_Reads_The_Cycle_Seam()
    {
        var f = new Fixture(TenantA);

        var created = await f.CreateCampaignAsync(null, Utc(2026, 1, 1), Utc(2026, 12, 31));

        Assert.Equal(201, created.StatusCode);
        Assert.Equal(0, f.Reader.GetByIdCalls);
        Assert.Null(f.Campaigns.Items.Single().CyclePeriodId);
    }

    /// <summary>Test 2 — an unbound campaign may sit on any dates at all: containment is a binding rule, not a
    /// campaign rule.</summary>
    [Fact]
    public async Task T02_Unbound_Campaign_Accepts_Any_Window()
    {
        var f = new Fixture(TenantA);

        var created = await f.CreateCampaignAsync(null, Utc(2019, 1, 1), Utc(2031, 12, 31));

        Assert.Equal(201, created.StatusCode);
    }

    /// <summary>Test 3 — an explicitly empty GUID is a caller error, not "no binding".</summary>
    [Fact]
    public async Task T03_Empty_Guid_Binding_Is_Rejected_As_Format_Error()
    {
        var f = new Fixture(TenantA);

        var created = await f.CreateCampaignAsync(Guid.Empty, Utc(2026, 3, 5), Utc(2026, 3, 20));

        Assert.Equal(400, created.StatusCode);
        Assert.Equal(0, f.Campaigns.WriteCount);
    }

    // ============ 4–7 · Bind requires an ACTIVE period ============

    /// <summary>Test 4 — binding to an active period whose window contains the campaign succeeds.</summary>
    [Fact]
    public async Task T04_Bind_To_Active_Period_Inside_Window_Succeeds()
    {
        var f = new Fixture(TenantA);
        var periodId = f.SeedPeriod(CyclePeriodStatuses.Active);

        var created = await f.CreateCampaignAsync(periodId, Utc(2026, 3, 10), Utc(2026, 4, 10));

        Assert.Equal(201, created.StatusCode);
        Assert.Equal(periodId, f.Campaigns.Items.Single().CyclePeriodId);
    }

    /// <summary>Test 5 — a draft period cannot be bound: its dates can still move, which would break containment.</summary>
    [Fact]
    public async Task T05_Bind_To_Draft_Period_Is_Refused()
    {
        var f = new Fixture(TenantA);
        var periodId = f.SeedPeriod(CyclePeriodStatuses.Draft);

        var created = await f.CreateCampaignAsync(periodId, Utc(2026, 3, 10), Utc(2026, 4, 10));

        Assert.Equal(400, created.StatusCode);
        Assert.Contains(CyclePeriodStatuses.Draft, string.Join(" ", created.Errors ?? new List<string>()));
        Assert.Equal(0, f.Campaigns.WriteCount);
    }

    /// <summary>Test 6 — a closed period cannot be bound for the FIRST time (close-resilience is about keeping an
    /// EXISTING binding, never about making a new one).</summary>
    [Fact]
    public async Task T06_New_Bind_To_Closed_Period_Is_Refused()
    {
        var f = new Fixture(TenantA);
        var periodId = f.SeedPeriod(CyclePeriodStatuses.Closed);

        var created = await f.CreateCampaignAsync(periodId, Utc(2026, 3, 10), Utc(2026, 4, 10));

        Assert.Equal(400, created.StatusCode);
        Assert.Equal(0, f.Campaigns.WriteCount);
    }

    /// <summary>Test 7 — an unknown period id is refused fail-closed and nothing is written.</summary>
    [Fact]
    public async Task T07_Unknown_Period_Is_Refused_And_Nothing_Is_Written()
    {
        var f = new Fixture(TenantA);

        var created = await f.CreateCampaignAsync(Guid.NewGuid(), Utc(2026, 3, 10), Utc(2026, 4, 10));

        Assert.Equal(400, created.StatusCode);
        Assert.Equal(0, f.Campaigns.WriteCount);
        Assert.Empty(f.Campaigns.Items);
    }

    // ============ 8–13 · Containment (B2) ============

    /// <summary>Test 8 — both ends touching the period's ends is INSIDE (inclusive on both sides).</summary>
    [Fact]
    public async Task T08_Exactly_Matching_Window_Is_Inside()
    {
        var f = new Fixture(TenantA);
        var periodId = f.SeedPeriod(CyclePeriodStatuses.Active);

        var created = await f.CreateCampaignAsync(periodId, PeriodStart, PeriodEnd);

        Assert.Equal(201, created.StatusCode);
    }

    /// <summary>Test 9 — one day before the period starts is outside.</summary>
    [Fact]
    public async Task T09_One_Day_Before_Period_Start_Is_Outside()
    {
        var f = new Fixture(TenantA);
        var periodId = f.SeedPeriod(CyclePeriodStatuses.Active);

        var created = await f.CreateCampaignAsync(periodId, Utc(2026, 2, 28), Utc(2026, 4, 10));

        Assert.Equal(400, created.StatusCode);
        Assert.Equal(0, f.Campaigns.WriteCount);
    }

    /// <summary>Test 10 — one day after the period ends is outside.</summary>
    [Fact]
    public async Task T10_One_Day_After_Period_End_Is_Outside()
    {
        var f = new Fixture(TenantA);
        var periodId = f.SeedPeriod(CyclePeriodStatuses.Active);

        var created = await f.CreateCampaignAsync(periodId, Utc(2026, 3, 10), Utc(2026, 5, 1));

        Assert.Equal(400, created.StatusCode);
    }

    /// <summary>Test 11 — a campaign that CONTAINS the period is not contained BY it.</summary>
    [Fact]
    public async Task T11_Campaign_Enclosing_The_Period_Is_Outside()
    {
        var f = new Fixture(TenantA);
        var periodId = f.SeedPeriod(CyclePeriodStatuses.Active);

        var created = await f.CreateCampaignAsync(periodId, Utc(2026, 1, 1), Utc(2026, 12, 31));

        Assert.Equal(400, created.StatusCode);
    }

    /// <summary>Test 12 — partial overlap is not containment: starting inside but ending outside is refused.</summary>
    [Fact]
    public async Task T12_Partial_Overlap_Is_Not_Containment()
    {
        var f = new Fixture(TenantA);
        var periodId = f.SeedPeriod(CyclePeriodStatuses.Active);

        var created = await f.CreateCampaignAsync(periodId, Utc(2026, 4, 20), Utc(2026, 5, 20));

        Assert.Equal(400, created.StatusCode);
    }

    /// <summary>
    /// Test 13 (AC-B2-4) — the canonical-day rule. The period stores its last day at 00:00Z; a campaign ending at
    /// 18:00Z on that same day is INSIDE. An instant-level comparison would reject a perfectly valid campaign, so this
    /// test is the one that keeps the <c>.Date</c> reduction in place.
    /// </summary>
    [Fact]
    public async Task T13_Same_Day_Later_Instant_Is_Inside_Canonical_Day()
    {
        var f = new Fixture(TenantA);
        var periodId = f.SeedPeriod(CyclePeriodStatuses.Active);

        var created = await f.CreateCampaignAsync(
            periodId, Utc(2026, 3, 1, 6), Utc(2026, 4, 30, 18));

        Assert.Equal(201, created.StatusCode);
    }

    /// <summary>Test 14 — the same instants expressed in a different offset land on the same UTC day and behave
    /// identically: the answer must not depend on the caller's timezone spelling.</summary>
    [Fact]
    public async Task T14_Offset_Does_Not_Change_The_Canonical_Day()
    {
        var f = new Fixture(TenantA);
        var periodId = f.SeedPeriod(CyclePeriodStatuses.Active);

        // 2026-04-30T21:00+03:00 == 2026-04-30T18:00Z — still the period's last day.
        var created = await f.CreateCampaignAsync(
            periodId,
            new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.FromHours(3)),
            new DateTimeOffset(2026, 4, 30, 21, 0, 0, TimeSpan.FromHours(3)));

        Assert.Equal(201, created.StatusCode);
    }

    /// <summary>Test 15 (D-OPENEND) — an open-ended campaign cannot be bound: a window with no last day can never sit
    /// inside a period that has one, and the period's end is never implied as the campaign's.</summary>
    [Fact]
    public async Task T15_Open_Ended_Campaign_Cannot_Be_Bound()
    {
        var f = new Fixture(TenantA);
        var periodId = f.SeedPeriod(CyclePeriodStatuses.Active);

        var created = await f.CreateCampaignAsync(periodId, Utc(2026, 3, 10), null);

        Assert.Equal(400, created.StatusCode);
        Assert.Equal(0, f.Campaigns.WriteCount);
    }

    /// <summary>Test 16 — but an open-ended campaign with NO binding is perfectly valid: FU08 narrowed nothing that
    /// already worked.</summary>
    [Fact]
    public async Task T16_Open_Ended_Campaign_Without_Binding_Is_Still_Valid()
    {
        var f = new Fixture(TenantA);

        var created = await f.CreateCampaignAsync(null, Utc(2026, 3, 10), null);

        Assert.Equal(201, created.StatusCode);
    }

    // ============ 17–21 · D-RECHECK: close-resilience and binding changes ============

    /// <summary>
    /// Test 17 — the heart of D-RECHECK. A campaign bound to a period that later CLOSES stays editable: the
    /// active check does not fire because the binding did not change, and the binding is kept.
    /// </summary>
    [Fact]
    public async Task T17_Closed_Period_Keeps_Its_Binding_And_Campaign_Stays_Editable()
    {
        var f = new Fixture(TenantA);
        var periodId = f.SeedPeriod(CyclePeriodStatuses.Active);
        var created = await f.CreateCampaignAsync(periodId, Utc(2026, 3, 10), Utc(2026, 4, 10));
        var campaignId = created.Data;

        f.ClosePeriod(periodId);

        var updated = await f.UpdateCampaignAsync(
            campaignId, periodId, Utc(2026, 3, 10), Utc(2026, 4, 10), name: "Renamed");

        Assert.True(updated.IsSuccessful);
        var campaign = f.Campaigns.Items.Single();
        Assert.Equal(periodId, campaign.CyclePeriodId);
        Assert.Equal(Utc(2026, 3, 10), campaign.StartDate);
        Assert.Equal(Utc(2026, 4, 10), campaign.EndDate);
    }

    /// <summary>Test 18 — containment still applies to a closed period's binding: dragging the dates out of the
    /// window is refused even though the active check is skipped.</summary>
    [Fact]
    public async Task T18_Containment_Still_Applies_After_The_Period_Closed()
    {
        var f = new Fixture(TenantA);
        var periodId = f.SeedPeriod(CyclePeriodStatuses.Active);
        var campaignId = (await f.CreateCampaignAsync(periodId, Utc(2026, 3, 10), Utc(2026, 4, 10))).Data;

        f.ClosePeriod(periodId);

        var updated = await f.UpdateCampaignAsync(campaignId, periodId, Utc(2026, 3, 10), Utc(2026, 6, 30));

        Assert.Equal(400, updated.StatusCode);
    }

    /// <summary>Test 19 — unbinding is always allowed, even from a closed period, and afterwards any window is fine.</summary>
    [Fact]
    public async Task T19_Unbind_Is_Always_Allowed_And_Lifts_The_Constraint()
    {
        var f = new Fixture(TenantA);
        var periodId = f.SeedPeriod(CyclePeriodStatuses.Active);
        var campaignId = (await f.CreateCampaignAsync(periodId, Utc(2026, 3, 10), Utc(2026, 4, 10))).Data;

        f.ClosePeriod(periodId);

        var unbound = await f.UpdateCampaignAsync(campaignId, null, Utc(2026, 3, 10), Utc(2026, 4, 10));
        Assert.True(unbound.IsSuccessful);
        Assert.Null(f.Campaigns.Items.Single().CyclePeriodId);

        var moved = await f.UpdateCampaignAsync(campaignId, null, Utc(2020, 1, 1), Utc(2030, 12, 31));
        Assert.True(moved.IsSuccessful);
    }

    /// <summary>Test 20 — CHANGING the binding re-arms the active check: moving to a draft period is refused.</summary>
    [Fact]
    public async Task T20_Changing_The_Binding_Requires_The_New_Period_To_Be_Active()
    {
        var f = new Fixture(TenantA);
        var active = f.SeedPeriod(CyclePeriodStatuses.Active);
        var draft = f.SeedPeriod(CyclePeriodStatuses.Draft, code: "2026-C4");
        var campaignId = (await f.CreateCampaignAsync(active, Utc(2026, 3, 10), Utc(2026, 4, 10))).Data;

        var updated = await f.UpdateCampaignAsync(campaignId, draft, Utc(2026, 3, 10), Utc(2026, 4, 10));

        Assert.Equal(400, updated.StatusCode);
    }

    /// <summary>Test 21 — changing the binding also re-checks containment against the NEW period.</summary>
    [Fact]
    public async Task T21_Changing_The_Binding_Rechecks_Containment_Against_The_New_Period()
    {
        var f = new Fixture(TenantA);
        var first = f.SeedPeriod(CyclePeriodStatuses.Active);
        var second = f.SeedPeriod(
            CyclePeriodStatuses.Active, Utc(2026, 5, 1), Utc(2026, 6, 30), code: "2026-C4");
        var campaignId = (await f.CreateCampaignAsync(first, Utc(2026, 3, 10), Utc(2026, 4, 10))).Data;

        var updated = await f.UpdateCampaignAsync(campaignId, second, Utc(2026, 3, 10), Utc(2026, 4, 10));

        Assert.Equal(400, updated.StatusCode);
    }

    // ============ 22–24 · Fail-closed and tenant isolation ============

    /// <summary>
    /// Test 22 — a period belonging to another tenant is invisible to the tenant-scoped read seam, which resolves it
    /// to null. The binding is then refused exactly like one pointing at a period that never existed: the message
    /// says "not found in this tenant" and NEVER reveals that the id exists elsewhere.
    /// </summary>
    [Fact]
    public async Task T22_Cross_Tenant_Period_Is_Refused_Without_Leaking_Its_Existence()
    {
        var f = new Fixture(TenantA);
        var foreignPeriodId = Guid.NewGuid(); // owned by another tenant => the seam returns null for this caller

        var created = await f.CreateCampaignAsync(foreignPeriodId, Utc(2026, 3, 10), Utc(2026, 4, 10));

        Assert.Equal(400, created.StatusCode);
        Assert.Equal(0, f.Campaigns.WriteCount);

        var message = string.Join(" ", created.Errors ?? new List<string>());
        Assert.Contains("not found in this tenant", message);
        Assert.DoesNotContain("another tenant", message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("exists", message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Test 22b — campaign reads stay tenant isolated with the binding in place.</summary>
    [Fact]
    public async Task T22b_Campaign_Reads_Stay_Tenant_Isolated()
    {
        var f = new Fixture(TenantA);
        var periodId = f.SeedPeriod(CyclePeriodStatuses.Active);
        await f.CreateCampaignAsync(periodId, Utc(2026, 3, 10), Utc(2026, 4, 10));

        var otherTenant = await f.List(TenantB).Handle(new ListCampaignsQuery(), default);

        Assert.Empty(otherTenant.Data!.Items);
    }

    /// <summary>Test 23 — a dangling reference on an already-stored campaign is refused fail-closed on the next write
    /// (the projection may go quiet, but the write path never accepts an unprovable binding).</summary>
    [Fact]
    public async Task T23_Dangling_Reference_Is_Refused_On_The_Next_Write()
    {
        var f = new Fixture(TenantA);
        var periodId = f.SeedPeriod(CyclePeriodStatuses.Active);
        var campaignId = (await f.CreateCampaignAsync(periodId, Utc(2026, 3, 10), Utc(2026, 4, 10))).Data;

        f.Reader.Periods.Clear();

        var updated = await f.UpdateCampaignAsync(campaignId, periodId, Utc(2026, 3, 10), Utc(2026, 4, 10));

        Assert.Equal(400, updated.StatusCode);
    }

    /// <summary>Test 24 — the guard's containment helper is pure and inclusive on both ends.</summary>
    [Theory]
    [InlineData(2026, 3, 1, 2026, 4, 30, true)]   // exact match
    [InlineData(2026, 3, 2, 2026, 4, 29, true)]   // strictly inside
    [InlineData(2026, 2, 28, 2026, 4, 30, false)] // starts too early
    [InlineData(2026, 3, 1, 2026, 5, 1, false)]   // ends too late
    public void T24_Containment_Helper_Is_Inclusive(
        int sy, int sm, int sd, int ey, int em, int ed, bool expected)
        => Assert.Equal(
            expected,
            CampaignCycleBindingGuard.IsWithin(Utc(sy, sm, sd), Utc(ey, em, ed), PeriodStart, PeriodEnd));

    // ============ 25–27 · Read-time projection (never persisted) ============

    /// <summary>Test 25 — the list projects the bound period in ONE seam read, not one per row.</summary>
    [Fact]
    public async Task T25_List_Projects_Periods_In_A_Single_Batch_Read()
    {
        var f = new Fixture(TenantA);
        var periodId = f.SeedPeriod(CyclePeriodStatuses.Active);
        await f.CreateCampaignAsync(periodId, Utc(2026, 3, 10), Utc(2026, 4, 10), "CMP-1");
        await f.CreateCampaignAsync(periodId, Utc(2026, 3, 11), Utc(2026, 4, 11), "CMP-2");
        await f.CreateCampaignAsync(null, Utc(2026, 3, 12), Utc(2026, 4, 12), "CMP-3");

        var before = f.Reader.GetByIdsCalls;
        var list = await f.List().Handle(new ListCampaignsQuery(), default);

        Assert.Equal(before + 1, f.Reader.GetByIdsCalls);
        Assert.Equal(3, list.Data!.Items.Count);
        Assert.All(
            list.Data.Items.Where(c => c.CampaignCode != "CMP-3"),
            c => Assert.Equal("2026-C3", c.CyclePeriod!.CycleCode));
        Assert.Null(list.Data.Items.Single(c => c.CampaignCode == "CMP-3").CyclePeriod);
    }

    /// <summary>Test 26 — a list with no bound campaigns does not read the seam at all.</summary>
    [Fact]
    public async Task T26_List_Without_Bindings_Does_Not_Read_The_Seam()
    {
        var f = new Fixture(TenantA);
        await f.CreateCampaignAsync(null, Utc(2026, 3, 12), Utc(2026, 4, 12));

        var before = f.Reader.GetByIdsCalls;
        await f.List().Handle(new ListCampaignsQuery(), default);

        Assert.Equal(before, f.Reader.GetByIdsCalls);
    }

    /// <summary>
    /// Test 27 — the projection is READ-TIME. Renaming the period changes what the campaign read shows without any
    /// campaign write, which is only possible because nothing was copied onto the campaign document.
    /// </summary>
    [Fact]
    public async Task T27_Projection_Is_Read_Time_And_Never_Persisted()
    {
        var f = new Fixture(TenantA);
        var periodId = f.SeedPeriod(CyclePeriodStatuses.Active);
        var campaignId = (await f.CreateCampaignAsync(periodId, Utc(2026, 3, 10), Utc(2026, 4, 10))).Data;

        var writesBefore = f.Campaigns.WriteCount;
        var index = f.Reader.Periods.FindIndex(p => p.CyclePeriodId == periodId);
        f.Reader.Periods[index] = f.Reader.Periods[index] with { CycleCode = "RENAMED" };

        var read = await f.Get().Handle(new GetCampaignQuery(campaignId), default);

        Assert.Equal("RENAMED", read.Data!.CyclePeriod!.CycleCode);
        Assert.Equal(writesBefore, f.Campaigns.WriteCount);

        // The stored aggregate carries the id and nothing else about the period.
        var stored = f.Campaigns.Items.Single();
        Assert.Equal(periodId, stored.CyclePeriodId);
        Assert.DoesNotContain(
            typeof(CampaignEntity).GetProperties().Select(p => p.Name),
            name => name is "CycleCode" or "CycleName" or "CycleStatus"
                    or "CyclePeriodStartDate" or "CyclePeriodEndDate");
    }

    /// <summary>Test 28 — a detail read whose period cannot be resolved still opens, with a null projection rather
    /// than an invented label.</summary>
    [Fact]
    public async Task T28_Detail_Read_Survives_An_Unresolvable_Period()
    {
        var f = new Fixture(TenantA);
        var periodId = f.SeedPeriod(CyclePeriodStatuses.Active);
        var campaignId = (await f.CreateCampaignAsync(periodId, Utc(2026, 3, 10), Utc(2026, 4, 10))).Data;

        f.Reader.Periods.Clear();
        var read = await f.Get().Handle(new GetCampaignQuery(campaignId), default);

        Assert.True(read.IsSuccessful);
        Assert.Equal(periodId, read.Data!.CyclePeriodId);
        Assert.Null(read.Data.CyclePeriod);
    }

    // ============ 29–33 · Direction and boundary ============

    /// <summary>Test 29 — the guard holds the READ seam and nothing else: no repository, no HttpClient. The direction
    /// cannot become two-way by accident.</summary>
    [Fact]
    public void T29_Guard_Holds_Only_The_Read_Seam()
    {
        var dependencies = typeof(CampaignCycleBindingGuard)
            .GetConstructors().Single()
            .GetParameters().Select(p => p.ParameterType).ToList();

        Assert.Equal(new[] { typeof(ICyclePeriodReader) }, dependencies);

        var fields = typeof(CampaignCycleBindingGuard)
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            .Select(f => f.FieldType);
        Assert.DoesNotContain(fields, t => t == typeof(HttpClient) || t == typeof(ICyclePeriodRepository));
    }

    /// <summary>Test 30 — the read seam still exposes no write method after FU08 widened it.</summary>
    [Fact]
    public void T30_Read_Seam_Stays_Read_Only()
    {
        var methods = typeof(ICyclePeriodReader).GetMethods().Select(m => m.Name).ToList();

        Assert.DoesNotContain(
            methods,
            name => name.Contains("Insert", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("Update", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("Replace", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("Delete", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("Save", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("Close", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("Activate", StringComparison.OrdinalIgnoreCase));

        // The three FU06/FU07 methods are still there — FU08 added, it did not reshape.
        Assert.Contains(nameof(ICyclePeriodReader.ResolveActiveAsync), methods);
        Assert.Contains(nameof(ICyclePeriodReader.GetByIdAsync), methods);
        Assert.Contains(nameof(ICyclePeriodReader.ListByYearAsync), methods);
        Assert.Contains(nameof(ICyclePeriodReader.GetByIdsAsync), methods);
    }

    /// <summary>Test 31 — CyclePeriod still holds no campaign reference. The pin is one-way by construction, not by
    /// convention.</summary>
    [Fact]
    public void T31_CyclePeriod_Holds_No_Campaign_Reference()
    {
        var properties = typeof(Domain.Entities.CyclePeriod).GetProperties().Select(p => p.Name).ToList();

        Assert.DoesNotContain(properties, name => name.Contains("Campaign", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Test 32 — the campaign write path holds no cycle-period REPOSITORY: it reads through the seam only.</summary>
    [Fact]
    public void T32_Campaign_Handlers_Never_Take_A_CyclePeriod_Repository()
    {
        foreach (var handler in new[] { typeof(CreateCampaignHandler), typeof(UpdateCampaignHandler) })
        {
            var parameters = handler.GetConstructors().Single().GetParameters().Select(p => p.ParameterType);
            Assert.DoesNotContain(parameters, t => t == typeof(ICyclePeriodRepository) || t == typeof(HttpClient));
        }
    }

    /// <summary>Test 33 — the contract advertises the binding, publishes the three reason codes, and says out loud
    /// both that the direction is one-way and that scope is NOT matched.</summary>
    [Fact]
    public async Task T33_Contract_Declares_The_Binding_And_Its_Limits()
    {
        var tenant = new TenantContext();
        tenant.SetTenant(TenantA);

        var contract = (await new GetCampaignContractHandler(tenant)
            .Handle(new GetCampaignContractQuery(), default)).Data!;

        Assert.True(contract.Features.SupportsCyclePeriodBinding);

        foreach (var code in new[]
                 {
                     CampaignReasonCodes.CampaignOutsideCycleWindow,
                     CampaignReasonCodes.CampaignCyclePeriodNotActive,
                     CampaignReasonCodes.CampaignCyclePeriodNotFound
                 })
        {
            Assert.Contains(code, contract.ReasonCodes);
        }

        var limitations = string.Join(" | ", contract.Limitations);
        Assert.Contains("ONE-DIRECTIONAL", limitations);
        Assert.Contains("supportsCampaignBinding flag stays false", limitations);
        // FU09 superseded the FU08 claim that the binding does not validate scope. The assertion is updated rather
        // than dropped, so the file still records that the contract makes a definite statement about scope.
        Assert.Contains("binding IS scope-aware", limitations);
        Assert.DoesNotContain("does NOT validate scope", limitations);
        Assert.Contains("never persisted", limitations);
    }

    /// <summary>
    /// Test 34 — REVISED BY MOD-0165 FU09. This test used to assert the opposite: FU08 deliberately did NOT match
    /// scope, and pinned that gap so it could not be mistaken for an oversight. FU09 revised the decision, so the test
    /// is inverted rather than deleted — the file then records that the behaviour changed on purpose, and when.
    ///
    /// <para>A campaign scoped to business unit "alpha" may not bind a period scoped to business unit "beta".</para>
    /// </summary>
    [Fact]
    public async Task T34_Scope_Is_Matched()
    {
        var f = new Fixture(TenantA);
        var periodId = Guid.NewGuid();
        f.Reader.Periods.Add(new CyclePeriodSnapshot(
            periodId, "2026-BU-BETA", "beta", 2026, 3, PeriodStart, PeriodEnd,
            CyclePeriodStatuses.Active, CyclePeriodScopeTypes.BusinessUnit, "beta", null, null, "beta"));

        var created = await f.Create().Handle(
            new CreateCampaignCommand(
                "CMP-ALPHA", "Alpha campaign", CampaignTypes.ProductCampaign, Utc(2026, 3, 10),
                BusinessUnitId: "alpha", EndDate: Utc(2026, 4, 10), CyclePeriodId: periodId),
            default);

        Assert.Equal(400, created.StatusCode);
        Assert.Equal(0, f.Campaigns.WriteCount);
        Assert.Contains(
            "does not apply",
            string.Join(" ", created.Errors ?? new List<string>()));
    }
}
