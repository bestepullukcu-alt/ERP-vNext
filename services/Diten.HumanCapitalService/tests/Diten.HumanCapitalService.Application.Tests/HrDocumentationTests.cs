using System.Reflection;
using Diten.HumanCapitalService.Api.Controllers.Hcm;
using Diten.HumanCapitalService.Api.Security;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Application.Features.HrDocumentation;
using Diten.HumanCapitalService.Application.Features.HrDocumentation.Commands;
using Diten.HumanCapitalService.Application.Features.HrDocumentation.Handlers;
using Diten.HumanCapitalService.Application.Features.HrDocumentation.Queries;
using Diten.HumanCapitalService.Domain.Entities;
using Diten.HumanCapitalService.Domain.Enums;
using Diten.HumanCapitalService.Domain.Repositories;
using Diten.HumanCapitalService.Persistence.Repositories;
using Xunit;

namespace Diten.HumanCapitalService.Application.Tests;

public sealed class HrDocumentationTests
{
    [Fact]
    public async Task Create_list_and_get_metadata_contract()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryHrDocumentationReadinessMetadataRepository();
        var create = CreateHandler(repository, tenantId);

        var created = await create.Handle(new CreateHrDocumentationReadinessCommand(ValidRequest()), CancellationToken.None);
        var list = await new GetHrDocumentationReadinessListHandler(repository, new FixedTenantContext(tenantId))
            .Handle(new GetHrDocumentationReadinessListQuery(), CancellationToken.None);
        var get = await new GetHrDocumentationReadinessByIdHandler(repository, new FixedTenantContext(tenantId))
            .Handle(new GetHrDocumentationReadinessByIdQuery(created.Data), CancellationToken.None);

        Assert.True(created.IsSuccessful);
        Assert.Equal(201, created.StatusCode);
        Assert.True(list.IsSuccessful);
        Assert.Single(list.Data!);
        Assert.True(get.IsSuccessful);
        Assert.Equal("SUCC-001", get.Data!.Code);
        Assert.Equal("HrDocumentation readiness", get.Data.DisplayName);
        Assert.Equal(HrDocumentationReadinessState.NotRequired, get.Data.DocumentWorkspaceBoundaryState);
        Assert.Equal(HrDocumentationReadinessState.NotRequired, get.Data.EvidenceLinkBoundaryState);
        Assert.Equal(HrDocumentationReadinessState.NotRequired, get.Data.DocumentClassificationBoundaryState);
        Assert.Equal(HrDocumentationReadinessState.NotRequired, get.Data.LegalHoldBoundaryState);
        Assert.Equal(HrDocumentationReadinessState.NotRequired, get.Data.DispositionScheduleBoundaryState);
        Assert.Equal(HrDocumentationReadinessState.NotRequired, get.Data.AutomatedDecisionBoundaryState);
        Assert.Equal(HrDocumentationReadinessState.NotRequired, get.Data.DocumentRepositoryDependencyState);
        Assert.Equal(HrDocumentationReadinessState.NotRequired, get.Data.EvidenceStoreDependencyState);
        Assert.Equal(HrDocumentationReadinessState.NotRequired, get.Data.DocumentDependencyState);
        Assert.Equal(HrDocumentationReadinessState.NotRequired, get.Data.NotificationDependencyState);
    }

    [Fact]
    public async Task Cross_tenant_lookup_returns_not_found()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var metadata = Metadata(tenantA);
        var handler = new GetHrDocumentationReadinessByIdHandler(
            new InMemoryHrDocumentationReadinessMetadataRepository(metadata),
            new FixedTenantContext(tenantB));

        var response = await handler.Handle(new GetHrDocumentationReadinessByIdQuery(metadata.Id), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Missing_tenant_context_fails_closed()
    {
        var handler = CreateHandler(new InMemoryHrDocumentationReadinessMetadataRepository(), Guid.Empty);

        var response = await handler.Handle(new CreateHrDocumentationReadinessCommand(ValidRequest()), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(401, response.StatusCode);
    }

    [Fact]
    public async Task Duplicate_active_code_returns_conflict_per_tenant()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryHrDocumentationReadinessMetadataRepository();
        var handler = CreateHandler(repository, tenantId);

        var first = await handler.Handle(new CreateHrDocumentationReadinessCommand(ValidRequest(code: "succ-001")), CancellationToken.None);
        var second = await handler.Handle(new CreateHrDocumentationReadinessCommand(ValidRequest(code: " SUCC-001 ")), CancellationToken.None);

        Assert.True(first.IsSuccessful);
        Assert.False(second.IsSuccessful);
        Assert.Equal(409, second.StatusCode);
    }

    [Fact]
    public async Task Soft_delete_hides_record_and_sets_deleted_at()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(tenantId);
        var repository = new InMemoryHrDocumentationReadinessMetadataRepository(metadata);
        var handler = new DeleteHrDocumentationReadinessHandler(repository, new FixedTenantContext(tenantId));

        var response = await handler.Handle(new DeleteHrDocumentationReadinessCommand(metadata.Id), CancellationToken.None);
        var hidden = await repository.GetByIdAsync(tenantId, metadata.Id, CancellationToken.None);
        var stored = RepositoryItems(repository)[metadata.Id];

        Assert.True(response.IsSuccessful);
        Assert.Equal(204, response.StatusCode);
        Assert.Null(hidden);
        Assert.True(stored.IsDeleted);
        Assert.NotNull(stored.DeletedAt);
        Assert.Equal(HrDocumentationReadinessState.Archived, stored.HrDocumentationReadinessState);
    }

    [Fact]
    public async Task Ready_state_fails_closed_when_preconditions_are_deferred()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryHrDocumentationReadinessMetadataRepository();
        var handler = CreateHandler(repository, tenantId);

        var response = await handler.Handle(
            new CreateHrDocumentationReadinessCommand(ValidRequest(readinessState: HrDocumentationReadinessState.Ready)),
            CancellationToken.None);
        var stored = RepositoryItems(repository)[response.Data];

        Assert.True(response.IsSuccessful);
        Assert.Equal(HrDocumentationReadinessState.Deferred, stored.HrDocumentationReadinessState);
        Assert.Contains("deferred", stored.DeferredReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Evaluation_promotes_ready_only_when_metadata_preconditions_are_satisfied()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(
            tenantId,
            consentPreconditionState: HrDocumentationReadinessState.Ready,
            dataMinimizationState: HrDocumentationReadinessState.Ready,
            retentionPolicyState: HrDocumentationReadinessState.Ready,
            evidencePolicyState: HrDocumentationReadinessState.Ready);
        var repository = new InMemoryHrDocumentationReadinessMetadataRepository(metadata);
        var handler = new EvaluateHrDocumentationReadinessHandler(repository, new FixedTenantContext(tenantId));

        var response = await handler.Handle(new EvaluateHrDocumentationReadinessCommand(metadata.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(HrDocumentationReadinessState.Ready, response.Data!.HrDocumentationReadinessState);
        Assert.NotNull(response.Data.LastEvaluatedAt);
    }

    [Fact]
    public async Task Non_activating_evaluation_preserves_deferred_metadata()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(tenantId, consentPreconditionState: HrDocumentationReadinessState.Deferred);
        var repository = new InMemoryHrDocumentationReadinessMetadataRepository(metadata);
        var handler = new EvaluateHrDocumentationReadinessHandler(repository, new FixedTenantContext(tenantId));

        var response = await handler.Handle(new EvaluateHrDocumentationReadinessCommand(metadata.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(HrDocumentationReadinessState.Deferred, response.Data!.HrDocumentationReadinessState);
        Assert.Contains("preconditions", response.Data.DeferredReason);
    }

    [Fact]
    public async Task Pool_highpotential_nomination_assessment_review_and_decision_boundaries_cannot_be_ready()
    {
        var tenantId = Guid.NewGuid();
        var handler = CreateHandler(new InMemoryHrDocumentationReadinessMetadataRepository(), tenantId);

        var requests = new[]
        {
            ValidRequest(documentWorkspaceBoundaryState: HrDocumentationReadinessState.Ready),
            ValidRequest(evidenceLinkBoundaryState: HrDocumentationReadinessState.Ready),
            ValidRequest(documentClassificationBoundaryState: HrDocumentationReadinessState.Ready),
            ValidRequest(legalHoldBoundaryState: HrDocumentationReadinessState.Ready),
            ValidRequest(dispositionScheduleBoundaryState: HrDocumentationReadinessState.Ready),
            ValidRequest(automatedDecisionBoundaryState: HrDocumentationReadinessState.Ready)
        };

        foreach (var request in requests)
        {
            var response = await handler.Handle(new CreateHrDocumentationReadinessCommand(request), CancellationToken.None);

            Assert.False(response.IsSuccessful);
            Assert.Equal(400, response.StatusCode);
        }
    }

    [Fact]
    public async Task Audit_metadata_uses_audit_permission_surface()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(tenantId);
        var repository = new InMemoryHrDocumentationReadinessMetadataRepository(metadata);
        var response = await new GetHrDocumentationAuditMetadataHandler(repository, new FixedTenantContext(tenantId))
            .Handle(new GetHrDocumentationAuditMetadataQuery(metadata.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(metadata.Code, response.Data!.Code);
        Assert.Equal(HrDocumentationGuard.AuditReadPermission, PermissionFor(nameof(HrDocumentationController.GetAuditMetadata)));
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
        var handler = CreateHandler(new InMemoryHrDocumentationReadinessMetadataRepository(), tenantId);

        // Marker injected as a STANDALONE token (whitespace-delimited) so the word-boundary
        // (\b) matcher rejects a genuine forbidden token.
        var response = await handler.Handle(
            new CreateHrDocumentationReadinessCommand(ValidRequest(sourceContractVersion: $"v1 {marker}")),
            CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
    }

    [Theory]
    [InlineData("talent taxonomy")]
    [InlineData("hr-documentation pipeline")]
    [InlineData("nomination scorecard")]
    public async Task Word_boundary_matcher_accepts_legitimate_values_that_merely_contain_marker_substrings(string legitimateValue)
    {
        // Regression guard for the substring false-positive that previously broke LearningTraining:
        // "taxonomy" contains "tax", "scorecard" contains "score" — with the \b matcher these are
        // legitimate standalone tokens and MUST be accepted (create succeeds).
        var tenantId = Guid.NewGuid();
        var handler = CreateHandler(new InMemoryHrDocumentationReadinessMetadataRepository(), tenantId);

        var response = await handler.Handle(
            new CreateHrDocumentationReadinessCommand(ValidRequest(displayName: legitimateValue)),
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
            typeof(HrDocumentationReadinessCreateRequest),
            typeof(HrDocumentationReadinessDto),
            typeof(HrDocumentationReadinessMetadata)
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
        Assert.Null(typeof(HrDocumentationReadinessCreateRequest).GetProperty("TenantId"));
    }

    [Fact]
    public void Controller_permissions_match_pack()
    {
        Assert.Equal(HrDocumentationGuard.ReadPermission, PermissionFor(nameof(HrDocumentationController.GetAll)));
        Assert.Equal(HrDocumentationGuard.ReadPermission, PermissionFor(nameof(HrDocumentationController.GetById)));
        Assert.Equal(HrDocumentationGuard.ManagePermission, PermissionFor(nameof(HrDocumentationController.Create)));
        Assert.Equal(HrDocumentationGuard.EvaluatePermission, PermissionFor(nameof(HrDocumentationController.Evaluate)));
        Assert.Equal(HrDocumentationGuard.ManagePermission, PermissionFor(nameof(HrDocumentationController.Delete)));
        Assert.Equal(HrDocumentationGuard.AuditReadPermission, PermissionFor(nameof(HrDocumentationController.GetAuditMetadata)));
    }

    [Fact]
    public void Mongo_repository_contract_names_are_stable()
    {
        Assert.Equal("hcm_hr_documentation_readiness", MongoHrDocumentationReadinessMetadataRepository.CollectionName);
        Assert.Equal("ux_hcm_hr_documentation_tenant_code_active", MongoHrDocumentationReadinessMetadataRepository.ActiveCodeUniqueIndexName);
        Assert.Equal("ix_hcm_hr_documentation_tenant_state", MongoHrDocumentationReadinessMetadataRepository.TenantStateIndexName);
    }

    [Fact]
    public void Runtime_literal_regression_guard_has_no_reserved_identity_literals()
    {
        var reserved = string.Join("-", "CAND", "CAP", "0035");
        var legacy = string.Join("-", "MOD", "0313");
        var runtimeStrings = new[]
        {
            HrDocumentationGuard.OwnerKey,
            HrDocumentationGuard.ReadPermission,
            HrDocumentationGuard.ManagePermission,
            HrDocumentationGuard.EvaluatePermission,
            HrDocumentationGuard.AuditReadPermission,
            MongoHrDocumentationReadinessMetadataRepository.CollectionName
        };

        Assert.DoesNotContain(runtimeStrings, value => value.Contains(reserved, StringComparison.Ordinal));
        Assert.DoesNotContain(runtimeStrings, value => value.Contains(legacy, StringComparison.Ordinal));
    }

    private static CreateHrDocumentationReadinessHandler CreateHandler(
        IHrDocumentationReadinessMetadataRepository repository,
        Guid tenantId) =>
        new(repository, new FixedTenantContext(tenantId == Guid.Empty ? null : tenantId));

    private static HrDocumentationReadinessCreateRequest ValidRequest(
        string code = "SUCC-001",
        string displayName = "HrDocumentation readiness",
        HrDocumentationReadinessState readinessState = HrDocumentationReadinessState.Draft,
        string sourceContractVersion = "v1",
        HrDocumentationReadinessState documentWorkspaceBoundaryState = HrDocumentationReadinessState.NotRequired,
        HrDocumentationReadinessState evidenceLinkBoundaryState = HrDocumentationReadinessState.NotRequired,
        HrDocumentationReadinessState documentClassificationBoundaryState = HrDocumentationReadinessState.NotRequired,
        HrDocumentationReadinessState legalHoldBoundaryState = HrDocumentationReadinessState.NotRequired,
        HrDocumentationReadinessState dispositionScheduleBoundaryState = HrDocumentationReadinessState.NotRequired,
        HrDocumentationReadinessState automatedDecisionBoundaryState = HrDocumentationReadinessState.NotRequired) =>
        new()
        {
            Code = code,
            DisplayName = displayName,
            HrDocumentationReadinessState = readinessState,
            DocumentWorkspaceBoundaryState = documentWorkspaceBoundaryState,
            EvidenceLinkBoundaryState = evidenceLinkBoundaryState,
            DocumentClassificationBoundaryState = documentClassificationBoundaryState,
            LegalHoldBoundaryState = legalHoldBoundaryState,
            DispositionScheduleBoundaryState = dispositionScheduleBoundaryState,
            AutomatedDecisionBoundaryState = automatedDecisionBoundaryState,
            DocumentRepositoryDependencyState = HrDocumentationReadinessState.NotRequired,
            EvidenceStoreDependencyState = HrDocumentationReadinessState.NotRequired,
            DocumentDependencyState = HrDocumentationReadinessState.NotRequired,
            NotificationDependencyState = HrDocumentationReadinessState.NotRequired,
            ConsentPreconditionState = HrDocumentationReadinessState.Deferred,
            DataMinimizationState = HrDocumentationReadinessState.Deferred,
            RetentionPolicyState = HrDocumentationReadinessState.Deferred,
            EvidencePolicyState = HrDocumentationReadinessState.Deferred,
            DependencyStates = new Dictionary<string, HrDocumentationReadinessState>
            {
                ["documentRepository"] = HrDocumentationReadinessState.Ready,
                ["evidenceStore"] = HrDocumentationReadinessState.Ready,
                ["documentWorkspace"] = HrDocumentationReadinessState.Ready
            },
            SourceContractVersion = sourceContractVersion,
            HrDocumentationReadinessVersion = 1
        };

    private static HrDocumentationReadinessMetadata Metadata(
        Guid tenantId,
        string code = "SUCC-001",
        HrDocumentationReadinessState consentPreconditionState = HrDocumentationReadinessState.Deferred,
        HrDocumentationReadinessState dataMinimizationState = HrDocumentationReadinessState.Deferred,
        HrDocumentationReadinessState retentionPolicyState = HrDocumentationReadinessState.Deferred,
        HrDocumentationReadinessState evidencePolicyState = HrDocumentationReadinessState.Deferred) =>
        new()
        {
            TenantId = tenantId,
            Code = code,
            DisplayName = "HrDocumentation readiness",
            HrDocumentationReadinessState = HrDocumentationReadinessState.Draft,
            DocumentWorkspaceBoundaryState = HrDocumentationReadinessState.NotRequired,
            EvidenceLinkBoundaryState = HrDocumentationReadinessState.NotRequired,
            DocumentClassificationBoundaryState = HrDocumentationReadinessState.NotRequired,
            LegalHoldBoundaryState = HrDocumentationReadinessState.NotRequired,
            DispositionScheduleBoundaryState = HrDocumentationReadinessState.NotRequired,
            AutomatedDecisionBoundaryState = HrDocumentationReadinessState.NotRequired,
            DocumentRepositoryDependencyState = HrDocumentationReadinessState.NotRequired,
            EvidenceStoreDependencyState = HrDocumentationReadinessState.NotRequired,
            DocumentDependencyState = HrDocumentationReadinessState.NotRequired,
            NotificationDependencyState = HrDocumentationReadinessState.NotRequired,
            ConsentPreconditionState = consentPreconditionState,
            DataMinimizationState = dataMinimizationState,
            RetentionPolicyState = retentionPolicyState,
            EvidencePolicyState = evidencePolicyState,
            DependencyStates = new Dictionary<string, HrDocumentationReadinessState>
            {
                ["documentRepository"] = HrDocumentationReadinessState.Ready
            },
            SourceContractVersion = "v1",
            HrDocumentationReadinessVersion = 1
        };

    private static IReadOnlyDictionary<Guid, HrDocumentationReadinessMetadata> RepositoryItems(
        InMemoryHrDocumentationReadinessMetadataRepository repository)
    {
        var field = typeof(InMemoryHrDocumentationReadinessMetadataRepository)
            .GetField("_items", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (IReadOnlyDictionary<Guid, HrDocumentationReadinessMetadata>)field.GetValue(repository)!;
    }

    private static string PermissionFor(string methodName)
    {
        var method = typeof(HrDocumentationController).GetMethod(methodName)!;
        var attribute = method.GetCustomAttribute<HasPermissionAttribute>()!;
        var field = typeof(HasPermissionAttribute).GetField("_permission", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (string)field.GetValue(attribute)!;
    }

    private sealed class FixedTenantContext : ITenantContext
    {
        public FixedTenantContext(Guid? tenantId) => TenantId = tenantId;

        public Guid? TenantId { get; }
    }

    private sealed class InMemoryHrDocumentationReadinessMetadataRepository : IHrDocumentationReadinessMetadataRepository
    {
        private readonly Dictionary<Guid, HrDocumentationReadinessMetadata> _items;

        public InMemoryHrDocumentationReadinessMetadataRepository(params HrDocumentationReadinessMetadata[] items) =>
            _items = items.ToDictionary(x => x.Id);

        public Task<IReadOnlyList<HrDocumentationReadinessMetadata>> ListAsync(Guid tenantId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<HrDocumentationReadinessMetadata>>(
                _items.Values.Where(x => x.TenantId == tenantId && !x.IsDeleted).OrderBy(x => x.Code).ToList());

        public Task<HrDocumentationReadinessMetadata?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct) =>
            Task.FromResult(_items.Values.FirstOrDefault(x => x.TenantId == tenantId && x.Id == id && !x.IsDeleted));

        public Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct) =>
            Task.FromResult(_items.Values.Any(x => x.TenantId == tenantId && x.Code == code && !x.IsDeleted && x.Id != excludingId));

        public Task CreateAsync(HrDocumentationReadinessMetadata metadata, CancellationToken ct)
        {
            _items[metadata.Id] = metadata;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(HrDocumentationReadinessMetadata metadata, CancellationToken ct)
        {
            _items[metadata.Id] = metadata;
            return Task.CompletedTask;
        }
    }
}
