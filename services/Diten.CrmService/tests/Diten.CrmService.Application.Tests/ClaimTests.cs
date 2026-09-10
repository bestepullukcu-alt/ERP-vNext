using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Features.ContentComposition;
using Diten.CrmService.Application.Features.ContentComposition.Claims;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using Xunit;

namespace Diten.CrmService.Application.Tests;

/// <summary>
/// SCMM-12 (CAND-CAP-0011) — Claim aggregate tests. In-memory fakes; handlers mutate the tracked reference in place
/// (Update is a no-op). Covers versioned CRUD + the approval lifecycle + approval-freeze (an approved claim's governed
/// body is frozen), duplicate-active-code, opaque evidence-ref round-trip, applicability persist, and the CAND-CAP-0011
/// audit event.
/// </summary>
public sealed class ClaimTests
{
    private static readonly Guid TenantA = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    private static readonly DateTimeOffset Jan1 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static TenantContext Tenant(Guid id)
    {
        var ctx = new TenantContext();
        ctx.SetTenant(id);
        return ctx;
    }

    private sealed class Fixture
    {
        public FakeClaimRepo Claims { get; } = new();
        public CapturingClaimAudit Audit { get; } = new();
        public Guid TenantId { get; }

        public Fixture(Guid tenant) => TenantId = tenant;

        public CreateClaimHandler Create() => new(Tenant(TenantId), new NullActorContext(), Claims, Audit);
        public UpdateClaimHandler Update() => new(Tenant(TenantId), new NullActorContext(), Claims, Audit);
        public ApproveClaimHandler Approve() => new(Tenant(TenantId), new NullActorContext(), Claims, Audit);
        public ArchiveClaimHandler Archive() => new(Tenant(TenantId), new NullActorContext(), Claims, Audit);
        public GetClaimHandler Get() => new(Tenant(TenantId), Claims);

        public async Task<Guid> SeedDraft(string code = "CL-1", string text = "Reduces risk by 30%")
        {
            var r = await Create().Handle(new CreateClaimCommand(code, "Claim " + code, text, Jan1), default);
            Assert.Equal(201, r.StatusCode);
            return r.Data;
        }
    }

    [Fact]
    public async Task Create_and_get_round_trip_incl_opaque_evidence_and_applicability()
    {
        var fx = new Fixture(TenantA);
        var policyId = Guid.NewGuid();
        var component = Guid.NewGuid();
        var created = await fx.Create().Handle(new CreateClaimCommand(
            "CL-RT", "Efficacy", "Improves outcomes", Jan1,
            Qualifiers: new[] { "vs placebo" },
            Applicability: new ClaimApplicabilityInput(
                ProductRefs: new[] { "gp-123" }, MarketRefs: new[] { "de" }, AudienceRefs: new[] { "cardiology" },
                EligibilityPolicyId: policyId),
            EvidenceRefs: new[] { "evidence://study-A", "doc:xyz" },
            ComponentRefs: new[] { component },
            ClaimVersion: "1.0"), default);
        Assert.Equal(201, created.StatusCode);

        var dto = (await fx.Get().Handle(new GetClaimQuery(created.Data), default)).Data!;
        Assert.Equal("Improves outcomes", dto.ClaimText);
        Assert.Equal(new[] { "vs placebo" }, dto.Qualifiers.ToArray());
        Assert.Equal(policyId, dto.Applicability.EligibilityPolicyId);
        Assert.Equal(new[] { "cardiology" }, dto.Applicability.AudienceRefs.ToArray());
        Assert.Equal(new[] { "evidence://study-A", "doc:xyz" }, dto.EvidenceRefs.ToArray()); // opaque, unresolved
        Assert.Contains(component, dto.ComponentRefs);
        Assert.Equal(ClaimStatuses.Draft, dto.Status);
    }

    [Fact]
    public async Task Duplicate_active_code_returns_409()
    {
        var fx = new Fixture(TenantA);
        await fx.SeedDraft("CL-DUP");
        var second = await fx.Create().Handle(new CreateClaimCommand("CL-DUP", "Other", "Text", Jan1), default);
        Assert.Equal(409, second.StatusCode);
    }

    [Fact]
    public async Task Claim_text_required()
    {
        var fx = new Fixture(TenantA);
        var r = await fx.Create().Handle(new CreateClaimCommand("CL-NT", "No text", "  ", Jan1), default);
        Assert.Equal(400, r.StatusCode);
    }

    [Fact]
    public async Task Approve_sets_approved_state_and_emits_audit()
    {
        var fx = new Fixture(TenantA);
        var id = await fx.SeedDraft("CL-AP");
        var r = await fx.Approve().Handle(new ApproveClaimCommand(id), default);
        Assert.True(r.IsSuccessful);

        var dto = (await fx.Get().Handle(new GetClaimQuery(id), default)).Data!;
        Assert.Equal(ClaimStatuses.Approved, dto.Status);
        Assert.NotNull(dto.ApprovedAt);
        Assert.Contains(fx.Audit.Events, e => e.Event == ClaimReasonCodes.Approved
            && e.EntityType == ContentCompositionAuditEntities.Claim && e.EntityId == id);
    }

    [Fact]
    public async Task Approved_claim_body_change_returns_409()
    {
        var fx = new Fixture(TenantA);
        var id = await fx.SeedDraft("CL-FRZ", "Original text");
        await fx.Approve().Handle(new ApproveClaimCommand(id), default);

        var upd = await fx.Update().Handle(new UpdateClaimCommand(id, "Claim CL-FRZ", "CHANGED text", Jan1), default);
        Assert.Equal(409, upd.StatusCode);
    }

    [Fact]
    public async Task Approved_claim_name_only_edit_is_allowed()
    {
        var fx = new Fixture(TenantA);
        var id = await fx.SeedDraft("CL-NAME", "Stable text");
        await fx.Approve().Handle(new ApproveClaimCommand(id), default);

        // Same governed body (ClaimText unchanged), only the name changes → allowed (body is what freezes).
        var upd = await fx.Update().Handle(new UpdateClaimCommand(id, "Renamed claim", "Stable text", Jan1), default);
        Assert.True(upd.IsSuccessful);
    }

    [Fact]
    public async Task Create_emits_cand_cap_0011_audit_event()
    {
        var fx = new Fixture(TenantA);
        var id = await fx.SeedDraft("CL-AUD");
        Assert.Contains(fx.Audit.Events, e => e.Event == ClaimReasonCodes.Created
            && e.EntityType == ContentCompositionAuditEntities.Claim && e.EntityId == id);
    }

    // ---------------- fakes ----------------

    private sealed class FakeClaimRepo : IClaimRepository
    {
        public List<Claim> Items { get; } = new();

        public Task<Claim?> GetByIdAsync(Guid t, Guid id, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(x => x.TenantId == t && x.Id == id && !x.IsDeleted));

        public Task<IReadOnlyList<Claim>> ListAsync(Guid t, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<Claim>)Items.Where(x => x.TenantId == t && !x.IsDeleted).ToList());

        public Task<IReadOnlyList<Claim>> ListByCodeAsync(Guid t, string code, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<Claim>)Items
                .Where(x => x.TenantId == t && !x.IsDeleted && x.ClaimCode == code).ToList());

        public Task<Claim?> GetActiveByCodeAsync(Guid t, string code, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(x =>
                x.TenantId == t && !x.IsDeleted && x.ClaimCode == code && !x.IsArchived()));

        public Task InsertAsync(Claim e, CancellationToken ct) { Items.Add(e); return Task.CompletedTask; }
        public Task UpdateAsync(Claim e, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class CapturingClaimAudit : IContentCompositionAuditPublisher
    {
        public List<(string Event, string EntityType, Guid EntityId, int Version, string? Detail)> Events { get; } = new();

        public Task PublishAsync(string eventName, Guid tenantId, string entityType, Guid entityId, int version,
            string? detail, CancellationToken cancellationToken)
        {
            Events.Add((eventName, entityType, entityId, version, detail));
            return Task.CompletedTask;
        }
    }
}
