using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.DocumentManagementAccessProfileTemplates;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.Platform.Application.Tests.DocumentManagement;

/// <summary>
/// MOD-0029-FU05 — access-profile → policy template engine: catalog rules, idempotent apply, manual-policy
/// preservation, status-folder read-only automation, scope and lifecycle guards, tenant isolation.
/// </summary>
public sealed class AccessProfileTemplateTests
{
    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TenantB = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid CompanyId = Guid.Parse("30000000-0000-0000-0000-000000000001");
    private const string Corr = "fu05-corr-001";

    // ── Catalog (pure engine) ───────────────────────────────────────────────────────────────────────

    [Fact]
    public void Gqms_controlled_grants_qa_gqd_localqa()
    {
        var specs = AccessProfileTemplateCatalog.Build("GQMS-Controlled", null, null, applyStatusRules: false, out var known);
        Assert.True(known);
        Assert.Contains(specs, s => s.Role == LogicalTemplateRole.QaDocumentation && s.Effect == DocumentAccessEffect.Allow);
        Assert.Contains(specs, s => s.Role == LogicalTemplateRole.Gqd);
        Assert.Contains(specs, s => s.Role == LogicalTemplateRole.LocalQa);
    }

    [Fact]
    public void Unknown_profile_yields_no_specs_and_known_false()
    {
        var specs = AccessProfileTemplateCatalog.Build("Totally-Made-Up", null, null, applyStatusRules: true, out var known);
        Assert.False(known);
        Assert.Empty(specs);
    }

    [Fact]
    public void Effective_status_folder_denies_write_actions()
    {
        var specs = AccessProfileTemplateCatalog.Build(
            "GQMS-Controlled", AccessProfileTemplateCatalog.GqmsStatusFolderType, "Effective", applyStatusRules: true, out _);

        var denies = specs.Where(s => s.Effect == DocumentAccessEffect.Deny).ToList();
        Assert.NotEmpty(denies);
        Assert.Contains(denies, d => d.Role == LogicalTemplateRole.QaDocumentation && d.Actions.Contains(DocumentAccessMatrixAction.UploadVersion));
        // View/Download allow is still present.
        Assert.Contains(specs, s => s.Effect == DocumentAccessEffect.Allow && s.Actions.Contains(DocumentAccessMatrixAction.View));
    }

    [Fact]
    public void Draft_status_folder_stays_writable()
    {
        var specs = AccessProfileTemplateCatalog.Build(
            "GQMS-Controlled", AccessProfileTemplateCatalog.GqmsStatusFolderType, "Draft", applyStatusRules: true, out _);
        Assert.DoesNotContain(specs, s => s.Effect == DocumentAccessEffect.Deny);
    }

    [Fact]
    public void Archive_restricted_grants_only_read()
    {
        var specs = AccessProfileTemplateCatalog.Build("Archive-Restricted", null, null, applyStatusRules: false, out _);
        Assert.All(specs, s => Assert.Equal(DocumentAccessEffect.Allow, s.Effect));
        Assert.All(specs, s => Assert.All(s.Actions, a =>
            Assert.True(a is DocumentAccessMatrixAction.View or DocumentAccessMatrixAction.Download)));
    }

    [Fact]
    public void Confidential_targets_only_hr_and_legal()
    {
        var specs = AccessProfileTemplateCatalog.Build("Confidential", null, null, applyStatusRules: false, out _);
        Assert.All(specs, s => Assert.True(s.Role is LogicalTemplateRole.Hr or LogicalTemplateRole.Legal));
    }

    // ── Dry-run vs apply ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Dry_run_writes_no_policies()
    {
        var env = new Env(TenantA);
        var b = env.SeedEffectiveInstanceTree("GQMS-Controlled");

        var response = await env.Planner().RunAsync(Request(b, AccessProfileTemplateScope.Instance), apply: false, Corr, CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.True(response.Data!.DryRun);
        Assert.True(response.Data.PoliciesPlanned > 0);
        Assert.Empty(env.Policies.Items); // nothing persisted
    }

    [Fact]
    public async Task Apply_creates_generated_policies_with_metadata()
    {
        var env = new Env(TenantA);
        var b = env.SeedEffectiveInstanceTree("GQMS-Controlled");

        var response = await env.Planner().RunAsync(Request(b, AccessProfileTemplateScope.Instance), apply: true, Corr, CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.True(response.Data!.Created > 0);
        Assert.NotEmpty(env.Policies.Items);
        Assert.All(env.Policies.Items, p =>
        {
            Assert.Equal(DocumentAccessPolicySource.AccessProfileTemplate, p.PolicySource);
            Assert.Equal("GQMS-Controlled", p.PolicyTemplateKey);
            Assert.Equal(DocumentAccessTargetType.CollectionInstance, p.TargetType);
            Assert.Equal(DocumentAccessPrincipalType.Role, p.PrincipalType);
            Assert.Equal(b.Id, p.SourceBaselineReleaseId);
        });
    }

    [Fact]
    public async Task Apply_is_idempotent()
    {
        var env = new Env(TenantA);
        var b = env.SeedEffectiveInstanceTree("GQMS-Controlled");

        await env.Planner().RunAsync(Request(b, AccessProfileTemplateScope.Instance), apply: true, Corr, CancellationToken.None);
        var countAfterFirst = env.Policies.Items.Count;
        var second = await env.Planner().RunAsync(Request(b, AccessProfileTemplateScope.Instance), apply: true, Corr, CancellationToken.None);

        Assert.Equal(0, second.Data!.Created);
        Assert.True(second.Data.SkippedUnchanged > 0);
        Assert.Equal(countAfterFirst, env.Policies.Items.Count); // no duplicates
    }

    [Fact]
    public async Task Manual_policy_is_not_overwritten()
    {
        var env = new Env(TenantA);
        var b = env.SeedEffectiveInstanceTree("GQMS-Controlled");
        var instance = env.Instances.Items.First();
        // A manually authored Allow for qa-documentation on the same instance target.
        var manual = new DocumentAccessPolicyEntry
        {
            Id = Guid.NewGuid(),
            TenantId = TenantA,
            TargetType = DocumentAccessTargetType.CollectionInstance,
            TargetId = instance.Id.ToString("D"),
            PrincipalType = DocumentAccessPrincipalType.Role,
            PrincipalId = "qa-documentation",
            Actions = [DocumentAccessMatrixAction.View],
            Effect = DocumentAccessEffect.Allow,
            PolicySource = DocumentAccessPolicySource.Manual
        };
        env.Policies.Items.Add(manual);

        var response = await env.Planner().RunAsync(Request(b, AccessProfileTemplateScope.Instance), apply: true, Corr, CancellationToken.None);

        Assert.True(response.Data!.SkippedManual > 0);
        var preserved = env.Policies.Items.Single(p => p.Id == manual.Id);
        Assert.Equal(DocumentAccessPolicySource.Manual, preserved.PolicySource);
        Assert.Equal([DocumentAccessMatrixAction.View], preserved.Actions); // untouched
    }

    [Fact]
    public async Task Unknown_profile_warns_without_crashing()
    {
        var env = new Env(TenantA);
        var b = env.SeedEffectiveInstanceTree("Made-Up-Profile");

        var response = await env.Planner().RunAsync(Request(b, AccessProfileTemplateScope.Instance), apply: false, Corr, CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Contains("Made-Up-Profile", response.Data!.UnknownProfiles);
        Assert.NotEmpty(response.Data.Warnings);
    }

    [Fact]
    public async Task Missing_principal_mapping_warns_and_skips()
    {
        var env = new Env(TenantA, o => o.LocalQa = null); // unmap Local QA
        var b = env.SeedEffectiveInstanceTree("GQMS-Controlled");

        var response = await env.Planner().RunAsync(Request(b, AccessProfileTemplateScope.Instance), apply: true, Corr, CancellationToken.None);

        Assert.Contains("LocalQa", response.Data!.MissingPrincipalRoles);
        Assert.DoesNotContain(env.Policies.Items, p => p.PrincipalId == "local-qa");
    }

    [Fact]
    public async Task Effective_status_folder_gets_deny_policy_on_apply()
    {
        var env = new Env(TenantA);
        var b = env.SeedEffectiveInstanceTree("GQMS-Controlled", folderType: AccessProfileTemplateCatalog.GqmsStatusFolderType, folderName: "Effective");

        await env.Planner().RunAsync(Request(b, AccessProfileTemplateScope.Instance, applyStatusRules: true), apply: true, Corr, CancellationToken.None);

        Assert.Contains(env.Policies.Items, p => p.Effect == DocumentAccessEffect.Deny && p.Actions.Contains(DocumentAccessMatrixAction.UploadVersion));
    }

    [Fact]
    public async Task Draft_status_folder_has_no_deny_policy()
    {
        var env = new Env(TenantA);
        var b = env.SeedEffectiveInstanceTree("GQMS-Controlled", folderType: AccessProfileTemplateCatalog.GqmsStatusFolderType, folderName: "Draft");

        await env.Planner().RunAsync(Request(b, AccessProfileTemplateScope.Instance, applyStatusRules: true), apply: true, Corr, CancellationToken.None);

        Assert.DoesNotContain(env.Policies.Items, p => p.Effect == DocumentAccessEffect.Deny);
    }

    // ── Scope + lifecycle guards ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Definition_scope_dry_run_is_allowed_on_draft()
    {
        var env = new Env(TenantA);
        var b = env.SeedDraftDefinitionTree("GQMS-Controlled");

        var response = await env.Planner().RunAsync(Request(b, AccessProfileTemplateScope.Definition), apply: false, Corr, CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.True(response.Data!.PoliciesPlanned > 0);
    }

    [Fact]
    public async Task Draft_baseline_apply_is_rejected()
    {
        var env = new Env(TenantA);
        var b = env.SeedDraftDefinitionTree("GQMS-Controlled");
        // Seed an instance so scope=Instance has nodes, but the baseline is Draft.
        env.SeedInstance(b, "CAN-1");

        var response = await env.Planner().RunAsync(Request(b, AccessProfileTemplateScope.Instance), apply: true, Corr, CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
        Assert.Equal(AccessProfileTemplateReasonCodes.BaselineNotEffective, response.ReasonCode);
        Assert.Empty(env.Policies.Items);
    }

    [Fact]
    public async Task Definition_scope_apply_is_rejected()
    {
        var env = new Env(TenantA);
        var b = env.SeedEffectiveInstanceTree("GQMS-Controlled");

        var response = await env.Planner().RunAsync(Request(b, AccessProfileTemplateScope.Definition), apply: true, Corr, CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(AccessProfileTemplateReasonCodes.ScopeNotApplicable, response.ReasonCode);
    }

    [Fact]
    public async Task Legacy_published_baseline_apply_is_allowed()
    {
        var env = new Env(TenantA);
        var b = env.SeedEffectiveInstanceTree("GQMS-Controlled", status: BaselineReleaseStatus.Published);

        var response = await env.Planner().RunAsync(Request(b, AccessProfileTemplateScope.Instance), apply: true, Corr, CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.True(response.Data!.Created > 0);
    }

    [Fact]
    public async Task Cross_tenant_baseline_is_404_non_leakage()
    {
        var other = new Env(TenantA);
        var baselineOfA = other.SeedEffectiveInstanceTree("GQMS-Controlled");
        var env = new Env(TenantB); // caller is tenant B

        var response = await env.Planner().RunAsync(Request(baselineOfA, AccessProfileTemplateScope.Instance), apply: false, Corr, CancellationToken.None);

        Assert.Equal(404, response.StatusCode);
        Assert.Equal(AccessProfileTemplateReasonCodes.NotFoundNonLeakage, response.ReasonCode);
    }

    // ── helpers ─────────────────────────────────────────────────────────────────────────────────────

    private static AccessProfileTemplateRequest Request(BaselineRelease b, AccessProfileTemplateScope scope, bool applyStatusRules = true) =>
        new(b.Id, scope, null, null, applyStatusRules, DryRun: false);

    private sealed class Env
    {
        public FakeBaselineReleaseRepository Baselines { get; }
        public FakeCollectionDefinitionRepository Definitions { get; } = new();
        public FakeCollectionInstanceRepository Instances { get; } = new();
        public FakeAccessPolicyRepository Policies { get; } = new();
        private readonly TenantContext _tenant = new();
        private readonly AccessProfileTemplateOptions _options = new();

        public Env(Guid tenantId, Action<AccessProfileTemplateOptions>? configure = null)
        {
            _tenant.SetTenant(tenantId);
            Baselines = new FakeBaselineReleaseRepository(tenantId);
            configure?.Invoke(_options);
        }

        public AccessProfilePolicyPlanner Planner() =>
            new(Baselines, Definitions, Instances, Policies, _tenant, new FakeCurrentUser(), Options.Create(_options));

        public BaselineRelease SeedEffectiveInstanceTree(
            string accessProfile,
            string? folderType = null,
            string? folderName = "Node",
            BaselineReleaseStatus status = BaselineReleaseStatus.Effective)
        {
            var baseline = SeedBaseline(status);
            SeedDefinition(baseline, "CAN-1", accessProfile, folderType, folderName ?? "Node", "ENT-01");
            SeedInstance(baseline, "CAN-1");
            return baseline;
        }

        public BaselineRelease SeedDraftDefinitionTree(string accessProfile)
        {
            var baseline = SeedBaseline(BaselineReleaseStatus.Draft);
            SeedDefinition(baseline, "CAN-1", accessProfile, null, "Node", "ENT-01");
            return baseline;
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

        public void SeedDefinition(BaselineRelease b, string canonicalId, string accessProfile, string? folderType, string name, string registerFolderId) =>
            Definitions.Items.Add(new CollectionDefinition
            {
                TenantId = _tenant.TenantId,
                BaselineReleaseId = b.Id,
                CanonicalId = canonicalId,
                Name = name,
                PathSegment = name,
                FullPath = $"Root/{name}",
                DefinitionHash = "h",
                AccessProfile = accessProfile,
                FolderType = folderType,
                RegisterFolderId = registerFolderId
            });

        public void SeedInstance(BaselineRelease b, string canonicalId) =>
            Instances.Items.Add(new CollectionInstance
            {
                TenantId = _tenant.TenantId,
                InstanceKey = $"IK-{Guid.NewGuid():N}",
                CompanyId = CompanyId,
                BaselineReleaseId = b.Id,
                CanonicalId = canonicalId,
                Name = "Node",
                FullPath = "Root/Node",
                SourceDefinitionHash = "h",
                InstanceStatus = CollectionInstanceStatus.Active
            });
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
        public Task<BaselineRelease> CreateAsync(BaselineRelease baseline, CancellationToken ct = default) { Items.Add(baseline); return Task.FromResult(baseline); }
        public Task<BaselineRelease?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(x => x.Id == id && x.TenantId == tenantScope));
        public Task<IReadOnlyList<BaselineRelease>> GetAllAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<BaselineRelease>>(Items.Where(x => x.TenantId == tenantScope).ToList());
        public Task<bool> UpdateAsync(BaselineRelease baseline, int expectedVersion, CancellationToken ct = default) => Task.FromResult(true);
    }

    private sealed class FakeCollectionDefinitionRepository : ICollectionDefinitionRepository
    {
        public List<CollectionDefinition> Items { get; } = [];
        public Task<CollectionDefinition> CreateAsync(CollectionDefinition d, CancellationToken ct = default) { Items.Add(d); return Task.FromResult(d); }
        public Task CreateManyAsync(IReadOnlyList<CollectionDefinition> d, CancellationToken ct = default) { Items.AddRange(d); return Task.CompletedTask; }
        public Task<IReadOnlyList<CollectionDefinition>> GetByBaselineAsync(Guid baselineReleaseId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<CollectionDefinition>>(Items.Where(x => x.BaselineReleaseId == baselineReleaseId).ToList());
        public Task<CollectionDefinition?> GetByCanonicalIdAsync(Guid baselineReleaseId, string canonicalId, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(x => x.BaselineReleaseId == baselineReleaseId && x.CanonicalId == canonicalId));
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
        public Task<CollectionInstance?> GetByInstanceKeyAsync(string instanceKey, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(x => x.InstanceKey == instanceKey));
        public Task<IReadOnlyList<CollectionInstance>> GetAllForTenantAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<CollectionInstance>>(Items.ToList());
        public Task<IReadOnlyList<CollectionInstance>> GetByCompanyAsync(Guid companyId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<CollectionInstance>>(Items.Where(x => x.CompanyId == companyId).ToList());
        public Task<IReadOnlyList<CollectionInstance>> GetByBaselineAndCompanyAsync(Guid baselineReleaseId, Guid companyId, string? instanceToken, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<CollectionInstance>>(Items.Where(x => x.BaselineReleaseId == baselineReleaseId && x.CompanyId == companyId).ToList());
        public Task<long> ArchiveManyAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default) => Task.FromResult(0L);
        public Task<long> ReactivateManyAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default) => Task.FromResult(0L);
    }

    private sealed class FakeAccessPolicyRepository : IDocumentAccessPolicyRepository
    {
        public List<DocumentAccessPolicyEntry> Items { get; } = [];
        public Task<DocumentAccessPolicyEntry> CreateAsync(DocumentAccessPolicyEntry e, CancellationToken ct = default) { Items.Add(e); return Task.FromResult(e); }
        public Task<DocumentAccessPolicyEntry?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(x => x.Id == id && !x.IsDeleted));
        public Task<IReadOnlyList<DocumentAccessPolicyEntry>> ListAsync(string? tt, string? ti, string? pt, string? pi, string? ef, string? ac, string? st, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<DocumentAccessPolicyEntry>>(Items.Where(x => !x.IsDeleted).ToList());
        public Task<IReadOnlyList<DocumentAccessPolicyEntry>> GetByTargetsAsync(IReadOnlyList<(DocumentAccessTargetType TargetType, string TargetId)> targets, CancellationToken ct = default)
        {
            var set = targets.Select(t => $"{t.TargetType}:{t.TargetId}".ToLowerInvariant()).ToHashSet();
            return Task.FromResult<IReadOnlyList<DocumentAccessPolicyEntry>>(Items.Where(x => !x.IsDeleted && set.Contains($"{x.TargetType}:{x.TargetId}".ToLowerInvariant())).ToList());
        }
        public Task<DocumentAccessPolicyEntry?> FindDuplicateAsync(DocumentAccessTargetType tt, string ti, DocumentAccessPrincipalType pt, string pi, DocumentAccessEffect ef, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(x => !x.IsDeleted && x.TargetType == tt && x.TargetId == ti && x.PrincipalType == pt && x.PrincipalId == pi && x.Effect == ef));
        public Task<bool> UpdateAsync(DocumentAccessPolicyEntry e, CancellationToken ct = default) { var i = Items.FindIndex(x => x.Id == e.Id); if (i >= 0) Items[i] = e; return Task.FromResult(i >= 0); }
        public Task SoftDeleteAsync(Guid id, CancellationToken ct = default) { var e = Items.FirstOrDefault(x => x.Id == id); if (e is not null) e.IsDeleted = true; return Task.CompletedTask; }
        public Task<int> BulkSoftDeleteAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default) => Task.FromResult(0);
    }
}
