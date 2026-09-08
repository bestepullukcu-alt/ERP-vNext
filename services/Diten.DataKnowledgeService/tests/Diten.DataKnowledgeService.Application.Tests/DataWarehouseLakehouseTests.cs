using System.Reflection;
using Diten.DataKnowledgeService.Api.Controllers.Dki;
using Diten.DataKnowledgeService.Api.Security;
using Diten.DataKnowledgeService.Application.Contracts;
using Diten.DataKnowledgeService.Application.Features.DataWarehouseLakehouse;
using Diten.DataKnowledgeService.Application.Features.DataWarehouseLakehouse.Commands;
using Diten.DataKnowledgeService.Application.Features.DataWarehouseLakehouse.Handlers;
using Diten.DataKnowledgeService.Application.Features.DataWarehouseLakehouse.Queries;
using Diten.DataKnowledgeService.Domain.Entities;
using Diten.DataKnowledgeService.Domain.Enums;
using Diten.DataKnowledgeService.Domain.Repositories;
using Diten.DataKnowledgeService.Persistence.Repositories;
using Xunit;

namespace Diten.DataKnowledgeService.Application.Tests;

public sealed class DataWarehouseLakehouseTests
{
    [Fact]
    public async Task Create_list_and_get_metadata_contract()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryDataWarehouseLakehouseReadinessMetadataRepository();
        var create = CreateHandler(repository, tenantId);

        var created = await create.Handle(new CreateDataWarehouseLakehouseReadinessCommand(ValidRequest()), CancellationToken.None);
        var list = await new GetDataWarehouseLakehouseReadinessListHandler(repository, new FixedTenantContext(tenantId))
            .Handle(new GetDataWarehouseLakehouseReadinessListQuery(), CancellationToken.None);
        var get = await new GetDataWarehouseLakehouseReadinessByIdHandler(repository, new FixedTenantContext(tenantId))
            .Handle(new GetDataWarehouseLakehouseReadinessByIdQuery(created.Data), CancellationToken.None);

        Assert.True(created.IsSuccessful);
        Assert.Equal(201, created.StatusCode);
        Assert.True(list.IsSuccessful);
        Assert.Single(list.Data!);
        Assert.True(get.IsSuccessful);
        Assert.Equal("SUCC-001", get.Data!.Code);
        Assert.Equal("DataWarehouseLakehouse readiness", get.Data.DisplayName);
        Assert.Equal(DataWarehouseLakehouseReadinessState.NotRequired, get.Data.StorageLayerCatalogBoundaryState);
        Assert.Equal(DataWarehouseLakehouseReadinessState.NotRequired, get.Data.IngestionIntakeBoundaryState);
        Assert.Equal(DataWarehouseLakehouseReadinessState.NotRequired, get.Data.PartitioningScopeBoundaryState);
        Assert.Equal(DataWarehouseLakehouseReadinessState.NotRequired, get.Data.LineageControlBoundaryState);
        Assert.Equal(DataWarehouseLakehouseReadinessState.NotRequired, get.Data.WarehouseReviewBoundaryState);
        Assert.Equal(DataWarehouseLakehouseReadinessState.NotRequired, get.Data.AutomatedDecisionBoundaryState);
        Assert.Equal(DataWarehouseLakehouseReadinessState.NotRequired, get.Data.VaultDependencyState);
        Assert.Equal(DataWarehouseLakehouseReadinessState.NotRequired, get.Data.LoggingMonitoringDependencyState);
        Assert.Equal(DataWarehouseLakehouseReadinessState.NotRequired, get.Data.DataContractRegistryDependencyState);
        Assert.Equal(DataWarehouseLakehouseReadinessState.NotRequired, get.Data.NotificationDependencyState);
    }

    [Fact]
    public async Task Cross_tenant_lookup_returns_not_found()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var metadata = Metadata(tenantA);
        var handler = new GetDataWarehouseLakehouseReadinessByIdHandler(
            new InMemoryDataWarehouseLakehouseReadinessMetadataRepository(metadata),
            new FixedTenantContext(tenantB));

        var response = await handler.Handle(new GetDataWarehouseLakehouseReadinessByIdQuery(metadata.Id), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Missing_tenant_context_fails_closed()
    {
        var handler = CreateHandler(new InMemoryDataWarehouseLakehouseReadinessMetadataRepository(), Guid.Empty);

        var response = await handler.Handle(new CreateDataWarehouseLakehouseReadinessCommand(ValidRequest()), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(401, response.StatusCode);
    }

    [Fact]
    public async Task Duplicate_active_code_returns_conflict_per_tenant()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryDataWarehouseLakehouseReadinessMetadataRepository();
        var handler = CreateHandler(repository, tenantId);

        var first = await handler.Handle(new CreateDataWarehouseLakehouseReadinessCommand(ValidRequest(code: "succ-001")), CancellationToken.None);
        var second = await handler.Handle(new CreateDataWarehouseLakehouseReadinessCommand(ValidRequest(code: " SUCC-001 ")), CancellationToken.None);

        Assert.True(first.IsSuccessful);
        Assert.False(second.IsSuccessful);
        Assert.Equal(409, second.StatusCode);
    }

    [Fact]
    public async Task Soft_delete_hides_record_and_sets_deleted_at()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(tenantId);
        var repository = new InMemoryDataWarehouseLakehouseReadinessMetadataRepository(metadata);
        var handler = new DeleteDataWarehouseLakehouseReadinessHandler(repository, new FixedTenantContext(tenantId));

        var response = await handler.Handle(new DeleteDataWarehouseLakehouseReadinessCommand(metadata.Id), CancellationToken.None);
        var hidden = await repository.GetByIdAsync(tenantId, metadata.Id, CancellationToken.None);
        var stored = RepositoryItems(repository)[metadata.Id];

        Assert.True(response.IsSuccessful);
        Assert.Equal(204, response.StatusCode);
        Assert.Null(hidden);
        Assert.True(stored.IsDeleted);
        Assert.NotNull(stored.DeletedAt);
        Assert.Equal(DataWarehouseLakehouseReadinessState.Archived, stored.DataWarehouseLakehouseReadinessState);
    }

    [Fact]
    public async Task Ready_state_fails_closed_when_preconditions_are_deferred()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryDataWarehouseLakehouseReadinessMetadataRepository();
        var handler = CreateHandler(repository, tenantId);

        var response = await handler.Handle(
            new CreateDataWarehouseLakehouseReadinessCommand(ValidRequest(readinessState: DataWarehouseLakehouseReadinessState.Ready)),
            CancellationToken.None);
        var stored = RepositoryItems(repository)[response.Data];

        Assert.True(response.IsSuccessful);
        Assert.Equal(DataWarehouseLakehouseReadinessState.Deferred, stored.DataWarehouseLakehouseReadinessState);
        Assert.Contains("deferred", stored.DeferredReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Evaluation_promotes_ready_only_when_metadata_preconditions_are_satisfied()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(
            tenantId,
            stewardshipPreconditionState: DataWarehouseLakehouseReadinessState.Ready,
            dataMinimizationState: DataWarehouseLakehouseReadinessState.Ready,
            retentionPolicyState: DataWarehouseLakehouseReadinessState.Ready,
            storageTierPolicyState: DataWarehouseLakehouseReadinessState.Ready);
        var repository = new InMemoryDataWarehouseLakehouseReadinessMetadataRepository(metadata);
        var handler = new EvaluateDataWarehouseLakehouseReadinessHandler(repository, new FixedTenantContext(tenantId));

        var response = await handler.Handle(new EvaluateDataWarehouseLakehouseReadinessCommand(metadata.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(DataWarehouseLakehouseReadinessState.Ready, response.Data!.DataWarehouseLakehouseReadinessState);
        Assert.NotNull(response.Data.LastEvaluatedAt);
    }

    [Fact]
    public async Task Non_activating_evaluation_preserves_deferred_metadata()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(tenantId, stewardshipPreconditionState: DataWarehouseLakehouseReadinessState.Deferred);
        var repository = new InMemoryDataWarehouseLakehouseReadinessMetadataRepository(metadata);
        var handler = new EvaluateDataWarehouseLakehouseReadinessHandler(repository, new FixedTenantContext(tenantId));

        var response = await handler.Handle(new EvaluateDataWarehouseLakehouseReadinessCommand(metadata.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(DataWarehouseLakehouseReadinessState.Deferred, response.Data!.DataWarehouseLakehouseReadinessState);
        Assert.Contains("preconditions", response.Data.DeferredReason);
    }

    [Fact]
    public async Task Pool_highpotential_nomination_assessment_review_and_decision_boundaries_cannot_be_ready()
    {
        var tenantId = Guid.NewGuid();
        var handler = CreateHandler(new InMemoryDataWarehouseLakehouseReadinessMetadataRepository(), tenantId);

        var requests = new[]
        {
            ValidRequest(storageLayerCatalogBoundaryState: DataWarehouseLakehouseReadinessState.Ready),
            ValidRequest(ingestionIntakeBoundaryState: DataWarehouseLakehouseReadinessState.Ready),
            ValidRequest(partitioningScopeBoundaryState: DataWarehouseLakehouseReadinessState.Ready),
            ValidRequest(lineageControlBoundaryState: DataWarehouseLakehouseReadinessState.Ready),
            ValidRequest(warehouseReviewBoundaryState: DataWarehouseLakehouseReadinessState.Ready),
            ValidRequest(automatedDecisionBoundaryState: DataWarehouseLakehouseReadinessState.Ready)
        };

        foreach (var request in requests)
        {
            var response = await handler.Handle(new CreateDataWarehouseLakehouseReadinessCommand(request), CancellationToken.None);

            Assert.False(response.IsSuccessful);
            Assert.Equal(400, response.StatusCode);
        }
    }

    [Fact]
    public async Task Audit_metadata_uses_audit_permission_surface()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(tenantId);
        var repository = new InMemoryDataWarehouseLakehouseReadinessMetadataRepository(metadata);
        var response = await new GetDataWarehouseLakehouseAuditMetadataHandler(repository, new FixedTenantContext(tenantId))
            .Handle(new GetDataWarehouseLakehouseAuditMetadataQuery(metadata.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(metadata.Code, response.Data!.Code);
        Assert.Equal(DataWarehouseLakehouseGuard.AuditReadPermission, PermissionFor(nameof(DataWarehouseLakehouseController.GetAuditMetadata)));
    }

    [Theory]
    [InlineData("workflow_body")]
    [InlineData("review_note")]
    [InlineData("appraisal_narrative")]
    [InlineData("free_text")]
    [InlineData("attachment")]
    [InlineData("provider_payload")]
    [InlineData("credential")]
    [InlineData("score")]
    [InlineData("rating")]
    [InlineData("calibration")]
    [InlineData("rank")]
    [InlineData("model_output")]
    [InlineData("automated_decision")]
    [InlineData("salary")]
    [InlineData("payroll")]
    [InlineData("benefits_election")]
    [InlineData("password")]
    [InlineData("tax")]
    public async Task Forbidden_workflow_scoring_rating_calibration_ranking_decision_and_sensitive_markers_are_rejected(string marker)
    {
        var tenantId = Guid.NewGuid();
        var handler = CreateHandler(new InMemoryDataWarehouseLakehouseReadinessMetadataRepository(), tenantId);

        // Marker injected as a STANDALONE token (whitespace-delimited) so the word-boundary
        // (\b) matcher rejects a genuine forbidden token.
        var response = await handler.Handle(
            new CreateDataWarehouseLakehouseReadinessCommand(ValidRequest(sourceContractVersion: $"v1 {marker}")),
            CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
    }

    [Theory]
    [InlineData("talent taxonomy")]
    [InlineData("data-warehouse-lakehouse pipeline")]
    [InlineData("nomination scorecard")]
    public async Task Word_boundary_matcher_accepts_legitimate_values_that_merely_contain_marker_substrings(string legitimateValue)
    {
        // Regression guard for the substring false-positive that previously broke LearningTraining:
        // "taxonomy" contains "tax", "scorecard" contains "score" — with the \b matcher these are
        // legitimate standalone tokens and MUST be accepted (create succeeds).
        var tenantId = Guid.NewGuid();
        var handler = CreateHandler(new InMemoryDataWarehouseLakehouseReadinessMetadataRepository(), tenantId);

        var response = await handler.Handle(
            new CreateDataWarehouseLakehouseReadinessCommand(ValidRequest(displayName: legitimateValue)),
            CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(201, response.StatusCode);
    }

    [Fact]
    public void Public_and_persisted_contract_excludes_forbidden_payload_and_sensitive_fields()
    {
        var forbiddenFragments = new[]
        {
            "WorkflowBody",
            "ManagerNote",
            "HrNote",
            "EmployeeStatement",
            "ReviewNote",
            "Appraisal",
            "FreeText",
            "Narrative",
            "GoalScore",
            "RatingValue",
            "RankValue",
            "CalibrationOutcome",
            "ModelOutput",
            "AutomatedDecisionResult",
            "Amount",
            "Salary",
            "Wage",
            "Bank",
            "TaxDetail",
            "TaxIdentifier",
            "PayrollDetail",
            "BenefitsElection",
            "Payload",
            "Credential",
            "Token",
            "Secret",
            "Password",
            "National",
            "Birth",
            "HomeAddress",
            "Biometric",
            "Geolocation"
        };
        var contractTypes = new[]
        {
            typeof(DataWarehouseLakehouseReadinessCreateRequest),
            typeof(DataWarehouseLakehouseReadinessDto),
            typeof(DataWarehouseLakehouseReadinessMetadata)
        };

        var names = contractTypes
            .SelectMany(type => type.GetProperties().Select(property => property.Name))
            .ToList();

        foreach (var fragment in forbiddenFragments)
        {
            Assert.DoesNotContain(names, name => name.Contains(fragment, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void Request_contract_does_not_accept_tenant_id()
    {
        Assert.Null(typeof(DataWarehouseLakehouseReadinessCreateRequest).GetProperty("TenantId"));
    }

    [Fact]
    public void Controller_permissions_match_pack()
    {
        Assert.Equal(DataWarehouseLakehouseGuard.ReadPermission, PermissionFor(nameof(DataWarehouseLakehouseController.GetAll)));
        Assert.Equal(DataWarehouseLakehouseGuard.ReadPermission, PermissionFor(nameof(DataWarehouseLakehouseController.GetById)));
        Assert.Equal(DataWarehouseLakehouseGuard.ManagePermission, PermissionFor(nameof(DataWarehouseLakehouseController.Create)));
        Assert.Equal(DataWarehouseLakehouseGuard.EvaluatePermission, PermissionFor(nameof(DataWarehouseLakehouseController.Evaluate)));
        Assert.Equal(DataWarehouseLakehouseGuard.ManagePermission, PermissionFor(nameof(DataWarehouseLakehouseController.Delete)));
        Assert.Equal(DataWarehouseLakehouseGuard.AuditReadPermission, PermissionFor(nameof(DataWarehouseLakehouseController.GetAuditMetadata)));
    }

    [Fact]
    public void Mongo_repository_contract_names_are_stable()
    {
        Assert.Equal("dki_data_warehouse_lakehouse_readiness", MongoDataWarehouseLakehouseReadinessMetadataRepository.CollectionName);
        Assert.Equal("ux_dki_data_warehouse_lakehouse_tenant_code_active", MongoDataWarehouseLakehouseReadinessMetadataRepository.ActiveCodeUniqueIndexName);
        Assert.Equal("ix_dki_data_warehouse_lakehouse_tenant_state", MongoDataWarehouseLakehouseReadinessMetadataRepository.TenantStateIndexName);
    }

    [Fact]
    public void Runtime_literal_regression_guard_has_no_reserved_identity_literals()
    {
        var reserved = string.Join("-", "CAND", "CAP");
        var legacy = string.Join("-", "MOD", "0063");
        var runtimeStrings = new[]
        {
            DataWarehouseLakehouseGuard.OwnerKey,
            DataWarehouseLakehouseGuard.ReadPermission,
            DataWarehouseLakehouseGuard.ManagePermission,
            DataWarehouseLakehouseGuard.EvaluatePermission,
            DataWarehouseLakehouseGuard.AuditReadPermission,
            MongoDataWarehouseLakehouseReadinessMetadataRepository.CollectionName
        };

        Assert.DoesNotContain(runtimeStrings, value => value.Contains(reserved, StringComparison.Ordinal));
        Assert.DoesNotContain(runtimeStrings, value => value.Contains(legacy, StringComparison.Ordinal));
    }

    private static CreateDataWarehouseLakehouseReadinessHandler CreateHandler(
        IDataWarehouseLakehouseReadinessMetadataRepository repository,
        Guid tenantId) =>
        new(repository, new FixedTenantContext(tenantId == Guid.Empty ? null : tenantId));

    private static DataWarehouseLakehouseReadinessCreateRequest ValidRequest(
        string code = "SUCC-001",
        string displayName = "DataWarehouseLakehouse readiness",
        DataWarehouseLakehouseReadinessState readinessState = DataWarehouseLakehouseReadinessState.Draft,
        string sourceContractVersion = "v1",
        DataWarehouseLakehouseReadinessState storageLayerCatalogBoundaryState = DataWarehouseLakehouseReadinessState.NotRequired,
        DataWarehouseLakehouseReadinessState ingestionIntakeBoundaryState = DataWarehouseLakehouseReadinessState.NotRequired,
        DataWarehouseLakehouseReadinessState partitioningScopeBoundaryState = DataWarehouseLakehouseReadinessState.NotRequired,
        DataWarehouseLakehouseReadinessState lineageControlBoundaryState = DataWarehouseLakehouseReadinessState.NotRequired,
        DataWarehouseLakehouseReadinessState warehouseReviewBoundaryState = DataWarehouseLakehouseReadinessState.NotRequired,
        DataWarehouseLakehouseReadinessState automatedDecisionBoundaryState = DataWarehouseLakehouseReadinessState.NotRequired) =>
        new()
        {
            Code = code,
            DisplayName = displayName,
            DataWarehouseLakehouseReadinessState = readinessState,
            StorageLayerCatalogBoundaryState = storageLayerCatalogBoundaryState,
            IngestionIntakeBoundaryState = ingestionIntakeBoundaryState,
            PartitioningScopeBoundaryState = partitioningScopeBoundaryState,
            LineageControlBoundaryState = lineageControlBoundaryState,
            WarehouseReviewBoundaryState = warehouseReviewBoundaryState,
            AutomatedDecisionBoundaryState = automatedDecisionBoundaryState,
            VaultDependencyState = DataWarehouseLakehouseReadinessState.NotRequired,
            LoggingMonitoringDependencyState = DataWarehouseLakehouseReadinessState.NotRequired,
            DataContractRegistryDependencyState = DataWarehouseLakehouseReadinessState.NotRequired,
            NotificationDependencyState = DataWarehouseLakehouseReadinessState.NotRequired,
            StewardshipPreconditionState = DataWarehouseLakehouseReadinessState.Deferred,
            DataMinimizationState = DataWarehouseLakehouseReadinessState.Deferred,
            RetentionPolicyState = DataWarehouseLakehouseReadinessState.Deferred,
            StorageTierPolicyState = DataWarehouseLakehouseReadinessState.Deferred,
            DependencyStates = new Dictionary<string, DataWarehouseLakehouseReadinessState>
            {
                ["talentDataSource"] = DataWarehouseLakehouseReadinessState.Ready,
                ["consentPolicy"] = DataWarehouseLakehouseReadinessState.Ready,
                ["storageLayerCatalog"] = DataWarehouseLakehouseReadinessState.Ready
            },
            SourceContractVersion = sourceContractVersion,
            DataWarehouseLakehouseReadinessVersion = 1
        };

    private static DataWarehouseLakehouseReadinessMetadata Metadata(
        Guid tenantId,
        string code = "SUCC-001",
        DataWarehouseLakehouseReadinessState stewardshipPreconditionState = DataWarehouseLakehouseReadinessState.Deferred,
        DataWarehouseLakehouseReadinessState dataMinimizationState = DataWarehouseLakehouseReadinessState.Deferred,
        DataWarehouseLakehouseReadinessState retentionPolicyState = DataWarehouseLakehouseReadinessState.Deferred,
        DataWarehouseLakehouseReadinessState storageTierPolicyState = DataWarehouseLakehouseReadinessState.Deferred) =>
        new()
        {
            TenantId = tenantId,
            Code = code,
            DisplayName = "DataWarehouseLakehouse readiness",
            DataWarehouseLakehouseReadinessState = DataWarehouseLakehouseReadinessState.Draft,
            StorageLayerCatalogBoundaryState = DataWarehouseLakehouseReadinessState.NotRequired,
            IngestionIntakeBoundaryState = DataWarehouseLakehouseReadinessState.NotRequired,
            PartitioningScopeBoundaryState = DataWarehouseLakehouseReadinessState.NotRequired,
            LineageControlBoundaryState = DataWarehouseLakehouseReadinessState.NotRequired,
            WarehouseReviewBoundaryState = DataWarehouseLakehouseReadinessState.NotRequired,
            AutomatedDecisionBoundaryState = DataWarehouseLakehouseReadinessState.NotRequired,
            VaultDependencyState = DataWarehouseLakehouseReadinessState.NotRequired,
            LoggingMonitoringDependencyState = DataWarehouseLakehouseReadinessState.NotRequired,
            DataContractRegistryDependencyState = DataWarehouseLakehouseReadinessState.NotRequired,
            NotificationDependencyState = DataWarehouseLakehouseReadinessState.NotRequired,
            StewardshipPreconditionState = stewardshipPreconditionState,
            DataMinimizationState = dataMinimizationState,
            RetentionPolicyState = retentionPolicyState,
            StorageTierPolicyState = storageTierPolicyState,
            DependencyStates = new Dictionary<string, DataWarehouseLakehouseReadinessState>
            {
                ["talentDataSource"] = DataWarehouseLakehouseReadinessState.Ready
            },
            SourceContractVersion = "v1",
            DataWarehouseLakehouseReadinessVersion = 1
        };

    private static IReadOnlyDictionary<Guid, DataWarehouseLakehouseReadinessMetadata> RepositoryItems(
        InMemoryDataWarehouseLakehouseReadinessMetadataRepository repository)
    {
        var field = typeof(InMemoryDataWarehouseLakehouseReadinessMetadataRepository)
            .GetField("_items", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (IReadOnlyDictionary<Guid, DataWarehouseLakehouseReadinessMetadata>)field.GetValue(repository)!;
    }

    private static string PermissionFor(string methodName)
    {
        var method = typeof(DataWarehouseLakehouseController).GetMethod(methodName)!;
        var attribute = method.GetCustomAttribute<HasPermissionAttribute>()!;
        var field = typeof(HasPermissionAttribute).GetField("_permission", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (string)field.GetValue(attribute)!;
    }

    private sealed class FixedTenantContext : ITenantContext
    {
        public FixedTenantContext(Guid? tenantId) => TenantId = tenantId;

        public Guid? TenantId { get; }
    }

    private sealed class InMemoryDataWarehouseLakehouseReadinessMetadataRepository : IDataWarehouseLakehouseReadinessMetadataRepository
    {
        private readonly Dictionary<Guid, DataWarehouseLakehouseReadinessMetadata> _items;

        public InMemoryDataWarehouseLakehouseReadinessMetadataRepository(params DataWarehouseLakehouseReadinessMetadata[] items) =>
            _items = items.ToDictionary(x => x.Id);

        public Task<IReadOnlyList<DataWarehouseLakehouseReadinessMetadata>> ListAsync(Guid tenantId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<DataWarehouseLakehouseReadinessMetadata>>(
                _items.Values.Where(x => x.TenantId == tenantId && !x.IsDeleted).OrderBy(x => x.Code).ToList());

        public Task<DataWarehouseLakehouseReadinessMetadata?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct) =>
            Task.FromResult(_items.Values.FirstOrDefault(x => x.TenantId == tenantId && x.Id == id && !x.IsDeleted));

        public Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct) =>
            Task.FromResult(_items.Values.Any(x => x.TenantId == tenantId && x.Code == code && !x.IsDeleted && x.Id != excludingId));

        public Task CreateAsync(DataWarehouseLakehouseReadinessMetadata metadata, CancellationToken ct)
        {
            _items[metadata.Id] = metadata;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(DataWarehouseLakehouseReadinessMetadata metadata, CancellationToken ct)
        {
            _items[metadata.Id] = metadata;
            return Task.CompletedTask;
        }
    }
}
