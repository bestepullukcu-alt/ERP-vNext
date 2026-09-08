using System.Reflection;
using Diten.TalentEcosystemService.Api.Controllers.Tep;
using Diten.TalentEcosystemService.Api.Security;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.IndustryKnowledgeNetwork;
using Diten.TalentEcosystemService.Application.Features.IndustryKnowledgeNetwork.Commands;
using Diten.TalentEcosystemService.Application.Features.IndustryKnowledgeNetwork.Handlers;
using Diten.TalentEcosystemService.Application.Features.IndustryKnowledgeNetwork.Queries;
using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Enums;
using Diten.TalentEcosystemService.Domain.Repositories;
using Diten.TalentEcosystemService.Persistence.Repositories;
using Xunit;

namespace Diten.TalentEcosystemService.Application.Tests;

public sealed class IndustryKnowledgeNetworkTests
{
    [Fact]
    public async Task Create_list_and_get_metadata_contract()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryIndustryKnowledgeNetworkReadinessMetadataRepository();
        var create = CreateHandler(repository, tenantId);

        var created = await create.Handle(new CreateIndustryKnowledgeNetworkReadinessCommand(ValidRequest()), CancellationToken.None);
        var list = await new GetIndustryKnowledgeNetworkReadinessListHandler(repository, new FixedTenantContext(tenantId))
            .Handle(new GetIndustryKnowledgeNetworkReadinessListQuery(), CancellationToken.None);
        var get = await new GetIndustryKnowledgeNetworkReadinessByIdHandler(repository, new FixedTenantContext(tenantId))
            .Handle(new GetIndustryKnowledgeNetworkReadinessByIdQuery(created.Data), CancellationToken.None);

        Assert.True(created.IsSuccessful);
        Assert.Equal(201, created.StatusCode);
        Assert.True(list.IsSuccessful);
        Assert.Single(list.Data!);
        Assert.True(get.IsSuccessful);
        Assert.Equal("SUCC-001", get.Data!.Code);
        Assert.Equal("IndustryKnowledgeNetwork readiness", get.Data.DisplayName);
        Assert.Equal(IndustryKnowledgeNetworkReadinessState.NotRequired, get.Data.KnowledgeCatalogBoundaryState);
        Assert.Equal(IndustryKnowledgeNetworkReadinessState.NotRequired, get.Data.ContentBindingIntakeBoundaryState);
        Assert.Equal(IndustryKnowledgeNetworkReadinessState.NotRequired, get.Data.NetworkScopeBoundaryState);
        Assert.Equal(IndustryKnowledgeNetworkReadinessState.NotRequired, get.Data.VisibilityControlBoundaryState);
        Assert.Equal(IndustryKnowledgeNetworkReadinessState.NotRequired, get.Data.KnowledgeReviewBoundaryState);
        Assert.Equal(IndustryKnowledgeNetworkReadinessState.NotRequired, get.Data.AutomatedDecisionBoundaryState);
        Assert.Equal(IndustryKnowledgeNetworkReadinessState.NotRequired, get.Data.KnowledgeSourceRegistryDependencyState);
        Assert.Equal(IndustryKnowledgeNetworkReadinessState.NotRequired, get.Data.SectorTrendSourceDependencyState);
        Assert.Equal(IndustryKnowledgeNetworkReadinessState.NotRequired, get.Data.DataGovernancePolicyDependencyState);
        Assert.Equal(IndustryKnowledgeNetworkReadinessState.NotRequired, get.Data.AssociationOperationsSourceDependencyState);
    }

    [Fact]
    public async Task Cross_tenant_lookup_returns_not_found()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var metadata = Metadata(tenantA);
        var handler = new GetIndustryKnowledgeNetworkReadinessByIdHandler(
            new InMemoryIndustryKnowledgeNetworkReadinessMetadataRepository(metadata),
            new FixedTenantContext(tenantB));

        var response = await handler.Handle(new GetIndustryKnowledgeNetworkReadinessByIdQuery(metadata.Id), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Missing_tenant_context_fails_closed()
    {
        var handler = CreateHandler(new InMemoryIndustryKnowledgeNetworkReadinessMetadataRepository(), Guid.Empty);

        var response = await handler.Handle(new CreateIndustryKnowledgeNetworkReadinessCommand(ValidRequest()), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(401, response.StatusCode);
    }

    [Fact]
    public async Task Duplicate_active_code_returns_conflict_per_tenant()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryIndustryKnowledgeNetworkReadinessMetadataRepository();
        var handler = CreateHandler(repository, tenantId);

        var first = await handler.Handle(new CreateIndustryKnowledgeNetworkReadinessCommand(ValidRequest(code: "succ-001")), CancellationToken.None);
        var second = await handler.Handle(new CreateIndustryKnowledgeNetworkReadinessCommand(ValidRequest(code: " SUCC-001 ")), CancellationToken.None);

        Assert.True(first.IsSuccessful);
        Assert.False(second.IsSuccessful);
        Assert.Equal(409, second.StatusCode);
    }

    [Fact]
    public async Task Soft_delete_hides_record_and_sets_deleted_at()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(tenantId);
        var repository = new InMemoryIndustryKnowledgeNetworkReadinessMetadataRepository(metadata);
        var handler = new DeleteIndustryKnowledgeNetworkReadinessHandler(repository, new FixedTenantContext(tenantId));

        var response = await handler.Handle(new DeleteIndustryKnowledgeNetworkReadinessCommand(metadata.Id), CancellationToken.None);
        var hidden = await repository.GetByIdAsync(tenantId, metadata.Id, CancellationToken.None);
        var stored = RepositoryItems(repository)[metadata.Id];

        Assert.True(response.IsSuccessful);
        Assert.Equal(204, response.StatusCode);
        Assert.Null(hidden);
        Assert.True(stored.IsDeleted);
        Assert.NotNull(stored.DeletedAt);
        Assert.Equal(IndustryKnowledgeNetworkReadinessState.Archived, stored.IndustryKnowledgeNetworkReadinessState);
    }

    [Fact]
    public async Task Ready_state_fails_closed_when_preconditions_are_deferred()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryIndustryKnowledgeNetworkReadinessMetadataRepository();
        var handler = CreateHandler(repository, tenantId);

        var response = await handler.Handle(
            new CreateIndustryKnowledgeNetworkReadinessCommand(ValidRequest(readinessState: IndustryKnowledgeNetworkReadinessState.Ready)),
            CancellationToken.None);
        var stored = RepositoryItems(repository)[response.Data];

        Assert.True(response.IsSuccessful);
        Assert.Equal(IndustryKnowledgeNetworkReadinessState.Deferred, stored.IndustryKnowledgeNetworkReadinessState);
        Assert.Contains("deferred", stored.DeferredReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Evaluation_promotes_ready_only_when_metadata_preconditions_are_satisfied()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(
            tenantId,
            consentPreconditionState: IndustryKnowledgeNetworkReadinessState.Ready,
            dataMinimizationState: IndustryKnowledgeNetworkReadinessState.Ready,
            retentionPolicyState: IndustryKnowledgeNetworkReadinessState.Ready,
            publicationPolicyState: IndustryKnowledgeNetworkReadinessState.Ready);
        var repository = new InMemoryIndustryKnowledgeNetworkReadinessMetadataRepository(metadata);
        var handler = new EvaluateIndustryKnowledgeNetworkReadinessHandler(repository, new FixedTenantContext(tenantId));

        var response = await handler.Handle(new EvaluateIndustryKnowledgeNetworkReadinessCommand(metadata.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(IndustryKnowledgeNetworkReadinessState.Ready, response.Data!.IndustryKnowledgeNetworkReadinessState);
        Assert.NotNull(response.Data.LastEvaluatedAt);
    }

    [Fact]
    public async Task Non_activating_evaluation_preserves_deferred_metadata()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(tenantId, consentPreconditionState: IndustryKnowledgeNetworkReadinessState.Deferred);
        var repository = new InMemoryIndustryKnowledgeNetworkReadinessMetadataRepository(metadata);
        var handler = new EvaluateIndustryKnowledgeNetworkReadinessHandler(repository, new FixedTenantContext(tenantId));

        var response = await handler.Handle(new EvaluateIndustryKnowledgeNetworkReadinessCommand(metadata.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(IndustryKnowledgeNetworkReadinessState.Deferred, response.Data!.IndustryKnowledgeNetworkReadinessState);
        Assert.Contains("preconditions", response.Data.DeferredReason);
    }

    [Fact]
    public async Task Pool_highpotential_nomination_assessment_review_and_decision_boundaries_cannot_be_ready()
    {
        var tenantId = Guid.NewGuid();
        var handler = CreateHandler(new InMemoryIndustryKnowledgeNetworkReadinessMetadataRepository(), tenantId);

        var requests = new[]
        {
            ValidRequest(knowledgeCatalogBoundaryState: IndustryKnowledgeNetworkReadinessState.Ready),
            ValidRequest(contentBindingIntakeBoundaryState: IndustryKnowledgeNetworkReadinessState.Ready),
            ValidRequest(networkScopeBoundaryState: IndustryKnowledgeNetworkReadinessState.Ready),
            ValidRequest(visibilityControlBoundaryState: IndustryKnowledgeNetworkReadinessState.Ready),
            ValidRequest(knowledgeReviewBoundaryState: IndustryKnowledgeNetworkReadinessState.Ready),
            ValidRequest(automatedDecisionBoundaryState: IndustryKnowledgeNetworkReadinessState.Ready)
        };

        foreach (var request in requests)
        {
            var response = await handler.Handle(new CreateIndustryKnowledgeNetworkReadinessCommand(request), CancellationToken.None);

            Assert.False(response.IsSuccessful);
            Assert.Equal(400, response.StatusCode);
        }
    }

    [Fact]
    public async Task Audit_metadata_uses_audit_permission_surface()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(tenantId);
        var repository = new InMemoryIndustryKnowledgeNetworkReadinessMetadataRepository(metadata);
        var response = await new GetIndustryKnowledgeNetworkAuditMetadataHandler(repository, new FixedTenantContext(tenantId))
            .Handle(new GetIndustryKnowledgeNetworkAuditMetadataQuery(metadata.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(metadata.Code, response.Data!.Code);
        Assert.Equal(IndustryKnowledgeNetworkGuard.AuditReadPermission, PermissionFor(nameof(IndustryKnowledgeNetworkController.GetAuditMetadata)));
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
        var handler = CreateHandler(new InMemoryIndustryKnowledgeNetworkReadinessMetadataRepository(), tenantId);

        // Marker injected as a STANDALONE token (whitespace-delimited) so the word-boundary
        // (\b) matcher rejects a genuine forbidden token.
        var response = await handler.Handle(
            new CreateIndustryKnowledgeNetworkReadinessCommand(ValidRequest(sourceContractVersion: $"v1 {marker}")),
            CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
    }

    [Theory]
    [InlineData("talent taxonomy")]
    [InlineData("benchmarking taxonomy")]
    [InlineData("nomination scorecard")]
    public async Task Word_boundary_matcher_accepts_legitimate_values_that_merely_contain_marker_substrings(string legitimateValue)
    {
        // Regression guard for the substring false-positive that previously broke LearningTraining:
        // "taxonomy" contains "tax", "scorecard" contains "score" — with the \b matcher these are
        // legitimate standalone tokens and MUST be accepted (create succeeds).
        var tenantId = Guid.NewGuid();
        var handler = CreateHandler(new InMemoryIndustryKnowledgeNetworkReadinessMetadataRepository(), tenantId);

        var response = await handler.Handle(
            new CreateIndustryKnowledgeNetworkReadinessCommand(ValidRequest(displayName: legitimateValue)),
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
            typeof(IndustryKnowledgeNetworkReadinessCreateRequest),
            typeof(IndustryKnowledgeNetworkReadinessDto),
            typeof(IndustryKnowledgeNetworkReadinessMetadata)
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
        Assert.Null(typeof(IndustryKnowledgeNetworkReadinessCreateRequest).GetProperty("TenantId"));
    }

    [Fact]
    public void Controller_permissions_match_pack()
    {
        Assert.Equal(IndustryKnowledgeNetworkGuard.ReadPermission, PermissionFor(nameof(IndustryKnowledgeNetworkController.GetAll)));
        Assert.Equal(IndustryKnowledgeNetworkGuard.ReadPermission, PermissionFor(nameof(IndustryKnowledgeNetworkController.GetById)));
        Assert.Equal(IndustryKnowledgeNetworkGuard.ManagePermission, PermissionFor(nameof(IndustryKnowledgeNetworkController.Create)));
        Assert.Equal(IndustryKnowledgeNetworkGuard.EvaluatePermission, PermissionFor(nameof(IndustryKnowledgeNetworkController.Evaluate)));
        Assert.Equal(IndustryKnowledgeNetworkGuard.ManagePermission, PermissionFor(nameof(IndustryKnowledgeNetworkController.Delete)));
        Assert.Equal(IndustryKnowledgeNetworkGuard.AuditReadPermission, PermissionFor(nameof(IndustryKnowledgeNetworkController.GetAuditMetadata)));
    }

    [Fact]
    public void Mongo_repository_contract_names_are_stable()
    {
        Assert.Equal("tep_industry_knowledge_network_readiness", MongoIndustryKnowledgeNetworkReadinessMetadataRepository.CollectionName);
        Assert.Equal("ux_tep_industry_knowledge_network_tenant_code_active", MongoIndustryKnowledgeNetworkReadinessMetadataRepository.ActiveCodeUniqueIndexName);
        Assert.Equal("ix_tep_industry_knowledge_network_tenant_state", MongoIndustryKnowledgeNetworkReadinessMetadataRepository.TenantStateIndexName);
    }

    [Fact]
    public void Runtime_literal_regression_guard_has_no_reserved_identity_literals()
    {
        var reserved = string.Join("-", "CAND", "CAP", "0060");
        var legacy = string.Join("-", "MOD", "0351");
        var runtimeStrings = new[]
        {
            IndustryKnowledgeNetworkGuard.OwnerKey,
            IndustryKnowledgeNetworkGuard.ReadPermission,
            IndustryKnowledgeNetworkGuard.ManagePermission,
            IndustryKnowledgeNetworkGuard.EvaluatePermission,
            IndustryKnowledgeNetworkGuard.AuditReadPermission,
            MongoIndustryKnowledgeNetworkReadinessMetadataRepository.CollectionName
        };

        Assert.DoesNotContain(runtimeStrings, value => value.Contains(reserved, StringComparison.Ordinal));
        Assert.DoesNotContain(runtimeStrings, value => value.Contains(legacy, StringComparison.Ordinal));
    }

    private static CreateIndustryKnowledgeNetworkReadinessHandler CreateHandler(
        IIndustryKnowledgeNetworkReadinessMetadataRepository repository,
        Guid tenantId) =>
        new(repository, new FixedTenantContext(tenantId == Guid.Empty ? null : tenantId));

    private static IndustryKnowledgeNetworkReadinessCreateRequest ValidRequest(
        string code = "SUCC-001",
        string displayName = "IndustryKnowledgeNetwork readiness",
        IndustryKnowledgeNetworkReadinessState readinessState = IndustryKnowledgeNetworkReadinessState.Draft,
        string sourceContractVersion = "v1",
        IndustryKnowledgeNetworkReadinessState knowledgeCatalogBoundaryState = IndustryKnowledgeNetworkReadinessState.NotRequired,
        IndustryKnowledgeNetworkReadinessState contentBindingIntakeBoundaryState = IndustryKnowledgeNetworkReadinessState.NotRequired,
        IndustryKnowledgeNetworkReadinessState networkScopeBoundaryState = IndustryKnowledgeNetworkReadinessState.NotRequired,
        IndustryKnowledgeNetworkReadinessState visibilityControlBoundaryState = IndustryKnowledgeNetworkReadinessState.NotRequired,
        IndustryKnowledgeNetworkReadinessState knowledgeReviewBoundaryState = IndustryKnowledgeNetworkReadinessState.NotRequired,
        IndustryKnowledgeNetworkReadinessState automatedDecisionBoundaryState = IndustryKnowledgeNetworkReadinessState.NotRequired) =>
        new()
        {
            Code = code,
            DisplayName = displayName,
            IndustryKnowledgeNetworkReadinessState = readinessState,
            KnowledgeCatalogBoundaryState = knowledgeCatalogBoundaryState,
            ContentBindingIntakeBoundaryState = contentBindingIntakeBoundaryState,
            NetworkScopeBoundaryState = networkScopeBoundaryState,
            VisibilityControlBoundaryState = visibilityControlBoundaryState,
            KnowledgeReviewBoundaryState = knowledgeReviewBoundaryState,
            AutomatedDecisionBoundaryState = automatedDecisionBoundaryState,
            KnowledgeSourceRegistryDependencyState = IndustryKnowledgeNetworkReadinessState.NotRequired,
            SectorTrendSourceDependencyState = IndustryKnowledgeNetworkReadinessState.NotRequired,
            DataGovernancePolicyDependencyState = IndustryKnowledgeNetworkReadinessState.NotRequired,
            AssociationOperationsSourceDependencyState = IndustryKnowledgeNetworkReadinessState.NotRequired,
            ConsentPreconditionState = IndustryKnowledgeNetworkReadinessState.Deferred,
            DataMinimizationState = IndustryKnowledgeNetworkReadinessState.Deferred,
            RetentionPolicyState = IndustryKnowledgeNetworkReadinessState.Deferred,
            PublicationPolicyState = IndustryKnowledgeNetworkReadinessState.Deferred,
            DependencyStates = new Dictionary<string, IndustryKnowledgeNetworkReadinessState>
            {
                ["talentDataSource"] = IndustryKnowledgeNetworkReadinessState.Ready,
                ["consentPolicy"] = IndustryKnowledgeNetworkReadinessState.Ready,
                ["knowledgeCatalog"] = IndustryKnowledgeNetworkReadinessState.Ready
            },
            SourceContractVersion = sourceContractVersion,
            IndustryKnowledgeNetworkReadinessVersion = 1
        };

    private static IndustryKnowledgeNetworkReadinessMetadata Metadata(
        Guid tenantId,
        string code = "SUCC-001",
        IndustryKnowledgeNetworkReadinessState consentPreconditionState = IndustryKnowledgeNetworkReadinessState.Deferred,
        IndustryKnowledgeNetworkReadinessState dataMinimizationState = IndustryKnowledgeNetworkReadinessState.Deferred,
        IndustryKnowledgeNetworkReadinessState retentionPolicyState = IndustryKnowledgeNetworkReadinessState.Deferred,
        IndustryKnowledgeNetworkReadinessState publicationPolicyState = IndustryKnowledgeNetworkReadinessState.Deferred) =>
        new()
        {
            TenantId = tenantId,
            Code = code,
            DisplayName = "IndustryKnowledgeNetwork readiness",
            IndustryKnowledgeNetworkReadinessState = IndustryKnowledgeNetworkReadinessState.Draft,
            KnowledgeCatalogBoundaryState = IndustryKnowledgeNetworkReadinessState.NotRequired,
            ContentBindingIntakeBoundaryState = IndustryKnowledgeNetworkReadinessState.NotRequired,
            NetworkScopeBoundaryState = IndustryKnowledgeNetworkReadinessState.NotRequired,
            VisibilityControlBoundaryState = IndustryKnowledgeNetworkReadinessState.NotRequired,
            KnowledgeReviewBoundaryState = IndustryKnowledgeNetworkReadinessState.NotRequired,
            AutomatedDecisionBoundaryState = IndustryKnowledgeNetworkReadinessState.NotRequired,
            KnowledgeSourceRegistryDependencyState = IndustryKnowledgeNetworkReadinessState.NotRequired,
            SectorTrendSourceDependencyState = IndustryKnowledgeNetworkReadinessState.NotRequired,
            DataGovernancePolicyDependencyState = IndustryKnowledgeNetworkReadinessState.NotRequired,
            AssociationOperationsSourceDependencyState = IndustryKnowledgeNetworkReadinessState.NotRequired,
            ConsentPreconditionState = consentPreconditionState,
            DataMinimizationState = dataMinimizationState,
            RetentionPolicyState = retentionPolicyState,
            PublicationPolicyState = publicationPolicyState,
            DependencyStates = new Dictionary<string, IndustryKnowledgeNetworkReadinessState>
            {
                ["talentDataSource"] = IndustryKnowledgeNetworkReadinessState.Ready
            },
            SourceContractVersion = "v1",
            IndustryKnowledgeNetworkReadinessVersion = 1
        };

    private static IReadOnlyDictionary<Guid, IndustryKnowledgeNetworkReadinessMetadata> RepositoryItems(
        InMemoryIndustryKnowledgeNetworkReadinessMetadataRepository repository)
    {
        var field = typeof(InMemoryIndustryKnowledgeNetworkReadinessMetadataRepository)
            .GetField("_items", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (IReadOnlyDictionary<Guid, IndustryKnowledgeNetworkReadinessMetadata>)field.GetValue(repository)!;
    }

    private static string PermissionFor(string methodName)
    {
        var method = typeof(IndustryKnowledgeNetworkController).GetMethod(methodName)!;
        var attribute = method.GetCustomAttribute<HasPermissionAttribute>()!;
        var field = typeof(HasPermissionAttribute).GetField("_permission", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (string)field.GetValue(attribute)!;
    }

    private sealed class FixedTenantContext : ITenantContext
    {
        public FixedTenantContext(Guid? tenantId) => TenantId = tenantId;

        public Guid? TenantId { get; }
    }

    private sealed class InMemoryIndustryKnowledgeNetworkReadinessMetadataRepository : IIndustryKnowledgeNetworkReadinessMetadataRepository
    {
        private readonly Dictionary<Guid, IndustryKnowledgeNetworkReadinessMetadata> _items;

        public InMemoryIndustryKnowledgeNetworkReadinessMetadataRepository(params IndustryKnowledgeNetworkReadinessMetadata[] items) =>
            _items = items.ToDictionary(x => x.Id);

        public Task<IReadOnlyList<IndustryKnowledgeNetworkReadinessMetadata>> ListAsync(Guid tenantId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<IndustryKnowledgeNetworkReadinessMetadata>>(
                _items.Values.Where(x => x.TenantId == tenantId && !x.IsDeleted).OrderBy(x => x.Code).ToList());

        public Task<IndustryKnowledgeNetworkReadinessMetadata?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct) =>
            Task.FromResult(_items.Values.FirstOrDefault(x => x.TenantId == tenantId && x.Id == id && !x.IsDeleted));

        public Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct) =>
            Task.FromResult(_items.Values.Any(x => x.TenantId == tenantId && x.Code == code && !x.IsDeleted && x.Id != excludingId));

        public Task CreateAsync(IndustryKnowledgeNetworkReadinessMetadata metadata, CancellationToken ct)
        {
            _items[metadata.Id] = metadata;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(IndustryKnowledgeNetworkReadinessMetadata metadata, CancellationToken ct)
        {
            _items[metadata.Id] = metadata;
            return Task.CompletedTask;
        }
    }
}
