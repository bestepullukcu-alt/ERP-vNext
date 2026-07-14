using Diten.Platform.Application.Features.DocumentManagementAccessMatrix;
using Diten.Platform.Application.Features.DocumentManagementAccessMatrix.Services;
using Diten.Platform.Application.Features.DocumentManagementControlledDocuments.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.Platform.Application.Tests.DocumentManagement;

// MOD-0029-FU04 — access matrix resolver + service tests (in-memory fakes, no Mongo). The fixed target is a
// CollectionInstance folder; its ancestry resolves to [folder(0), structure(1), company(2), tenant(3)].
public sealed class DocumentAccessMatrixTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly Guid CompanyId = Guid.Parse("aaaaaaaa-1111-2222-3333-444444444444");
    private static readonly Guid BaselineId = Guid.Parse("bbbbbbbb-1111-2222-3333-444444444444");
    private static readonly Guid FolderId = Guid.Parse("cccccccc-1111-2222-3333-444444444444");
    private static readonly Guid RoleId = Guid.Parse("dddddddd-1111-2222-3333-444444444444");
    private const string Corr = "fu04-corr-1";

    // ── Resolver: deny precedence + inheritance ────────────────────────────────

    [Fact]
    public async Task Inherited_company_allow_applies_to_folder_when_no_deny()
    {
        var f = Fixture();
        f.SeedPolicy(DocumentAccessTargetType.Company, CompanyId, DocumentAccessEffect.Allow, inherit: true, DocumentAccessMatrixAction.View);

        var eff = await f.Resolver.ResolveAsync(DocumentAccessTargetType.CollectionInstance, FolderId.ToString("D"),
            DocumentAccessPrincipalType.Role, RoleId.ToString("D"), CancellationToken.None);

        Assert.Contains("View", eff.AllowedActions);
    }

    [Fact]
    public async Task Folder_deny_overrides_parent_company_allow()
    {
        var f = Fixture();
        f.SeedPolicy(DocumentAccessTargetType.Company, CompanyId, DocumentAccessEffect.Allow, inherit: true, DocumentAccessMatrixAction.View);
        f.SeedPolicy(DocumentAccessTargetType.CollectionInstance, FolderId, DocumentAccessEffect.Deny, inherit: true, DocumentAccessMatrixAction.View);

        var eff = await f.Resolver.ResolveAsync(DocumentAccessTargetType.CollectionInstance, FolderId.ToString("D"),
            DocumentAccessPrincipalType.Role, RoleId.ToString("D"), CancellationToken.None);

        Assert.DoesNotContain("View", eff.AllowedActions);
    }

    [Fact]
    public async Task Non_inheriting_company_policy_does_not_reach_folder()
    {
        var f = Fixture();
        f.SeedPolicy(DocumentAccessTargetType.Company, CompanyId, DocumentAccessEffect.Allow, inherit: false, DocumentAccessMatrixAction.View);

        var eff = await f.Resolver.ResolveAsync(DocumentAccessTargetType.CollectionInstance, FolderId.ToString("D"),
            DocumentAccessPrincipalType.Role, RoleId.ToString("D"), CancellationToken.None);

        Assert.DoesNotContain("View", eff.AllowedActions);
    }

    [Fact]
    public async Task Expired_policy_is_ignored()
    {
        var f = Fixture();
        f.SeedPolicy(DocumentAccessTargetType.CollectionInstance, FolderId, DocumentAccessEffect.Allow, inherit: true,
            actions: new[] { DocumentAccessMatrixAction.View }, validTo: DateTimeOffset.UtcNow.AddDays(-1));

        var eff = await f.Resolver.ResolveAsync(DocumentAccessTargetType.CollectionInstance, FolderId.ToString("D"),
            DocumentAccessPrincipalType.Role, RoleId.ToString("D"), CancellationToken.None);

        Assert.DoesNotContain("View", eff.AllowedActions);
    }

    [Fact]
    public async Task Disabled_policy_is_ignored()
    {
        var f = Fixture();
        f.SeedPolicy(DocumentAccessTargetType.CollectionInstance, FolderId, DocumentAccessEffect.Allow, inherit: true,
            actions: new[] { DocumentAccessMatrixAction.View }, status: DocumentAccessPolicyStatus.Disabled);

        var eff = await f.Resolver.ResolveAsync(DocumentAccessTargetType.CollectionInstance, FolderId.ToString("D"),
            DocumentAccessPrincipalType.Role, RoleId.ToString("D"), CancellationToken.None);

        Assert.DoesNotContain("View", eff.AllowedActions);
    }

    [Fact]
    public async Task CreateTemplate_requires_explicit_action()
    {
        var f = Fixture();
        f.SeedPolicy(DocumentAccessTargetType.CollectionInstance, FolderId, DocumentAccessEffect.Allow, inherit: true,
            DocumentAccessMatrixAction.View, DocumentAccessMatrixAction.CreateTemplate);

        var eff = await f.Resolver.ResolveAsync(DocumentAccessTargetType.CollectionInstance, FolderId.ToString("D"),
            DocumentAccessPrincipalType.Role, RoleId.ToString("D"), CancellationToken.None);

        Assert.Contains("CreateTemplate", eff.AllowedActions);
        Assert.DoesNotContain("UploadVersion", eff.AllowedActions);
    }

    // ── MOD-0029-FU05 — generated (access-profile template) policies at runtime ────────────────

    [Fact]
    public async Task Generated_instance_allow_grants_runtime_access()
    {
        var f = Fixture();
        f.Policies.Items.Add(new DocumentAccessPolicyEntry
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            TargetType = DocumentAccessTargetType.CollectionInstance,
            TargetId = FolderId.ToString("D"),
            PrincipalType = DocumentAccessPrincipalType.Role,
            PrincipalId = RoleId.ToString("D"),
            Actions = [DocumentAccessMatrixAction.View, DocumentAccessMatrixAction.Download],
            Effect = DocumentAccessEffect.Allow,
            InheritFromParent = true,
            PolicySource = DocumentAccessPolicySource.AccessProfileTemplate,
            PolicyTemplateKey = "GQMS-Controlled"
        });

        var eff = await f.Resolver.ResolveAsync(DocumentAccessTargetType.CollectionInstance, FolderId.ToString("D"),
            DocumentAccessPrincipalType.Role, RoleId.ToString("D"), CancellationToken.None);

        Assert.Contains("View", eff.AllowedActions);
        Assert.Contains("Download", eff.AllowedActions);
    }

    [Fact]
    public async Task Manual_deny_wins_over_generated_allow_on_same_target()
    {
        var f = Fixture();
        // Generated Allow (from the template engine) …
        f.Policies.Items.Add(new DocumentAccessPolicyEntry
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            TargetType = DocumentAccessTargetType.CollectionInstance,
            TargetId = FolderId.ToString("D"),
            PrincipalType = DocumentAccessPrincipalType.Role,
            PrincipalId = RoleId.ToString("D"),
            Actions = [DocumentAccessMatrixAction.UploadVersion],
            Effect = DocumentAccessEffect.Allow,
            InheritFromParent = true,
            PolicySource = DocumentAccessPolicySource.AccessProfileTemplate
        });
        // … and a manual Deny on the same target/principal wins (deny precedence at the same distance).
        f.Policies.Items.Add(new DocumentAccessPolicyEntry
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            TargetType = DocumentAccessTargetType.CollectionInstance,
            TargetId = FolderId.ToString("D"),
            PrincipalType = DocumentAccessPrincipalType.Role,
            PrincipalId = RoleId.ToString("D"),
            Actions = [DocumentAccessMatrixAction.UploadVersion],
            Effect = DocumentAccessEffect.Deny,
            InheritFromParent = true,
            PolicySource = DocumentAccessPolicySource.Manual
        });

        var eff = await f.Resolver.ResolveAsync(DocumentAccessTargetType.CollectionInstance, FolderId.ToString("D"),
            DocumentAccessPrincipalType.Role, RoleId.ToString("D"), CancellationToken.None);

        Assert.DoesNotContain("UploadVersion", eff.AllowedActions);
    }

    [Fact]
    public async Task Existing_folder_grant_is_bridged_by_compatibility_adapter()
    {
        var f = Fixture();
        f.SeedFolderGrant(AccessTargetType.Role, RoleId.ToString("D"), set =>
        {
            set.CanViewFolderDocuments = true;
            set.CanUploadDocument = true;
        });

        var eff = await f.Resolver.ResolveAsync(DocumentAccessTargetType.CollectionInstance, FolderId.ToString("D"),
            DocumentAccessPrincipalType.Role, RoleId.ToString("D"), CancellationToken.None);

        Assert.Contains("View", eff.AllowedActions);
        Assert.Contains("Download", eff.AllowedActions);
        Assert.Contains("CreateTemplate", eff.AllowedActions);
    }

    [Fact]
    public async Task Different_principal_gets_no_access()
    {
        var f = Fixture();
        f.SeedPolicy(DocumentAccessTargetType.CollectionInstance, FolderId, DocumentAccessEffect.Allow, inherit: true, DocumentAccessMatrixAction.View);

        var eff = await f.Resolver.ResolveAsync(DocumentAccessTargetType.CollectionInstance, FolderId.ToString("D"),
            DocumentAccessPrincipalType.Role, Guid.NewGuid().ToString("D"), CancellationToken.None);

        Assert.Empty(eff.AllowedActions);
    }

    // ── Service: validation + CRUD ─────────────────────────────────────────────

    [Fact]
    public async Task Create_policy_persists_and_returns_detail()
    {
        var f = Fixture();
        var res = await f.Service.CreateAsync(Input(), Corr, CancellationToken.None);

        Assert.True(res.IsSuccessful);
        Assert.Equal(201, res.StatusCode);
        Assert.Single(f.Policies.Items);
        Assert.Contains("View", res.Data!.Actions);
    }

    [Fact]
    public async Task Duplicate_policy_is_rejected()
    {
        var f = Fixture();
        await f.Service.CreateAsync(Input(), Corr, CancellationToken.None);

        var res = await f.Service.CreateAsync(Input(), Corr, CancellationToken.None);

        Assert.False(res.IsSuccessful);
        Assert.Equal(409, res.StatusCode);
        Assert.Equal(AccessMatrixReasonCodes.DuplicatePolicy, res.ReasonCode);
        Assert.Single(f.Policies.Items);
    }

    [Fact]
    public async Task Missing_target_returns_not_found_non_leakage()
    {
        var f = Fixture();
        var res = await f.Service.CreateAsync(Input() with { TargetType = "CollectionInstance", TargetId = Guid.NewGuid().ToString("D") }, Corr, CancellationToken.None);

        Assert.False(res.IsSuccessful);
        Assert.Equal(404, res.StatusCode);
        Assert.Equal(AccessMatrixReasonCodes.NotFoundNonLeakage, res.ReasonCode);
    }

    [Fact]
    public async Task Group_principal_is_blocked()
    {
        var f = Fixture();
        var res = await f.Service.CreateAsync(Input() with { PrincipalType = "Group" }, Corr, CancellationToken.None);

        Assert.False(res.IsSuccessful);
        Assert.Equal(400, res.StatusCode);
        Assert.Equal(AccessMatrixReasonCodes.GroupPrincipalUnavailable, res.ReasonCode);
    }

    [Fact]
    public async Task Unknown_action_is_rejected()
    {
        var f = Fixture();
        var res = await f.Service.CreateAsync(Input() with { Actions = new[] { "Teleport" } }, Corr, CancellationToken.None);

        Assert.False(res.IsSuccessful);
        Assert.Equal(400, res.StatusCode);
        Assert.Equal(AccessMatrixReasonCodes.InvalidAction, res.ReasonCode);
    }

    [Fact]
    public async Task Effective_preview_via_service_returns_allowed_actions()
    {
        var f = Fixture();
        await f.Service.CreateAsync(Input(), Corr, CancellationToken.None);

        var res = await f.Service.GetEffectiveAsync("CollectionInstance", FolderId.ToString("D"), "Role", RoleId.ToString("D"), Corr, CancellationToken.None);

        Assert.True(res.IsSuccessful);
        Assert.Contains("View", res.Data!.AllowedActions);
        Assert.Equal("Compatibility", res.Data.Mode);
    }

    [Fact]
    public async Task Delete_soft_deletes_policy()
    {
        var f = Fixture();
        var created = await f.Service.CreateAsync(Input(), Corr, CancellationToken.None);

        var res = await f.Service.DeleteAsync(created.Data!.Id, Corr, CancellationToken.None);

        Assert.True(res.IsSuccessful);
        Assert.True(f.Policies.Items.Single().IsDeleted);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static DocumentAccessPolicyInput Input() => new(
        "CollectionInstance",
        FolderId.ToString("D"),
        "Role",
        RoleId.ToString("D"),
        new[] { "View" },
        "ALLOW",
        InheritFromParent: true,
        SourcePolicyId: null,
        ValidFrom: null,
        ValidTo: null,
        Status: "ACTIVE",
        Reason: null);

    private static Harness Fixture()
    {
        var tenant = new TenantContext();
        tenant.SetTenant(TenantId);
        var folderReader = new FakeFolderReader();
        var folderPolicies = new FakeFolderPolicyRepository();
        var policies = new FakeAccessPolicyRepository();
        var variants = new EmptyTemplateVariantRepository();
        var templateDocs = new EmptyTemplateDocumentRepository();
        var controlledDocs = new EmptyControlledDocumentRepository();
        var masters = new EmptyTemplateMasterRepository();
        var principal = new FakeAccessPrincipalAccessor();

        var inheritance = new DocumentAccessInheritanceResolver(variants, templateDocs, controlledDocs, masters, folderReader, tenant);
        var compatibility = new DocumentAccessCompatibilityAdapter(folderPolicies);
        var options = Options.Create(new AccessMatrixOptions());
        var resolver = new DocumentAccessResolver(policies, inheritance, compatibility, principal, options);
        var targetResolver = new DocumentAccessTargetResolver(variants, templateDocs, controlledDocs, masters, folderReader, tenant);
        var service = new DocumentAccessMatrixService(policies, targetResolver, resolver, masters, variants, templateDocs, controlledDocs, tenant, new FakeCurrentUserContext());

        return new Harness(resolver, service, policies, folderPolicies);
    }

    private sealed class Harness
    {
        public DocumentAccessResolver Resolver { get; }
        public DocumentAccessMatrixService Service { get; }
        public FakeAccessPolicyRepository Policies { get; }
        public FakeFolderPolicyRepository FolderPolicies { get; }

        public Harness(DocumentAccessResolver resolver, DocumentAccessMatrixService service, FakeAccessPolicyRepository policies, FakeFolderPolicyRepository folderPolicies)
        {
            Resolver = resolver; Service = service; Policies = policies; FolderPolicies = folderPolicies;
        }

        public void SeedPolicy(DocumentAccessTargetType targetType, Guid targetId, DocumentAccessEffect effect, bool inherit,
            params DocumentAccessMatrixAction[] actions) =>
            SeedPolicy(targetType, targetId, effect, inherit, actions, null, DocumentAccessPolicyStatus.Active);

        public void SeedPolicy(DocumentAccessTargetType targetType, Guid targetId, DocumentAccessEffect effect, bool inherit,
            DocumentAccessMatrixAction[] actions, DateTimeOffset? validTo = null, DocumentAccessPolicyStatus status = DocumentAccessPolicyStatus.Active) =>
            Policies.Items.Add(new DocumentAccessPolicyEntry
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                TargetType = targetType,
                TargetId = targetId.ToString("D"),
                PrincipalType = DocumentAccessPrincipalType.Role,
                PrincipalId = RoleId.ToString("D"),
                Actions = actions.ToList(),
                Effect = effect,
                InheritFromParent = inherit,
                ValidTo = validTo,
                Status = status
            });

        public void SeedFolderGrant(AccessTargetType targetType, string targetId, Action<FolderPermissionSet> configure)
        {
            var set = new FolderPermissionSet();
            configure(set);
            FolderPolicies.Items.Add(new FolderDocumentAccessPolicy
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                CollectionInstanceId = FolderId,
                CompanyId = CompanyId,
                TargetType = targetType,
                TargetId = targetId,
                FolderPermissions = set
            });
        }
    }

    private sealed class FakeFolderReader : ICollectionInstanceReferenceReader
    {
        private readonly CollectionInstanceReferenceDto _folder = new(
            FolderId, CompanyId, BaselineId, "ROOT", null, "Root Folder", "Root Folder", "Active", true, []);

        public Task<CollectionInstanceReferenceDto?> ResolveByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult<CollectionInstanceReferenceDto?>(id == FolderId ? _folder : null);
        public Task<bool> ValidateScopeAsync(Guid id, Guid companyId, CancellationToken ct = default) => Task.FromResult(true);
        public Task<CollectionPathSnapshot?> GetPathSnapshotAsync(Guid id, CancellationToken ct = default) => Task.FromResult<CollectionPathSnapshot?>(null);
        public Task<CollectionInstanceCompanyBinding?> GetCompanyBindingAsync(Guid id, CancellationToken ct = default) => Task.FromResult<CollectionInstanceCompanyBinding?>(null);
        public Task<bool> IsUsableAsync(Guid id, CancellationToken ct = default) => Task.FromResult(true);
        public Task<IReadOnlyList<CollectionInstanceReferenceDto>> GetBranchAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<CollectionInstanceReferenceDto>>(new[] { _folder });
        public Task<IReadOnlyList<CollectionInstanceReferenceDto>> GetCompanyInstancesAsync(Guid companyId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<CollectionInstanceReferenceDto>>(new[] { _folder });
    }

    private sealed class FakeFolderPolicyRepository : IFolderDocumentAccessPolicyRepository
    {
        public List<FolderDocumentAccessPolicy> Items { get; } = [];
        public Task<FolderDocumentAccessPolicy> UpsertAsync(FolderDocumentAccessPolicy policy, CancellationToken ct = default) { Items.Add(policy); return Task.FromResult(policy); }
        public Task<IReadOnlyList<FolderDocumentAccessPolicy>> GetByCollectionInstanceAsync(Guid collectionInstanceId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<FolderDocumentAccessPolicy>>(Items.Where(x => x.CollectionInstanceId == collectionInstanceId).ToList());
        public Task<IReadOnlyList<FolderDocumentAccessPolicy>> GetByCollectionInstanceAndTargetsAsync(Guid collectionInstanceId, IReadOnlyList<(AccessTargetType TargetType, string TargetId)> targets, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<FolderDocumentAccessPolicy>>(Items.Where(x => x.CollectionInstanceId == collectionInstanceId).ToList());
    }

    private sealed class FakeAccessPolicyRepository : IDocumentAccessPolicyRepository
    {
        public List<DocumentAccessPolicyEntry> Items { get; } = [];

        public Task<DocumentAccessPolicyEntry> CreateAsync(DocumentAccessPolicyEntry entry, CancellationToken ct = default) { Items.Add(entry); return Task.FromResult(entry); }
        public Task<DocumentAccessPolicyEntry?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(x => x.Id == id && !x.IsDeleted));

        public Task<IReadOnlyList<DocumentAccessPolicyEntry>> ListAsync(string? targetType, string? targetId, string? principalType, string? principalId, string? effect, string? action, string? status, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentAccessPolicyEntry>>(Items.Where(x => !x.IsDeleted).ToList());

        public Task<IReadOnlyList<DocumentAccessPolicyEntry>> GetByTargetsAsync(IReadOnlyList<(DocumentAccessTargetType TargetType, string TargetId)> targets, CancellationToken ct = default)
        {
            var set = targets.Select(t => $"{t.TargetType}:{t.TargetId}".ToLowerInvariant()).ToHashSet();
            return Task.FromResult<IReadOnlyList<DocumentAccessPolicyEntry>>(
                Items.Where(x => !x.IsDeleted && set.Contains($"{x.TargetType}:{x.TargetId}".ToLowerInvariant())).ToList());
        }

        public Task<DocumentAccessPolicyEntry?> FindDuplicateAsync(DocumentAccessTargetType targetType, string targetId, DocumentAccessPrincipalType principalType, string principalId, DocumentAccessEffect effect, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(x => !x.IsDeleted && x.TargetType == targetType && x.TargetId == targetId && x.PrincipalType == principalType && x.PrincipalId == principalId && x.Effect == effect));

        public Task<bool> UpdateAsync(DocumentAccessPolicyEntry entry, CancellationToken ct = default)
        {
            var i = Items.FindIndex(x => x.Id == entry.Id);
            if (i >= 0) Items[i] = entry;
            return Task.FromResult(i >= 0);
        }

        public Task SoftDeleteAsync(Guid id, CancellationToken ct = default)
        {
            var e = Items.FirstOrDefault(x => x.Id == id && !x.IsDeleted);
            if (e is not null) e.IsDeleted = true;
            return Task.CompletedTask;
        }

        public Task<int> BulkSoftDeleteAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default)
        {
            var set = ids.ToHashSet();
            var affected = Items.Where(x => set.Contains(x.Id) && !x.IsDeleted).ToList();
            foreach (var e in affected) e.IsDeleted = true;
            return Task.FromResult(affected.Count);
        }
    }

    private sealed class FakeAccessPrincipalAccessor : IDocumentAccessPrincipalAccessor
    {
        public DocumentPrincipal GetPrincipal() => new(Guid.NewGuid(), new[] { RoleId.ToString("D") }, new[] { CompanyId });
    }

    private sealed class EmptyTemplateVariantRepository : ITemplateVariantRepository
    {
        public Task<TemplateVariant> CreateAsync(TemplateVariant v, CancellationToken ct = default) => Task.FromResult(v);
        public Task<TemplateVariant?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<TemplateVariant?>(null);
        public Task<TemplateVariant?> GetByScopeAndCodeAsync(TemplateVariantScopeType scopeType, Guid scopeId, string variantCode, CancellationToken ct = default) => Task.FromResult<TemplateVariant?>(null);
        public Task<IReadOnlyList<TemplateVariant>> ListAsync(Guid? templateMasterId, string? scopeType, Guid? scopeId, string? status, string? approvalStatus, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<TemplateVariant>>([]);
        public Task<IReadOnlyList<TemplateVariant>> GetByMasterAsync(Guid templateMasterId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<TemplateVariant>>([]);
        public Task<bool> UpdateAsync(TemplateVariant v, CancellationToken ct = default) => Task.FromResult(true);
        public Task SoftDeleteAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class EmptyTemplateDocumentRepository : ITemplateDocumentRepository
    {
        public Task<TemplateDocument> CreateAsync(TemplateDocument t, CancellationToken ct = default) => Task.FromResult(t);
        public Task<TemplateDocument?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<TemplateDocument?>(null);
        public Task<TemplateDocument?> GetByTemplateKeyAsync(string templateKey, CancellationToken ct = default) => Task.FromResult<TemplateDocument?>(null);
        public Task<IReadOnlyList<TemplateDocument>> GetAllForTenantAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<TemplateDocument>>([]);
        public Task<IReadOnlyList<TemplateDocument>> GetByCompanyAsync(Guid companyId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<TemplateDocument>>([]);
        public Task<IReadOnlyList<TemplateDocument>> GetByCollectionInstanceAsync(Guid collectionInstanceId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<TemplateDocument>>([]);
        public Task<bool> UpdateAsync(TemplateDocument t, CancellationToken ct = default) => Task.FromResult(true);
        public Task SoftDeleteAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class EmptyControlledDocumentRepository : IControlledDocumentRepository
    {
        public Task<ControlledDocument> CreateAsync(ControlledDocument d, CancellationToken ct = default) => Task.FromResult(d);
        public Task<ControlledDocument?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<ControlledDocument?>(null);
        public Task<ControlledDocument?> GetByDocumentKeyAsync(string documentKey, CancellationToken ct = default) => Task.FromResult<ControlledDocument?>(null);
        public Task<IReadOnlyList<ControlledDocument>> GetAllForTenantAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<ControlledDocument>>([]);
        public Task<IReadOnlyList<ControlledDocument>> GetByCompanyAsync(Guid companyId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<ControlledDocument>>([]);
        public Task<IReadOnlyList<ControlledDocument>> GetByCollectionInstanceAsync(Guid collectionInstanceId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<ControlledDocument>>([]);
        public Task<bool> UpdateAsync(ControlledDocument d, CancellationToken ct = default) => Task.FromResult(true);
        public Task SoftDeleteAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class EmptyTemplateMasterRepository : ITemplateMasterRepository
    {
        public Task<TemplateMaster> CreateAsync(TemplateMaster m, CancellationToken ct = default) => Task.FromResult(m);
        public Task<TemplateMaster?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<TemplateMaster?>(null);
        public Task<TemplateMaster?> GetByMasterCodeAsync(string masterCode, CancellationToken ct = default) => Task.FromResult<TemplateMaster?>(null);
        public Task<IReadOnlyList<TemplateMaster>> ListAsync(string? status, string? classification, Guid? collectionDefinitionId, string? canonicalId, string? variantPolicy, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<TemplateMaster>>([]);
        public Task<bool> UpdateAsync(TemplateMaster m, CancellationToken ct = default) => Task.FromResult(true);
        public Task<int> CountByCurrentVersionAsync(Guid templateMasterVersionId, CancellationToken ct = default) => Task.FromResult(0);
        public Task SoftDeleteAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;
        public Task<int> BulkSoftDeleteAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default) => Task.FromResult(0);
    }
}
