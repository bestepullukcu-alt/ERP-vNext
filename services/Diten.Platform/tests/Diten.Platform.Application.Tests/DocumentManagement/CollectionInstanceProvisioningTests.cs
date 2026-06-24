using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.DocumentManagementContract;
using Diten.Platform.Application.Features.DocumentManagementInstantiation;
using Diten.Platform.Application.Features.DocumentManagementInstantiation.Services;
using Diten.Platform.Application.Features.TenantOrganization.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.Platform.Application.Tests.DocumentManagement;

public sealed class CollectionInstanceProvisioningTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid CompanyId = Guid.Parse("30000000-0000-0000-0000-000000000001");
    private const string Correlation = "fu05-corr-001";

    [Fact]
    public async Task Dry_run_valid_published_baseline_mutates_nothing()
    {
        var fixture = Fixture(referenceable: true);
        var baseline = PublishedBaseline();
        fixture.Baselines.Items.Add(baseline);
        fixture.Definitions.Items.AddRange(Definitions(baseline.Id));

        var response = await fixture.Service.DryRunAsync(baseline.Id, Scope(), InstantiationSelectionRequest.Default, Correlation, CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(Correlation, response.CorrelationId);
        Assert.Equal(2, response.Data!.Diagnostics!.NodesToCreate);
        Assert.Empty(fixture.Instances.Items);
        Assert.Empty(fixture.Operations.Items);
        Assert.Empty(fixture.Outcomes.Items);
    }

    [Fact]
    public async Task Draft_baseline_is_blocked_with_validation_failed()
    {
        var fixture = Fixture(referenceable: true);
        var baseline = PublishedBaseline();
        baseline.Status = BaselineReleaseStatus.Draft;
        fixture.Baselines.Items.Add(baseline);

        var response = await fixture.Service.DryRunAsync(baseline.Id, Scope(), InstantiationSelectionRequest.Default, Correlation, CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
        Assert.Equal(DocumentManagementInstantiationReasonCodes.ValidationFailed, response.ReasonCode);
    }

    [Fact]
    public async Task Legal_entity_not_referenceable_fails_closed_without_writes()
    {
        var fixture = Fixture(referenceable: false);
        var baseline = PublishedBaseline();
        fixture.Baselines.Items.Add(baseline);
        fixture.Definitions.Items.AddRange(Definitions(baseline.Id));

        var response = await fixture.Service.ExecuteAsync(baseline.Id, Scope(), InstantiationSelectionRequest.Default, Correlation, CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
        Assert.Equal(DocumentManagementInstantiationReasonCodes.NotFoundNonLeakage, response.ReasonCode);
        Assert.Empty(fixture.Instances.Items);
        Assert.Empty(fixture.Operations.Items);
    }

    [Fact]
    public async Task Execute_creates_collection_instances_and_rerun_skips_existing()
    {
        var fixture = Fixture(referenceable: true);
        var baseline = PublishedBaseline();
        fixture.Baselines.Items.Add(baseline);
        fixture.Definitions.Items.AddRange(Definitions(baseline.Id));

        var first = await fixture.Service.ExecuteAsync(baseline.Id, Scope("blue"), InstantiationSelectionRequest.Default, Correlation, CancellationToken.None);
        var second = await fixture.Service.ExecuteAsync(baseline.Id, Scope("blue"), InstantiationSelectionRequest.Default, Correlation, CancellationToken.None);

        Assert.True(first.IsSuccessful);
        Assert.Equal(201, first.StatusCode);
        Assert.Equal(2, first.Data!.Created);
        Assert.Equal(0, first.Data.Skipped);
        Assert.Equal(2, fixture.Instances.Items.Count);
        Assert.All(fixture.Instances.Items, x =>
        {
            Assert.Equal(TenantId, x.TenantId);
            Assert.Equal(CompanyId, x.CompanyId);
            Assert.Equal("blue", x.InstanceToken);
            Assert.Contains($"|{CompanyId:D}|{baseline.Id:D}|", x.InstanceKey, StringComparison.Ordinal);
            Assert.EndsWith("|blue", x.InstanceKey, StringComparison.Ordinal);
        });

        Assert.True(second.IsSuccessful);
        Assert.Equal(0, second.Data!.Created);
        Assert.Equal(2, second.Data.Skipped);
        Assert.Equal(2, fixture.Instances.Items.Count);
        Assert.Equal(2, fixture.Operations.Items.Count);
        Assert.Equal(4, fixture.Outcomes.Items.Count);
    }

    [Fact]
    public async Task Retry_disabled_returns_controlled_unavailable_failure()
    {
        var fixture = Fixture(referenceable: true, retryEnabled: false);

        var response = await fixture.Service.RetryAsync(Guid.NewGuid(), [], Correlation, CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(405, response.StatusCode);
        Assert.Equal(DocumentManagementInstantiationReasonCodes.RetryUnavailable, response.ReasonCode);
        Assert.Equal(Correlation, response.CorrelationId);
    }

    [Fact]
    public async Task Dry_run_selected_branch_includes_required_ancestors_and_excludes_unselected_siblings()
    {
        var fixture = Fixture(referenceable: true);
        var baseline = PublishedBaseline();
        fixture.Baselines.Items.Add(baseline);
        fixture.Definitions.Items.AddRange(BranchingDefinitions(baseline.Id));

        var response = await fixture.Service.DryRunAsync(
            baseline.Id,
            Scope(),
            Selection("tax-forms"),
            Correlation,
            CancellationToken.None);

        Assert.True(response.IsSuccessful);
        var diagnostics = response.Data!.Diagnostics!;
        Assert.False(diagnostics.Blocked);
        Assert.Equal("SELECTED_BRANCHES", diagnostics.SelectionMode);
        Assert.Equal(["finance", "tax", "tax-forms"], diagnostics.IncludedCanonicalIds);
        Assert.Equal(["tax", "finance"], diagnostics.IncludedAncestors);
        Assert.Empty(diagnostics.IncludedDescendants);
        Assert.Equal(1, diagnostics.ExcludedCanonicalIdsCount);
        Assert.DoesNotContain("hr", diagnostics.IncludedCanonicalIds);
        Assert.Empty(fixture.Instances.Items);
    }

    [Fact]
    public async Task Execute_selected_branch_creates_only_included_nodes_and_later_partial_expands_without_duplicates()
    {
        var fixture = Fixture(referenceable: true);
        var baseline = PublishedBaseline();
        fixture.Baselines.Items.Add(baseline);
        fixture.Definitions.Items.AddRange(BranchingDefinitions(baseline.Id));

        var first = await fixture.Service.ExecuteAsync(
            baseline.Id,
            Scope(),
            Selection("tax"),
            Correlation,
            CancellationToken.None);
        var second = await fixture.Service.ExecuteAsync(
            baseline.Id,
            Scope(),
            Selection("hr"),
            Correlation,
            CancellationToken.None);
        var third = await fixture.Service.ExecuteAsync(
            baseline.Id,
            Scope(),
            Selection("tax"),
            Correlation,
            CancellationToken.None);

        Assert.True(first.IsSuccessful);
        Assert.Equal(3, first.Data!.Created);
        Assert.True(second.IsSuccessful);
        Assert.Equal(1, second.Data!.Created);
        Assert.Equal(1, second.Data.Skipped);
        Assert.True(third.IsSuccessful);
        Assert.Equal(0, third.Data!.Created);
        Assert.Equal(3, third.Data.Skipped);
        Assert.Equal(4, fixture.Instances.Items.Count);
        Assert.Single(fixture.Instances.Items, x => x.CanonicalId == "finance");
        Assert.Single(fixture.Instances.Items, x => x.CanonicalId == "tax");
        Assert.Single(fixture.Instances.Items, x => x.CanonicalId == "tax-forms");
        Assert.Single(fixture.Instances.Items, x => x.CanonicalId == "hr");
    }

    [Fact]
    public async Task Execute_blocked_when_required_ancestors_are_disabled_for_deep_branch()
    {
        var fixture = Fixture(referenceable: true);
        var baseline = PublishedBaseline();
        fixture.Baselines.Items.Add(baseline);
        fixture.Definitions.Items.AddRange(BranchingDefinitions(baseline.Id));

        var dryRun = await fixture.Service.DryRunAsync(
            baseline.Id,
            Scope(),
            Selection("tax-forms", includeRequiredAncestors: false),
            Correlation,
            CancellationToken.None);
        var execute = await fixture.Service.ExecuteAsync(
            baseline.Id,
            Scope(),
            Selection("tax-forms", includeRequiredAncestors: false),
            Correlation,
            CancellationToken.None);

        Assert.True(dryRun.IsSuccessful);
        Assert.True(dryRun.Data!.Diagnostics!.Blocked);
        Assert.False(execute.IsSuccessful);
        Assert.Equal(400, execute.StatusCode);
        Assert.Empty(fixture.Instances.Items);
    }

    [Fact]
    public async Task Local_smoke_fallback_allows_missing_mod0220_only_when_flag_is_enabled()
    {
        var fixture = Fixture(referenceable: false, fallbackEnabled: true);
        var baseline = PublishedBaseline();
        fixture.Baselines.Items.Add(baseline);
        fixture.Definitions.Items.AddRange(Definitions(baseline.Id));

        var response = await fixture.Service.DryRunAsync(baseline.Id, Scope(), InstantiationSelectionRequest.Default, Correlation, CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Contains(response.Data!.Diagnostics!.Warnings, x => x.Contains("Local-smoke", StringComparison.OrdinalIgnoreCase));
    }

    private static TestFixture Fixture(bool referenceable, bool fallbackEnabled = false, bool retryEnabled = false)
    {
        var tenantContext = new TenantContext();
        tenantContext.SetTenant(TenantId);
        var baselines = new FakeBaselineReleaseRepository();
        var definitions = new FakeCollectionDefinitionRepository();
        var instances = new FakeCollectionInstanceRepository();
        var operations = new FakeInstantiationOperationRepository();
        var outcomes = new FakeInstantiationOutcomeRepository();
        var options = Options.Create(new DocumentManagementFeatureFlagOptions
        {
            CompanyProvisioningEnabled = true,
            Mod0220FallbackEnabled = fallbackEnabled,
            InstantiationRetryEnabled = retryEnabled
        });
        var planner = new InstantiationPlanner(
            baselines,
            definitions,
            instances,
            new FakeLegalEntityReferenceValidator(referenceable),
            tenantContext,
            new CompanyInstanceKeyFactory(),
            options);
        var service = new InstantiationService(
            planner,
            instances,
            operations,
            outcomes,
            new FakeCurrentUserContext(),
            tenantContext,
            options);
        return new TestFixture(service, baselines, definitions, instances, operations, outcomes);
    }

    private static InstantiationScopeRequest Scope(string? token = null) =>
        new(CompanyId, null, null, token);

    private static InstantiationSelectionRequest Selection(
        params string[] canonicalIds) =>
        new(InstantiationSelectionMode.SelectedBranches, canonicalIds, true, true);

    private static InstantiationSelectionRequest Selection(
        string canonicalId,
        bool includeRequiredAncestors) =>
        new(InstantiationSelectionMode.SelectedBranches, [canonicalId], true, includeRequiredAncestors);

    private static BaselineRelease PublishedBaseline() => new()
    {
        TenantId = TenantId,
        BaselineReleaseId = "BR-PUB-TEST0001",
        SourceBaselineKey = "manual:test",
        BaselineVersion = "1.0",
        Status = BaselineReleaseStatus.Published,
        PublishedAt = DateTimeOffset.UtcNow
    };

    private static IReadOnlyList<CollectionDefinition> Definitions(Guid baselineId) =>
    [
        new()
        {
            TenantId = TenantId,
            BaselineReleaseId = baselineId,
            CanonicalId = "root",
            Name = "Quality",
            PathSegment = "Quality",
            FullPath = "Quality",
            DefinitionHash = "hash-root",
            DisplayOrder = 1
        },
        new()
        {
            TenantId = TenantId,
            BaselineReleaseId = baselineId,
            CanonicalId = "child",
            ParentCanonicalId = "root",
            Name = "Manuals",
            PathSegment = "Manuals",
            FullPath = "Quality/Manuals",
            DefinitionHash = "hash-child",
            DisplayOrder = 2
        }
    ];

    private static IReadOnlyList<CollectionDefinition> BranchingDefinitions(Guid baselineId) =>
    [
        new()
        {
            TenantId = TenantId,
            BaselineReleaseId = baselineId,
            CanonicalId = "finance",
            Name = "Finance",
            PathSegment = "Finance",
            FullPath = "Finance",
            DefinitionHash = "hash-finance",
            DisplayOrder = 1
        },
        new()
        {
            TenantId = TenantId,
            BaselineReleaseId = baselineId,
            CanonicalId = "tax",
            ParentCanonicalId = "finance",
            Name = "Tax",
            PathSegment = "Tax",
            FullPath = "Finance/Tax",
            DefinitionHash = "hash-tax",
            DisplayOrder = 2
        },
        new()
        {
            TenantId = TenantId,
            BaselineReleaseId = baselineId,
            CanonicalId = "tax-forms",
            ParentCanonicalId = "tax",
            Name = "Tax Forms",
            PathSegment = "Tax Forms",
            FullPath = "Finance/Tax/Tax Forms",
            DefinitionHash = "hash-tax-forms",
            DisplayOrder = 3
        },
        new()
        {
            TenantId = TenantId,
            BaselineReleaseId = baselineId,
            CanonicalId = "hr",
            ParentCanonicalId = "finance",
            Name = "HR",
            PathSegment = "HR",
            FullPath = "Finance/HR",
            DefinitionHash = "hash-hr",
            DisplayOrder = 4
        }
    ];

    private sealed record TestFixture(
        InstantiationService Service,
        FakeBaselineReleaseRepository Baselines,
        FakeCollectionDefinitionRepository Definitions,
        FakeCollectionInstanceRepository Instances,
        FakeInstantiationOperationRepository Operations,
        FakeInstantiationOutcomeRepository Outcomes);

    private sealed class FakeCurrentUserContext : ICurrentUserContext
    {
        public Guid UserId { get; } = Guid.Parse("99999999-9999-9999-9999-999999999999");
        public string? Email => "fu05@example.test";
        public string? DisplayName => "FU05 Tester";
        public string ActorName => "fu05@example.test";
        public bool IsAuthenticated => true;
    }

    private sealed class FakeLegalEntityReferenceValidator : ILegalEntityReferenceValidator
    {
        private readonly bool _referenceable;

        public FakeLegalEntityReferenceValidator(bool referenceable) => _referenceable = referenceable;

        public Task<Response<LegalEntityReferenceDto>> ValidateAsync(Guid legalEntityId, CancellationToken ct = default) =>
            Task.FromResult(_referenceable
                ? Response<LegalEntityReferenceDto>.Success(new LegalEntityReferenceDto(legalEntityId, "Company", "Company", "ACTIVE", true))
                : Response<LegalEntityReferenceDto>.Fail("Legal Entity is not referenceable.", 404));
    }

    private sealed class FakeBaselineReleaseRepository : IBaselineReleaseRepository
    {
        public List<BaselineRelease> Items { get; } = [];
        public Task<BaselineRelease> CreateAsync(BaselineRelease baseline, CancellationToken ct = default) { Items.Add(baseline); return Task.FromResult(baseline); }
        public Task<BaselineRelease?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(x => x.Id == id));
        public Task<IReadOnlyList<BaselineRelease>> GetAllAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<BaselineRelease>>(Items);
        public Task<bool> UpdateAsync(BaselineRelease baseline, int expectedVersion, CancellationToken ct = default) => Task.FromResult(true);
    }

    private sealed class FakeCollectionDefinitionRepository : ICollectionDefinitionRepository
    {
        public List<CollectionDefinition> Items { get; } = [];
        public Task<CollectionDefinition> CreateAsync(CollectionDefinition definition, CancellationToken ct = default) { Items.Add(definition); return Task.FromResult(definition); }
        public Task CreateManyAsync(IReadOnlyList<CollectionDefinition> definitions, CancellationToken ct = default) { Items.AddRange(definitions); return Task.CompletedTask; }
        public Task<IReadOnlyList<CollectionDefinition>> GetByBaselineAsync(Guid baselineReleaseId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<CollectionDefinition>>(Items.Where(x => x.BaselineReleaseId == baselineReleaseId && !x.IsDeleted).ToList());
        public Task<CollectionDefinition?> GetByCanonicalIdAsync(Guid baselineReleaseId, string canonicalId, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(x => x.BaselineReleaseId == baselineReleaseId && x.CanonicalId == canonicalId && !x.IsDeleted));
        public Task<bool> UpdateAsync(CollectionDefinition definition, int expectedVersion, CancellationToken ct = default) => Task.FromResult(true);
        public Task UpdateManyAsync(IReadOnlyList<CollectionDefinition> definitions, CancellationToken ct = default) => Task.CompletedTask;
        public Task<bool> SoftDeleteAsync(CollectionDefinition definition, int expectedVersion, CancellationToken ct = default) => Task.FromResult(true);
    }

    private sealed class FakeCollectionInstanceRepository : ICollectionInstanceRepository
    {
        public List<CollectionInstance> Items { get; } = [];
        public Task<CollectionInstance> CreateAsync(CollectionInstance instance, CancellationToken ct = default) { Items.Add(instance); return Task.FromResult(instance); }
        public Task<IReadOnlyList<CollectionInstance>> CreateManyAsync(IReadOnlyList<CollectionInstance> instances, CancellationToken ct = default) { Items.AddRange(instances); return Task.FromResult(instances); }
        public Task<CollectionInstance?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(x => x.Id == id && !x.IsDeleted));
        public Task<CollectionInstance?> GetByInstanceKeyAsync(string instanceKey, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(x => x.InstanceKey == instanceKey && !x.IsDeleted));
        public Task<IReadOnlyList<CollectionInstance>> GetAllForTenantAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<CollectionInstance>>(Items.Where(x => !x.IsDeleted).ToList());
        public Task<IReadOnlyList<CollectionInstance>> GetByCompanyAsync(Guid companyId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<CollectionInstance>>(Items.Where(x => x.CompanyId == companyId && !x.IsDeleted).ToList());
        public Task<IReadOnlyList<CollectionInstance>> GetByBaselineAndCompanyAsync(Guid baselineReleaseId, Guid companyId, string? instanceToken, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<CollectionInstance>>(Items.Where(x => x.BaselineReleaseId == baselineReleaseId && x.CompanyId == companyId && !x.IsDeleted).ToList());
        // NOTE (MOD-0023 Batch 01, unrelated unblock): the FU05 interface (untracked working-tree change) added
        // these two archive/restore members but this fake was not updated, leaving the whole test assembly
        // uncompilable. Added faithful stubs so the test project builds; no FU05 production code touched.
        public Task<long> ArchiveManyAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default) { var m = Items.Where(x => ids.Contains(x.Id)).ToList(); foreach (var x in m) { x.InstanceStatus = CollectionInstanceStatus.Archived; } return Task.FromResult((long)m.Count); }
        public Task<long> ReactivateManyAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default) { var m = Items.Where(x => ids.Contains(x.Id)).ToList(); foreach (var x in m) { x.InstanceStatus = CollectionInstanceStatus.Active; } return Task.FromResult((long)m.Count); }
    }

    private sealed class FakeInstantiationOperationRepository : IInstantiationOperationRepository
    {
        public List<InstantiationOperation> Items { get; } = [];
        public Task<InstantiationOperation> CreateAsync(InstantiationOperation operation, CancellationToken ct = default) { Items.Add(operation); return Task.FromResult(operation); }
        public Task<InstantiationOperation?> GetByOperationIdAsync(Guid operationId, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(x => x.OperationId == operationId && !x.IsDeleted));
    }

    private sealed class FakeInstantiationOutcomeRepository : IInstantiationOutcomeRepository
    {
        public List<InstantiationOutcome> Items { get; } = [];
        public Task<IReadOnlyList<InstantiationOutcome>> CreateManyAsync(IReadOnlyList<InstantiationOutcome> outcomes, CancellationToken ct = default) { Items.AddRange(outcomes); return Task.FromResult(outcomes); }
        public Task<IReadOnlyList<InstantiationOutcome>> GetByOperationIdAsync(Guid operationId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<InstantiationOutcome>>(Items.Where(x => x.OperationId == operationId && !x.IsDeleted).ToList());
        public Task<IReadOnlyList<InstantiationOutcome>> GetRetryableFailedByOperationIdAsync(Guid operationId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<InstantiationOutcome>>(Items.Where(x => x.OperationId == operationId && x.Status == InstantiationOutcomeStatus.Failed && x.Retryable && !x.IsDeleted).ToList());
    }
}
