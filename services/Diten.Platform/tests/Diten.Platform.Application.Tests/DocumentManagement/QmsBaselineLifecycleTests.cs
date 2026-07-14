using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.DocumentManagementQmsBaseline;
using Diten.Platform.Application.Features.DocumentManagementQmsBaseline.Commands;
using Diten.Platform.Application.Features.DocumentManagementQmsBaseline.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.DocumentManagementQmsBaseline.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;
using Xunit;

namespace Diten.Platform.Application.Tests.DocumentManagement;

/// <summary>
/// MOD-0028-FU08 — Draft → Approved → Effective → Superseded lifecycle, the single-effective-per-source-key rule,
/// the package-status gate, backward-compatible legacy Published, and tenant isolation.
/// </summary>
public sealed class QmsBaselineLifecycleTests
{
    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TenantB = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private const string SourceKey = "GMG-QMS-LOG-0007";
    private const string Correlation = "fu08-corr-001";

    // ── Approve (Draft → Approved) ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Approve_transitions_draft_to_approved_and_freezes_manifest()
    {
        var env = new Env(TenantA);
        var baseline = env.SeedBaseline(BaselineReleaseStatus.Draft, definitionCount: 3);

        var response = await env.ApproveHandler().Handle(
            new ApproveQmsBaselineCommand(baseline.Id, baseline.Version, "APPR-2026-001", "Reviewed by QA", Correlation),
            CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal("APPROVED", response.Data!.Status);
        Assert.Equal(BaselineReleaseStatus.Approved, baseline.Status);
        Assert.NotNull(baseline.ApprovedAt);
        Assert.Equal("APPR-2026-001", baseline.ApprovalReference);
        Assert.False(string.IsNullOrWhiteSpace(baseline.SnapshotHash));
        Assert.NotNull(baseline.ManifestId);
        Assert.Single(env.Manifests.Created);
    }

    [Fact]
    public async Task Approve_rejects_non_draft_baseline()
    {
        var env = new Env(TenantA);
        var baseline = env.SeedBaseline(BaselineReleaseStatus.Approved, definitionCount: 2);

        var response = await env.ApproveHandler().Handle(
            new ApproveQmsBaselineCommand(baseline.Id, 0, null, null, Correlation), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
        Assert.Equal(QmsBaselineReasonCodes.ValidationFailed, response.ReasonCode);
    }

    [Fact]
    public async Task Approve_cross_tenant_is_404_non_leakage()
    {
        var env = new Env(TenantB); // caller is tenant B
        var baselineOfA = new Env(TenantA).SeedBaseline(BaselineReleaseStatus.Draft, definitionCount: 1);

        var response = await env.ApproveHandler().Handle(
            new ApproveQmsBaselineCommand(baselineOfA.Id, 0, null, null, Correlation), CancellationToken.None);

        Assert.Equal(404, response.StatusCode);
        Assert.Equal(QmsBaselineReasonCodes.NotFoundNonLeakage, response.ReasonCode);
    }

    // ── Mark Effective (Approved → Effective) ───────────────────────────────────────────────────────

    [Fact]
    public async Task Mark_effective_transitions_approved_to_effective()
    {
        var env = new Env(TenantA);
        var baseline = env.SeedBaseline(BaselineReleaseStatus.Approved, definitionCount: 2);

        var response = await env.MarkEffectiveHandler().Handle(
            new MarkEffectiveQmsBaselineCommand(baseline.Id, baseline.Version, Correlation), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal("EFFECTIVE", response.Data!.Status);
        Assert.Equal(BaselineReleaseStatus.Effective, baseline.Status);
        Assert.NotNull(baseline.EffectiveAt);
    }

    [Fact]
    public async Task Draft_cannot_be_marked_effective_directly()
    {
        var env = new Env(TenantA);
        var baseline = env.SeedBaseline(BaselineReleaseStatus.Draft, definitionCount: 2);

        var response = await env.MarkEffectiveHandler().Handle(
            new MarkEffectiveQmsBaselineCommand(baseline.Id, 0, Correlation), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
        Assert.Equal(QmsBaselineReasonCodes.ValidationFailed, response.ReasonCode);
    }

    [Fact]
    public async Task Superseded_cannot_be_marked_effective()
    {
        var env = new Env(TenantA);
        var baseline = env.SeedBaseline(BaselineReleaseStatus.Superseded, definitionCount: 2);

        var response = await env.MarkEffectiveHandler().Handle(
            new MarkEffectiveQmsBaselineCommand(baseline.Id, 0, Correlation), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
    }

    [Fact]
    public async Task Effective_cannot_be_re_approved_or_reverted()
    {
        var env = new Env(TenantA);
        var baseline = env.SeedBaseline(BaselineReleaseStatus.Effective, definitionCount: 2);

        var approve = await env.ApproveHandler().Handle(
            new ApproveQmsBaselineCommand(baseline.Id, 0, null, null, Correlation), CancellationToken.None);
        var effectiveAgain = await env.MarkEffectiveHandler().Handle(
            new MarkEffectiveQmsBaselineCommand(baseline.Id, 0, Correlation), CancellationToken.None);

        Assert.Equal(400, approve.StatusCode);        // Effective → Approved rejected
        Assert.Equal(400, effectiveAgain.StatusCode); // Effective → Effective rejected
        Assert.Equal(BaselineReleaseStatus.Effective, baseline.Status);
    }

    // ── Single effective per source key + non-destructive supersede ─────────────────────────────────

    [Fact]
    public async Task Marking_new_effective_supersedes_previous_effective_of_same_source_key()
    {
        var env = new Env(TenantA);
        var previous = env.SeedBaseline(BaselineReleaseStatus.Effective, definitionCount: 2, version: "0.7");
        previous.EffectiveAt = DateTimeOffset.UtcNow.AddDays(-10);
        var next = env.SeedBaseline(BaselineReleaseStatus.Approved, definitionCount: 2, version: "0.8");

        var response = await env.MarkEffectiveHandler().Handle(
            new MarkEffectiveQmsBaselineCommand(next.Id, next.Version, Correlation), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(BaselineReleaseStatus.Effective, next.Status);
        Assert.Equal(BaselineReleaseStatus.Superseded, previous.Status); // non-destructive
        Assert.Equal(next.Id, previous.SupersededByBaselineReleaseId);
        Assert.Equal(previous.Id, next.SupersedesBaselineReleaseId);

        // Exactly one Effective baseline remains for the source key; the old one is still readable (not deleted).
        var all = await env.Baselines.GetAllAsync(CancellationToken.None);
        Assert.Single(all.Where(b => b.SourceBaselineKey == SourceKey && b.Status == BaselineReleaseStatus.Effective));
        Assert.NotNull(await env.Baselines.GetByIdAsync(previous.Id, CancellationToken.None));
    }

    // ── Package status gate ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Draft_package_status_blocks_mark_effective()
    {
        var env = new Env(TenantA);
        var baseline = env.SeedBaseline(BaselineReleaseStatus.Approved, definitionCount: 2);
        baseline.SourcePackageStatus = "Draft — do not execute until approved";

        var response = await env.MarkEffectiveHandler().Handle(
            new MarkEffectiveQmsBaselineCommand(baseline.Id, baseline.Version, Correlation), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
        Assert.Equal(QmsBaselineReasonCodes.PackageNotApproved, response.ReasonCode);
        Assert.Equal(BaselineReleaseStatus.Approved, baseline.Status); // unchanged
    }

    [Fact]
    public async Task Approved_package_status_allows_mark_effective()
    {
        var env = new Env(TenantA);
        var baseline = env.SeedBaseline(BaselineReleaseStatus.Approved, definitionCount: 2);
        baseline.SourcePackageStatus = "Approved";

        var response = await env.MarkEffectiveHandler().Handle(
            new MarkEffectiveQmsBaselineCommand(baseline.Id, baseline.Version, Correlation), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(BaselineReleaseStatus.Effective, baseline.Status);
    }

    // ── Concurrency + tenant isolation ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Stale_expected_version_conflicts_on_approve()
    {
        var env = new Env(TenantA);
        var baseline = env.SeedBaseline(BaselineReleaseStatus.Draft, definitionCount: 2);

        var response = await env.ApproveHandler().Handle(
            new ApproveQmsBaselineCommand(baseline.Id, baseline.Version + 5, null, null, Correlation), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
        Assert.Equal(QmsBaselineReasonCodes.Conflict, response.ReasonCode);
    }

    [Fact]
    public async Task Tenant_a_mark_effective_does_not_supersede_tenant_b_effective()
    {
        // Tenant B already has an Effective baseline with the same source key.
        var envB = new Env(TenantB);
        var effectiveB = envB.SeedBaseline(BaselineReleaseStatus.Effective, definitionCount: 2);

        // Tenant A promotes its own Approved baseline; it must not touch tenant B's data.
        var envA = new Env(TenantA);
        var approvedA = envA.SeedBaseline(BaselineReleaseStatus.Approved, definitionCount: 2);

        var response = await envA.MarkEffectiveHandler().Handle(
            new MarkEffectiveQmsBaselineCommand(approvedA.Id, approvedA.Version, Correlation), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(BaselineReleaseStatus.Effective, approvedA.Status);
        Assert.Equal(BaselineReleaseStatus.Effective, effectiveB.Status); // untouched
        Assert.Null(effectiveB.SupersededByBaselineReleaseId);
    }

    // ── Pure helpers (guard logic) ──────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(BaselineReleaseStatus.Effective, true)]
    [InlineData(BaselineReleaseStatus.Published, true)] // legacy backward compatibility
    [InlineData(BaselineReleaseStatus.Draft, false)]
    [InlineData(BaselineReleaseStatus.Approved, false)]
    [InlineData(BaselineReleaseStatus.Superseded, false)]
    public void IsInstantiable_matches_lifecycle(BaselineReleaseStatus status, bool expected) =>
        Assert.Equal(expected, status.IsInstantiable());

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("Approved", true)]
    [InlineData("Effective", true)]
    [InlineData("Draft", false)]
    [InlineData("Draft — do not execute until approved", false)]
    [InlineData("Not for execution", false)]
    public void Package_status_gate_matches_expectation(string? status, bool expected) =>
        Assert.Equal(expected, BaselinePackageStatus.AllowsEffective(status));

    // ── environment ─────────────────────────────────────────────────────────────────────────────────

    private sealed class Env
    {
        public FakeBaselineReleaseRepository Baselines { get; }
        public FakeCollectionDefinitionRepository Definitions { get; } = new();
        public FakeManifestRepository Manifests { get; } = new();
        private readonly TenantContext _tenant = new();
        private readonly FakeCurrentUser _user = new();

        public Env(Guid tenantId)
        {
            _tenant.SetTenant(tenantId);
            Baselines = new FakeBaselineReleaseRepository(tenantId);
        }

        public ApproveQmsBaselineHandler ApproveHandler() =>
            new(Baselines, Definitions, Manifests, new BaselineSnapshotHasher(), _tenant, _user);

        public MarkEffectiveQmsBaselineHandler MarkEffectiveHandler() =>
            new(Baselines, Definitions, _tenant, _user);

        public BaselineRelease SeedBaseline(BaselineReleaseStatus status, int definitionCount, string version = "0.8")
        {
            var tenantId = _tenant.TenantId;
            var baseline = new BaselineRelease
            {
                TenantId = tenantId,
                BaselineReleaseId = $"BR-{Guid.NewGuid():N}"[..15].ToUpperInvariant(),
                SourceBaselineKey = SourceKey,
                BaselineVersion = version,
                Status = status,
                Version = 1
            };
            Baselines.Items.Add(baseline);

            for (var i = 0; i < definitionCount; i++)
            {
                Definitions.Items.Add(new CollectionDefinition
                {
                    TenantId = tenantId,
                    BaselineReleaseId = baseline.Id,
                    CanonicalId = $"CAN-QMS-{Guid.NewGuid():N}"[..20].ToUpperInvariant(),
                    Name = $"Node{i}",
                    PathSegment = $"Node{i}",
                    FullPath = $"Root/Node{i}",
                    DisplayOrder = i,
                    DefinitionHash = $"hash{i}"
                });
            }

            return baseline;
        }
    }

    private sealed class FakeCurrentUser : ICurrentUserContext
    {
        public Guid UserId => Guid.Parse("99999999-9999-9999-9999-999999999999");
        public string? Email => "qa@example.com";
        public string? DisplayName => "QA Reviewer";
        public string ActorName => "qa-reviewer";
        public bool IsAuthenticated => true;
    }

    private sealed class FakeBaselineReleaseRepository(Guid tenantScope) : IBaselineReleaseRepository
    {
        public List<BaselineRelease> Items { get; } = [];

        public Task<BaselineRelease> CreateAsync(BaselineRelease baseline, CancellationToken ct = default)
        {
            Items.Add(baseline);
            return Task.FromResult(baseline);
        }

        // Tenant-scoped like the production repository (non-leakage for other tenants).
        public Task<BaselineRelease?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(x => x.Id == id && x.TenantId == tenantScope));

        public Task<IReadOnlyList<BaselineRelease>> GetAllAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<BaselineRelease>>(Items.Where(x => x.TenantId == tenantScope).ToList());

        public Task<bool> UpdateAsync(BaselineRelease baseline, int expectedVersion, CancellationToken ct = default)
        {
            if (baseline.Version != expectedVersion)
            {
                return Task.FromResult(false);
            }

            baseline.Version = expectedVersion + 1;
            return Task.FromResult(true);
        }
    }

    private sealed class FakeCollectionDefinitionRepository : ICollectionDefinitionRepository
    {
        public List<CollectionDefinition> Items { get; } = [];

        public Task<CollectionDefinition> CreateAsync(CollectionDefinition definition, CancellationToken ct = default)
        {
            Items.Add(definition);
            return Task.FromResult(definition);
        }

        public Task CreateManyAsync(IReadOnlyList<CollectionDefinition> definitions, CancellationToken ct = default)
        {
            Items.AddRange(definitions);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<CollectionDefinition>> GetByBaselineAsync(Guid baselineReleaseId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<CollectionDefinition>>(Items.Where(x => x.BaselineReleaseId == baselineReleaseId).ToList());

        public Task<CollectionDefinition?> GetByCanonicalIdAsync(Guid baselineReleaseId, string canonicalId, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(x => x.BaselineReleaseId == baselineReleaseId && x.CanonicalId == canonicalId));

        public Task<bool> UpdateAsync(CollectionDefinition definition, int expectedVersion, CancellationToken ct = default) => Task.FromResult(true);

        public Task UpdateManyAsync(IReadOnlyList<CollectionDefinition> definitions, CancellationToken ct = default) => Task.CompletedTask;

        public Task<bool> SoftDeleteAsync(CollectionDefinition definition, int expectedVersion, CancellationToken ct = default) => Task.FromResult(true);
    }

    private sealed class FakeManifestRepository : IBaselineSnapshotManifestRepository
    {
        public List<BaselineSnapshotManifest> Created { get; } = [];

        public Task<BaselineSnapshotManifest> CreateAsync(BaselineSnapshotManifest manifest, CancellationToken ct = default)
        {
            Created.Add(manifest);
            return Task.FromResult(manifest);
        }

        public Task<BaselineSnapshotManifest?> GetByBaselineAsync(Guid baselineReleaseId, CancellationToken ct = default) =>
            Task.FromResult(Created.FirstOrDefault(x => x.BaselineReleaseId == baselineReleaseId));
    }
}
