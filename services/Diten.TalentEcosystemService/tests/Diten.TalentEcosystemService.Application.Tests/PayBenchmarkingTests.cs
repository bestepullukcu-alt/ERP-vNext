using System.Reflection;
using Diten.TalentEcosystemService.Api.Controllers.Tep;
using Diten.TalentEcosystemService.Api.Security;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.PayBenchmarking;
using Diten.TalentEcosystemService.Application.Features.PayBenchmarking.Commands;
using Diten.TalentEcosystemService.Application.Features.PayBenchmarking.Handlers;
using Diten.TalentEcosystemService.Application.Features.PayBenchmarking.Queries;
using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Enums;
using Diten.TalentEcosystemService.Domain.Repositories;
using Diten.TalentEcosystemService.Persistence.Repositories;
using Xunit;

namespace Diten.TalentEcosystemService.Application.Tests;

public sealed class PayBenchmarkingTests
{
    [Fact]
    public async Task Create_list_and_get_metadata_contract()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryPayBenchmarkingReadinessMetadataRepository();
        var create = CreateHandler(repository, tenantId);

        var created = await create.Handle(new CreatePayBenchmarkingReadinessCommand(ValidRequest()), CancellationToken.None);
        var list = await new GetPayBenchmarkingReadinessListHandler(repository, new FixedTenantContext(tenantId))
            .Handle(new GetPayBenchmarkingReadinessListQuery(), CancellationToken.None);
        var get = await new GetPayBenchmarkingReadinessByIdHandler(repository, new FixedTenantContext(tenantId))
            .Handle(new GetPayBenchmarkingReadinessByIdQuery(created.Data), CancellationToken.None);

        Assert.True(created.IsSuccessful);
        Assert.Equal(201, created.StatusCode);
        Assert.True(list.IsSuccessful);
        Assert.Single(list.Data!);
        Assert.True(get.IsSuccessful);
        Assert.Equal("SUCC-001", get.Data!.Code);
        Assert.Equal("PayBenchmarking readiness", get.Data.DisplayName);
        Assert.Equal(PayBenchmarkingReadinessState.NotRequired, get.Data.ReferenceRangeCatalogBoundaryState);
        Assert.Equal(PayBenchmarkingReadinessState.NotRequired, get.Data.ContributionIntakeBoundaryState);
        Assert.Equal(PayBenchmarkingReadinessState.NotRequired, get.Data.AggregationScopeBoundaryState);
        Assert.Equal(PayBenchmarkingReadinessState.NotRequired, get.Data.VisibilityControlBoundaryState);
        Assert.Equal(PayBenchmarkingReadinessState.NotRequired, get.Data.BenchmarkingReviewBoundaryState);
        Assert.Equal(PayBenchmarkingReadinessState.NotRequired, get.Data.AutomatedDecisionBoundaryState);
        Assert.Equal(PayBenchmarkingReadinessState.NotRequired, get.Data.TalentDataSourceDependencyState);
        Assert.Equal(PayBenchmarkingReadinessState.NotRequired, get.Data.ConsentPolicyDependencyState);
        Assert.Equal(PayBenchmarkingReadinessState.NotRequired, get.Data.DataGovernancePolicyDependencyState);
        Assert.Equal(PayBenchmarkingReadinessState.NotRequired, get.Data.NotificationDependencyState);
    }

    [Fact]
    public async Task Cross_tenant_lookup_returns_not_found()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var metadata = Metadata(tenantA);
        var handler = new GetPayBenchmarkingReadinessByIdHandler(
            new InMemoryPayBenchmarkingReadinessMetadataRepository(metadata),
            new FixedTenantContext(tenantB));

        var response = await handler.Handle(new GetPayBenchmarkingReadinessByIdQuery(metadata.Id), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Missing_tenant_context_fails_closed()
    {
        var handler = CreateHandler(new InMemoryPayBenchmarkingReadinessMetadataRepository(), Guid.Empty);

        var response = await handler.Handle(new CreatePayBenchmarkingReadinessCommand(ValidRequest()), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(401, response.StatusCode);
    }

    [Fact]
    public async Task Duplicate_active_code_returns_conflict_per_tenant()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryPayBenchmarkingReadinessMetadataRepository();
        var handler = CreateHandler(repository, tenantId);

        var first = await handler.Handle(new CreatePayBenchmarkingReadinessCommand(ValidRequest(code: "succ-001")), CancellationToken.None);
        var second = await handler.Handle(new CreatePayBenchmarkingReadinessCommand(ValidRequest(code: " SUCC-001 ")), CancellationToken.None);

        Assert.True(first.IsSuccessful);
        Assert.False(second.IsSuccessful);
        Assert.Equal(409, second.StatusCode);
    }

    [Fact]
    public async Task Soft_delete_hides_record_and_sets_deleted_at()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(tenantId);
        var repository = new InMemoryPayBenchmarkingReadinessMetadataRepository(metadata);
        var handler = new DeletePayBenchmarkingReadinessHandler(repository, new FixedTenantContext(tenantId));

        var response = await handler.Handle(new DeletePayBenchmarkingReadinessCommand(metadata.Id), CancellationToken.None);
        var hidden = await repository.GetByIdAsync(tenantId, metadata.Id, CancellationToken.None);
        var stored = RepositoryItems(repository)[metadata.Id];

        Assert.True(response.IsSuccessful);
        Assert.Equal(204, response.StatusCode);
        Assert.Null(hidden);
        Assert.True(stored.IsDeleted);
        Assert.NotNull(stored.DeletedAt);
        Assert.Equal(PayBenchmarkingReadinessState.Archived, stored.PayBenchmarkingReadinessState);
    }

    [Fact]
    public async Task Ready_state_fails_closed_when_preconditions_are_deferred()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryPayBenchmarkingReadinessMetadataRepository();
        var handler = CreateHandler(repository, tenantId);

        var response = await handler.Handle(
            new CreatePayBenchmarkingReadinessCommand(ValidRequest(readinessState: PayBenchmarkingReadinessState.Ready)),
            CancellationToken.None);
        var stored = RepositoryItems(repository)[response.Data];

        Assert.True(response.IsSuccessful);
        Assert.Equal(PayBenchmarkingReadinessState.Deferred, stored.PayBenchmarkingReadinessState);
        Assert.Contains("deferred", stored.DeferredReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Evaluation_promotes_ready_only_when_metadata_preconditions_are_satisfied()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(
            tenantId,
            consentPreconditionState: PayBenchmarkingReadinessState.Ready,
            dataMinimizationState: PayBenchmarkingReadinessState.Ready,
            retentionPolicyState: PayBenchmarkingReadinessState.Ready,
            publicationPolicyState: PayBenchmarkingReadinessState.Ready);
        var repository = new InMemoryPayBenchmarkingReadinessMetadataRepository(metadata);
        var handler = new EvaluatePayBenchmarkingReadinessHandler(repository, new FixedTenantContext(tenantId));

        var response = await handler.Handle(new EvaluatePayBenchmarkingReadinessCommand(metadata.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(PayBenchmarkingReadinessState.Ready, response.Data!.PayBenchmarkingReadinessState);
        Assert.NotNull(response.Data.LastEvaluatedAt);
    }

    [Fact]
    public async Task Non_activating_evaluation_preserves_deferred_metadata()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(tenantId, consentPreconditionState: PayBenchmarkingReadinessState.Deferred);
        var repository = new InMemoryPayBenchmarkingReadinessMetadataRepository(metadata);
        var handler = new EvaluatePayBenchmarkingReadinessHandler(repository, new FixedTenantContext(tenantId));

        var response = await handler.Handle(new EvaluatePayBenchmarkingReadinessCommand(metadata.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(PayBenchmarkingReadinessState.Deferred, response.Data!.PayBenchmarkingReadinessState);
        Assert.Contains("preconditions", response.Data.DeferredReason);
    }

    [Fact]
    public async Task Pool_highpotential_nomination_assessment_review_and_decision_boundaries_cannot_be_ready()
    {
        var tenantId = Guid.NewGuid();
        var handler = CreateHandler(new InMemoryPayBenchmarkingReadinessMetadataRepository(), tenantId);

        var requests = new[]
        {
            ValidRequest(referenceRangeCatalogBoundaryState: PayBenchmarkingReadinessState.Ready),
            ValidRequest(contributionIntakeBoundaryState: PayBenchmarkingReadinessState.Ready),
            ValidRequest(aggregationScopeBoundaryState: PayBenchmarkingReadinessState.Ready),
            ValidRequest(visibilityControlBoundaryState: PayBenchmarkingReadinessState.Ready),
            ValidRequest(benchmarkingReviewBoundaryState: PayBenchmarkingReadinessState.Ready),
            ValidRequest(automatedDecisionBoundaryState: PayBenchmarkingReadinessState.Ready)
        };

        foreach (var request in requests)
        {
            var response = await handler.Handle(new CreatePayBenchmarkingReadinessCommand(request), CancellationToken.None);

            Assert.False(response.IsSuccessful);
            Assert.Equal(400, response.StatusCode);
        }
    }

    [Fact]
    public async Task Audit_metadata_uses_audit_permission_surface()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(tenantId);
        var repository = new InMemoryPayBenchmarkingReadinessMetadataRepository(metadata);
        var response = await new GetPayBenchmarkingAuditMetadataHandler(repository, new FixedTenantContext(tenantId))
            .Handle(new GetPayBenchmarkingAuditMetadataQuery(metadata.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(metadata.Code, response.Data!.Code);
        Assert.Equal(PayBenchmarkingGuard.AuditReadPermission, PermissionFor(nameof(PayBenchmarkingController.GetAuditMetadata)));
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
        var handler = CreateHandler(new InMemoryPayBenchmarkingReadinessMetadataRepository(), tenantId);

        // Marker injected as a STANDALONE token (whitespace-delimited) so the word-boundary
        // (\b) matcher rejects a genuine forbidden token.
        var response = await handler.Handle(
            new CreatePayBenchmarkingReadinessCommand(ValidRequest(sourceContractVersion: $"v1 {marker}")),
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
        var handler = CreateHandler(new InMemoryPayBenchmarkingReadinessMetadataRepository(), tenantId);

        var response = await handler.Handle(
            new CreatePayBenchmarkingReadinessCommand(ValidRequest(displayName: legitimateValue)),
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
            typeof(PayBenchmarkingReadinessCreateRequest),
            typeof(PayBenchmarkingReadinessDto),
            typeof(PayBenchmarkingReadinessMetadata)
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
        Assert.Null(typeof(PayBenchmarkingReadinessCreateRequest).GetProperty("TenantId"));
    }

    [Fact]
    public void Controller_permissions_match_pack()
    {
        Assert.Equal(PayBenchmarkingGuard.ReadPermission, PermissionFor(nameof(PayBenchmarkingController.GetAll)));
        Assert.Equal(PayBenchmarkingGuard.ReadPermission, PermissionFor(nameof(PayBenchmarkingController.GetById)));
        Assert.Equal(PayBenchmarkingGuard.ManagePermission, PermissionFor(nameof(PayBenchmarkingController.Create)));
        Assert.Equal(PayBenchmarkingGuard.EvaluatePermission, PermissionFor(nameof(PayBenchmarkingController.Evaluate)));
        Assert.Equal(PayBenchmarkingGuard.ManagePermission, PermissionFor(nameof(PayBenchmarkingController.Delete)));
        Assert.Equal(PayBenchmarkingGuard.AuditReadPermission, PermissionFor(nameof(PayBenchmarkingController.GetAuditMetadata)));
    }

    [Fact]
    public void Mongo_repository_contract_names_are_stable()
    {
        Assert.Equal("tep_salary_benchmarking_readiness", MongoPayBenchmarkingReadinessMetadataRepository.CollectionName);
        Assert.Equal("ux_tep_salary_benchmarking_tenant_code_active", MongoPayBenchmarkingReadinessMetadataRepository.ActiveCodeUniqueIndexName);
        Assert.Equal("ix_tep_salary_benchmarking_tenant_state", MongoPayBenchmarkingReadinessMetadataRepository.TenantStateIndexName);
    }

    [Fact]
    public void Runtime_literal_regression_guard_has_no_reserved_identity_literals()
    {
        var reserved = string.Join("-", "CAND", "CAP", "0053");
        var legacy = string.Join("-", "MOD", "0344");
        var runtimeStrings = new[]
        {
            PayBenchmarkingGuard.OwnerKey,
            PayBenchmarkingGuard.ReadPermission,
            PayBenchmarkingGuard.ManagePermission,
            PayBenchmarkingGuard.EvaluatePermission,
            PayBenchmarkingGuard.AuditReadPermission,
            MongoPayBenchmarkingReadinessMetadataRepository.CollectionName
        };

        Assert.DoesNotContain(runtimeStrings, value => value.Contains(reserved, StringComparison.Ordinal));
        Assert.DoesNotContain(runtimeStrings, value => value.Contains(legacy, StringComparison.Ordinal));
    }

    private static CreatePayBenchmarkingReadinessHandler CreateHandler(
        IPayBenchmarkingReadinessMetadataRepository repository,
        Guid tenantId) =>
        new(repository, new FixedTenantContext(tenantId == Guid.Empty ? null : tenantId));

    private static PayBenchmarkingReadinessCreateRequest ValidRequest(
        string code = "SUCC-001",
        string displayName = "PayBenchmarking readiness",
        PayBenchmarkingReadinessState readinessState = PayBenchmarkingReadinessState.Draft,
        string sourceContractVersion = "v1",
        PayBenchmarkingReadinessState referenceRangeCatalogBoundaryState = PayBenchmarkingReadinessState.NotRequired,
        PayBenchmarkingReadinessState contributionIntakeBoundaryState = PayBenchmarkingReadinessState.NotRequired,
        PayBenchmarkingReadinessState aggregationScopeBoundaryState = PayBenchmarkingReadinessState.NotRequired,
        PayBenchmarkingReadinessState visibilityControlBoundaryState = PayBenchmarkingReadinessState.NotRequired,
        PayBenchmarkingReadinessState benchmarkingReviewBoundaryState = PayBenchmarkingReadinessState.NotRequired,
        PayBenchmarkingReadinessState automatedDecisionBoundaryState = PayBenchmarkingReadinessState.NotRequired) =>
        new()
        {
            Code = code,
            DisplayName = displayName,
            PayBenchmarkingReadinessState = readinessState,
            ReferenceRangeCatalogBoundaryState = referenceRangeCatalogBoundaryState,
            ContributionIntakeBoundaryState = contributionIntakeBoundaryState,
            AggregationScopeBoundaryState = aggregationScopeBoundaryState,
            VisibilityControlBoundaryState = visibilityControlBoundaryState,
            BenchmarkingReviewBoundaryState = benchmarkingReviewBoundaryState,
            AutomatedDecisionBoundaryState = automatedDecisionBoundaryState,
            TalentDataSourceDependencyState = PayBenchmarkingReadinessState.NotRequired,
            ConsentPolicyDependencyState = PayBenchmarkingReadinessState.NotRequired,
            DataGovernancePolicyDependencyState = PayBenchmarkingReadinessState.NotRequired,
            NotificationDependencyState = PayBenchmarkingReadinessState.NotRequired,
            ConsentPreconditionState = PayBenchmarkingReadinessState.Deferred,
            DataMinimizationState = PayBenchmarkingReadinessState.Deferred,
            RetentionPolicyState = PayBenchmarkingReadinessState.Deferred,
            PublicationPolicyState = PayBenchmarkingReadinessState.Deferred,
            DependencyStates = new Dictionary<string, PayBenchmarkingReadinessState>
            {
                ["talentDataSource"] = PayBenchmarkingReadinessState.Ready,
                ["consentPolicy"] = PayBenchmarkingReadinessState.Ready,
                ["referenceRangeCatalog"] = PayBenchmarkingReadinessState.Ready
            },
            SourceContractVersion = sourceContractVersion,
            PayBenchmarkingReadinessVersion = 1
        };

    private static PayBenchmarkingReadinessMetadata Metadata(
        Guid tenantId,
        string code = "SUCC-001",
        PayBenchmarkingReadinessState consentPreconditionState = PayBenchmarkingReadinessState.Deferred,
        PayBenchmarkingReadinessState dataMinimizationState = PayBenchmarkingReadinessState.Deferred,
        PayBenchmarkingReadinessState retentionPolicyState = PayBenchmarkingReadinessState.Deferred,
        PayBenchmarkingReadinessState publicationPolicyState = PayBenchmarkingReadinessState.Deferred) =>
        new()
        {
            TenantId = tenantId,
            Code = code,
            DisplayName = "PayBenchmarking readiness",
            PayBenchmarkingReadinessState = PayBenchmarkingReadinessState.Draft,
            ReferenceRangeCatalogBoundaryState = PayBenchmarkingReadinessState.NotRequired,
            ContributionIntakeBoundaryState = PayBenchmarkingReadinessState.NotRequired,
            AggregationScopeBoundaryState = PayBenchmarkingReadinessState.NotRequired,
            VisibilityControlBoundaryState = PayBenchmarkingReadinessState.NotRequired,
            BenchmarkingReviewBoundaryState = PayBenchmarkingReadinessState.NotRequired,
            AutomatedDecisionBoundaryState = PayBenchmarkingReadinessState.NotRequired,
            TalentDataSourceDependencyState = PayBenchmarkingReadinessState.NotRequired,
            ConsentPolicyDependencyState = PayBenchmarkingReadinessState.NotRequired,
            DataGovernancePolicyDependencyState = PayBenchmarkingReadinessState.NotRequired,
            NotificationDependencyState = PayBenchmarkingReadinessState.NotRequired,
            ConsentPreconditionState = consentPreconditionState,
            DataMinimizationState = dataMinimizationState,
            RetentionPolicyState = retentionPolicyState,
            PublicationPolicyState = publicationPolicyState,
            DependencyStates = new Dictionary<string, PayBenchmarkingReadinessState>
            {
                ["talentDataSource"] = PayBenchmarkingReadinessState.Ready
            },
            SourceContractVersion = "v1",
            PayBenchmarkingReadinessVersion = 1
        };

    private static IReadOnlyDictionary<Guid, PayBenchmarkingReadinessMetadata> RepositoryItems(
        InMemoryPayBenchmarkingReadinessMetadataRepository repository)
    {
        var field = typeof(InMemoryPayBenchmarkingReadinessMetadataRepository)
            .GetField("_items", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (IReadOnlyDictionary<Guid, PayBenchmarkingReadinessMetadata>)field.GetValue(repository)!;
    }

    private static string PermissionFor(string methodName)
    {
        var method = typeof(PayBenchmarkingController).GetMethod(methodName)!;
        var attribute = method.GetCustomAttribute<HasPermissionAttribute>()!;
        var field = typeof(HasPermissionAttribute).GetField("_permission", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (string)field.GetValue(attribute)!;
    }

    private sealed class FixedTenantContext : ITenantContext
    {
        public FixedTenantContext(Guid? tenantId) => TenantId = tenantId;

        public Guid? TenantId { get; }
    }

    private sealed class InMemoryPayBenchmarkingReadinessMetadataRepository : IPayBenchmarkingReadinessMetadataRepository
    {
        private readonly Dictionary<Guid, PayBenchmarkingReadinessMetadata> _items;

        public InMemoryPayBenchmarkingReadinessMetadataRepository(params PayBenchmarkingReadinessMetadata[] items) =>
            _items = items.ToDictionary(x => x.Id);

        public Task<IReadOnlyList<PayBenchmarkingReadinessMetadata>> ListAsync(Guid tenantId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<PayBenchmarkingReadinessMetadata>>(
                _items.Values.Where(x => x.TenantId == tenantId && !x.IsDeleted).OrderBy(x => x.Code).ToList());

        public Task<PayBenchmarkingReadinessMetadata?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct) =>
            Task.FromResult(_items.Values.FirstOrDefault(x => x.TenantId == tenantId && x.Id == id && !x.IsDeleted));

        public Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct) =>
            Task.FromResult(_items.Values.Any(x => x.TenantId == tenantId && x.Code == code && !x.IsDeleted && x.Id != excludingId));

        public Task CreateAsync(PayBenchmarkingReadinessMetadata metadata, CancellationToken ct)
        {
            _items[metadata.Id] = metadata;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(PayBenchmarkingReadinessMetadata metadata, CancellationToken ct)
        {
            _items[metadata.Id] = metadata;
            return Task.CompletedTask;
        }
    }
}
