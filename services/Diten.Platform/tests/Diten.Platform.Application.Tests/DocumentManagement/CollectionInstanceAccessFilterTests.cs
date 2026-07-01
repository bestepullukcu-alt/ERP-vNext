using Diten.Platform.Application.Features.DocumentManagementAccessMatrix.Services;
using Diten.Platform.Application.Features.DocumentManagementControlledDocuments.Services;
using Diten.Platform.Application.Features.DocumentManagementInstantiation.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.DocumentManagementInstantiation.Queries;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Services.DocumentManagement;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using Xunit;

namespace Diten.Platform.Application.Tests.DocumentManagement;

// MOD-0029-FU04D — regression coverage for CollectionInstance read/list filtering after Access Matrix rollout.
public sealed class CollectionInstanceAccessFilterTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly Guid CompanyId = Guid.Parse("aaaaaaaa-1111-2222-3333-444444444444");
    private static readonly Guid OtherCompanyId = Guid.Parse("aaaaaaaa-9999-2222-3333-444444444444");
    private static readonly Guid BaselineId = Guid.Parse("bbbbbbbb-1111-2222-3333-444444444444");
    private static readonly Guid FolderId = Guid.Parse("cccccccc-1111-2222-3333-444444444444");
    private static readonly Guid UserId = Guid.Parse("dddddddd-1111-2222-3333-444444444444");

    [Fact]
    public async Task Read_list_without_required_action_tenant_admin_returns_instances()
    {
        var f = Harness(new DocumentPrincipal(UserId, [], [], IsTenantAdmin: true));

        var result = await f.HandleAsync(requiredAction: null);

        Assert.True(result.IsSuccessful);
        Assert.Single(result.Data!);
    }

    [Fact]
    public async Task Read_list_without_required_action_platform_admin_returns_instances()
    {
        var f = Harness(new DocumentPrincipal(UserId, [], [], IsPlatformAdmin: true));

        var result = await f.HandleAsync(requiredAction: null);

        Assert.True(result.IsSuccessful);
        Assert.Single(result.Data!);
    }

    [Fact]
    public async Task Read_list_without_required_action_owner_company_transitional_view_returns_instances()
    {
        var f = Harness(new DocumentPrincipal(UserId, [], [CompanyId]));

        var result = await f.HandleAsync(requiredAction: null);

        Assert.True(result.IsSuccessful);
        Assert.Single(result.Data!);
    }

    [Fact]
    public async Task Read_list_without_required_action_no_company_claim_compatibility_still_returns_instances()
    {
        // Regression (FU04): the Instantiate Structures grid had no row filtering before the matrix. A live token
        // without company claims (claim wiring not yet issued) must not empty the list in Compatibility mode.
        var f = Harness(new DocumentPrincipal(UserId, [], []));

        var result = await f.HandleAsync(requiredAction: null);

        Assert.True(result.IsSuccessful);
        Assert.Single(result.Data!);
    }

    [Fact]
    public async Task Read_list_without_required_action_unrelated_company_is_hidden_in_enforce_mode()
    {
        var f = Harness(
            new DocumentPrincipal(UserId, [], [OtherCompanyId]),
            new AccessMatrixOptions { Mode = AccessMatrixEnforcementMode.Enforce, OwnerCompanyTransitionalView = false });

        var result = await f.HandleAsync(requiredAction: null);

        Assert.True(result.IsSuccessful);
        Assert.Empty(result.Data!);
    }

    [Fact]
    public async Task Read_list_without_required_action_explicit_deny_view_hides_owner_company_instance()
    {
        var f = Harness(new DocumentPrincipal(UserId, [], [CompanyId]));
        f.SeedMatrixPolicy(DocumentAccessEffect.Deny, DocumentAccessMatrixAction.View);

        var result = await f.HandleAsync(requiredAction: null);

        Assert.True(result.IsSuccessful);
        Assert.Empty(result.Data!);
    }

    [Fact]
    public async Task Read_list_without_required_action_collection_instance_deny_overrides_legacy_folder_allow()
    {
        var f = Harness(new DocumentPrincipal(UserId, [], [CompanyId]));
        f.SeedLegacyFolderGrant(set => set.CanViewFolderDocuments = true);
        f.SeedMatrixPolicy(DocumentAccessEffect.Deny, DocumentAccessMatrixAction.View);

        var result = await f.HandleAsync(requiredAction: null);

        Assert.True(result.IsSuccessful);
        Assert.Empty(result.Data!);
    }

    [Fact]
    public async Task Required_action_create_template_without_grant_is_hidden()
    {
        var f = Harness(new DocumentPrincipal(UserId, [], [CompanyId]));

        var result = await f.HandleAsync("CreateTemplate");

        Assert.True(result.IsSuccessful);
        Assert.Empty(result.Data!);
    }

    [Fact]
    public async Task Required_action_create_template_with_explicit_allow_is_visible()
    {
        var f = Harness(new DocumentPrincipal(UserId, [], [CompanyId]));
        f.SeedMatrixPolicy(DocumentAccessEffect.Allow, DocumentAccessMatrixAction.CreateTemplate);

        var result = await f.HandleAsync("CreateTemplate");

        Assert.True(result.IsSuccessful);
        Assert.Single(result.Data!);
    }

    [Fact]
    public async Task Required_action_create_template_with_legacy_upload_grant_is_visible()
    {
        var f = Harness(new DocumentPrincipal(UserId, [], [CompanyId]));
        f.SeedLegacyFolderGrant(set => set.CanUploadDocument = true);

        var result = await f.HandleAsync("CreateTemplate");

        Assert.True(result.IsSuccessful);
        Assert.Single(result.Data!);
    }

    [Fact]
    public async Task Required_action_create_template_explicit_deny_overrides_legacy_upload_grant()
    {
        var f = Harness(new DocumentPrincipal(UserId, [], [CompanyId]));
        f.SeedLegacyFolderGrant(set => set.CanUploadDocument = true);
        f.SeedMatrixPolicy(DocumentAccessEffect.Deny, DocumentAccessMatrixAction.CreateTemplate);

        var result = await f.HandleAsync("CreateTemplate");

        Assert.True(result.IsSuccessful);
        Assert.Empty(result.Data!);
    }

    // requiredAction=View is the Controlled Documents explorer TREE gate. It must match the folder-CONTENTS
    // (folder-documents) gate so a folder never appears in the tree yet 403s when opened.
    [Fact]
    public async Task Required_action_view_owner_company_is_visible()
    {
        var f = Harness(new DocumentPrincipal(UserId, [], [CompanyId]));

        var result = await f.HandleAsync("View");

        Assert.True(result.IsSuccessful);
        Assert.Single(result.Data!);
    }

    [Fact]
    public async Task Required_action_view_claimed_other_company_without_grant_is_hidden()
    {
        // A CLAIMED principal of an unrelated company with no grant: the tree must hide this folder (the contents
        // endpoint would otherwise 403 it). A claimless token, by contrast, is a tenant-wide viewer under the FU04
        // Deny-only rollout — this test pins the claimed-user path so cross-company isolation still holds.
        var f = Harness(
            new DocumentPrincipal(UserId, [], [OtherCompanyId]),
            new AccessMatrixOptions { Mode = AccessMatrixEnforcementMode.Compatibility, OwnerCompanyTransitionalView = true });

        var result = await f.HandleAsync("View");

        Assert.True(result.IsSuccessful);
        Assert.Empty(result.Data!);
    }

    [Theory]
    [InlineData("platform_admin")]
    [InlineData("tenant_admin")]
    public void Principal_accessor_marks_document_admin_actor_types(string actorType)
    {
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("actor_type", actorType)], "test"))
        };
        var accessor = new HttpContextAccessor { HttpContext = http };
        var principalAccessor = new DocumentAccessPrincipalAccessor(
            accessor,
            new FakeCurrentUserContext { UserId = UserId });

        var principal = principalAccessor.GetPrincipal();

        Assert.True(principal.HasAdministrativeDocumentAccess);
    }

    private static TestHarness Harness(DocumentPrincipal principal, AccessMatrixOptions? options = null)
    {
        options ??= new AccessMatrixOptions();
        var instances = new FakeCollectionInstanceRepository();
        instances.Items.Add(new CollectionInstance
        {
            Id = FolderId,
            TenantId = TenantId,
            InstanceKey = "quality-manuals",
            CompanyId = CompanyId,
            BaselineReleaseId = BaselineId,
            CanonicalId = "manuals",
            ParentCanonicalId = "quality",
            Name = "Manuals",
            FullPath = "Quality/Manuals",
            SourceDefinitionHash = "hash"
        });

        var folderReader = new FakeCollectionInstanceReferenceReader();
        folderReader.Items.Add(new CollectionInstanceReferenceDto(
            FolderId,
            CompanyId,
            BaselineId,
            "manuals",
            "quality",
            "Manuals",
            "Quality/Manuals",
            "ACTIVE",
            true,
            []));

        var accessPolicies = new FakeDocumentAccessPolicyRepository();
        var folderPolicies = new FakeFolderDocumentAccessPolicyRepository();
        var principalAccessor = new FakePrincipalAccessor(principal);
        var tenant = new TenantContext();
        tenant.SetTenant(TenantId);

        var inheritance = new DocumentAccessInheritanceResolver(
            new EmptyTemplateVariantRepository(),
            new FakeTemplateDocumentRepository(),
            new FakeControlledDocumentRepository(),
            new EmptyTemplateMasterRepository(),
            folderReader,
            tenant);

        var resolver = new DocumentAccessResolver(
            accessPolicies,
            inheritance,
            new DocumentAccessCompatibilityAdapter(folderPolicies),
            principalAccessor,
            Options.Create(options));

        var evaluator = new DocumentAccessEvaluator(
            folderPolicies,
            new FakeDocumentShareRecordRepository(),
            principalAccessor,
            resolver,
            Options.Create(options));

        return new TestHarness(
            new GetCollectionInstancesHandler(instances, evaluator),
            accessPolicies,
            folderPolicies);
    }

    private sealed class TestHarness
    {
        private readonly GetCollectionInstancesHandler _handler;
        private readonly FakeDocumentAccessPolicyRepository _policies;
        private readonly FakeFolderDocumentAccessPolicyRepository _folderPolicies;

        public TestHarness(
            GetCollectionInstancesHandler handler,
            FakeDocumentAccessPolicyRepository policies,
            FakeFolderDocumentAccessPolicyRepository folderPolicies)
        {
            _handler = handler;
            _policies = policies;
            _folderPolicies = folderPolicies;
        }

        public Task<Application.Common.Response<IReadOnlyList<Application.Features.DocumentManagementInstantiation.CollectionInstanceListItemModel>>> HandleAsync(
            string? requiredAction)
        {
            var query = new GetCollectionInstancesQuery(CompanyId, BaselineId, null, requiredAction, "fu04d-test");
            return _handler.Handle(query, CancellationToken.None);
        }

        public void SeedMatrixPolicy(DocumentAccessEffect effect, params DocumentAccessMatrixAction[] actions)
        {
            _policies.Items.Add(new DocumentAccessPolicyEntry
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                TargetType = DocumentAccessTargetType.CollectionInstance,
                TargetId = FolderId.ToString("D"),
                PrincipalType = DocumentAccessPrincipalType.Company,
                PrincipalId = CompanyId.ToString("D"),
                Actions = actions.ToList(),
                Effect = effect,
                InheritFromParent = true,
                Status = DocumentAccessPolicyStatus.Active
            });
        }

        public void SeedLegacyFolderGrant(Action<FolderPermissionSet> configure)
        {
            var permissions = new FolderPermissionSet();
            configure(permissions);
            _folderPolicies.Items.Add(new FolderDocumentAccessPolicy
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                CollectionInstanceId = FolderId,
                CompanyId = CompanyId,
                TargetType = AccessTargetType.Company,
                TargetId = CompanyId.ToString("D"),
                FolderPermissions = permissions
            });
        }
    }

    private sealed class FakeCollectionInstanceRepository : ICollectionInstanceRepository
    {
        public List<CollectionInstance> Items { get; } = [];
        public Task<CollectionInstance> CreateAsync(CollectionInstance instance, CancellationToken ct = default) => Task.FromResult(instance);
        public Task<IReadOnlyList<CollectionInstance>> CreateManyAsync(IReadOnlyList<CollectionInstance> instances, CancellationToken ct = default) => Task.FromResult(instances);
        public Task<CollectionInstance?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(x => x.Id == id));
        public Task<CollectionInstance?> GetByInstanceKeyAsync(string instanceKey, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(x => x.InstanceKey == instanceKey));
        public Task<IReadOnlyList<CollectionInstance>> GetAllForTenantAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<CollectionInstance>>(Items);
        public Task<IReadOnlyList<CollectionInstance>> GetByCompanyAsync(Guid companyId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<CollectionInstance>>(Items.Where(x => x.CompanyId == companyId).ToList());
        public Task<IReadOnlyList<CollectionInstance>> GetByBaselineAndCompanyAsync(Guid baselineReleaseId, Guid companyId, string? instanceToken, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<CollectionInstance>>(Items.Where(x =>
                x.BaselineReleaseId == baselineReleaseId
                && x.CompanyId == companyId
                && (string.IsNullOrWhiteSpace(instanceToken) || x.InstanceToken == instanceToken)).ToList());
        public Task<long> ArchiveManyAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default) => Task.FromResult(0L);
        public Task<long> ReactivateManyAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default) => Task.FromResult(0L);
    }

    private sealed class FakeDocumentAccessPolicyRepository : IDocumentAccessPolicyRepository
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
        public Task<bool> UpdateAsync(DocumentAccessPolicyEntry entry, CancellationToken ct = default) => Task.FromResult(true);
        public Task SoftDeleteAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;
        public Task<int> BulkSoftDeleteAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default) => Task.FromResult(0);
    }

    private sealed class EmptyTemplateVariantRepository : ITemplateVariantRepository
    {
        public Task<TemplateVariant> CreateAsync(TemplateVariant variant, CancellationToken ct = default) => Task.FromResult(variant);
        public Task<TemplateVariant?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<TemplateVariant?>(null);
        public Task<TemplateVariant?> GetByScopeAndCodeAsync(TemplateVariantScopeType scopeType, Guid scopeId, string variantCode, CancellationToken ct = default) => Task.FromResult<TemplateVariant?>(null);
        public Task<IReadOnlyList<TemplateVariant>> ListAsync(Guid? templateMasterId, string? scopeType, Guid? scopeId, string? status, string? approvalStatus, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<TemplateVariant>>([]);
        public Task<IReadOnlyList<TemplateVariant>> GetByMasterAsync(Guid templateMasterId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<TemplateVariant>>([]);
        public Task<bool> UpdateAsync(TemplateVariant variant, CancellationToken ct = default) => Task.FromResult(true);
        public Task SoftDeleteAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class EmptyTemplateMasterRepository : ITemplateMasterRepository
    {
        public Task<TemplateMaster> CreateAsync(TemplateMaster master, CancellationToken ct = default) => Task.FromResult(master);
        public Task<TemplateMaster?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<TemplateMaster?>(null);
        public Task<TemplateMaster?> GetByMasterCodeAsync(string masterCode, CancellationToken ct = default) => Task.FromResult<TemplateMaster?>(null);
        public Task<IReadOnlyList<TemplateMaster>> ListAsync(string? status, string? classification, Guid? collectionDefinitionId, string? canonicalId, string? variantPolicy, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<TemplateMaster>>([]);
        public Task<bool> UpdateAsync(TemplateMaster master, CancellationToken ct = default) => Task.FromResult(true);
        public Task<int> CountByCurrentVersionAsync(Guid templateMasterVersionId, CancellationToken ct = default) => Task.FromResult(0);
        public Task SoftDeleteAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;
        public Task<int> BulkSoftDeleteAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default) => Task.FromResult(0);
    }
}
