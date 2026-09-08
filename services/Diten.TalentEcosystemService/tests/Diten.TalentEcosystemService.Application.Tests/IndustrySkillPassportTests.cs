using System.Reflection;
using Diten.TalentEcosystemService.Api.Controllers.Tep;
using Diten.TalentEcosystemService.Api.Security;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.IndustrySkillPassport;
using Diten.TalentEcosystemService.Application.Features.IndustrySkillPassport.Commands;
using Diten.TalentEcosystemService.Application.Features.IndustrySkillPassport.Handlers;
using Diten.TalentEcosystemService.Application.Features.IndustrySkillPassport.Queries;
using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Enums;
using Diten.TalentEcosystemService.Domain.Repositories;
using Diten.TalentEcosystemService.Persistence.Repositories;
using Xunit;

namespace Diten.TalentEcosystemService.Application.Tests;

public sealed class IndustrySkillPassportTests
{
    [Fact]
    public async Task Create_list_and_get_metadata_contract()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryIndustrySkillPassportReadinessMetadataRepository();
        var create = CreateHandler(repository, tenantId);

        var created = await create.Handle(new CreateIndustrySkillPassportReadinessCommand(ValidRequest()), CancellationToken.None);
        var list = await new GetIndustrySkillPassportReadinessListHandler(repository, new FixedTenantContext(tenantId))
            .Handle(new GetIndustrySkillPassportReadinessListQuery(), CancellationToken.None);
        var get = await new GetIndustrySkillPassportReadinessByIdHandler(repository, new FixedTenantContext(tenantId))
            .Handle(new GetIndustrySkillPassportReadinessByIdQuery(created.Data), CancellationToken.None);

        Assert.True(created.IsSuccessful);
        Assert.Equal(201, created.StatusCode);
        Assert.True(list.IsSuccessful);
        Assert.Single(list.Data!);
        Assert.True(get.IsSuccessful);
        Assert.Equal("SUCC-001", get.Data!.Code);
        Assert.Equal("IndustrySkillPassport readiness", get.Data.DisplayName);
        Assert.Equal(IndustrySkillPassportReadinessState.NotRequired, get.Data.SkillClaimCatalogBoundaryState);
        Assert.Equal(IndustrySkillPassportReadinessState.NotRequired, get.Data.AttestationIntakeBoundaryState);
        Assert.Equal(IndustrySkillPassportReadinessState.NotRequired, get.Data.VerificationScopeBoundaryState);
        Assert.Equal(IndustrySkillPassportReadinessState.NotRequired, get.Data.VisibilityControlBoundaryState);
        Assert.Equal(IndustrySkillPassportReadinessState.NotRequired, get.Data.PassportReviewBoundaryState);
        Assert.Equal(IndustrySkillPassportReadinessState.NotRequired, get.Data.AutomatedDecisionBoundaryState);
        Assert.Equal(IndustrySkillPassportReadinessState.NotRequired, get.Data.TalentDataSourceDependencyState);
        Assert.Equal(IndustrySkillPassportReadinessState.NotRequired, get.Data.ConsentPolicyDependencyState);
        Assert.Equal(IndustrySkillPassportReadinessState.NotRequired, get.Data.CertificationSourceDependencyState);
        Assert.Equal(IndustrySkillPassportReadinessState.NotRequired, get.Data.NotificationDependencyState);
    }

    [Fact]
    public async Task Cross_tenant_lookup_returns_not_found()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var metadata = Metadata(tenantA);
        var handler = new GetIndustrySkillPassportReadinessByIdHandler(
            new InMemoryIndustrySkillPassportReadinessMetadataRepository(metadata),
            new FixedTenantContext(tenantB));

        var response = await handler.Handle(new GetIndustrySkillPassportReadinessByIdQuery(metadata.Id), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Missing_tenant_context_fails_closed()
    {
        var handler = CreateHandler(new InMemoryIndustrySkillPassportReadinessMetadataRepository(), Guid.Empty);

        var response = await handler.Handle(new CreateIndustrySkillPassportReadinessCommand(ValidRequest()), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(401, response.StatusCode);
    }

    [Fact]
    public async Task Duplicate_active_code_returns_conflict_per_tenant()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryIndustrySkillPassportReadinessMetadataRepository();
        var handler = CreateHandler(repository, tenantId);

        var first = await handler.Handle(new CreateIndustrySkillPassportReadinessCommand(ValidRequest(code: "succ-001")), CancellationToken.None);
        var second = await handler.Handle(new CreateIndustrySkillPassportReadinessCommand(ValidRequest(code: " SUCC-001 ")), CancellationToken.None);

        Assert.True(first.IsSuccessful);
        Assert.False(second.IsSuccessful);
        Assert.Equal(409, second.StatusCode);
    }

    [Fact]
    public async Task Soft_delete_hides_record_and_sets_deleted_at()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(tenantId);
        var repository = new InMemoryIndustrySkillPassportReadinessMetadataRepository(metadata);
        var handler = new DeleteIndustrySkillPassportReadinessHandler(repository, new FixedTenantContext(tenantId));

        var response = await handler.Handle(new DeleteIndustrySkillPassportReadinessCommand(metadata.Id), CancellationToken.None);
        var hidden = await repository.GetByIdAsync(tenantId, metadata.Id, CancellationToken.None);
        var stored = RepositoryItems(repository)[metadata.Id];

        Assert.True(response.IsSuccessful);
        Assert.Equal(204, response.StatusCode);
        Assert.Null(hidden);
        Assert.True(stored.IsDeleted);
        Assert.NotNull(stored.DeletedAt);
        Assert.Equal(IndustrySkillPassportReadinessState.Archived, stored.IndustrySkillPassportReadinessState);
    }

    [Fact]
    public async Task Ready_state_fails_closed_when_preconditions_are_deferred()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryIndustrySkillPassportReadinessMetadataRepository();
        var handler = CreateHandler(repository, tenantId);

        var response = await handler.Handle(
            new CreateIndustrySkillPassportReadinessCommand(ValidRequest(readinessState: IndustrySkillPassportReadinessState.Ready)),
            CancellationToken.None);
        var stored = RepositoryItems(repository)[response.Data];

        Assert.True(response.IsSuccessful);
        Assert.Equal(IndustrySkillPassportReadinessState.Deferred, stored.IndustrySkillPassportReadinessState);
        Assert.Contains("deferred", stored.DeferredReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Evaluation_promotes_ready_only_when_metadata_preconditions_are_satisfied()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(
            tenantId,
            consentPreconditionState: IndustrySkillPassportReadinessState.Ready,
            dataMinimizationState: IndustrySkillPassportReadinessState.Ready,
            retentionPolicyState: IndustrySkillPassportReadinessState.Ready,
            verificationPolicyState: IndustrySkillPassportReadinessState.Ready);
        var repository = new InMemoryIndustrySkillPassportReadinessMetadataRepository(metadata);
        var handler = new EvaluateIndustrySkillPassportReadinessHandler(repository, new FixedTenantContext(tenantId));

        var response = await handler.Handle(new EvaluateIndustrySkillPassportReadinessCommand(metadata.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(IndustrySkillPassportReadinessState.Ready, response.Data!.IndustrySkillPassportReadinessState);
        Assert.NotNull(response.Data.LastEvaluatedAt);
    }

    [Fact]
    public async Task Non_activating_evaluation_preserves_deferred_metadata()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(tenantId, consentPreconditionState: IndustrySkillPassportReadinessState.Deferred);
        var repository = new InMemoryIndustrySkillPassportReadinessMetadataRepository(metadata);
        var handler = new EvaluateIndustrySkillPassportReadinessHandler(repository, new FixedTenantContext(tenantId));

        var response = await handler.Handle(new EvaluateIndustrySkillPassportReadinessCommand(metadata.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(IndustrySkillPassportReadinessState.Deferred, response.Data!.IndustrySkillPassportReadinessState);
        Assert.Contains("preconditions", response.Data.DeferredReason);
    }

    [Fact]
    public async Task Pool_highpotential_nomination_assessment_review_and_decision_boundaries_cannot_be_ready()
    {
        var tenantId = Guid.NewGuid();
        var handler = CreateHandler(new InMemoryIndustrySkillPassportReadinessMetadataRepository(), tenantId);

        var requests = new[]
        {
            ValidRequest(skillClaimCatalogBoundaryState: IndustrySkillPassportReadinessState.Ready),
            ValidRequest(attestationIntakeBoundaryState: IndustrySkillPassportReadinessState.Ready),
            ValidRequest(verificationScopeBoundaryState: IndustrySkillPassportReadinessState.Ready),
            ValidRequest(visibilityControlBoundaryState: IndustrySkillPassportReadinessState.Ready),
            ValidRequest(passportReviewBoundaryState: IndustrySkillPassportReadinessState.Ready),
            ValidRequest(automatedDecisionBoundaryState: IndustrySkillPassportReadinessState.Ready)
        };

        foreach (var request in requests)
        {
            var response = await handler.Handle(new CreateIndustrySkillPassportReadinessCommand(request), CancellationToken.None);

            Assert.False(response.IsSuccessful);
            Assert.Equal(400, response.StatusCode);
        }
    }

    [Fact]
    public async Task Audit_metadata_uses_audit_permission_surface()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(tenantId);
        var repository = new InMemoryIndustrySkillPassportReadinessMetadataRepository(metadata);
        var response = await new GetIndustrySkillPassportAuditMetadataHandler(repository, new FixedTenantContext(tenantId))
            .Handle(new GetIndustrySkillPassportAuditMetadataQuery(metadata.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(metadata.Code, response.Data!.Code);
        Assert.Equal(IndustrySkillPassportGuard.AuditReadPermission, PermissionFor(nameof(IndustrySkillPassportController.GetAuditMetadata)));
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
        var handler = CreateHandler(new InMemoryIndustrySkillPassportReadinessMetadataRepository(), tenantId);

        // Marker injected as a STANDALONE token (whitespace-delimited) so the word-boundary
        // (\b) matcher rejects a genuine forbidden token.
        var response = await handler.Handle(
            new CreateIndustrySkillPassportReadinessCommand(ValidRequest(sourceContractVersion: $"v1 {marker}")),
            CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
    }

    [Theory]
    [InlineData("talent taxonomy")]
    [InlineData("industry-skill-passport pipeline")]
    [InlineData("nomination scorecard")]
    public async Task Word_boundary_matcher_accepts_legitimate_values_that_merely_contain_marker_substrings(string legitimateValue)
    {
        // Regression guard for the substring false-positive that previously broke LearningTraining:
        // "taxonomy" contains "tax", "scorecard" contains "score" — with the \b matcher these are
        // legitimate standalone tokens and MUST be accepted (create succeeds).
        var tenantId = Guid.NewGuid();
        var handler = CreateHandler(new InMemoryIndustrySkillPassportReadinessMetadataRepository(), tenantId);

        var response = await handler.Handle(
            new CreateIndustrySkillPassportReadinessCommand(ValidRequest(displayName: legitimateValue)),
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
            typeof(IndustrySkillPassportReadinessCreateRequest),
            typeof(IndustrySkillPassportReadinessDto),
            typeof(IndustrySkillPassportReadinessMetadata)
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
        Assert.Null(typeof(IndustrySkillPassportReadinessCreateRequest).GetProperty("TenantId"));
    }

    [Fact]
    public void Controller_permissions_match_pack()
    {
        Assert.Equal(IndustrySkillPassportGuard.ReadPermission, PermissionFor(nameof(IndustrySkillPassportController.GetAll)));
        Assert.Equal(IndustrySkillPassportGuard.ReadPermission, PermissionFor(nameof(IndustrySkillPassportController.GetById)));
        Assert.Equal(IndustrySkillPassportGuard.ManagePermission, PermissionFor(nameof(IndustrySkillPassportController.Create)));
        Assert.Equal(IndustrySkillPassportGuard.EvaluatePermission, PermissionFor(nameof(IndustrySkillPassportController.Evaluate)));
        Assert.Equal(IndustrySkillPassportGuard.ManagePermission, PermissionFor(nameof(IndustrySkillPassportController.Delete)));
        Assert.Equal(IndustrySkillPassportGuard.AuditReadPermission, PermissionFor(nameof(IndustrySkillPassportController.GetAuditMetadata)));
    }

    [Fact]
    public void Mongo_repository_contract_names_are_stable()
    {
        Assert.Equal("tep_industry_skill_passport_readiness", MongoIndustrySkillPassportReadinessMetadataRepository.CollectionName);
        Assert.Equal("ux_tep_industry_skill_passport_tenant_code_active", MongoIndustrySkillPassportReadinessMetadataRepository.ActiveCodeUniqueIndexName);
        Assert.Equal("ix_tep_industry_skill_passport_tenant_state", MongoIndustrySkillPassportReadinessMetadataRepository.TenantStateIndexName);
    }

    [Fact]
    public void Runtime_literal_regression_guard_has_no_reserved_identity_literals()
    {
        var reserved = string.Join("-", "CAND", "CAP", "0047");
        var legacy = string.Join("-", "MOD", "0338");
        var runtimeStrings = new[]
        {
            IndustrySkillPassportGuard.OwnerKey,
            IndustrySkillPassportGuard.ReadPermission,
            IndustrySkillPassportGuard.ManagePermission,
            IndustrySkillPassportGuard.EvaluatePermission,
            IndustrySkillPassportGuard.AuditReadPermission,
            MongoIndustrySkillPassportReadinessMetadataRepository.CollectionName
        };

        Assert.DoesNotContain(runtimeStrings, value => value.Contains(reserved, StringComparison.Ordinal));
        Assert.DoesNotContain(runtimeStrings, value => value.Contains(legacy, StringComparison.Ordinal));
    }

    private static CreateIndustrySkillPassportReadinessHandler CreateHandler(
        IIndustrySkillPassportReadinessMetadataRepository repository,
        Guid tenantId) =>
        new(repository, new FixedTenantContext(tenantId == Guid.Empty ? null : tenantId));

    private static IndustrySkillPassportReadinessCreateRequest ValidRequest(
        string code = "SUCC-001",
        string displayName = "IndustrySkillPassport readiness",
        IndustrySkillPassportReadinessState readinessState = IndustrySkillPassportReadinessState.Draft,
        string sourceContractVersion = "v1",
        IndustrySkillPassportReadinessState skillClaimCatalogBoundaryState = IndustrySkillPassportReadinessState.NotRequired,
        IndustrySkillPassportReadinessState attestationIntakeBoundaryState = IndustrySkillPassportReadinessState.NotRequired,
        IndustrySkillPassportReadinessState verificationScopeBoundaryState = IndustrySkillPassportReadinessState.NotRequired,
        IndustrySkillPassportReadinessState visibilityControlBoundaryState = IndustrySkillPassportReadinessState.NotRequired,
        IndustrySkillPassportReadinessState passportReviewBoundaryState = IndustrySkillPassportReadinessState.NotRequired,
        IndustrySkillPassportReadinessState automatedDecisionBoundaryState = IndustrySkillPassportReadinessState.NotRequired) =>
        new()
        {
            Code = code,
            DisplayName = displayName,
            IndustrySkillPassportReadinessState = readinessState,
            SkillClaimCatalogBoundaryState = skillClaimCatalogBoundaryState,
            AttestationIntakeBoundaryState = attestationIntakeBoundaryState,
            VerificationScopeBoundaryState = verificationScopeBoundaryState,
            VisibilityControlBoundaryState = visibilityControlBoundaryState,
            PassportReviewBoundaryState = passportReviewBoundaryState,
            AutomatedDecisionBoundaryState = automatedDecisionBoundaryState,
            TalentDataSourceDependencyState = IndustrySkillPassportReadinessState.NotRequired,
            ConsentPolicyDependencyState = IndustrySkillPassportReadinessState.NotRequired,
            CertificationSourceDependencyState = IndustrySkillPassportReadinessState.NotRequired,
            NotificationDependencyState = IndustrySkillPassportReadinessState.NotRequired,
            ConsentPreconditionState = IndustrySkillPassportReadinessState.Deferred,
            DataMinimizationState = IndustrySkillPassportReadinessState.Deferred,
            RetentionPolicyState = IndustrySkillPassportReadinessState.Deferred,
            VerificationPolicyState = IndustrySkillPassportReadinessState.Deferred,
            DependencyStates = new Dictionary<string, IndustrySkillPassportReadinessState>
            {
                ["talentDataSource"] = IndustrySkillPassportReadinessState.Ready,
                ["consentPolicy"] = IndustrySkillPassportReadinessState.Ready,
                ["skillClaimCatalog"] = IndustrySkillPassportReadinessState.Ready
            },
            SourceContractVersion = sourceContractVersion,
            IndustrySkillPassportReadinessVersion = 1
        };

    private static IndustrySkillPassportReadinessMetadata Metadata(
        Guid tenantId,
        string code = "SUCC-001",
        IndustrySkillPassportReadinessState consentPreconditionState = IndustrySkillPassportReadinessState.Deferred,
        IndustrySkillPassportReadinessState dataMinimizationState = IndustrySkillPassportReadinessState.Deferred,
        IndustrySkillPassportReadinessState retentionPolicyState = IndustrySkillPassportReadinessState.Deferred,
        IndustrySkillPassportReadinessState verificationPolicyState = IndustrySkillPassportReadinessState.Deferred) =>
        new()
        {
            TenantId = tenantId,
            Code = code,
            DisplayName = "IndustrySkillPassport readiness",
            IndustrySkillPassportReadinessState = IndustrySkillPassportReadinessState.Draft,
            SkillClaimCatalogBoundaryState = IndustrySkillPassportReadinessState.NotRequired,
            AttestationIntakeBoundaryState = IndustrySkillPassportReadinessState.NotRequired,
            VerificationScopeBoundaryState = IndustrySkillPassportReadinessState.NotRequired,
            VisibilityControlBoundaryState = IndustrySkillPassportReadinessState.NotRequired,
            PassportReviewBoundaryState = IndustrySkillPassportReadinessState.NotRequired,
            AutomatedDecisionBoundaryState = IndustrySkillPassportReadinessState.NotRequired,
            TalentDataSourceDependencyState = IndustrySkillPassportReadinessState.NotRequired,
            ConsentPolicyDependencyState = IndustrySkillPassportReadinessState.NotRequired,
            CertificationSourceDependencyState = IndustrySkillPassportReadinessState.NotRequired,
            NotificationDependencyState = IndustrySkillPassportReadinessState.NotRequired,
            ConsentPreconditionState = consentPreconditionState,
            DataMinimizationState = dataMinimizationState,
            RetentionPolicyState = retentionPolicyState,
            VerificationPolicyState = verificationPolicyState,
            DependencyStates = new Dictionary<string, IndustrySkillPassportReadinessState>
            {
                ["talentDataSource"] = IndustrySkillPassportReadinessState.Ready
            },
            SourceContractVersion = "v1",
            IndustrySkillPassportReadinessVersion = 1
        };

    private static IReadOnlyDictionary<Guid, IndustrySkillPassportReadinessMetadata> RepositoryItems(
        InMemoryIndustrySkillPassportReadinessMetadataRepository repository)
    {
        var field = typeof(InMemoryIndustrySkillPassportReadinessMetadataRepository)
            .GetField("_items", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (IReadOnlyDictionary<Guid, IndustrySkillPassportReadinessMetadata>)field.GetValue(repository)!;
    }

    private static string PermissionFor(string methodName)
    {
        var method = typeof(IndustrySkillPassportController).GetMethod(methodName)!;
        var attribute = method.GetCustomAttribute<HasPermissionAttribute>()!;
        var field = typeof(HasPermissionAttribute).GetField("_permission", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (string)field.GetValue(attribute)!;
    }

    private sealed class FixedTenantContext : ITenantContext
    {
        public FixedTenantContext(Guid? tenantId) => TenantId = tenantId;

        public Guid? TenantId { get; }
    }

    private sealed class InMemoryIndustrySkillPassportReadinessMetadataRepository : IIndustrySkillPassportReadinessMetadataRepository
    {
        private readonly Dictionary<Guid, IndustrySkillPassportReadinessMetadata> _items;

        public InMemoryIndustrySkillPassportReadinessMetadataRepository(params IndustrySkillPassportReadinessMetadata[] items) =>
            _items = items.ToDictionary(x => x.Id);

        public Task<IReadOnlyList<IndustrySkillPassportReadinessMetadata>> ListAsync(Guid tenantId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<IndustrySkillPassportReadinessMetadata>>(
                _items.Values.Where(x => x.TenantId == tenantId && !x.IsDeleted).OrderBy(x => x.Code).ToList());

        public Task<IndustrySkillPassportReadinessMetadata?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct) =>
            Task.FromResult(_items.Values.FirstOrDefault(x => x.TenantId == tenantId && x.Id == id && !x.IsDeleted));

        public Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct) =>
            Task.FromResult(_items.Values.Any(x => x.TenantId == tenantId && x.Code == code && !x.IsDeleted && x.Id != excludingId));

        public Task CreateAsync(IndustrySkillPassportReadinessMetadata metadata, CancellationToken ct)
        {
            _items[metadata.Id] = metadata;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(IndustrySkillPassportReadinessMetadata metadata, CancellationToken ct)
        {
            _items[metadata.Id] = metadata;
            return Task.CompletedTask;
        }
    }
}
