using System.Reflection;
using Diten.TalentEcosystemService.Api.Controllers.Tep;
using Diten.TalentEcosystemService.Api.Security;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.IndustrySuccessionPool;
using Diten.TalentEcosystemService.Application.Features.IndustrySuccessionPool.Commands;
using Diten.TalentEcosystemService.Application.Features.IndustrySuccessionPool.Handlers;
using Diten.TalentEcosystemService.Application.Features.IndustrySuccessionPool.Queries;
using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Enums;
using Diten.TalentEcosystemService.Domain.Repositories;
using Diten.TalentEcosystemService.Persistence.Repositories;
using Xunit;

namespace Diten.TalentEcosystemService.Application.Tests;

public sealed class IndustrySuccessionPoolTests
{
    [Fact]
    public async Task Create_list_and_get_metadata_contract()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryIndustrySuccessionPoolReadinessMetadataRepository();
        var create = CreateHandler(repository, tenantId);

        var created = await create.Handle(new CreateIndustrySuccessionPoolReadinessCommand(ValidRequest()), CancellationToken.None);
        var list = await new GetIndustrySuccessionPoolReadinessListHandler(repository, new FixedTenantContext(tenantId))
            .Handle(new GetIndustrySuccessionPoolReadinessListQuery(), CancellationToken.None);
        var get = await new GetIndustrySuccessionPoolReadinessByIdHandler(repository, new FixedTenantContext(tenantId))
            .Handle(new GetIndustrySuccessionPoolReadinessByIdQuery(created.Data), CancellationToken.None);

        Assert.True(created.IsSuccessful);
        Assert.Equal(201, created.StatusCode);
        Assert.True(list.IsSuccessful);
        Assert.Single(list.Data!);
        Assert.True(get.IsSuccessful);
        Assert.Equal("SUCC-001", get.Data!.Code);
        Assert.Equal("IndustrySuccessionPool readiness", get.Data.DisplayName);
        Assert.Equal(IndustrySuccessionPoolReadinessState.NotRequired, get.Data.PoolCatalogBoundaryState);
        Assert.Equal(IndustrySuccessionPoolReadinessState.NotRequired, get.Data.CandidateInclusionIntakeBoundaryState);
        Assert.Equal(IndustrySuccessionPoolReadinessState.NotRequired, get.Data.ReadinessTierScopeBoundaryState);
        Assert.Equal(IndustrySuccessionPoolReadinessState.NotRequired, get.Data.VisibilityControlBoundaryState);
        Assert.Equal(IndustrySuccessionPoolReadinessState.NotRequired, get.Data.SuccessionReviewBoundaryState);
        Assert.Equal(IndustrySuccessionPoolReadinessState.NotRequired, get.Data.AutomatedDecisionBoundaryState);
        Assert.Equal(IndustrySuccessionPoolReadinessState.NotRequired, get.Data.TalentDataSourceDependencyState);
        Assert.Equal(IndustrySuccessionPoolReadinessState.NotRequired, get.Data.ConsentPolicyDependencyState);
        Assert.Equal(IndustrySuccessionPoolReadinessState.NotRequired, get.Data.TalentPoolSourceDependencyState);
        Assert.Equal(IndustrySuccessionPoolReadinessState.NotRequired, get.Data.NotificationDependencyState);
    }

    [Fact]
    public async Task Cross_tenant_lookup_returns_not_found()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var metadata = Metadata(tenantA);
        var handler = new GetIndustrySuccessionPoolReadinessByIdHandler(
            new InMemoryIndustrySuccessionPoolReadinessMetadataRepository(metadata),
            new FixedTenantContext(tenantB));

        var response = await handler.Handle(new GetIndustrySuccessionPoolReadinessByIdQuery(metadata.Id), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Missing_tenant_context_fails_closed()
    {
        var handler = CreateHandler(new InMemoryIndustrySuccessionPoolReadinessMetadataRepository(), Guid.Empty);

        var response = await handler.Handle(new CreateIndustrySuccessionPoolReadinessCommand(ValidRequest()), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(401, response.StatusCode);
    }

    [Fact]
    public async Task Duplicate_active_code_returns_conflict_per_tenant()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryIndustrySuccessionPoolReadinessMetadataRepository();
        var handler = CreateHandler(repository, tenantId);

        var first = await handler.Handle(new CreateIndustrySuccessionPoolReadinessCommand(ValidRequest(code: "succ-001")), CancellationToken.None);
        var second = await handler.Handle(new CreateIndustrySuccessionPoolReadinessCommand(ValidRequest(code: " SUCC-001 ")), CancellationToken.None);

        Assert.True(first.IsSuccessful);
        Assert.False(second.IsSuccessful);
        Assert.Equal(409, second.StatusCode);
    }

    [Fact]
    public async Task Soft_delete_hides_record_and_sets_deleted_at()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(tenantId);
        var repository = new InMemoryIndustrySuccessionPoolReadinessMetadataRepository(metadata);
        var handler = new DeleteIndustrySuccessionPoolReadinessHandler(repository, new FixedTenantContext(tenantId));

        var response = await handler.Handle(new DeleteIndustrySuccessionPoolReadinessCommand(metadata.Id), CancellationToken.None);
        var hidden = await repository.GetByIdAsync(tenantId, metadata.Id, CancellationToken.None);
        var stored = RepositoryItems(repository)[metadata.Id];

        Assert.True(response.IsSuccessful);
        Assert.Equal(204, response.StatusCode);
        Assert.Null(hidden);
        Assert.True(stored.IsDeleted);
        Assert.NotNull(stored.DeletedAt);
        Assert.Equal(IndustrySuccessionPoolReadinessState.Archived, stored.IndustrySuccessionPoolReadinessState);
    }

    [Fact]
    public async Task Ready_state_fails_closed_when_preconditions_are_deferred()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryIndustrySuccessionPoolReadinessMetadataRepository();
        var handler = CreateHandler(repository, tenantId);

        var response = await handler.Handle(
            new CreateIndustrySuccessionPoolReadinessCommand(ValidRequest(readinessState: IndustrySuccessionPoolReadinessState.Ready)),
            CancellationToken.None);
        var stored = RepositoryItems(repository)[response.Data];

        Assert.True(response.IsSuccessful);
        Assert.Equal(IndustrySuccessionPoolReadinessState.Deferred, stored.IndustrySuccessionPoolReadinessState);
        Assert.Contains("deferred", stored.DeferredReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Evaluation_promotes_ready_only_when_metadata_preconditions_are_satisfied()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(
            tenantId,
            consentPreconditionState: IndustrySuccessionPoolReadinessState.Ready,
            dataMinimizationState: IndustrySuccessionPoolReadinessState.Ready,
            retentionPolicyState: IndustrySuccessionPoolReadinessState.Ready,
            publicationPolicyState: IndustrySuccessionPoolReadinessState.Ready);
        var repository = new InMemoryIndustrySuccessionPoolReadinessMetadataRepository(metadata);
        var handler = new EvaluateIndustrySuccessionPoolReadinessHandler(repository, new FixedTenantContext(tenantId));

        var response = await handler.Handle(new EvaluateIndustrySuccessionPoolReadinessCommand(metadata.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(IndustrySuccessionPoolReadinessState.Ready, response.Data!.IndustrySuccessionPoolReadinessState);
        Assert.NotNull(response.Data.LastEvaluatedAt);
    }

    [Fact]
    public async Task Non_activating_evaluation_preserves_deferred_metadata()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(tenantId, consentPreconditionState: IndustrySuccessionPoolReadinessState.Deferred);
        var repository = new InMemoryIndustrySuccessionPoolReadinessMetadataRepository(metadata);
        var handler = new EvaluateIndustrySuccessionPoolReadinessHandler(repository, new FixedTenantContext(tenantId));

        var response = await handler.Handle(new EvaluateIndustrySuccessionPoolReadinessCommand(metadata.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(IndustrySuccessionPoolReadinessState.Deferred, response.Data!.IndustrySuccessionPoolReadinessState);
        Assert.Contains("preconditions", response.Data.DeferredReason);
    }

    [Fact]
    public async Task Pool_highpotential_nomination_assessment_review_and_decision_boundaries_cannot_be_ready()
    {
        var tenantId = Guid.NewGuid();
        var handler = CreateHandler(new InMemoryIndustrySuccessionPoolReadinessMetadataRepository(), tenantId);

        var requests = new[]
        {
            ValidRequest(poolCatalogBoundaryState: IndustrySuccessionPoolReadinessState.Ready),
            ValidRequest(candidateInclusionIntakeBoundaryState: IndustrySuccessionPoolReadinessState.Ready),
            ValidRequest(readinessTierScopeBoundaryState: IndustrySuccessionPoolReadinessState.Ready),
            ValidRequest(visibilityControlBoundaryState: IndustrySuccessionPoolReadinessState.Ready),
            ValidRequest(successionReviewBoundaryState: IndustrySuccessionPoolReadinessState.Ready),
            ValidRequest(automatedDecisionBoundaryState: IndustrySuccessionPoolReadinessState.Ready)
        };

        foreach (var request in requests)
        {
            var response = await handler.Handle(new CreateIndustrySuccessionPoolReadinessCommand(request), CancellationToken.None);

            Assert.False(response.IsSuccessful);
            Assert.Equal(400, response.StatusCode);
        }
    }

    [Fact]
    public async Task Audit_metadata_uses_audit_permission_surface()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(tenantId);
        var repository = new InMemoryIndustrySuccessionPoolReadinessMetadataRepository(metadata);
        var response = await new GetIndustrySuccessionPoolAuditMetadataHandler(repository, new FixedTenantContext(tenantId))
            .Handle(new GetIndustrySuccessionPoolAuditMetadataQuery(metadata.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(metadata.Code, response.Data!.Code);
        Assert.Equal(IndustrySuccessionPoolGuard.AuditReadPermission, PermissionFor(nameof(IndustrySuccessionPoolController.GetAuditMetadata)));
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
        var handler = CreateHandler(new InMemoryIndustrySuccessionPoolReadinessMetadataRepository(), tenantId);

        // Marker injected as a STANDALONE token (whitespace-delimited) so the word-boundary
        // (\b) matcher rejects a genuine forbidden token.
        var response = await handler.Handle(
            new CreateIndustrySuccessionPoolReadinessCommand(ValidRequest(sourceContractVersion: $"v1 {marker}")),
            CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
    }

    [Theory]
    [InlineData("talent taxonomy")]
    [InlineData("industry-succession-pool pipeline")]
    [InlineData("nomination scorecard")]
    public async Task Word_boundary_matcher_accepts_legitimate_values_that_merely_contain_marker_substrings(string legitimateValue)
    {
        // Regression guard for the substring false-positive that previously broke LearningTraining:
        // "taxonomy" contains "tax", "scorecard" contains "score" — with the \b matcher these are
        // legitimate standalone tokens and MUST be accepted (create succeeds).
        var tenantId = Guid.NewGuid();
        var handler = CreateHandler(new InMemoryIndustrySuccessionPoolReadinessMetadataRepository(), tenantId);

        var response = await handler.Handle(
            new CreateIndustrySuccessionPoolReadinessCommand(ValidRequest(displayName: legitimateValue)),
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
            typeof(IndustrySuccessionPoolReadinessCreateRequest),
            typeof(IndustrySuccessionPoolReadinessDto),
            typeof(IndustrySuccessionPoolReadinessMetadata)
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
        Assert.Null(typeof(IndustrySuccessionPoolReadinessCreateRequest).GetProperty("TenantId"));
    }

    [Fact]
    public void Controller_permissions_match_pack()
    {
        Assert.Equal(IndustrySuccessionPoolGuard.ReadPermission, PermissionFor(nameof(IndustrySuccessionPoolController.GetAll)));
        Assert.Equal(IndustrySuccessionPoolGuard.ReadPermission, PermissionFor(nameof(IndustrySuccessionPoolController.GetById)));
        Assert.Equal(IndustrySuccessionPoolGuard.ManagePermission, PermissionFor(nameof(IndustrySuccessionPoolController.Create)));
        Assert.Equal(IndustrySuccessionPoolGuard.EvaluatePermission, PermissionFor(nameof(IndustrySuccessionPoolController.Evaluate)));
        Assert.Equal(IndustrySuccessionPoolGuard.ManagePermission, PermissionFor(nameof(IndustrySuccessionPoolController.Delete)));
        Assert.Equal(IndustrySuccessionPoolGuard.AuditReadPermission, PermissionFor(nameof(IndustrySuccessionPoolController.GetAuditMetadata)));
    }

    [Fact]
    public void Mongo_repository_contract_names_are_stable()
    {
        Assert.Equal("tep_industry_succession_pool_readiness", MongoIndustrySuccessionPoolReadinessMetadataRepository.CollectionName);
        Assert.Equal("ux_tep_industry_succession_pool_tenant_code_active", MongoIndustrySuccessionPoolReadinessMetadataRepository.ActiveCodeUniqueIndexName);
        Assert.Equal("ix_tep_industry_succession_pool_tenant_state", MongoIndustrySuccessionPoolReadinessMetadataRepository.TenantStateIndexName);
    }

    [Fact]
    public void Runtime_literal_regression_guard_has_no_reserved_identity_literals()
    {
        var reserved = string.Join("-", "CAND", "CAP", "0050");
        var legacy = string.Join("-", "MOD", "0341");
        var runtimeStrings = new[]
        {
            IndustrySuccessionPoolGuard.OwnerKey,
            IndustrySuccessionPoolGuard.ReadPermission,
            IndustrySuccessionPoolGuard.ManagePermission,
            IndustrySuccessionPoolGuard.EvaluatePermission,
            IndustrySuccessionPoolGuard.AuditReadPermission,
            MongoIndustrySuccessionPoolReadinessMetadataRepository.CollectionName
        };

        Assert.DoesNotContain(runtimeStrings, value => value.Contains(reserved, StringComparison.Ordinal));
        Assert.DoesNotContain(runtimeStrings, value => value.Contains(legacy, StringComparison.Ordinal));
    }

    private static CreateIndustrySuccessionPoolReadinessHandler CreateHandler(
        IIndustrySuccessionPoolReadinessMetadataRepository repository,
        Guid tenantId) =>
        new(repository, new FixedTenantContext(tenantId == Guid.Empty ? null : tenantId));

    private static IndustrySuccessionPoolReadinessCreateRequest ValidRequest(
        string code = "SUCC-001",
        string displayName = "IndustrySuccessionPool readiness",
        IndustrySuccessionPoolReadinessState readinessState = IndustrySuccessionPoolReadinessState.Draft,
        string sourceContractVersion = "v1",
        IndustrySuccessionPoolReadinessState poolCatalogBoundaryState = IndustrySuccessionPoolReadinessState.NotRequired,
        IndustrySuccessionPoolReadinessState candidateInclusionIntakeBoundaryState = IndustrySuccessionPoolReadinessState.NotRequired,
        IndustrySuccessionPoolReadinessState readinessTierScopeBoundaryState = IndustrySuccessionPoolReadinessState.NotRequired,
        IndustrySuccessionPoolReadinessState visibilityControlBoundaryState = IndustrySuccessionPoolReadinessState.NotRequired,
        IndustrySuccessionPoolReadinessState successionReviewBoundaryState = IndustrySuccessionPoolReadinessState.NotRequired,
        IndustrySuccessionPoolReadinessState automatedDecisionBoundaryState = IndustrySuccessionPoolReadinessState.NotRequired) =>
        new()
        {
            Code = code,
            DisplayName = displayName,
            IndustrySuccessionPoolReadinessState = readinessState,
            PoolCatalogBoundaryState = poolCatalogBoundaryState,
            CandidateInclusionIntakeBoundaryState = candidateInclusionIntakeBoundaryState,
            ReadinessTierScopeBoundaryState = readinessTierScopeBoundaryState,
            VisibilityControlBoundaryState = visibilityControlBoundaryState,
            SuccessionReviewBoundaryState = successionReviewBoundaryState,
            AutomatedDecisionBoundaryState = automatedDecisionBoundaryState,
            TalentDataSourceDependencyState = IndustrySuccessionPoolReadinessState.NotRequired,
            ConsentPolicyDependencyState = IndustrySuccessionPoolReadinessState.NotRequired,
            TalentPoolSourceDependencyState = IndustrySuccessionPoolReadinessState.NotRequired,
            NotificationDependencyState = IndustrySuccessionPoolReadinessState.NotRequired,
            ConsentPreconditionState = IndustrySuccessionPoolReadinessState.Deferred,
            DataMinimizationState = IndustrySuccessionPoolReadinessState.Deferred,
            RetentionPolicyState = IndustrySuccessionPoolReadinessState.Deferred,
            PublicationPolicyState = IndustrySuccessionPoolReadinessState.Deferred,
            DependencyStates = new Dictionary<string, IndustrySuccessionPoolReadinessState>
            {
                ["talentDataSource"] = IndustrySuccessionPoolReadinessState.Ready,
                ["consentPolicy"] = IndustrySuccessionPoolReadinessState.Ready,
                ["poolCatalog"] = IndustrySuccessionPoolReadinessState.Ready
            },
            SourceContractVersion = sourceContractVersion,
            IndustrySuccessionPoolReadinessVersion = 1
        };

    private static IndustrySuccessionPoolReadinessMetadata Metadata(
        Guid tenantId,
        string code = "SUCC-001",
        IndustrySuccessionPoolReadinessState consentPreconditionState = IndustrySuccessionPoolReadinessState.Deferred,
        IndustrySuccessionPoolReadinessState dataMinimizationState = IndustrySuccessionPoolReadinessState.Deferred,
        IndustrySuccessionPoolReadinessState retentionPolicyState = IndustrySuccessionPoolReadinessState.Deferred,
        IndustrySuccessionPoolReadinessState publicationPolicyState = IndustrySuccessionPoolReadinessState.Deferred) =>
        new()
        {
            TenantId = tenantId,
            Code = code,
            DisplayName = "IndustrySuccessionPool readiness",
            IndustrySuccessionPoolReadinessState = IndustrySuccessionPoolReadinessState.Draft,
            PoolCatalogBoundaryState = IndustrySuccessionPoolReadinessState.NotRequired,
            CandidateInclusionIntakeBoundaryState = IndustrySuccessionPoolReadinessState.NotRequired,
            ReadinessTierScopeBoundaryState = IndustrySuccessionPoolReadinessState.NotRequired,
            VisibilityControlBoundaryState = IndustrySuccessionPoolReadinessState.NotRequired,
            SuccessionReviewBoundaryState = IndustrySuccessionPoolReadinessState.NotRequired,
            AutomatedDecisionBoundaryState = IndustrySuccessionPoolReadinessState.NotRequired,
            TalentDataSourceDependencyState = IndustrySuccessionPoolReadinessState.NotRequired,
            ConsentPolicyDependencyState = IndustrySuccessionPoolReadinessState.NotRequired,
            TalentPoolSourceDependencyState = IndustrySuccessionPoolReadinessState.NotRequired,
            NotificationDependencyState = IndustrySuccessionPoolReadinessState.NotRequired,
            ConsentPreconditionState = consentPreconditionState,
            DataMinimizationState = dataMinimizationState,
            RetentionPolicyState = retentionPolicyState,
            PublicationPolicyState = publicationPolicyState,
            DependencyStates = new Dictionary<string, IndustrySuccessionPoolReadinessState>
            {
                ["talentDataSource"] = IndustrySuccessionPoolReadinessState.Ready
            },
            SourceContractVersion = "v1",
            IndustrySuccessionPoolReadinessVersion = 1
        };

    private static IReadOnlyDictionary<Guid, IndustrySuccessionPoolReadinessMetadata> RepositoryItems(
        InMemoryIndustrySuccessionPoolReadinessMetadataRepository repository)
    {
        var field = typeof(InMemoryIndustrySuccessionPoolReadinessMetadataRepository)
            .GetField("_items", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (IReadOnlyDictionary<Guid, IndustrySuccessionPoolReadinessMetadata>)field.GetValue(repository)!;
    }

    private static string PermissionFor(string methodName)
    {
        var method = typeof(IndustrySuccessionPoolController).GetMethod(methodName)!;
        var attribute = method.GetCustomAttribute<HasPermissionAttribute>()!;
        var field = typeof(HasPermissionAttribute).GetField("_permission", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (string)field.GetValue(attribute)!;
    }

    private sealed class FixedTenantContext : ITenantContext
    {
        public FixedTenantContext(Guid? tenantId) => TenantId = tenantId;

        public Guid? TenantId { get; }
    }

    private sealed class InMemoryIndustrySuccessionPoolReadinessMetadataRepository : IIndustrySuccessionPoolReadinessMetadataRepository
    {
        private readonly Dictionary<Guid, IndustrySuccessionPoolReadinessMetadata> _items;

        public InMemoryIndustrySuccessionPoolReadinessMetadataRepository(params IndustrySuccessionPoolReadinessMetadata[] items) =>
            _items = items.ToDictionary(x => x.Id);

        public Task<IReadOnlyList<IndustrySuccessionPoolReadinessMetadata>> ListAsync(Guid tenantId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<IndustrySuccessionPoolReadinessMetadata>>(
                _items.Values.Where(x => x.TenantId == tenantId && !x.IsDeleted).OrderBy(x => x.Code).ToList());

        public Task<IndustrySuccessionPoolReadinessMetadata?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct) =>
            Task.FromResult(_items.Values.FirstOrDefault(x => x.TenantId == tenantId && x.Id == id && !x.IsDeleted));

        public Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct) =>
            Task.FromResult(_items.Values.Any(x => x.TenantId == tenantId && x.Code == code && !x.IsDeleted && x.Id != excludingId));

        public Task CreateAsync(IndustrySuccessionPoolReadinessMetadata metadata, CancellationToken ct)
        {
            _items[metadata.Id] = metadata;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(IndustrySuccessionPoolReadinessMetadata metadata, CancellationToken ct)
        {
            _items[metadata.Id] = metadata;
            return Task.CompletedTask;
        }
    }
}
