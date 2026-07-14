using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.DocumentManagementReconciliation;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;
using Xunit;

namespace Diten.Platform.Application.Tests.DocumentManagement;

/// <summary>
/// MOD-0028-FU09 — read-back reconciliation engine, in-house read-back provider, provisioning evidence + IT/QA
/// sign-off, deviation workflow (non-destructive), and qualification readiness.
/// </summary>
public sealed class ReconciliationAndEvidenceTests
{
    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TenantB = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private const string Corr = "fu09-corr-001";

    // ── Pure engine ─────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Exact_match_produces_zero_deviations()
    {
        // Top-level nodes (no parent) so the comparison is a clean 1:1 with no structural orphans.
        var expected = new[] { Exp("ENT-01", "Quality", "Quality", null, "GQMS-Controlled") };
        var actual = new[] { Act("ENT-01", "Quality", "Quality", null, "GQMS-Controlled") };

        var devs = CollectionTreeReconciliationEngine.Compare(expected, actual);
        Assert.Empty(devs);
    }

    [Fact]
    public void Missing_expected_folder_is_detected()
    {
        var expected = new[] { Exp("ENT-01", "Quality", "Root/Quality", "Root") };
        var actual = Array.Empty<ReadBackNode>();

        var devs = CollectionTreeReconciliationEngine.Compare(expected, actual);
        Assert.Contains(devs, d => d.DeviationType == CollectionDeviationType.MissingFolder);
    }

    [Fact]
    public void Extra_actual_folder_is_detected()
    {
        var expected = Array.Empty<ExpectedNode>();
        var actual = new[] { Act("X-1", "Rogue", "Root/Rogue", "Root") };

        var devs = CollectionTreeReconciliationEngine.Compare(expected, actual);
        Assert.Contains(devs, d => d.DeviationType == CollectionDeviationType.ExtraFolder);
    }

    [Fact]
    public void Same_folder_id_different_name_is_rename_mismatch()
    {
        var expected = new[] { Exp("ENT-01", "Quality", "Root/Quality", "Root") };
        var actual = new[] { Act("ENT-01", "Quality_Renamed", "Root/Quality_Renamed", "Root") };

        var devs = CollectionTreeReconciliationEngine.Compare(expected, actual);
        Assert.Contains(devs, d => d.DeviationType == CollectionDeviationType.RenameMismatch);
    }

    [Fact]
    public void Same_folder_id_different_parent_is_move_mismatch()
    {
        var expected = new[] { Exp("ENT-01", "Quality", "Root/A/Quality", "Root/A") };
        var actual = new[] { Act("ENT-01", "Quality", "Root/B/Quality", "Root/B") };

        var devs = CollectionTreeReconciliationEngine.Compare(expected, actual);
        Assert.Contains(devs, d => d.DeviationType == CollectionDeviationType.MoveMismatch);
    }

    [Fact]
    public void Duplicate_full_path_is_detected()
    {
        var expected = new[] { Exp("ENT-01", "Quality", "Root/Quality", "Root") };
        var actual = new[]
        {
            Act("ENT-01", "Quality", "Root/Quality", "Root"),
            Act("ENT-02", "Quality", "Root/Quality", "Root")
        };

        var devs = CollectionTreeReconciliationEngine.Compare(expected, actual);
        Assert.Contains(devs, d => d.DeviationType == CollectionDeviationType.DuplicateFullPath);
    }

    [Fact]
    public void Orphan_folder_is_detected()
    {
        var expected = Array.Empty<ExpectedNode>();
        var actual = new[] { Act("ENT-09", "Child", "Root/Missing/Child", "Root/Missing") };

        var devs = CollectionTreeReconciliationEngine.Compare(expected, actual);
        Assert.Contains(devs, d => d.DeviationType == CollectionDeviationType.OrphanFolder);
    }

    [Fact]
    public void Metadata_mismatch_is_detected()
    {
        var expected = new[] { Exp("ENT-01", "Quality", "Root/Quality", "Root", accessProfile: "GQMS-Controlled") };
        var actual = new[] { Act("ENT-01", "Quality", "Root/Quality", "Root", accessProfile: "Business-Controlled") };

        var devs = CollectionTreeReconciliationEngine.Compare(expected, actual);
        Assert.Contains(devs, d => d.DeviationType == CollectionDeviationType.MetadataMismatch);
    }

    // ── In-house provider + service ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task InHouse_provider_returns_collection_instance_tree()
    {
        var env = new Env(TenantA);
        var b = env.SeedMatchedTree();
        var provider = new InHouseCollectionTreeReadBackProvider(env.Instances, env.Definitions);

        var nodes = await provider.ReadAsync(b.Id, CancellationToken.None);

        Assert.NotEmpty(nodes);
        Assert.All(nodes, n => Assert.False(string.IsNullOrWhiteSpace(n.RegisterFolderId)));
    }

    [Fact]
    public async Task Reconciliation_exact_match_is_clean()
    {
        var env = new Env(TenantA);
        var b = env.SeedMatchedTree();

        var response = await env.Recon().RunAsync(Req(b), apply: false, Corr, CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.True(response.Data!.DryRun);
        Assert.True(response.Data.IsClean);
        Assert.Empty(env.Deviations.Items); // dry-run persists nothing
    }

    [Fact]
    public async Task Apply_findings_persists_deviations_idempotently()
    {
        var env = new Env(TenantA);
        var b = env.SeedBaseline(BaselineReleaseStatus.Effective);
        env.SeedDefinition(b, "CAN-1", "ENT-01", "Quality", "Root/Quality", "GQMS-Controlled"); // no matching instance → MissingFolder

        var first = await env.Recon().RunAsync(Req(b), apply: true, Corr, CancellationToken.None);
        var countAfterFirst = env.Deviations.Items.Count;
        var second = await env.Recon().RunAsync(Req(b), apply: true, Corr, CancellationToken.None);

        Assert.True(first.Data!.Summary.MissingCount > 0);
        Assert.True(countAfterFirst > 0);
        Assert.Equal(countAfterFirst, env.Deviations.Items.Count); // no duplicates on re-run
    }

    [Fact]
    public async Task Resolving_deviation_changes_status_without_delete()
    {
        var env = new Env(TenantA);
        var d = env.SeedDeviation(env.SeedBaseline(BaselineReleaseStatus.Effective).Id);

        var response = await env.Deviation().ResolveAsync(d.Id, "Fixed via governed change", Corr, CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal("Resolved", response.Data!.Status);
        Assert.Single(env.Deviations.Items); // still present, not deleted
        Assert.Equal(DeviationStatus.Resolved, env.Deviations.Items[0].Status);
    }

    [Fact]
    public async Task Accepting_deviation_changes_status_without_delete()
    {
        var env = new Env(TenantA);
        var d = env.SeedDeviation(env.SeedBaseline(BaselineReleaseStatus.Effective).Id);

        var response = await env.Deviation().AcceptAsync(d.Id, "Accepted deviation", Corr, CancellationToken.None);

        Assert.Equal("Accepted", response.Data!.Status);
        Assert.Single(env.Deviations.Items);
    }

    [Fact]
    public async Task Evidence_upsert_creates_then_updates_same_record()
    {
        var env = new Env(TenantA);
        var b = env.SeedBaseline(BaselineReleaseStatus.Effective);
        var instanceId = Guid.NewGuid();

        var create = await env.EvidenceService().UpsertAsync(EvidenceInput(b.Id, instanceId, "Root/Quality"), Corr, CancellationToken.None);
        var update = await env.EvidenceService().UpsertAsync(EvidenceInput(b.Id, instanceId, "Root/Quality/Updated"), Corr, CancellationToken.None);

        Assert.Equal(201, create.StatusCode);
        Assert.Equal(200, update.StatusCode);
        Assert.Single(env.Evidence.Items); // same record updated, not duplicated
        Assert.Equal("Root/Quality/Updated", env.Evidence.Items[0].FullPath);
    }

    [Fact]
    public async Task Permissions_and_qa_signoff_set_fields()
    {
        var env = new Env(TenantA);
        var b = env.SeedBaseline(BaselineReleaseStatus.Effective);
        var created = await env.EvidenceService().UpsertAsync(EvidenceInput(b.Id, Guid.NewGuid(), "Root/Quality"), Corr, CancellationToken.None);
        var id = created.Data!.Id;

        var perm = await env.EvidenceService().MarkPermissionsAppliedAsync(id, Corr, CancellationToken.None);
        var qa = await env.EvidenceService().MarkQaVerifiedAsync(id, Corr, CancellationToken.None);

        Assert.True(perm.Data!.PermissionsApplied);
        Assert.False(string.IsNullOrWhiteSpace(perm.Data.PermissionsAppliedBy));
        Assert.True(qa.Data!.QaVerified);
        Assert.False(string.IsNullOrWhiteSpace(qa.Data.QaVerifiedBy));
    }

    // ── Qualification readiness ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Readiness_is_false_when_evidence_is_missing()
    {
        var env = new Env(TenantA);
        var b = env.SeedBaseline(BaselineReleaseStatus.Effective);
        env.SeedInstance(b, "CAN-1", "Quality", "Root/Quality", null); // instance without evidence

        var response = await env.Readiness().EvaluateAsync(b.Id, Corr, CancellationToken.None);

        Assert.False(response.Data!.Ready);
        Assert.True(response.Data.MissingEvidenceCount > 0);
    }

    [Fact]
    public async Task Readiness_is_false_when_open_critical_deviation_exists()
    {
        var env = new Env(TenantA);
        var b = env.SeedBaseline(BaselineReleaseStatus.Effective);
        var instance = env.SeedInstance(b, "CAN-1", "Quality", "Root/Quality", null);
        env.SeedEvidence(b.Id, instance.Id, permissions: true, qa: true);
        env.SeedDeviation(b.Id, DeviationSeverity.Critical, DeviationStatus.Open);

        var response = await env.Readiness().EvaluateAsync(b.Id, Corr, CancellationToken.None);

        Assert.False(response.Data!.Ready);
        Assert.True(response.Data.OpenBlockingDeviationCount > 0);
    }

    [Fact]
    public async Task Readiness_is_true_when_all_evidence_signed_and_no_blocking()
    {
        var env = new Env(TenantA);
        var b = env.SeedBaseline(BaselineReleaseStatus.Effective);
        var instance = env.SeedInstance(b, "CAN-1", "Quality", "Root/Quality", null);
        env.SeedEvidence(b.Id, instance.Id, permissions: true, qa: true);

        var response = await env.Readiness().EvaluateAsync(b.Id, Corr, CancellationToken.None);

        Assert.True(response.Data!.Ready);
    }

    // ── Guards ──────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Draft_baseline_dry_run_is_allowed()
    {
        var env = new Env(TenantA);
        var b = env.SeedBaseline(BaselineReleaseStatus.Draft);
        env.SeedDefinition(b, "CAN-1", "ENT-01", "Quality", "Root/Quality", "GQMS-Controlled");

        var response = await env.Recon().RunAsync(Req(b), apply: false, Corr, CancellationToken.None);
        Assert.True(response.IsSuccessful);
    }

    [Fact]
    public async Task Superseded_baseline_reconciliation_is_allowed()
    {
        var env = new Env(TenantA);
        var b = env.SeedBaseline(BaselineReleaseStatus.Superseded);

        var response = await env.Recon().RunAsync(Req(b), apply: false, Corr, CancellationToken.None);
        Assert.True(response.IsSuccessful);
    }

    [Fact]
    public async Task Cross_tenant_baseline_is_404_non_leakage()
    {
        var other = new Env(TenantA);
        var b = other.SeedBaseline(BaselineReleaseStatus.Effective);
        var env = new Env(TenantB);

        var response = await env.Recon().RunAsync(Req(b), apply: false, Corr, CancellationToken.None);

        Assert.Equal(404, response.StatusCode);
        Assert.Equal(ReconciliationReasonCodes.NotFoundNonLeakage, response.ReasonCode);
    }

    [Fact]
    public async Task Google_drive_provider_is_a_controlled_unavailable_stub()
    {
        var env = new Env(TenantA);
        var b = env.SeedBaseline(BaselineReleaseStatus.Effective);
        var request = new ReconciliationRequest(b.Id, ReconciliationScope.DefinitionToProvider, ProvisioningPlatformProvider.GoogleDrive, DryRun: true);

        var response = await env.Recon().RunAsync(request, apply: false, Corr, CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(ReconciliationReasonCodes.ProviderUnavailable, response.ReasonCode);
    }

    // ── helpers ─────────────────────────────────────────────────────────────────────────────────────

    private static ReconciliationRequest Req(BaselineRelease b) =>
        new(b.Id, ReconciliationScope.DefinitionToInstance, ProvisioningPlatformProvider.InHouse, DryRun: false);

    private static ExpectedNode Exp(string folderId, string name, string fullPath, string? parentPath, string? accessProfile = null) =>
        new(folderId, null, name, fullPath, parentPath, accessProfile, null, null, Guid.NewGuid(), null);

    private static ReadBackNode Act(string folderId, string name, string fullPath, string? parentPath, string? accessProfile = null) =>
        new(folderId, null, name, fullPath, parentPath, folderId, DateTimeOffset.UtcNow, null,
            new Dictionary<string, string?> { ["AccessProfile"] = accessProfile }, Guid.NewGuid());

    private static EvidenceUpsertInput EvidenceInput(Guid baselineId, Guid instanceId, string fullPath) =>
        new(baselineId, instanceId, null, "ENT-01", null, fullPath, ProvisioningPlatformProvider.InHouse,
            "drive-123", null, ProvisioningEvidenceStatus.Created, DateTimeOffset.UtcNow, "it", null);

    private sealed class Env
    {
        public FakeBaselineReleaseRepository Baselines { get; }
        public FakeCollectionDefinitionRepository Definitions { get; } = new();
        public FakeCollectionInstanceRepository Instances { get; } = new();
        public FakeEvidenceRepository Evidence { get; } = new();
        public FakeDeviationRepository Deviations { get; } = new();
        private readonly TenantContext _tenant = new();
        private readonly FakeCurrentUser _user = new();

        public Env(Guid tenantId)
        {
            _tenant.SetTenant(tenantId);
            Baselines = new FakeBaselineReleaseRepository(tenantId);
        }

        public CollectionTreeReconciliationService Recon() => new(
            Baselines, Definitions,
            [new InHouseCollectionTreeReadBackProvider(Instances, Definitions), new GoogleDriveCollectionTreeReadBackProvider()],
            Deviations, _tenant, _user);

        public ProvisioningEvidenceService EvidenceService() => new(Evidence, _tenant, _user);
        public DeviationWorkflowService Deviation() => new(Deviations, _tenant, _user);
        public BaselineQualificationReadinessService Readiness() => new(Baselines, Instances, Evidence, Deviations, _tenant);

        public BaselineRelease SeedMatchedTree()
        {
            // A complete top-level node (no parent) so read-back is a clean 1:1 match.
            var b = SeedBaseline(BaselineReleaseStatus.Effective);
            SeedDefinition(b, "CAN-1", "ENT-01", "Quality", "Quality", "GQMS-Controlled");
            SeedInstance(b, "CAN-1", "Quality", "Quality", null);
            return b;
        }

        public BaselineRelease SeedBaseline(BaselineReleaseStatus status)
        {
            var b = new BaselineRelease
            {
                TenantId = _tenant.TenantId,
                BaselineReleaseId = $"BR-{Guid.NewGuid():N}"[..15].ToUpperInvariant(),
                SourceBaselineKey = "GMG-QMS-LOG-0007",
                BaselineVersion = "0.8",
                Status = status
            };
            Baselines.Items.Add(b);
            return b;
        }

        public void SeedDefinition(BaselineRelease b, string canonicalId, string registerFolderId, string name, string fullPath, string accessProfile) =>
            Definitions.Items.Add(new CollectionDefinition
            {
                TenantId = _tenant.TenantId,
                BaselineReleaseId = b.Id,
                CanonicalId = canonicalId,
                Name = name,
                PathSegment = name,
                FullPath = fullPath,
                DefinitionHash = "h",
                RegisterFolderId = registerFolderId,
                AccessProfile = accessProfile
            });

        public CollectionInstance SeedInstance(BaselineRelease b, string canonicalId, string name, string fullPath, string? parentCanonical)
        {
            var i = new CollectionInstance
            {
                TenantId = _tenant.TenantId,
                InstanceKey = $"IK-{Guid.NewGuid():N}",
                CompanyId = Guid.NewGuid(),
                BaselineReleaseId = b.Id,
                CanonicalId = canonicalId,
                ParentCanonicalId = parentCanonical,
                Name = name,
                FullPath = fullPath,
                SourceDefinitionHash = "h",
                InstanceStatus = CollectionInstanceStatus.Active
            };
            Instances.Items.Add(i);
            return i;
        }

        public DocumentCollectionProvisioningEvidence SeedEvidence(Guid baselineId, Guid instanceId, bool permissions, bool qa)
        {
            var e = new DocumentCollectionProvisioningEvidence
            {
                TenantId = _tenant.TenantId,
                BaselineReleaseId = baselineId,
                CollectionInstanceId = instanceId,
                FullPath = "Root/Quality",
                PermissionsApplied = permissions,
                QaVerified = qa
            };
            Evidence.Items.Add(e);
            return e;
        }

        public DocumentCollectionDeviation SeedDeviation(Guid baselineId, DeviationSeverity severity = DeviationSeverity.Major, DeviationStatus status = DeviationStatus.Open)
        {
            var d = new DocumentCollectionDeviation
            {
                TenantId = _tenant.TenantId,
                BaselineReleaseId = baselineId,
                ExpectedFullPath = "Root/Quality",
                DeviationType = CollectionDeviationType.MissingFolder,
                Severity = severity,
                Status = status
            };
            Deviations.Items.Add(d);
            return d;
        }
    }

    private sealed class FakeCurrentUser : ICurrentUserContext
    {
        public Guid UserId => Guid.Parse("99999999-9999-9999-9999-999999999999");
        public string? Email => "qa@example.com";
        public string? DisplayName => "QA";
        public string ActorName => "qa";
        public bool IsAuthenticated => true;
    }

    private sealed class FakeBaselineReleaseRepository(Guid tenantScope) : IBaselineReleaseRepository
    {
        public List<BaselineRelease> Items { get; } = [];
        public Task<BaselineRelease> CreateAsync(BaselineRelease b, CancellationToken ct = default) { Items.Add(b); return Task.FromResult(b); }
        public Task<BaselineRelease?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(x => x.Id == id && x.TenantId == tenantScope));
        public Task<IReadOnlyList<BaselineRelease>> GetAllAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<BaselineRelease>>(Items.Where(x => x.TenantId == tenantScope).ToList());
        public Task<bool> UpdateAsync(BaselineRelease b, int expectedVersion, CancellationToken ct = default) => Task.FromResult(true);
    }

    private sealed class FakeCollectionDefinitionRepository : ICollectionDefinitionRepository
    {
        public List<CollectionDefinition> Items { get; } = [];
        public Task<CollectionDefinition> CreateAsync(CollectionDefinition d, CancellationToken ct = default) { Items.Add(d); return Task.FromResult(d); }
        public Task CreateManyAsync(IReadOnlyList<CollectionDefinition> d, CancellationToken ct = default) { Items.AddRange(d); return Task.CompletedTask; }
        public Task<IReadOnlyList<CollectionDefinition>> GetByBaselineAsync(Guid id, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<CollectionDefinition>>(Items.Where(x => x.BaselineReleaseId == id).ToList());
        public Task<CollectionDefinition?> GetByCanonicalIdAsync(Guid id, string canonicalId, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(x => x.BaselineReleaseId == id && x.CanonicalId == canonicalId));
        public Task<bool> UpdateAsync(CollectionDefinition d, int expectedVersion, CancellationToken ct = default) => Task.FromResult(true);
        public Task UpdateManyAsync(IReadOnlyList<CollectionDefinition> d, CancellationToken ct = default) => Task.CompletedTask;
        public Task<bool> SoftDeleteAsync(CollectionDefinition d, int expectedVersion, CancellationToken ct = default) => Task.FromResult(true);
    }

    private sealed class FakeCollectionInstanceRepository : ICollectionInstanceRepository
    {
        public List<CollectionInstance> Items { get; } = [];
        public Task<CollectionInstance> CreateAsync(CollectionInstance i, CancellationToken ct = default) { Items.Add(i); return Task.FromResult(i); }
        public Task<IReadOnlyList<CollectionInstance>> CreateManyAsync(IReadOnlyList<CollectionInstance> i, CancellationToken ct = default) { Items.AddRange(i); return Task.FromResult(i); }
        public Task<CollectionInstance?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(x => x.Id == id));
        public Task<CollectionInstance?> GetByInstanceKeyAsync(string key, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(x => x.InstanceKey == key));
        public Task<IReadOnlyList<CollectionInstance>> GetAllForTenantAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<CollectionInstance>>(Items.ToList());
        public Task<IReadOnlyList<CollectionInstance>> GetByCompanyAsync(Guid companyId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<CollectionInstance>>(Items.Where(x => x.CompanyId == companyId).ToList());
        public Task<IReadOnlyList<CollectionInstance>> GetByBaselineAndCompanyAsync(Guid baselineReleaseId, Guid companyId, string? instanceToken, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<CollectionInstance>>(Items.Where(x => x.BaselineReleaseId == baselineReleaseId && x.CompanyId == companyId).ToList());
        public Task<long> ArchiveManyAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default) => Task.FromResult(0L);
        public Task<long> ReactivateManyAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default) => Task.FromResult(0L);
    }

    private sealed class FakeEvidenceRepository : IProvisioningEvidenceRepository
    {
        public List<DocumentCollectionProvisioningEvidence> Items { get; } = [];
        public Task<DocumentCollectionProvisioningEvidence> CreateAsync(DocumentCollectionProvisioningEvidence e, CancellationToken ct = default) { Items.Add(e); return Task.FromResult(e); }
        public Task<DocumentCollectionProvisioningEvidence?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(x => x.Id == id));
        public Task<DocumentCollectionProvisioningEvidence?> GetByCollectionInstanceAsync(Guid instanceId, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(x => x.CollectionInstanceId == instanceId));
        public Task<IReadOnlyList<DocumentCollectionProvisioningEvidence>> GetByBaselineAsync(Guid baselineId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<DocumentCollectionProvisioningEvidence>>(Items.Where(x => x.BaselineReleaseId == baselineId).ToList());
        public Task<bool> UpdateAsync(DocumentCollectionProvisioningEvidence e, CancellationToken ct = default) { var i = Items.FindIndex(x => x.Id == e.Id); if (i >= 0) Items[i] = e; return Task.FromResult(i >= 0); }
    }

    private sealed class FakeDeviationRepository : IDocumentCollectionDeviationRepository
    {
        public List<DocumentCollectionDeviation> Items { get; } = [];
        public Task<DocumentCollectionDeviation> CreateAsync(DocumentCollectionDeviation d, CancellationToken ct = default) { Items.Add(d); return Task.FromResult(d); }
        public Task<DocumentCollectionDeviation?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(x => x.Id == id));
        public Task<IReadOnlyList<DocumentCollectionDeviation>> GetByBaselineAsync(Guid baselineId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<DocumentCollectionDeviation>>(Items.Where(x => x.BaselineReleaseId == baselineId).ToList());
        public Task<IReadOnlyList<DocumentCollectionDeviation>> GetOpenByBaselineAsync(Guid baselineId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<DocumentCollectionDeviation>>(Items.Where(x => x.BaselineReleaseId == baselineId && x.Status == DeviationStatus.Open).ToList());
        public Task<bool> UpdateAsync(DocumentCollectionDeviation d, CancellationToken ct = default) { var i = Items.FindIndex(x => x.Id == d.Id); if (i >= 0) Items[i] = d; return Task.FromResult(i >= 0); }
    }
}
