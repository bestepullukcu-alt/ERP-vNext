using System.Reflection;
using Diten.HumanCapitalService.Api.Controllers.Hcm;
using Diten.HumanCapitalService.Api.Security;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Application.Features.HrCompliance;
using Diten.HumanCapitalService.Application.Features.HrCompliance.Commands;
using Diten.HumanCapitalService.Application.Features.HrCompliance.Handlers;
using Diten.HumanCapitalService.Application.Features.HrCompliance.Queries;
using Diten.HumanCapitalService.Domain.Entities;
using Diten.HumanCapitalService.Domain.Enums;
using Diten.HumanCapitalService.Domain.Repositories;
using Diten.HumanCapitalService.Persistence.Repositories;
using Xunit;

namespace Diten.HumanCapitalService.Application.Tests;

public sealed class HrComplianceTests
{
    [Fact]
    public async Task Create_list_and_get_metadata_contract()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryHrComplianceReadinessMetadataRepository();
        var create = CreateHandler(repository, tenantId);

        var created = await create.Handle(new CreateHrComplianceReadinessCommand(ValidRequest()), CancellationToken.None);
        var list = await new GetHrComplianceReadinessListHandler(repository, new FixedTenantContext(tenantId), PilotLegalEntityContext())
            .Handle(new GetHrComplianceReadinessListQuery(), CancellationToken.None);
        var get = await new GetHrComplianceReadinessByIdHandler(repository, new FixedTenantContext(tenantId), PilotLegalEntityContext())
            .Handle(new GetHrComplianceReadinessByIdQuery(created.Data), CancellationToken.None);

        Assert.True(created.IsSuccessful);
        Assert.Equal(201, created.StatusCode);
        Assert.True(list.IsSuccessful);
        Assert.Single(list.Data!);
        Assert.True(get.IsSuccessful);
        Assert.Equal("SUCC-001", get.Data!.Code);
        Assert.Equal("HrCompliance readiness", get.Data.DisplayName);
        Assert.Equal(HrComplianceReadinessState.NotRequired, get.Data.ObligationCatalogBoundaryState);
        Assert.Equal(HrComplianceReadinessState.NotRequired, get.Data.ControlMappingBoundaryState);
        Assert.Equal(HrComplianceReadinessState.NotRequired, get.Data.StatutoryReportDefinitionBoundaryState);
        Assert.Equal(HrComplianceReadinessState.NotRequired, get.Data.FilingScheduleBoundaryState);
        Assert.Equal(HrComplianceReadinessState.NotRequired, get.Data.AttestationClosureBoundaryState);
        Assert.Equal(HrComplianceReadinessState.NotRequired, get.Data.AutomatedDecisionBoundaryState);
        Assert.Equal(HrComplianceReadinessState.NotRequired, get.Data.RegulatorySourceDependencyState);
        Assert.Equal(HrComplianceReadinessState.NotRequired, get.Data.HcmDataSourceDependencyState);
        Assert.Equal(HrComplianceReadinessState.NotRequired, get.Data.DocumentDependencyState);
        Assert.Equal(HrComplianceReadinessState.NotRequired, get.Data.NotificationDependencyState);
    }

    [Fact]
    public async Task Cross_tenant_lookup_returns_not_found()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var metadata = Metadata(tenantA);
        var handler = new GetHrComplianceReadinessByIdHandler(
            new InMemoryHrComplianceReadinessMetadataRepository(metadata),
            new FixedTenantContext(tenantB), PilotLegalEntityContext());

        var response = await handler.Handle(new GetHrComplianceReadinessByIdQuery(metadata.Id), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Missing_tenant_context_fails_closed()
    {
        var handler = CreateHandler(new InMemoryHrComplianceReadinessMetadataRepository(), Guid.Empty);

        var response = await handler.Handle(new CreateHrComplianceReadinessCommand(ValidRequest()), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(401, response.StatusCode);
    }

    [Fact]
    public async Task Duplicate_active_code_returns_conflict_per_tenant()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryHrComplianceReadinessMetadataRepository();
        var handler = CreateHandler(repository, tenantId);

        var first = await handler.Handle(new CreateHrComplianceReadinessCommand(ValidRequest(code: "succ-001")), CancellationToken.None);
        var second = await handler.Handle(new CreateHrComplianceReadinessCommand(ValidRequest(code: " SUCC-001 ")), CancellationToken.None);

        Assert.True(first.IsSuccessful);
        Assert.False(second.IsSuccessful);
        Assert.Equal(409, second.StatusCode);
    }

    [Fact]
    public async Task Soft_delete_hides_record_and_sets_deleted_at()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(tenantId);
        var repository = new InMemoryHrComplianceReadinessMetadataRepository(metadata);
        var handler = new DeleteHrComplianceReadinessHandler(repository, new FixedTenantContext(tenantId), PilotLegalEntityContext());

        var response = await handler.Handle(new DeleteHrComplianceReadinessCommand(metadata.Id), CancellationToken.None);
        var hidden = await repository.GetByIdAsync(tenantId, new[] { Holding }, metadata.Id, CancellationToken.None);
        var stored = RepositoryItems(repository)[metadata.Id];

        Assert.True(response.IsSuccessful);
        Assert.Equal(204, response.StatusCode);
        Assert.Null(hidden);
        Assert.True(stored.IsDeleted);
        Assert.NotNull(stored.DeletedAt);
        Assert.Equal(HrComplianceReadinessState.Archived, stored.HrComplianceReadinessState);
    }

    [Fact]
    public async Task Ready_state_fails_closed_when_preconditions_are_deferred()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryHrComplianceReadinessMetadataRepository();
        var handler = CreateHandler(repository, tenantId);

        var response = await handler.Handle(
            new CreateHrComplianceReadinessCommand(ValidRequest(readinessState: HrComplianceReadinessState.Ready)),
            CancellationToken.None);
        var stored = RepositoryItems(repository)[response.Data];

        Assert.True(response.IsSuccessful);
        Assert.Equal(HrComplianceReadinessState.Deferred, stored.HrComplianceReadinessState);
        Assert.Contains("deferred", stored.DeferredReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Evaluation_promotes_ready_only_when_metadata_preconditions_are_satisfied()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(
            tenantId,
            consentPreconditionState: HrComplianceReadinessState.Ready,
            dataMinimizationState: HrComplianceReadinessState.Ready,
            retentionPolicyState: HrComplianceReadinessState.Ready,
            evidencePolicyState: HrComplianceReadinessState.Ready);
        var repository = new InMemoryHrComplianceReadinessMetadataRepository(metadata);
        var handler = new EvaluateHrComplianceReadinessHandler(repository, new FixedTenantContext(tenantId), PilotLegalEntityContext());

        var response = await handler.Handle(new EvaluateHrComplianceReadinessCommand(metadata.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(HrComplianceReadinessState.Ready, response.Data!.HrComplianceReadinessState);
        Assert.NotNull(response.Data.LastEvaluatedAt);
    }

    [Fact]
    public async Task Non_activating_evaluation_preserves_deferred_metadata()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(tenantId, consentPreconditionState: HrComplianceReadinessState.Deferred);
        var repository = new InMemoryHrComplianceReadinessMetadataRepository(metadata);
        var handler = new EvaluateHrComplianceReadinessHandler(repository, new FixedTenantContext(tenantId), PilotLegalEntityContext());

        var response = await handler.Handle(new EvaluateHrComplianceReadinessCommand(metadata.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(HrComplianceReadinessState.Deferred, response.Data!.HrComplianceReadinessState);
        Assert.Contains("preconditions", response.Data.DeferredReason);
    }

    [Fact]
    public async Task Pool_highpotential_nomination_assessment_review_and_decision_boundaries_cannot_be_ready()
    {
        var tenantId = Guid.NewGuid();
        var handler = CreateHandler(new InMemoryHrComplianceReadinessMetadataRepository(), tenantId);

        var requests = new[]
        {
            ValidRequest(obligationCatalogBoundaryState: HrComplianceReadinessState.Ready),
            ValidRequest(controlMappingBoundaryState: HrComplianceReadinessState.Ready),
            ValidRequest(statutoryReportDefinitionBoundaryState: HrComplianceReadinessState.Ready),
            ValidRequest(filingScheduleBoundaryState: HrComplianceReadinessState.Ready),
            ValidRequest(attestationClosureBoundaryState: HrComplianceReadinessState.Ready),
            ValidRequest(automatedDecisionBoundaryState: HrComplianceReadinessState.Ready)
        };

        foreach (var request in requests)
        {
            var response = await handler.Handle(new CreateHrComplianceReadinessCommand(request), CancellationToken.None);

            Assert.False(response.IsSuccessful);
            Assert.Equal(400, response.StatusCode);
        }
    }

    [Fact]
    public async Task Audit_metadata_uses_audit_permission_surface()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(tenantId);
        var repository = new InMemoryHrComplianceReadinessMetadataRepository(metadata);
        var response = await new GetHrComplianceAuditMetadataHandler(repository, new FixedTenantContext(tenantId), PilotLegalEntityContext())
            .Handle(new GetHrComplianceAuditMetadataQuery(metadata.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(metadata.Code, response.Data!.Code);
        Assert.Equal(HrComplianceGuard.AuditReadPermission, PermissionFor(nameof(HrComplianceController.GetAuditMetadata)));
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
        var handler = CreateHandler(new InMemoryHrComplianceReadinessMetadataRepository(), tenantId);

        // Marker injected as a STANDALONE token (whitespace-delimited) so the word-boundary
        // (\b) matcher rejects a genuine forbidden token.
        var response = await handler.Handle(
            new CreateHrComplianceReadinessCommand(ValidRequest(sourceContractVersion: $"v1 {marker}")),
            CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
    }

    [Theory]
    [InlineData("talent taxonomy")]
    [InlineData("hr-compliance pipeline")]
    [InlineData("nomination scorecard")]
    public async Task Word_boundary_matcher_accepts_legitimate_values_that_merely_contain_marker_substrings(string legitimateValue)
    {
        // Regression guard for the substring false-positive that previously broke LearningTraining:
        // "taxonomy" contains "tax", "scorecard" contains "score" — with the \b matcher these are
        // legitimate standalone tokens and MUST be accepted (create succeeds).
        var tenantId = Guid.NewGuid();
        var handler = CreateHandler(new InMemoryHrComplianceReadinessMetadataRepository(), tenantId);

        var response = await handler.Handle(
            new CreateHrComplianceReadinessCommand(ValidRequest(displayName: legitimateValue)),
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
            typeof(HrComplianceReadinessCreateRequest),
            typeof(HrComplianceReadinessDto),
            typeof(HrComplianceReadinessMetadata)
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
        Assert.Null(typeof(HrComplianceReadinessCreateRequest).GetProperty("TenantId"));
    }

    [Fact]
    public void Controller_permissions_match_pack()
    {
        Assert.Equal(HrComplianceGuard.ReadPermission, PermissionFor(nameof(HrComplianceController.GetAll)));
        Assert.Equal(HrComplianceGuard.ReadPermission, PermissionFor(nameof(HrComplianceController.GetById)));
        Assert.Equal(HrComplianceGuard.ManagePermission, PermissionFor(nameof(HrComplianceController.Create)));
        Assert.Equal(HrComplianceGuard.EvaluatePermission, PermissionFor(nameof(HrComplianceController.Evaluate)));
        Assert.Equal(HrComplianceGuard.ManagePermission, PermissionFor(nameof(HrComplianceController.Delete)));
        Assert.Equal(HrComplianceGuard.AuditReadPermission, PermissionFor(nameof(HrComplianceController.GetAuditMetadata)));
    }

    [Fact]
    public void Mongo_repository_contract_names_are_stable()
    {
        Assert.Equal("hcm_hr_compliance_readiness", MongoHrComplianceReadinessMetadataRepository.CollectionName);
        Assert.Equal("ux_hcm_hr_compliance_tenant_code_active", MongoHrComplianceReadinessMetadataRepository.ActiveCodeUniqueIndexName);
        Assert.Equal("ix_hcm_hr_compliance_tenant_state", MongoHrComplianceReadinessMetadataRepository.TenantStateIndexName);
    }

    [Fact]
    public void Runtime_literal_regression_guard_has_no_reserved_identity_literals()
    {
        var reserved = string.Join("-", "CAND", "CAP", "0040");
        var legacy = string.Join("-", "MOD", "0318");
        var runtimeStrings = new[]
        {
            HrComplianceGuard.OwnerKey,
            HrComplianceGuard.ReadPermission,
            HrComplianceGuard.ManagePermission,
            HrComplianceGuard.EvaluatePermission,
            HrComplianceGuard.AuditReadPermission,
            MongoHrComplianceReadinessMetadataRepository.CollectionName
        };

        Assert.DoesNotContain(runtimeStrings, value => value.Contains(reserved, StringComparison.Ordinal));
        Assert.DoesNotContain(runtimeStrings, value => value.Contains(legacy, StringComparison.Ordinal));
    }

    private static CreateHrComplianceReadinessHandler CreateHandler(
        IHrComplianceReadinessMetadataRepository repository,
        Guid tenantId) =>
        new(repository, new FixedTenantContext(tenantId == Guid.Empty ? null : tenantId), PilotLegalEntityContext());

    private static CreateHrComplianceReadinessHandler CreateHandler(
        IHrComplianceReadinessMetadataRepository repository,
        Guid tenantId,
        ILegalEntityContext legalEntityContext) =>
        new(repository, new FixedTenantContext(tenantId), legalEntityContext);

    private static HrComplianceReadinessCreateRequest ValidRequest(
        string code = "SUCC-001",
        string displayName = "HrCompliance readiness",
        HrComplianceReadinessState readinessState = HrComplianceReadinessState.Draft,
        string sourceContractVersion = "v1",
        HrComplianceReadinessState obligationCatalogBoundaryState = HrComplianceReadinessState.NotRequired,
        HrComplianceReadinessState controlMappingBoundaryState = HrComplianceReadinessState.NotRequired,
        HrComplianceReadinessState statutoryReportDefinitionBoundaryState = HrComplianceReadinessState.NotRequired,
        HrComplianceReadinessState filingScheduleBoundaryState = HrComplianceReadinessState.NotRequired,
        HrComplianceReadinessState attestationClosureBoundaryState = HrComplianceReadinessState.NotRequired,
        HrComplianceReadinessState automatedDecisionBoundaryState = HrComplianceReadinessState.NotRequired) =>
        new()
        {
            Code = code,
            DisplayName = displayName,
            HrComplianceReadinessState = readinessState,
            ObligationCatalogBoundaryState = obligationCatalogBoundaryState,
            ControlMappingBoundaryState = controlMappingBoundaryState,
            StatutoryReportDefinitionBoundaryState = statutoryReportDefinitionBoundaryState,
            FilingScheduleBoundaryState = filingScheduleBoundaryState,
            AttestationClosureBoundaryState = attestationClosureBoundaryState,
            AutomatedDecisionBoundaryState = automatedDecisionBoundaryState,
            RegulatorySourceDependencyState = HrComplianceReadinessState.NotRequired,
            HcmDataSourceDependencyState = HrComplianceReadinessState.NotRequired,
            DocumentDependencyState = HrComplianceReadinessState.NotRequired,
            NotificationDependencyState = HrComplianceReadinessState.NotRequired,
            ConsentPreconditionState = HrComplianceReadinessState.Deferred,
            DataMinimizationState = HrComplianceReadinessState.Deferred,
            RetentionPolicyState = HrComplianceReadinessState.Deferred,
            EvidencePolicyState = HrComplianceReadinessState.Deferred,
            DependencyStates = new Dictionary<string, HrComplianceReadinessState>
            {
                ["regulatorySource"] = HrComplianceReadinessState.Ready,
                ["hcmDataSource"] = HrComplianceReadinessState.Ready,
                ["obligationCatalog"] = HrComplianceReadinessState.Ready
            },
            SourceContractVersion = sourceContractVersion,
            HrComplianceReadinessVersion = 1
        };

    private static HrComplianceReadinessMetadata Metadata(
        Guid tenantId,
        string code = "SUCC-001",
        HrComplianceReadinessState consentPreconditionState = HrComplianceReadinessState.Deferred,
        HrComplianceReadinessState dataMinimizationState = HrComplianceReadinessState.Deferred,
        HrComplianceReadinessState retentionPolicyState = HrComplianceReadinessState.Deferred,
        HrComplianceReadinessState evidencePolicyState = HrComplianceReadinessState.Deferred) =>
        new()
        {
            TenantId = tenantId,
            LegalEntityId = Holding,
            Code = code,
            DisplayName = "HrCompliance readiness",
            HrComplianceReadinessState = HrComplianceReadinessState.Draft,
            ObligationCatalogBoundaryState = HrComplianceReadinessState.NotRequired,
            ControlMappingBoundaryState = HrComplianceReadinessState.NotRequired,
            StatutoryReportDefinitionBoundaryState = HrComplianceReadinessState.NotRequired,
            FilingScheduleBoundaryState = HrComplianceReadinessState.NotRequired,
            AttestationClosureBoundaryState = HrComplianceReadinessState.NotRequired,
            AutomatedDecisionBoundaryState = HrComplianceReadinessState.NotRequired,
            RegulatorySourceDependencyState = HrComplianceReadinessState.NotRequired,
            HcmDataSourceDependencyState = HrComplianceReadinessState.NotRequired,
            DocumentDependencyState = HrComplianceReadinessState.NotRequired,
            NotificationDependencyState = HrComplianceReadinessState.NotRequired,
            ConsentPreconditionState = consentPreconditionState,
            DataMinimizationState = dataMinimizationState,
            RetentionPolicyState = retentionPolicyState,
            EvidencePolicyState = evidencePolicyState,
            DependencyStates = new Dictionary<string, HrComplianceReadinessState>
            {
                ["regulatorySource"] = HrComplianceReadinessState.Ready
            },
            SourceContractVersion = "v1",
            HrComplianceReadinessVersion = 1
        };

    private static IReadOnlyDictionary<Guid, HrComplianceReadinessMetadata> RepositoryItems(
        InMemoryHrComplianceReadinessMetadataRepository repository)
    {
        var field = typeof(InMemoryHrComplianceReadinessMetadataRepository)
            .GetField("_items", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (IReadOnlyDictionary<Guid, HrComplianceReadinessMetadata>)field.GetValue(repository)!;
    }

    private static string PermissionFor(string methodName)
    {
        var method = typeof(HrComplianceController).GetMethod(methodName)!;
        var attribute = method.GetCustomAttribute<HasPermissionAttribute>()!;
        var field = typeof(HasPermissionAttribute).GetField("_permission", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (string)field.GetValue(attribute)!;
    }

    private static readonly Guid Holding = Guid.Parse("1e9a1000-0000-0000-0000-000000000001");
    private static readonly Guid Medikal = Guid.Parse("1e9a1000-0000-0000-0000-000000000002");
    private static readonly Guid Teknoloji = Guid.Parse("1e9a1000-0000-0000-0000-000000000003");

    private static FixedLegalEntityContext PilotLegalEntityContext() =>
        new(Holding, new[] { Holding, Medikal, Teknoloji });

    private static async Task<IReadOnlyList<HrComplianceReadinessListItemDto>> ListWith(
        IHrComplianceReadinessMetadataRepository repository,
        Guid tenantId,
        IReadOnlyCollection<Guid> effective)
    {
        var handler = new GetHrComplianceReadinessListHandler(
            repository,
            new FixedTenantContext(tenantId),
            new FixedLegalEntityContext(effective.First(), effective));
        var response = await handler.Handle(new GetHrComplianceReadinessListQuery(), CancellationToken.None);
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

    private sealed class InMemoryHrComplianceReadinessMetadataRepository : IHrComplianceReadinessMetadataRepository
    {
        private readonly Dictionary<Guid, HrComplianceReadinessMetadata> _items;

        public InMemoryHrComplianceReadinessMetadataRepository(params HrComplianceReadinessMetadata[] items) =>
            _items = items.ToDictionary(x => x.Id);

        public Task<IReadOnlyList<HrComplianceReadinessMetadata>> ListAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<HrComplianceReadinessMetadata>>(
                _items.Values.Where(x => x.TenantId == tenantId && !x.IsDeleted && legalEntityIds.Contains(x.LegalEntityId)).OrderBy(x => x.Code).ToList());

        public Task<HrComplianceReadinessMetadata?> GetByIdAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, Guid id, CancellationToken ct) =>
            Task.FromResult(_items.Values.FirstOrDefault(x => x.TenantId == tenantId && x.Id == id && !x.IsDeleted && legalEntityIds.Contains(x.LegalEntityId)));

        public Task<bool> ExistsActiveCodeAsync(Guid tenantId, Guid legalEntityId, string code, Guid? excludingId, CancellationToken ct) =>
            Task.FromResult(_items.Values.Any(x => x.TenantId == tenantId && x.LegalEntityId == legalEntityId && x.Code == code && !x.IsDeleted && x.Id != excludingId));

        public Task CreateAsync(HrComplianceReadinessMetadata metadata, CancellationToken ct)
        {
            _items[metadata.Id] = metadata;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(HrComplianceReadinessMetadata metadata, CancellationToken ct)
        {
            _items[metadata.Id] = metadata;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task LegalEntity_create_stamps_the_selected_legal_entity()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryHrComplianceReadinessMetadataRepository();
        var handler = CreateHandler(repository, tenantId, new FixedLegalEntityContext(Medikal, new[] { Medikal }));

        var created = await handler.Handle(new CreateHrComplianceReadinessCommand(ValidRequest()), CancellationToken.None);
        var stored = RepositoryItems(repository)[created.Data];

        Assert.True(created.IsSuccessful);
        Assert.Equal(Medikal, stored.LegalEntityId);
    }

    [Fact]
    public async Task LegalEntity_create_without_a_permitted_selection_is_forbidden()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryHrComplianceReadinessMetadataRepository();
        var handler = CreateHandler(repository, tenantId, new FixedLegalEntityContext(Teknoloji, selectionAllowed: false));

        var response = await handler.Handle(new CreateHrComplianceReadinessCommand(ValidRequest()), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(403, response.StatusCode);
        Assert.Empty(RepositoryItems(repository));
    }

    [Fact]
    public async Task LegalEntity_list_rolls_up_holding_and_isolates_siblings()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryHrComplianceReadinessMetadataRepository();

        await CreateHandler(repository, tenantId, new FixedLegalEntityContext(Medikal, new[] { Medikal }))
            .Handle(new CreateHrComplianceReadinessCommand(ValidRequest(code: "MED-01")), CancellationToken.None);
        await CreateHandler(repository, tenantId, new FixedLegalEntityContext(Teknoloji, new[] { Teknoloji }))
            .Handle(new CreateHrComplianceReadinessCommand(ValidRequest(code: "TEK-01")), CancellationToken.None);

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
        var repository = new InMemoryHrComplianceReadinessMetadataRepository();
        var medikal = new FixedLegalEntityContext(Medikal, new[] { Medikal });
        var teknoloji = new FixedLegalEntityContext(Teknoloji, new[] { Teknoloji });

        var first = await CreateHandler(repository, tenantId, medikal)
            .Handle(new CreateHrComplianceReadinessCommand(ValidRequest(code: "SHARED-01")), CancellationToken.None);
        var duplicateSameEntity = await CreateHandler(repository, tenantId, medikal)
            .Handle(new CreateHrComplianceReadinessCommand(ValidRequest(code: "SHARED-01")), CancellationToken.None);
        var sameCodeOtherEntity = await CreateHandler(repository, tenantId, teknoloji)
            .Handle(new CreateHrComplianceReadinessCommand(ValidRequest(code: "SHARED-01")), CancellationToken.None);

        Assert.True(first.IsSuccessful);
        Assert.Equal(409, duplicateSameEntity.StatusCode);
        Assert.True(sameCodeOtherEntity.IsSuccessful);
    }
}
