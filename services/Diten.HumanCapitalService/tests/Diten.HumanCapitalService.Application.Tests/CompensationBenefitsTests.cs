using System.Reflection;
using Diten.HumanCapitalService.Api.Controllers.Hcm;
using Diten.HumanCapitalService.Api.Security;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Application.Features.CompensationBenefits;
using Diten.HumanCapitalService.Application.Features.CompensationBenefits.Commands;
using Diten.HumanCapitalService.Application.Features.CompensationBenefits.Handlers;
using Diten.HumanCapitalService.Application.Features.CompensationBenefits.Queries;
using Diten.HumanCapitalService.Domain.Entities;
using Diten.HumanCapitalService.Domain.Enums;
using Diten.HumanCapitalService.Domain.Repositories;
using Diten.HumanCapitalService.Persistence.Repositories;
using Xunit;

namespace Diten.HumanCapitalService.Application.Tests;

public sealed class CompensationBenefitsTests
{
    [Fact]
    public async Task Create_list_and_get_metadata_contract()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryCompensationBenefitsReadinessMetadataRepository();
        var create = CreateHandler(repository, tenantId);

        var created = await create.Handle(new CreateCompensationBenefitsReadinessCommand(ValidRequest()), CancellationToken.None);
        var list = await new GetCompensationBenefitsReadinessListHandler(repository, new FixedTenantContext(tenantId), PilotLegalEntityContext())
            .Handle(new GetCompensationBenefitsReadinessListQuery(), CancellationToken.None);
        var get = await new GetCompensationBenefitsReadinessByIdHandler(repository, new FixedTenantContext(tenantId), PilotLegalEntityContext())
            .Handle(new GetCompensationBenefitsReadinessByIdQuery(created.Data), CancellationToken.None);

        Assert.True(created.IsSuccessful);
        Assert.Equal(201, created.StatusCode);
        Assert.True(list.IsSuccessful);
        Assert.Single(list.Data!);
        Assert.True(get.IsSuccessful);
        Assert.Equal("SUCC-001", get.Data!.Code);
        Assert.Equal("CompensationBenefits readiness", get.Data.DisplayName);
        Assert.Equal(CompensationBenefitsReadinessState.NotRequired, get.Data.CompensationPlanBoundaryState);
        Assert.Equal(CompensationBenefitsReadinessState.NotRequired, get.Data.BenefitProgramBoundaryState);
        Assert.Equal(CompensationBenefitsReadinessState.NotRequired, get.Data.PayGradeMappingBoundaryState);
        Assert.Equal(CompensationBenefitsReadinessState.NotRequired, get.Data.BenefitEnrollmentBoundaryState);
        Assert.Equal(CompensationBenefitsReadinessState.NotRequired, get.Data.CompensationReviewBoundaryState);
        Assert.Equal(CompensationBenefitsReadinessState.NotRequired, get.Data.AutomatedDecisionBoundaryState);
        Assert.Equal(CompensationBenefitsReadinessState.NotRequired, get.Data.CompensationSourceDependencyState);
        Assert.Equal(CompensationBenefitsReadinessState.NotRequired, get.Data.BenefitProviderSourceDependencyState);
        Assert.Equal(CompensationBenefitsReadinessState.NotRequired, get.Data.DocumentDependencyState);
        Assert.Equal(CompensationBenefitsReadinessState.NotRequired, get.Data.NotificationDependencyState);
    }

    [Fact]
    public async Task Cross_tenant_lookup_returns_not_found()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var metadata = Metadata(tenantA);
        var handler = new GetCompensationBenefitsReadinessByIdHandler(
            new InMemoryCompensationBenefitsReadinessMetadataRepository(metadata),
            new FixedTenantContext(tenantB), PilotLegalEntityContext());

        var response = await handler.Handle(new GetCompensationBenefitsReadinessByIdQuery(metadata.Id), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Missing_tenant_context_fails_closed()
    {
        var handler = CreateHandler(new InMemoryCompensationBenefitsReadinessMetadataRepository(), Guid.Empty);

        var response = await handler.Handle(new CreateCompensationBenefitsReadinessCommand(ValidRequest()), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(401, response.StatusCode);
    }

    [Fact]
    public async Task Duplicate_active_code_returns_conflict_per_tenant()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryCompensationBenefitsReadinessMetadataRepository();
        var handler = CreateHandler(repository, tenantId);

        var first = await handler.Handle(new CreateCompensationBenefitsReadinessCommand(ValidRequest(code: "succ-001")), CancellationToken.None);
        var second = await handler.Handle(new CreateCompensationBenefitsReadinessCommand(ValidRequest(code: " SUCC-001 ")), CancellationToken.None);

        Assert.True(first.IsSuccessful);
        Assert.False(second.IsSuccessful);
        Assert.Equal(409, second.StatusCode);
    }

    [Fact]
    public async Task Soft_delete_hides_record_and_sets_deleted_at()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(tenantId);
        var repository = new InMemoryCompensationBenefitsReadinessMetadataRepository(metadata);
        var handler = new DeleteCompensationBenefitsReadinessHandler(repository, new FixedTenantContext(tenantId), PilotLegalEntityContext());

        var response = await handler.Handle(new DeleteCompensationBenefitsReadinessCommand(metadata.Id), CancellationToken.None);
        var hidden = await repository.GetByIdAsync(tenantId, new[] { Holding }, metadata.Id, CancellationToken.None);
        var stored = RepositoryItems(repository)[metadata.Id];

        Assert.True(response.IsSuccessful);
        Assert.Equal(204, response.StatusCode);
        Assert.Null(hidden);
        Assert.True(stored.IsDeleted);
        Assert.NotNull(stored.DeletedAt);
        Assert.Equal(CompensationBenefitsReadinessState.Archived, stored.CompensationBenefitsReadinessState);
    }

    [Fact]
    public async Task Ready_state_fails_closed_when_preconditions_are_deferred()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryCompensationBenefitsReadinessMetadataRepository();
        var handler = CreateHandler(repository, tenantId);

        var response = await handler.Handle(
            new CreateCompensationBenefitsReadinessCommand(ValidRequest(readinessState: CompensationBenefitsReadinessState.Ready)),
            CancellationToken.None);
        var stored = RepositoryItems(repository)[response.Data];

        Assert.True(response.IsSuccessful);
        Assert.Equal(CompensationBenefitsReadinessState.Deferred, stored.CompensationBenefitsReadinessState);
        Assert.Contains("deferred", stored.DeferredReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Evaluation_promotes_ready_only_when_metadata_preconditions_are_satisfied()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(
            tenantId,
            consentPreconditionState: CompensationBenefitsReadinessState.Ready,
            dataMinimizationState: CompensationBenefitsReadinessState.Ready,
            retentionPolicyState: CompensationBenefitsReadinessState.Ready,
            evidencePolicyState: CompensationBenefitsReadinessState.Ready);
        var repository = new InMemoryCompensationBenefitsReadinessMetadataRepository(metadata);
        var handler = new EvaluateCompensationBenefitsReadinessHandler(repository, new FixedTenantContext(tenantId), PilotLegalEntityContext());

        var response = await handler.Handle(new EvaluateCompensationBenefitsReadinessCommand(metadata.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(CompensationBenefitsReadinessState.Ready, response.Data!.CompensationBenefitsReadinessState);
        Assert.NotNull(response.Data.LastEvaluatedAt);
    }

    [Fact]
    public async Task Non_activating_evaluation_preserves_deferred_metadata()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(tenantId, consentPreconditionState: CompensationBenefitsReadinessState.Deferred);
        var repository = new InMemoryCompensationBenefitsReadinessMetadataRepository(metadata);
        var handler = new EvaluateCompensationBenefitsReadinessHandler(repository, new FixedTenantContext(tenantId), PilotLegalEntityContext());

        var response = await handler.Handle(new EvaluateCompensationBenefitsReadinessCommand(metadata.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(CompensationBenefitsReadinessState.Deferred, response.Data!.CompensationBenefitsReadinessState);
        Assert.Contains("preconditions", response.Data.DeferredReason);
    }

    [Fact]
    public async Task Pool_highpotential_nomination_assessment_review_and_decision_boundaries_cannot_be_ready()
    {
        var tenantId = Guid.NewGuid();
        var handler = CreateHandler(new InMemoryCompensationBenefitsReadinessMetadataRepository(), tenantId);

        var requests = new[]
        {
            ValidRequest(compensationPlanBoundaryState: CompensationBenefitsReadinessState.Ready),
            ValidRequest(benefitProgramBoundaryState: CompensationBenefitsReadinessState.Ready),
            ValidRequest(payGradeMappingBoundaryState: CompensationBenefitsReadinessState.Ready),
            ValidRequest(benefitEnrollmentBoundaryState: CompensationBenefitsReadinessState.Ready),
            ValidRequest(compensationReviewBoundaryState: CompensationBenefitsReadinessState.Ready),
            ValidRequest(automatedDecisionBoundaryState: CompensationBenefitsReadinessState.Ready)
        };

        foreach (var request in requests)
        {
            var response = await handler.Handle(new CreateCompensationBenefitsReadinessCommand(request), CancellationToken.None);

            Assert.False(response.IsSuccessful);
            Assert.Equal(400, response.StatusCode);
        }
    }

    [Fact]
    public async Task Audit_metadata_uses_audit_permission_surface()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(tenantId);
        var repository = new InMemoryCompensationBenefitsReadinessMetadataRepository(metadata);
        var response = await new GetCompensationBenefitsAuditMetadataHandler(repository, new FixedTenantContext(tenantId), PilotLegalEntityContext())
            .Handle(new GetCompensationBenefitsAuditMetadataQuery(metadata.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(metadata.Code, response.Data!.Code);
        Assert.Equal(CompensationBenefitsGuard.AuditReadPermission, PermissionFor(nameof(CompensationBenefitsController.GetAuditMetadata)));
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
        var handler = CreateHandler(new InMemoryCompensationBenefitsReadinessMetadataRepository(), tenantId);

        // Marker injected as a STANDALONE token (whitespace-delimited) so the word-boundary
        // (\b) matcher rejects a genuine forbidden token.
        var response = await handler.Handle(
            new CreateCompensationBenefitsReadinessCommand(ValidRequest(sourceContractVersion: $"v1 {marker}")),
            CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
    }

    [Theory]
    [InlineData("talent taxonomy")]
    [InlineData("compensation-benefits pipeline")]
    [InlineData("nomination scorecard")]
    public async Task Word_boundary_matcher_accepts_legitimate_values_that_merely_contain_marker_substrings(string legitimateValue)
    {
        // Regression guard for the substring false-positive that previously broke LearningTraining:
        // "taxonomy" contains "tax", "scorecard" contains "score" — with the \b matcher these are
        // legitimate standalone tokens and MUST be accepted (create succeeds).
        var tenantId = Guid.NewGuid();
        var handler = CreateHandler(new InMemoryCompensationBenefitsReadinessMetadataRepository(), tenantId);

        var response = await handler.Handle(
            new CreateCompensationBenefitsReadinessCommand(ValidRequest(displayName: legitimateValue)),
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
            typeof(CompensationBenefitsReadinessCreateRequest),
            typeof(CompensationBenefitsReadinessDto),
            typeof(CompensationBenefitsReadinessMetadata)
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
        Assert.Null(typeof(CompensationBenefitsReadinessCreateRequest).GetProperty("TenantId"));
    }

    [Fact]
    public void Controller_permissions_match_pack()
    {
        Assert.Equal(CompensationBenefitsGuard.ReadPermission, PermissionFor(nameof(CompensationBenefitsController.GetAll)));
        Assert.Equal(CompensationBenefitsGuard.ReadPermission, PermissionFor(nameof(CompensationBenefitsController.GetById)));
        Assert.Equal(CompensationBenefitsGuard.ManagePermission, PermissionFor(nameof(CompensationBenefitsController.Create)));
        Assert.Equal(CompensationBenefitsGuard.EvaluatePermission, PermissionFor(nameof(CompensationBenefitsController.Evaluate)));
        Assert.Equal(CompensationBenefitsGuard.ManagePermission, PermissionFor(nameof(CompensationBenefitsController.Delete)));
        Assert.Equal(CompensationBenefitsGuard.AuditReadPermission, PermissionFor(nameof(CompensationBenefitsController.GetAuditMetadata)));
    }

    [Fact]
    public void Mongo_repository_contract_names_are_stable()
    {
        Assert.Equal("hcm_compensation_benefits_readiness", MongoCompensationBenefitsReadinessMetadataRepository.CollectionName);
        Assert.Equal("ux_hcm_compensation_benefits_tenant_code_active", MongoCompensationBenefitsReadinessMetadataRepository.ActiveCodeUniqueIndexName);
        Assert.Equal("ix_hcm_compensation_benefits_tenant_state", MongoCompensationBenefitsReadinessMetadataRepository.TenantStateIndexName);
    }

    [Fact]
    public void Runtime_literal_regression_guard_has_no_reserved_identity_literals()
    {
        var reserved = string.Join("-", "CAND", "CAP", "0037");
        var legacy = string.Join("-", "MOD", "0316");
        var runtimeStrings = new[]
        {
            CompensationBenefitsGuard.OwnerKey,
            CompensationBenefitsGuard.ReadPermission,
            CompensationBenefitsGuard.ManagePermission,
            CompensationBenefitsGuard.EvaluatePermission,
            CompensationBenefitsGuard.AuditReadPermission,
            MongoCompensationBenefitsReadinessMetadataRepository.CollectionName
        };

        Assert.DoesNotContain(runtimeStrings, value => value.Contains(reserved, StringComparison.Ordinal));
        Assert.DoesNotContain(runtimeStrings, value => value.Contains(legacy, StringComparison.Ordinal));
    }

    private static CreateCompensationBenefitsReadinessHandler CreateHandler(
        ICompensationBenefitsReadinessMetadataRepository repository,
        Guid tenantId) =>
        new(repository, new FixedTenantContext(tenantId == Guid.Empty ? null : tenantId), PilotLegalEntityContext());

    private static CreateCompensationBenefitsReadinessHandler CreateHandler(
        ICompensationBenefitsReadinessMetadataRepository repository,
        Guid tenantId,
        ILegalEntityContext legalEntityContext) =>
        new(repository, new FixedTenantContext(tenantId), legalEntityContext);

    private static CompensationBenefitsReadinessCreateRequest ValidRequest(
        string code = "SUCC-001",
        string displayName = "CompensationBenefits readiness",
        CompensationBenefitsReadinessState readinessState = CompensationBenefitsReadinessState.Draft,
        string sourceContractVersion = "v1",
        CompensationBenefitsReadinessState compensationPlanBoundaryState = CompensationBenefitsReadinessState.NotRequired,
        CompensationBenefitsReadinessState benefitProgramBoundaryState = CompensationBenefitsReadinessState.NotRequired,
        CompensationBenefitsReadinessState payGradeMappingBoundaryState = CompensationBenefitsReadinessState.NotRequired,
        CompensationBenefitsReadinessState benefitEnrollmentBoundaryState = CompensationBenefitsReadinessState.NotRequired,
        CompensationBenefitsReadinessState compensationReviewBoundaryState = CompensationBenefitsReadinessState.NotRequired,
        CompensationBenefitsReadinessState automatedDecisionBoundaryState = CompensationBenefitsReadinessState.NotRequired) =>
        new()
        {
            Code = code,
            DisplayName = displayName,
            CompensationBenefitsReadinessState = readinessState,
            CompensationPlanBoundaryState = compensationPlanBoundaryState,
            BenefitProgramBoundaryState = benefitProgramBoundaryState,
            PayGradeMappingBoundaryState = payGradeMappingBoundaryState,
            BenefitEnrollmentBoundaryState = benefitEnrollmentBoundaryState,
            CompensationReviewBoundaryState = compensationReviewBoundaryState,
            AutomatedDecisionBoundaryState = automatedDecisionBoundaryState,
            CompensationSourceDependencyState = CompensationBenefitsReadinessState.NotRequired,
            BenefitProviderSourceDependencyState = CompensationBenefitsReadinessState.NotRequired,
            DocumentDependencyState = CompensationBenefitsReadinessState.NotRequired,
            NotificationDependencyState = CompensationBenefitsReadinessState.NotRequired,
            ConsentPreconditionState = CompensationBenefitsReadinessState.Deferred,
            DataMinimizationState = CompensationBenefitsReadinessState.Deferred,
            RetentionPolicyState = CompensationBenefitsReadinessState.Deferred,
            EvidencePolicyState = CompensationBenefitsReadinessState.Deferred,
            DependencyStates = new Dictionary<string, CompensationBenefitsReadinessState>
            {
                ["compensationSource"] = CompensationBenefitsReadinessState.Ready,
                ["benefitProviderSource"] = CompensationBenefitsReadinessState.Ready,
                ["compensationPlan"] = CompensationBenefitsReadinessState.Ready
            },
            SourceContractVersion = sourceContractVersion,
            CompensationBenefitsReadinessVersion = 1
        };

    private static CompensationBenefitsReadinessMetadata Metadata(
        Guid tenantId,
        string code = "SUCC-001",
        CompensationBenefitsReadinessState consentPreconditionState = CompensationBenefitsReadinessState.Deferred,
        CompensationBenefitsReadinessState dataMinimizationState = CompensationBenefitsReadinessState.Deferred,
        CompensationBenefitsReadinessState retentionPolicyState = CompensationBenefitsReadinessState.Deferred,
        CompensationBenefitsReadinessState evidencePolicyState = CompensationBenefitsReadinessState.Deferred) =>
        new()
        {
            TenantId = tenantId,
            LegalEntityId = Holding,
            Code = code,
            DisplayName = "CompensationBenefits readiness",
            CompensationBenefitsReadinessState = CompensationBenefitsReadinessState.Draft,
            CompensationPlanBoundaryState = CompensationBenefitsReadinessState.NotRequired,
            BenefitProgramBoundaryState = CompensationBenefitsReadinessState.NotRequired,
            PayGradeMappingBoundaryState = CompensationBenefitsReadinessState.NotRequired,
            BenefitEnrollmentBoundaryState = CompensationBenefitsReadinessState.NotRequired,
            CompensationReviewBoundaryState = CompensationBenefitsReadinessState.NotRequired,
            AutomatedDecisionBoundaryState = CompensationBenefitsReadinessState.NotRequired,
            CompensationSourceDependencyState = CompensationBenefitsReadinessState.NotRequired,
            BenefitProviderSourceDependencyState = CompensationBenefitsReadinessState.NotRequired,
            DocumentDependencyState = CompensationBenefitsReadinessState.NotRequired,
            NotificationDependencyState = CompensationBenefitsReadinessState.NotRequired,
            ConsentPreconditionState = consentPreconditionState,
            DataMinimizationState = dataMinimizationState,
            RetentionPolicyState = retentionPolicyState,
            EvidencePolicyState = evidencePolicyState,
            DependencyStates = new Dictionary<string, CompensationBenefitsReadinessState>
            {
                ["compensationSource"] = CompensationBenefitsReadinessState.Ready
            },
            SourceContractVersion = "v1",
            CompensationBenefitsReadinessVersion = 1
        };

    private static IReadOnlyDictionary<Guid, CompensationBenefitsReadinessMetadata> RepositoryItems(
        InMemoryCompensationBenefitsReadinessMetadataRepository repository)
    {
        var field = typeof(InMemoryCompensationBenefitsReadinessMetadataRepository)
            .GetField("_items", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (IReadOnlyDictionary<Guid, CompensationBenefitsReadinessMetadata>)field.GetValue(repository)!;
    }

    private static string PermissionFor(string methodName)
    {
        var method = typeof(CompensationBenefitsController).GetMethod(methodName)!;
        var attribute = method.GetCustomAttribute<HasPermissionAttribute>()!;
        var field = typeof(HasPermissionAttribute).GetField("_permission", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (string)field.GetValue(attribute)!;
    }

    private static readonly Guid Holding = Guid.Parse("1e9a1000-0000-0000-0000-000000000001");
    private static readonly Guid Medikal = Guid.Parse("1e9a1000-0000-0000-0000-000000000002");
    private static readonly Guid Teknoloji = Guid.Parse("1e9a1000-0000-0000-0000-000000000003");

    private static FixedLegalEntityContext PilotLegalEntityContext() =>
        new(Holding, new[] { Holding, Medikal, Teknoloji });

    private static async Task<IReadOnlyList<CompensationBenefitsReadinessListItemDto>> ListWith(
        ICompensationBenefitsReadinessMetadataRepository repository,
        Guid tenantId,
        IReadOnlyCollection<Guid> effective)
    {
        var handler = new GetCompensationBenefitsReadinessListHandler(
            repository,
            new FixedTenantContext(tenantId),
            new FixedLegalEntityContext(effective.First(), effective));
        var response = await handler.Handle(new GetCompensationBenefitsReadinessListQuery(), CancellationToken.None);
        return response.Data!;
    }

    private sealed class FixedLegalEntityContext : ILegalEntityContext
    {
        private readonly IReadOnlyCollection<Guid> _effective;
        private readonly bool _selectionAllowed;

        public FixedLegalEntityContext(
            Guid? selected,
            IReadOnlyCollection<Guid>? effective = null,
            bool? selectionAllowed = null)
        {
            SelectedLegalEntityId = selected;
            _effective = effective ?? (selected is { } s ? new[] { s } : Array.Empty<Guid>());
            _selectionAllowed = selectionAllowed ?? selected.HasValue;
        }

        public Guid? SelectedLegalEntityId { get; }

        public Task<bool> IsSelectionAllowedAsync(CancellationToken ct) => Task.FromResult(_selectionAllowed);

        public Task<IReadOnlyCollection<Guid>> GetEffectiveLegalEntityIdsAsync(CancellationToken ct) =>
            Task.FromResult(_effective);
    }

    private sealed class FixedTenantContext : ITenantContext
    {
        public FixedTenantContext(Guid? tenantId) => TenantId = tenantId;

        public Guid? TenantId { get; }
    }

    private sealed class InMemoryCompensationBenefitsReadinessMetadataRepository : ICompensationBenefitsReadinessMetadataRepository
    {
        private readonly Dictionary<Guid, CompensationBenefitsReadinessMetadata> _items;

        public InMemoryCompensationBenefitsReadinessMetadataRepository(params CompensationBenefitsReadinessMetadata[] items) =>
            _items = items.ToDictionary(x => x.Id);

        public Task<IReadOnlyList<CompensationBenefitsReadinessMetadata>> ListAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<CompensationBenefitsReadinessMetadata>>(
                _items.Values.Where(x => x.TenantId == tenantId && !x.IsDeleted && legalEntityIds.Contains(x.LegalEntityId)).OrderBy(x => x.Code).ToList());

        public Task<CompensationBenefitsReadinessMetadata?> GetByIdAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, Guid id, CancellationToken ct) =>
            Task.FromResult(_items.Values.FirstOrDefault(x => x.TenantId == tenantId && x.Id == id && !x.IsDeleted && legalEntityIds.Contains(x.LegalEntityId)));

        public Task<bool> ExistsActiveCodeAsync(Guid tenantId, Guid legalEntityId, string code, Guid? excludingId, CancellationToken ct) =>
            Task.FromResult(_items.Values.Any(x => x.TenantId == tenantId && x.LegalEntityId == legalEntityId && x.Code == code && !x.IsDeleted && x.Id != excludingId));

        public Task CreateAsync(CompensationBenefitsReadinessMetadata metadata, CancellationToken ct)
        {
            _items[metadata.Id] = metadata;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(CompensationBenefitsReadinessMetadata metadata, CancellationToken ct)
        {
            _items[metadata.Id] = metadata;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task LegalEntity_create_stamps_the_selected_legal_entity()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryCompensationBenefitsReadinessMetadataRepository();
        var handler = CreateHandler(repository, tenantId, new FixedLegalEntityContext(Medikal, new[] { Medikal }));

        var created = await handler.Handle(new CreateCompensationBenefitsReadinessCommand(ValidRequest()), CancellationToken.None);
        var stored = RepositoryItems(repository)[created.Data];

        Assert.True(created.IsSuccessful);
        Assert.Equal(Medikal, stored.LegalEntityId);
    }

    [Fact]
    public async Task LegalEntity_create_without_a_permitted_selection_is_forbidden()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryCompensationBenefitsReadinessMetadataRepository();
        var handler = CreateHandler(repository, tenantId, new FixedLegalEntityContext(Teknoloji, selectionAllowed: false));

        var response = await handler.Handle(new CreateCompensationBenefitsReadinessCommand(ValidRequest()), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(403, response.StatusCode);
        Assert.Empty(RepositoryItems(repository));
    }

    [Fact]
    public async Task LegalEntity_list_rolls_up_holding_and_isolates_siblings()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryCompensationBenefitsReadinessMetadataRepository();

        await CreateHandler(repository, tenantId, new FixedLegalEntityContext(Medikal, new[] { Medikal }))
            .Handle(new CreateCompensationBenefitsReadinessCommand(ValidRequest(code: "MED-01")), CancellationToken.None);
        await CreateHandler(repository, tenantId, new FixedLegalEntityContext(Teknoloji, new[] { Teknoloji }))
            .Handle(new CreateCompensationBenefitsReadinessCommand(ValidRequest(code: "TEK-01")), CancellationToken.None);

        var medikalOnly = await ListWith(repository, tenantId, new[] { Medikal });
        var teknolojiOnly = await ListWith(repository, tenantId, new[] { Teknoloji });
        var holdingRollup = await ListWith(repository, tenantId, new[] { Holding, Medikal, Teknoloji });

        Assert.Equal(new[] { "MED-01" }, medikalOnly.Select(x => x.Code).ToArray());
        Assert.Equal(new[] { "TEK-01" }, teknolojiOnly.Select(x => x.Code).ToArray());
        Assert.Equal(new[] { "MED-01", "TEK-01" }, holdingRollup.Select(x => x.Code).OrderBy(x => x).ToArray());
    }

    [Fact]
    public async Task LegalEntity_same_code_is_unique_per_legal_entity_not_per_tenant()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryCompensationBenefitsReadinessMetadataRepository();
        var medikal = new FixedLegalEntityContext(Medikal, new[] { Medikal });
        var teknoloji = new FixedLegalEntityContext(Teknoloji, new[] { Teknoloji });

        var first = await CreateHandler(repository, tenantId, medikal)
            .Handle(new CreateCompensationBenefitsReadinessCommand(ValidRequest(code: "SHARED-01")), CancellationToken.None);
        var duplicateSameEntity = await CreateHandler(repository, tenantId, medikal)
            .Handle(new CreateCompensationBenefitsReadinessCommand(ValidRequest(code: "SHARED-01")), CancellationToken.None);
        var sameCodeOtherEntity = await CreateHandler(repository, tenantId, teknoloji)
            .Handle(new CreateCompensationBenefitsReadinessCommand(ValidRequest(code: "SHARED-01")), CancellationToken.None);

        Assert.True(first.IsSuccessful);
        Assert.Equal(409, duplicateSameEntity.StatusCode);
        Assert.True(sameCodeOtherEntity.IsSuccessful);
    }
}
