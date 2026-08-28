using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.DocumentManagementControlledDocuments.Services;
using Diten.Platform.Application.Features.DocumentManagementCorporateCollectionInstances;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;
using Xunit;

namespace Diten.Platform.Application.Tests.DocumentManagement;

public sealed class CorporateCollectionInstanceFoundationTests
{
    private static readonly Guid TenantId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid BaselineId = Guid.Parse("20000000-0000-0000-0000-000000000002");
    private static readonly Guid CorporateOwnerId = Guid.Parse("30000000-0000-0000-0000-000000000003");
    private static readonly Guid CompanyId = Guid.Parse("40000000-0000-0000-0000-000000000004");
    private static readonly Guid UserId = Guid.Parse("50000000-0000-0000-0000-000000000005");

    [Fact]
    public async Task Provision_creates_real_corporate_tree_without_company_id_and_replays_idempotently()
    {
        var harness = CreateHarness();

        var first = await harness.Service.ProvisionAsync(
            BaselineId, CorporateOwnerId, "corporate-qms-v1", null, null, "corr-1", CancellationToken.None);
        var replay = await harness.Service.ProvisionAsync(
            BaselineId, CorporateOwnerId, "corporate-qms-v1", null, null, "corr-2", CancellationToken.None);

        Assert.True(first.IsSuccessful);
        Assert.Equal(2, first.Data!.FolderCount);
        Assert.True(replay.IsSuccessful);
        Assert.True(replay.Data!.IdempotentReplay);
        Assert.Equal(first.Data.CollectionInstanceId, replay.Data.CollectionInstanceId);
        Assert.All(harness.Instances.Items, node =>
        {
            Assert.Equal(CollectionScopeType.Corporate, node.CollectionScopeType);
            Assert.Equal(CorporateOwnerId, node.ScopeOwnerId);
            Assert.Equal(CorporateOwnerId, node.CorporateOwnerId);
            Assert.Equal(Guid.Empty, node.CompanyId);
            Assert.Contains($"/corporate/{CorporateOwnerId:D}/folder/", node.StoragePartition);
        });
    }

    [Fact]
    public void Partition_builder_pins_company_compatibility_and_corporate_separation()
    {
        var tenant = new TenantContext();
        tenant.SetTenant(TenantId);
        var builder = new CorporateCollectionStoragePartitionBuilder(tenant);
        var folderId = Guid.Parse("60000000-0000-0000-0000-000000000006");

        Assert.Equal(
            $"tenant/{TenantId:D}/company/{CompanyId:D}/folder/{folderId:D}",
            builder.ForCompany(CompanyId, folderId));
        Assert.Equal(
            $"tenant/{TenantId:D}/corporate/{CorporateOwnerId:D}/folder/{folderId:D}",
            builder.ForCorporate(CorporateOwnerId, folderId));
    }

    [Fact]
    public async Task Corporate_access_is_deny_by_default_and_company_membership_is_not_a_grant()
    {
        var policies = new FakeAccessPolicyRepository();
        var principal = new FakePrincipalAccessor(
            new DocumentPrincipal(UserId, [], [CompanyId], IsTenantAdmin: false, IsPlatformAdmin: false));
        var evaluator = new CorporateCollectionFolderAccessEvaluator(policies, principal);
        var folderId = Guid.NewGuid();

        Assert.False(await evaluator.HasExplicitGrantAsync(folderId, DocumentAccessMatrixAction.View, CancellationToken.None));

        policies.Items.Add(new DocumentAccessPolicyEntry
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            TargetType = DocumentAccessTargetType.CollectionInstance,
            TargetId = folderId.ToString("D"),
            PrincipalType = DocumentAccessPrincipalType.User,
            PrincipalId = UserId.ToString("D"),
            Actions = [DocumentAccessMatrixAction.View],
            Effect = DocumentAccessEffect.Allow,
            Status = DocumentAccessPolicyStatus.Active
        });

        Assert.True(await evaluator.HasExplicitGrantAsync(folderId, DocumentAccessMatrixAction.View, CancellationToken.None));
    }

    [Fact]
    public void Provision_api_request_exposes_neither_tenant_nor_company()
    {
        var properties = typeof(Diten.Platform.API.Models.DocumentManagement.ProvisionCorporateCollectionInstanceRequest)
            .GetProperties()
            .Select(x => x.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.DoesNotContain("TenantId", properties);
        Assert.DoesNotContain("CompanyId", properties);
    }

    [Fact]
    public void Corporate_unique_index_uses_positive_active_filter_only()
    {
        var repositoryRoot = FindRepositoryRoot();
        var indexSource = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "services",
            "Diten.Platform",
            "src",
            "Diten.Platform.Infrastructure",
            "Persistence",
            "Configurations",
            "MongoDbIndexConfigurations.cs"));
        var marker = indexSource.IndexOf(
            "Name = CorporateActiveInstanceIndexName",
            StringComparison.Ordinal);
        Assert.True(marker >= 0);
        var windowStart = Math.Max(0, marker - 1200);
        var windowLength = Math.Min(indexSource.Length - windowStart, 3000);
        var indexBlock = indexSource.Substring(windowStart, windowLength);

        Assert.Contains("CollectionInstanceStatus.Active", indexBlock, StringComparison.Ordinal);
        Assert.DoesNotContain("Filter.Ne", indexBlock, StringComparison.Ordinal);
        Assert.DoesNotContain("Filter.Lt", indexBlock, StringComparison.Ordinal);
        Assert.DoesNotContain("Filter.Not", indexBlock, StringComparison.Ordinal);
    }

    private static Harness CreateHarness()
    {
        var tenant = new TenantContext();
        tenant.SetTenant(TenantId);
        var baselines = new FakeBaselineRepository
        {
            Baseline = new BaselineRelease
            {
                Id = BaselineId,
                TenantId = TenantId,
                BaselineReleaseId = "CORP-QMS",
                SourceBaselineKey = "CORP-QMS",
                BaselineVersion = "1.0",
                Status = BaselineReleaseStatus.Effective
            }
        };
        var definitions = new FakeDefinitionRepository();
        definitions.Items.AddRange([
            Definition("root", null, "Corporate", "Corporate", 1),
            Definition("policies", "root", "Policies", "Corporate/Policies", 2)
        ]);
        var instances = new FakeInstanceRepository();
        var operations = new FakeOperationRepository();
        var service = new CorporateCollectionInstanceProvisioningService(
            baselines,
            definitions,
            instances,
            operations,
            tenant,
            new FakeCurrentUserContext(),
            new CorporateCollectionStoragePartitionBuilder(tenant));
        return new Harness(service, instances);
    }

    private static CollectionDefinition Definition(string id, string? parent, string name, string path, int order) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            BaselineReleaseId = BaselineId,
            CanonicalId = id,
            ParentCanonicalId = parent,
            Name = name,
            PathSegment = name,
            FullPath = path,
            DisplayOrder = order,
            DefinitionHash = $"hash-{id}"
        };

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "AGENTS.md")))
        {
            current = current.Parent;
        }

        return current?.FullName ?? throw new InvalidOperationException("Repository root could not be resolved.");
    }

    private sealed record Harness(
        CorporateCollectionInstanceProvisioningService Service,
        FakeInstanceRepository Instances);

    private sealed class FakeBaselineRepository : IBaselineReleaseRepository
    {
        public BaselineRelease? Baseline { get; init; }
        public Task<BaselineRelease> CreateAsync(BaselineRelease baseline, CancellationToken ct = default) => Task.FromResult(baseline);
        public Task<BaselineRelease?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Baseline?.Id == id ? Baseline : null);
        public Task<IReadOnlyList<BaselineRelease>> GetAllAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<BaselineRelease>>(Baseline is null ? [] : [Baseline]);
        public Task<bool> UpdateAsync(BaselineRelease baseline, int expectedVersion, CancellationToken ct = default) => Task.FromResult(true);
    }

    private sealed class FakeDefinitionRepository : ICollectionDefinitionRepository
    {
        public List<CollectionDefinition> Items { get; } = [];
        public Task<CollectionDefinition> CreateAsync(CollectionDefinition definition, CancellationToken ct = default) => Task.FromResult(definition);
        public Task CreateManyAsync(IReadOnlyList<CollectionDefinition> definitions, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<CollectionDefinition>> GetByBaselineAsync(Guid baselineReleaseId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<CollectionDefinition>>(Items.Where(x => x.BaselineReleaseId == baselineReleaseId).ToList());
        public Task<CollectionDefinition?> GetByCanonicalIdAsync(Guid baselineReleaseId, string canonicalId, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(x => x.BaselineReleaseId == baselineReleaseId && x.CanonicalId == canonicalId));
        public Task<bool> UpdateAsync(CollectionDefinition definition, int expectedVersion, CancellationToken ct = default) => Task.FromResult(true);
        public Task UpdateManyAsync(IReadOnlyList<CollectionDefinition> definitions, CancellationToken ct = default) => Task.CompletedTask;
        public Task<bool> SoftDeleteAsync(CollectionDefinition definition, int expectedVersion, CancellationToken ct = default) => Task.FromResult(true);
    }

    private sealed class FakeInstanceRepository : ICollectionInstanceRepository
    {
        public List<CollectionInstance> Items { get; } = [];
        public Task<CollectionInstance> CreateAsync(CollectionInstance instance, CancellationToken ct = default) => Task.FromResult(instance);
        public Task<IReadOnlyList<CollectionInstance>> CreateManyAsync(IReadOnlyList<CollectionInstance> instances, CancellationToken ct = default) =>
            Task.FromResult(instances);
        public Task<CollectionInstance?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(x => x.Id == id));
        public Task<CollectionInstance?> GetByInstanceKeyAsync(string instanceKey, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(x => x.InstanceKey == instanceKey));
        public Task<IReadOnlyList<CollectionInstance>> GetAllForTenantAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<CollectionInstance>>(Items);
        public Task<IReadOnlyList<CollectionInstance>> GetByCompanyAsync(Guid companyId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<CollectionInstance>>(Items.Where(x => x.CompanyId == companyId).ToList());
        public Task<IReadOnlyList<CollectionInstance>> GetByBaselineAndCompanyAsync(Guid baselineReleaseId, Guid companyId, string? instanceToken, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<CollectionInstance>>(Items.Where(x => x.BaselineReleaseId == baselineReleaseId && x.CompanyId == companyId).ToList());
        public Task<IReadOnlyList<CollectionInstance>> GetCorporateAsync(Guid? baselineReleaseId, Guid? corporateOwnerId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<CollectionInstance>>(Items.Where(x =>
                x.CollectionScopeType == CollectionScopeType.Corporate
                && (!baselineReleaseId.HasValue || x.BaselineReleaseId == baselineReleaseId)
                && (!corporateOwnerId.HasValue || x.ScopeOwnerId == corporateOwnerId)).ToList());
        public Task<IReadOnlyList<CollectionInstance>> CreateCorporateTreeIfAbsentAsync(Guid baselineReleaseId, Guid corporateOwnerId, IReadOnlyList<CollectionInstance> instances, CancellationToken ct = default)
        {
            foreach (var item in instances)
            {
                if (!Items.Any(x => x.BaselineReleaseId == baselineReleaseId && x.ScopeOwnerId == corporateOwnerId && x.CanonicalId == item.CanonicalId))
                    Items.Add(item);
            }
            return GetCorporateAsync(baselineReleaseId, corporateOwnerId, ct);
        }
        public Task<long> ArchiveManyAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default) => Task.FromResult(0L);
        public Task<long> ReactivateManyAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default) => Task.FromResult(0L);
    }

    private sealed class FakeOperationRepository : ICorporateCollectionProvisioningOperationRepository
    {
        private readonly List<CorporateCollectionInstanceProvisioningOperation> _items = [];
        public Task<CorporateCollectionInstanceProvisioningOperation> CreateOrGetAsync(CorporateCollectionInstanceProvisioningOperation operation, CancellationToken ct = default)
        {
            var existing = _items.FirstOrDefault(x => x.IdempotencyKey == operation.IdempotencyKey);
            if (existing is not null) return Task.FromResult(existing);
            _items.Add(operation);
            return Task.FromResult(operation);
        }
        public Task<CorporateCollectionInstanceProvisioningOperation?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(_items.FirstOrDefault(x => x.Id == id));
        public Task<CorporateCollectionInstanceProvisioningOperation?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct = default) =>
            Task.FromResult(_items.FirstOrDefault(x => x.IdempotencyKey == idempotencyKey));
        public Task<bool> UpdateAsync(CorporateCollectionInstanceProvisioningOperation operation, CancellationToken ct = default) =>
            Task.FromResult(true);
    }

    private sealed class FakeCurrentUserContext : ICurrentUserContext
    {
        public Guid UserId => CorporateCollectionInstanceFoundationTests.UserId;
        public string? Email => "document.control@example.test";
        public string? DisplayName => "Document Control";
        public string ActorName => "Document Control";
        public bool IsAuthenticated => true;
    }

    private sealed class FakePrincipalAccessor(DocumentPrincipal principal) : IDocumentAccessPrincipalAccessor
    {
        public DocumentPrincipal GetPrincipal() => principal;
    }

    private sealed class FakeAccessPolicyRepository : IDocumentAccessPolicyRepository
    {
        public List<DocumentAccessPolicyEntry> Items { get; } = [];
        public Task<DocumentAccessPolicyEntry> CreateAsync(DocumentAccessPolicyEntry entry, CancellationToken ct = default) => Task.FromResult(entry);
        public Task<DocumentAccessPolicyEntry?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(x => x.Id == id));
        public Task<IReadOnlyList<DocumentAccessPolicyEntry>> ListAsync(string? targetType, string? targetId, string? principalType, string? principalId, string? effect, string? action, string? status, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentAccessPolicyEntry>>(Items);
        public Task<IReadOnlyList<DocumentAccessPolicyEntry>> GetByTargetsAsync(IReadOnlyList<(DocumentAccessTargetType TargetType, string TargetId)> targets, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentAccessPolicyEntry>>(Items.Where(x => targets.Any(t => t.TargetType == x.TargetType && t.TargetId == x.TargetId)).ToList());
        public Task<DocumentAccessPolicyEntry?> FindDuplicateAsync(DocumentAccessTargetType targetType, string targetId, DocumentAccessPrincipalType principalType, string principalId, DocumentAccessEffect effect, CancellationToken ct = default) =>
            Task.FromResult<DocumentAccessPolicyEntry?>(null);
        public Task<bool> UpdateAsync(DocumentAccessPolicyEntry entry, CancellationToken ct = default) => Task.FromResult(true);
        public Task SoftDeleteAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;
        public Task<int> BulkSoftDeleteAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default) => Task.FromResult(0);
    }
}
