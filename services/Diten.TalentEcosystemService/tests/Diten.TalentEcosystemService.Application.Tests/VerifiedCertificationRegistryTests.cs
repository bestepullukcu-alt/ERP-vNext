using System.Reflection;
using Diten.TalentEcosystemService.Api.Controllers.Tep;
using Diten.TalentEcosystemService.Api.Security;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.VerifiedCertificationRegistry;
using Diten.TalentEcosystemService.Application.Features.VerifiedCertificationRegistry.Commands;
using Diten.TalentEcosystemService.Application.Features.VerifiedCertificationRegistry.Handlers;
using Diten.TalentEcosystemService.Application.Features.VerifiedCertificationRegistry.Queries;
using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Enums;
using Diten.TalentEcosystemService.Domain.Repositories;
using Diten.TalentEcosystemService.Persistence.Repositories;
using Xunit;

namespace Diten.TalentEcosystemService.Application.Tests;

public sealed class VerifiedCertificationRegistryTests
{
    [Fact]
    public async Task Create_list_and_get_metadata_contract()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryVerifiedCertificationRegistryReadinessMetadataRepository();
        var create = CreateHandler(repository, tenantId);

        var created = await create.Handle(new CreateVerifiedCertificationRegistryReadinessCommand(ValidRequest()), CancellationToken.None);
        var list = await new GetVerifiedCertificationRegistryReadinessListHandler(repository, new FixedTenantContext(tenantId))
            .Handle(new GetVerifiedCertificationRegistryReadinessListQuery(), CancellationToken.None);
        var get = await new GetVerifiedCertificationRegistryReadinessByIdHandler(repository, new FixedTenantContext(tenantId))
            .Handle(new GetVerifiedCertificationRegistryReadinessByIdQuery(created.Data), CancellationToken.None);

        Assert.True(created.IsSuccessful);
        Assert.Equal(201, created.StatusCode);
        Assert.True(list.IsSuccessful);
        Assert.Single(list.Data!);
        Assert.True(get.IsSuccessful);
        Assert.Equal("SUCC-001", get.Data!.Code);
        Assert.Equal("VerifiedCertificationRegistry readiness", get.Data.DisplayName);
        Assert.Equal(VerifiedCertificationRegistryReadinessState.NotRequired, get.Data.CertificationCatalogBoundaryState);
        Assert.Equal(VerifiedCertificationRegistryReadinessState.NotRequired, get.Data.VerificationIntakeBoundaryState);
        Assert.Equal(VerifiedCertificationRegistryReadinessState.NotRequired, get.Data.IssuerBindingScopeBoundaryState);
        Assert.Equal(VerifiedCertificationRegistryReadinessState.NotRequired, get.Data.VisibilityControlBoundaryState);
        Assert.Equal(VerifiedCertificationRegistryReadinessState.NotRequired, get.Data.RegistryReviewBoundaryState);
        Assert.Equal(VerifiedCertificationRegistryReadinessState.NotRequired, get.Data.AutomatedDecisionBoundaryState);
        Assert.Equal(VerifiedCertificationRegistryReadinessState.NotRequired, get.Data.TalentDataSourceDependencyState);
        Assert.Equal(VerifiedCertificationRegistryReadinessState.NotRequired, get.Data.ConsentPolicyDependencyState);
        Assert.Equal(VerifiedCertificationRegistryReadinessState.NotRequired, get.Data.SkillPassportSourceDependencyState);
        Assert.Equal(VerifiedCertificationRegistryReadinessState.NotRequired, get.Data.NotificationDependencyState);
    }

    [Fact]
    public async Task Cross_tenant_lookup_returns_not_found()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var metadata = Metadata(tenantA);
        var handler = new GetVerifiedCertificationRegistryReadinessByIdHandler(
            new InMemoryVerifiedCertificationRegistryReadinessMetadataRepository(metadata),
            new FixedTenantContext(tenantB));

        var response = await handler.Handle(new GetVerifiedCertificationRegistryReadinessByIdQuery(metadata.Id), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Missing_tenant_context_fails_closed()
    {
        var handler = CreateHandler(new InMemoryVerifiedCertificationRegistryReadinessMetadataRepository(), Guid.Empty);

        var response = await handler.Handle(new CreateVerifiedCertificationRegistryReadinessCommand(ValidRequest()), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(401, response.StatusCode);
    }

    [Fact]
    public async Task Duplicate_active_code_returns_conflict_per_tenant()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryVerifiedCertificationRegistryReadinessMetadataRepository();
        var handler = CreateHandler(repository, tenantId);

        var first = await handler.Handle(new CreateVerifiedCertificationRegistryReadinessCommand(ValidRequest(code: "succ-001")), CancellationToken.None);
        var second = await handler.Handle(new CreateVerifiedCertificationRegistryReadinessCommand(ValidRequest(code: " SUCC-001 ")), CancellationToken.None);

        Assert.True(first.IsSuccessful);
        Assert.False(second.IsSuccessful);
        Assert.Equal(409, second.StatusCode);
    }

    [Fact]
    public async Task Soft_delete_hides_record_and_sets_deleted_at()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(tenantId);
        var repository = new InMemoryVerifiedCertificationRegistryReadinessMetadataRepository(metadata);
        var handler = new DeleteVerifiedCertificationRegistryReadinessHandler(repository, new FixedTenantContext(tenantId));

        var response = await handler.Handle(new DeleteVerifiedCertificationRegistryReadinessCommand(metadata.Id), CancellationToken.None);
        var hidden = await repository.GetByIdAsync(tenantId, metadata.Id, CancellationToken.None);
        var stored = RepositoryItems(repository)[metadata.Id];

        Assert.True(response.IsSuccessful);
        Assert.Equal(204, response.StatusCode);
        Assert.Null(hidden);
        Assert.True(stored.IsDeleted);
        Assert.NotNull(stored.DeletedAt);
        Assert.Equal(VerifiedCertificationRegistryReadinessState.Archived, stored.VerifiedCertificationRegistryReadinessState);
    }

    [Fact]
    public async Task Ready_state_fails_closed_when_preconditions_are_deferred()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryVerifiedCertificationRegistryReadinessMetadataRepository();
        var handler = CreateHandler(repository, tenantId);

        var response = await handler.Handle(
            new CreateVerifiedCertificationRegistryReadinessCommand(ValidRequest(readinessState: VerifiedCertificationRegistryReadinessState.Ready)),
            CancellationToken.None);
        var stored = RepositoryItems(repository)[response.Data];

        Assert.True(response.IsSuccessful);
        Assert.Equal(VerifiedCertificationRegistryReadinessState.Deferred, stored.VerifiedCertificationRegistryReadinessState);
        Assert.Contains("deferred", stored.DeferredReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Evaluation_promotes_ready_only_when_metadata_preconditions_are_satisfied()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(
            tenantId,
            consentPreconditionState: VerifiedCertificationRegistryReadinessState.Ready,
            dataMinimizationState: VerifiedCertificationRegistryReadinessState.Ready,
            retentionPolicyState: VerifiedCertificationRegistryReadinessState.Ready,
            publicationPolicyState: VerifiedCertificationRegistryReadinessState.Ready);
        var repository = new InMemoryVerifiedCertificationRegistryReadinessMetadataRepository(metadata);
        var handler = new EvaluateVerifiedCertificationRegistryReadinessHandler(repository, new FixedTenantContext(tenantId));

        var response = await handler.Handle(new EvaluateVerifiedCertificationRegistryReadinessCommand(metadata.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(VerifiedCertificationRegistryReadinessState.Ready, response.Data!.VerifiedCertificationRegistryReadinessState);
        Assert.NotNull(response.Data.LastEvaluatedAt);
    }

    [Fact]
    public async Task Non_activating_evaluation_preserves_deferred_metadata()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(tenantId, consentPreconditionState: VerifiedCertificationRegistryReadinessState.Deferred);
        var repository = new InMemoryVerifiedCertificationRegistryReadinessMetadataRepository(metadata);
        var handler = new EvaluateVerifiedCertificationRegistryReadinessHandler(repository, new FixedTenantContext(tenantId));

        var response = await handler.Handle(new EvaluateVerifiedCertificationRegistryReadinessCommand(metadata.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(VerifiedCertificationRegistryReadinessState.Deferred, response.Data!.VerifiedCertificationRegistryReadinessState);
        Assert.Contains("preconditions", response.Data.DeferredReason);
    }

    [Fact]
    public async Task Pool_highpotential_nomination_assessment_review_and_decision_boundaries_cannot_be_ready()
    {
        var tenantId = Guid.NewGuid();
        var handler = CreateHandler(new InMemoryVerifiedCertificationRegistryReadinessMetadataRepository(), tenantId);

        var requests = new[]
        {
            ValidRequest(certificationCatalogBoundaryState: VerifiedCertificationRegistryReadinessState.Ready),
            ValidRequest(verificationIntakeBoundaryState: VerifiedCertificationRegistryReadinessState.Ready),
            ValidRequest(issuerBindingScopeBoundaryState: VerifiedCertificationRegistryReadinessState.Ready),
            ValidRequest(visibilityControlBoundaryState: VerifiedCertificationRegistryReadinessState.Ready),
            ValidRequest(registryReviewBoundaryState: VerifiedCertificationRegistryReadinessState.Ready),
            ValidRequest(automatedDecisionBoundaryState: VerifiedCertificationRegistryReadinessState.Ready)
        };

        foreach (var request in requests)
        {
            var response = await handler.Handle(new CreateVerifiedCertificationRegistryReadinessCommand(request), CancellationToken.None);

            Assert.False(response.IsSuccessful);
            Assert.Equal(400, response.StatusCode);
        }
    }

    [Fact]
    public async Task Audit_metadata_uses_audit_permission_surface()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(tenantId);
        var repository = new InMemoryVerifiedCertificationRegistryReadinessMetadataRepository(metadata);
        var response = await new GetVerifiedCertificationRegistryAuditMetadataHandler(repository, new FixedTenantContext(tenantId))
            .Handle(new GetVerifiedCertificationRegistryAuditMetadataQuery(metadata.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(metadata.Code, response.Data!.Code);
        Assert.Equal(VerifiedCertificationRegistryGuard.AuditReadPermission, PermissionFor(nameof(VerifiedCertificationRegistryController.GetAuditMetadata)));
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
        var handler = CreateHandler(new InMemoryVerifiedCertificationRegistryReadinessMetadataRepository(), tenantId);

        // Marker injected as a STANDALONE token (whitespace-delimited) so the word-boundary
        // (\b) matcher rejects a genuine forbidden token.
        var response = await handler.Handle(
            new CreateVerifiedCertificationRegistryReadinessCommand(ValidRequest(sourceContractVersion: $"v1 {marker}")),
            CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
    }

    [Theory]
    [InlineData("talent taxonomy")]
    [InlineData("verified-certification-registry pipeline")]
    [InlineData("nomination scorecard")]
    public async Task Word_boundary_matcher_accepts_legitimate_values_that_merely_contain_marker_substrings(string legitimateValue)
    {
        // Regression guard for the substring false-positive that previously broke LearningTraining:
        // "taxonomy" contains "tax", "scorecard" contains "score" — with the \b matcher these are
        // legitimate standalone tokens and MUST be accepted (create succeeds).
        var tenantId = Guid.NewGuid();
        var handler = CreateHandler(new InMemoryVerifiedCertificationRegistryReadinessMetadataRepository(), tenantId);

        var response = await handler.Handle(
            new CreateVerifiedCertificationRegistryReadinessCommand(ValidRequest(displayName: legitimateValue)),
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
            typeof(VerifiedCertificationRegistryReadinessCreateRequest),
            typeof(VerifiedCertificationRegistryReadinessDto),
            typeof(VerifiedCertificationRegistryReadinessMetadata)
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
        Assert.Null(typeof(VerifiedCertificationRegistryReadinessCreateRequest).GetProperty("TenantId"));
    }

    [Fact]
    public void Controller_permissions_match_pack()
    {
        Assert.Equal(VerifiedCertificationRegistryGuard.ReadPermission, PermissionFor(nameof(VerifiedCertificationRegistryController.GetAll)));
        Assert.Equal(VerifiedCertificationRegistryGuard.ReadPermission, PermissionFor(nameof(VerifiedCertificationRegistryController.GetById)));
        Assert.Equal(VerifiedCertificationRegistryGuard.ManagePermission, PermissionFor(nameof(VerifiedCertificationRegistryController.Create)));
        Assert.Equal(VerifiedCertificationRegistryGuard.EvaluatePermission, PermissionFor(nameof(VerifiedCertificationRegistryController.Evaluate)));
        Assert.Equal(VerifiedCertificationRegistryGuard.ManagePermission, PermissionFor(nameof(VerifiedCertificationRegistryController.Delete)));
        Assert.Equal(VerifiedCertificationRegistryGuard.AuditReadPermission, PermissionFor(nameof(VerifiedCertificationRegistryController.GetAuditMetadata)));
    }

    [Fact]
    public void Mongo_repository_contract_names_are_stable()
    {
        Assert.Equal("tep_verified_certification_registry_readiness", MongoVerifiedCertificationRegistryReadinessMetadataRepository.CollectionName);
        Assert.Equal("ux_tep_verified_certification_registry_tenant_code_active", MongoVerifiedCertificationRegistryReadinessMetadataRepository.ActiveCodeUniqueIndexName);
        Assert.Equal("ix_tep_verified_certification_registry_tenant_state", MongoVerifiedCertificationRegistryReadinessMetadataRepository.TenantStateIndexName);
    }

    [Fact]
    public void Runtime_literal_regression_guard_has_no_reserved_identity_literals()
    {
        var reserved = string.Join("-", "CAND", "CAP", "0051");
        var legacy = string.Join("-", "MOD", "0342");
        var runtimeStrings = new[]
        {
            VerifiedCertificationRegistryGuard.OwnerKey,
            VerifiedCertificationRegistryGuard.ReadPermission,
            VerifiedCertificationRegistryGuard.ManagePermission,
            VerifiedCertificationRegistryGuard.EvaluatePermission,
            VerifiedCertificationRegistryGuard.AuditReadPermission,
            MongoVerifiedCertificationRegistryReadinessMetadataRepository.CollectionName
        };

        Assert.DoesNotContain(runtimeStrings, value => value.Contains(reserved, StringComparison.Ordinal));
        Assert.DoesNotContain(runtimeStrings, value => value.Contains(legacy, StringComparison.Ordinal));
    }

    private static CreateVerifiedCertificationRegistryReadinessHandler CreateHandler(
        IVerifiedCertificationRegistryReadinessMetadataRepository repository,
        Guid tenantId) =>
        new(repository, new FixedTenantContext(tenantId == Guid.Empty ? null : tenantId));

    private static VerifiedCertificationRegistryReadinessCreateRequest ValidRequest(
        string code = "SUCC-001",
        string displayName = "VerifiedCertificationRegistry readiness",
        VerifiedCertificationRegistryReadinessState readinessState = VerifiedCertificationRegistryReadinessState.Draft,
        string sourceContractVersion = "v1",
        VerifiedCertificationRegistryReadinessState certificationCatalogBoundaryState = VerifiedCertificationRegistryReadinessState.NotRequired,
        VerifiedCertificationRegistryReadinessState verificationIntakeBoundaryState = VerifiedCertificationRegistryReadinessState.NotRequired,
        VerifiedCertificationRegistryReadinessState issuerBindingScopeBoundaryState = VerifiedCertificationRegistryReadinessState.NotRequired,
        VerifiedCertificationRegistryReadinessState visibilityControlBoundaryState = VerifiedCertificationRegistryReadinessState.NotRequired,
        VerifiedCertificationRegistryReadinessState registryReviewBoundaryState = VerifiedCertificationRegistryReadinessState.NotRequired,
        VerifiedCertificationRegistryReadinessState automatedDecisionBoundaryState = VerifiedCertificationRegistryReadinessState.NotRequired) =>
        new()
        {
            Code = code,
            DisplayName = displayName,
            VerifiedCertificationRegistryReadinessState = readinessState,
            CertificationCatalogBoundaryState = certificationCatalogBoundaryState,
            VerificationIntakeBoundaryState = verificationIntakeBoundaryState,
            IssuerBindingScopeBoundaryState = issuerBindingScopeBoundaryState,
            VisibilityControlBoundaryState = visibilityControlBoundaryState,
            RegistryReviewBoundaryState = registryReviewBoundaryState,
            AutomatedDecisionBoundaryState = automatedDecisionBoundaryState,
            TalentDataSourceDependencyState = VerifiedCertificationRegistryReadinessState.NotRequired,
            ConsentPolicyDependencyState = VerifiedCertificationRegistryReadinessState.NotRequired,
            SkillPassportSourceDependencyState = VerifiedCertificationRegistryReadinessState.NotRequired,
            NotificationDependencyState = VerifiedCertificationRegistryReadinessState.NotRequired,
            ConsentPreconditionState = VerifiedCertificationRegistryReadinessState.Deferred,
            DataMinimizationState = VerifiedCertificationRegistryReadinessState.Deferred,
            RetentionPolicyState = VerifiedCertificationRegistryReadinessState.Deferred,
            PublicationPolicyState = VerifiedCertificationRegistryReadinessState.Deferred,
            DependencyStates = new Dictionary<string, VerifiedCertificationRegistryReadinessState>
            {
                ["talentDataSource"] = VerifiedCertificationRegistryReadinessState.Ready,
                ["consentPolicy"] = VerifiedCertificationRegistryReadinessState.Ready,
                ["certificationCatalog"] = VerifiedCertificationRegistryReadinessState.Ready
            },
            SourceContractVersion = sourceContractVersion,
            VerifiedCertificationRegistryReadinessVersion = 1
        };

    private static VerifiedCertificationRegistryReadinessMetadata Metadata(
        Guid tenantId,
        string code = "SUCC-001",
        VerifiedCertificationRegistryReadinessState consentPreconditionState = VerifiedCertificationRegistryReadinessState.Deferred,
        VerifiedCertificationRegistryReadinessState dataMinimizationState = VerifiedCertificationRegistryReadinessState.Deferred,
        VerifiedCertificationRegistryReadinessState retentionPolicyState = VerifiedCertificationRegistryReadinessState.Deferred,
        VerifiedCertificationRegistryReadinessState publicationPolicyState = VerifiedCertificationRegistryReadinessState.Deferred) =>
        new()
        {
            TenantId = tenantId,
            Code = code,
            DisplayName = "VerifiedCertificationRegistry readiness",
            VerifiedCertificationRegistryReadinessState = VerifiedCertificationRegistryReadinessState.Draft,
            CertificationCatalogBoundaryState = VerifiedCertificationRegistryReadinessState.NotRequired,
            VerificationIntakeBoundaryState = VerifiedCertificationRegistryReadinessState.NotRequired,
            IssuerBindingScopeBoundaryState = VerifiedCertificationRegistryReadinessState.NotRequired,
            VisibilityControlBoundaryState = VerifiedCertificationRegistryReadinessState.NotRequired,
            RegistryReviewBoundaryState = VerifiedCertificationRegistryReadinessState.NotRequired,
            AutomatedDecisionBoundaryState = VerifiedCertificationRegistryReadinessState.NotRequired,
            TalentDataSourceDependencyState = VerifiedCertificationRegistryReadinessState.NotRequired,
            ConsentPolicyDependencyState = VerifiedCertificationRegistryReadinessState.NotRequired,
            SkillPassportSourceDependencyState = VerifiedCertificationRegistryReadinessState.NotRequired,
            NotificationDependencyState = VerifiedCertificationRegistryReadinessState.NotRequired,
            ConsentPreconditionState = consentPreconditionState,
            DataMinimizationState = dataMinimizationState,
            RetentionPolicyState = retentionPolicyState,
            PublicationPolicyState = publicationPolicyState,
            DependencyStates = new Dictionary<string, VerifiedCertificationRegistryReadinessState>
            {
                ["talentDataSource"] = VerifiedCertificationRegistryReadinessState.Ready
            },
            SourceContractVersion = "v1",
            VerifiedCertificationRegistryReadinessVersion = 1
        };

    private static IReadOnlyDictionary<Guid, VerifiedCertificationRegistryReadinessMetadata> RepositoryItems(
        InMemoryVerifiedCertificationRegistryReadinessMetadataRepository repository)
    {
        var field = typeof(InMemoryVerifiedCertificationRegistryReadinessMetadataRepository)
            .GetField("_items", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (IReadOnlyDictionary<Guid, VerifiedCertificationRegistryReadinessMetadata>)field.GetValue(repository)!;
    }

    private static string PermissionFor(string methodName)
    {
        var method = typeof(VerifiedCertificationRegistryController).GetMethod(methodName)!;
        var attribute = method.GetCustomAttribute<HasPermissionAttribute>()!;
        var field = typeof(HasPermissionAttribute).GetField("_permission", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (string)field.GetValue(attribute)!;
    }

    private sealed class FixedTenantContext : ITenantContext
    {
        public FixedTenantContext(Guid? tenantId) => TenantId = tenantId;

        public Guid? TenantId { get; }
    }

    private sealed class InMemoryVerifiedCertificationRegistryReadinessMetadataRepository : IVerifiedCertificationRegistryReadinessMetadataRepository
    {
        private readonly Dictionary<Guid, VerifiedCertificationRegistryReadinessMetadata> _items;

        public InMemoryVerifiedCertificationRegistryReadinessMetadataRepository(params VerifiedCertificationRegistryReadinessMetadata[] items) =>
            _items = items.ToDictionary(x => x.Id);

        public Task<IReadOnlyList<VerifiedCertificationRegistryReadinessMetadata>> ListAsync(Guid tenantId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<VerifiedCertificationRegistryReadinessMetadata>>(
                _items.Values.Where(x => x.TenantId == tenantId && !x.IsDeleted).OrderBy(x => x.Code).ToList());

        public Task<VerifiedCertificationRegistryReadinessMetadata?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct) =>
            Task.FromResult(_items.Values.FirstOrDefault(x => x.TenantId == tenantId && x.Id == id && !x.IsDeleted));

        public Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct) =>
            Task.FromResult(_items.Values.Any(x => x.TenantId == tenantId && x.Code == code && !x.IsDeleted && x.Id != excludingId));

        public Task CreateAsync(VerifiedCertificationRegistryReadinessMetadata metadata, CancellationToken ct)
        {
            _items[metadata.Id] = metadata;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(VerifiedCertificationRegistryReadinessMetadata metadata, CancellationToken ct)
        {
            _items[metadata.Id] = metadata;
            return Task.CompletedTask;
        }
    }
}
