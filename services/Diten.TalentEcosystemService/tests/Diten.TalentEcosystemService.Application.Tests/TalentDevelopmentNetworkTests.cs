using System.Reflection;
using Diten.TalentEcosystemService.Api.Controllers.Tep;
using Diten.TalentEcosystemService.Api.Security;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.TalentDevelopmentNetwork;
using Diten.TalentEcosystemService.Application.Features.TalentDevelopmentNetwork.Commands;
using Diten.TalentEcosystemService.Application.Features.TalentDevelopmentNetwork.Handlers;
using Diten.TalentEcosystemService.Application.Features.TalentDevelopmentNetwork.Queries;
using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Enums;
using Diten.TalentEcosystemService.Domain.Repositories;
using Diten.TalentEcosystemService.Persistence.Repositories;
using Xunit;

namespace Diten.TalentEcosystemService.Application.Tests;

public sealed class TalentDevelopmentNetworkTests
{
    [Fact]
    public async Task Create_list_and_get_metadata_contract()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryTalentDevelopmentNetworkReadinessMetadataRepository();
        var create = CreateHandler(repository, tenantId);

        var created = await create.Handle(new CreateTalentDevelopmentNetworkReadinessCommand(ValidRequest()), CancellationToken.None);
        var list = await new GetTalentDevelopmentNetworkReadinessListHandler(repository, new FixedTenantContext(tenantId), PilotLegalEntityContext())
            .Handle(new GetTalentDevelopmentNetworkReadinessListQuery(), CancellationToken.None);
        var get = await new GetTalentDevelopmentNetworkReadinessByIdHandler(repository, new FixedTenantContext(tenantId), PilotLegalEntityContext())
            .Handle(new GetTalentDevelopmentNetworkReadinessByIdQuery(created.Data), CancellationToken.None);

        Assert.True(created.IsSuccessful);
        Assert.Equal(201, created.StatusCode);
        Assert.True(list.IsSuccessful);
        Assert.Single(list.Data!);
        Assert.True(get.IsSuccessful);
        Assert.Equal("SUCC-001", get.Data!.Code);
        Assert.Equal("TalentDevelopmentNetwork readiness", get.Data.DisplayName);
        Assert.Equal(TalentDevelopmentNetworkReadinessState.NotRequired, get.Data.PathwayCatalogBoundaryState);
        Assert.Equal(TalentDevelopmentNetworkReadinessState.NotRequired, get.Data.MentorshipLinkIntakeBoundaryState);
        Assert.Equal(TalentDevelopmentNetworkReadinessState.NotRequired, get.Data.ProgressionScopeBoundaryState);
        Assert.Equal(TalentDevelopmentNetworkReadinessState.NotRequired, get.Data.VisibilityControlBoundaryState);
        Assert.Equal(TalentDevelopmentNetworkReadinessState.NotRequired, get.Data.NetworkReviewBoundaryState);
        Assert.Equal(TalentDevelopmentNetworkReadinessState.NotRequired, get.Data.AutomatedDecisionBoundaryState);
        Assert.Equal(TalentDevelopmentNetworkReadinessState.NotRequired, get.Data.TalentDataSourceDependencyState);
        Assert.Equal(TalentDevelopmentNetworkReadinessState.NotRequired, get.Data.ConsentPolicyDependencyState);
        Assert.Equal(TalentDevelopmentNetworkReadinessState.NotRequired, get.Data.SkillPassportSourceDependencyState);
        Assert.Equal(TalentDevelopmentNetworkReadinessState.NotRequired, get.Data.NotificationDependencyState);
    }

    [Fact]
    public async Task Cross_tenant_lookup_returns_not_found()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var metadata = Metadata(tenantA);
        var handler = new GetTalentDevelopmentNetworkReadinessByIdHandler(
            new InMemoryTalentDevelopmentNetworkReadinessMetadataRepository(metadata),
            new FixedTenantContext(tenantB), PilotLegalEntityContext());

        var response = await handler.Handle(new GetTalentDevelopmentNetworkReadinessByIdQuery(metadata.Id), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Missing_tenant_context_fails_closed()
    {
        var handler = CreateHandler(new InMemoryTalentDevelopmentNetworkReadinessMetadataRepository(), Guid.Empty);

        var response = await handler.Handle(new CreateTalentDevelopmentNetworkReadinessCommand(ValidRequest()), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(401, response.StatusCode);
    }

    [Fact]
    public async Task Duplicate_active_code_returns_conflict_per_tenant()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryTalentDevelopmentNetworkReadinessMetadataRepository();
        var handler = CreateHandler(repository, tenantId);

        var first = await handler.Handle(new CreateTalentDevelopmentNetworkReadinessCommand(ValidRequest(code: "succ-001")), CancellationToken.None);
        var second = await handler.Handle(new CreateTalentDevelopmentNetworkReadinessCommand(ValidRequest(code: " SUCC-001 ")), CancellationToken.None);

        Assert.True(first.IsSuccessful);
        Assert.False(second.IsSuccessful);
        Assert.Equal(409, second.StatusCode);
    }

    [Fact]
    public async Task Soft_delete_hides_record_and_sets_deleted_at()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(tenantId);
        var repository = new InMemoryTalentDevelopmentNetworkReadinessMetadataRepository(metadata);
        var handler = new DeleteTalentDevelopmentNetworkReadinessHandler(repository, new FixedTenantContext(tenantId), PilotLegalEntityContext());

        var response = await handler.Handle(new DeleteTalentDevelopmentNetworkReadinessCommand(metadata.Id), CancellationToken.None);
        var hidden = await repository.GetByIdAsync(tenantId, new[] { Holding }, metadata.Id, CancellationToken.None);
        var stored = RepositoryItems(repository)[metadata.Id];

        Assert.True(response.IsSuccessful);
        Assert.Equal(204, response.StatusCode);
        Assert.Null(hidden);
        Assert.True(stored.IsDeleted);
        Assert.NotNull(stored.DeletedAt);
        Assert.Equal(TalentDevelopmentNetworkReadinessState.Archived, stored.TalentDevelopmentNetworkReadinessState);
    }

    [Fact]
    public async Task Ready_state_fails_closed_when_preconditions_are_deferred()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryTalentDevelopmentNetworkReadinessMetadataRepository();
        var handler = CreateHandler(repository, tenantId);

        var response = await handler.Handle(
            new CreateTalentDevelopmentNetworkReadinessCommand(ValidRequest(readinessState: TalentDevelopmentNetworkReadinessState.Ready)),
            CancellationToken.None);
        var stored = RepositoryItems(repository)[response.Data];

        Assert.True(response.IsSuccessful);
        Assert.Equal(TalentDevelopmentNetworkReadinessState.Deferred, stored.TalentDevelopmentNetworkReadinessState);
        Assert.Contains("deferred", stored.DeferredReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Evaluation_promotes_ready_only_when_metadata_preconditions_are_satisfied()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(
            tenantId,
            consentPreconditionState: TalentDevelopmentNetworkReadinessState.Ready,
            dataMinimizationState: TalentDevelopmentNetworkReadinessState.Ready,
            retentionPolicyState: TalentDevelopmentNetworkReadinessState.Ready,
            progressionPolicyState: TalentDevelopmentNetworkReadinessState.Ready);
        var repository = new InMemoryTalentDevelopmentNetworkReadinessMetadataRepository(metadata);
        var handler = new EvaluateTalentDevelopmentNetworkReadinessHandler(repository, new FixedTenantContext(tenantId), PilotLegalEntityContext());

        var response = await handler.Handle(new EvaluateTalentDevelopmentNetworkReadinessCommand(metadata.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(TalentDevelopmentNetworkReadinessState.Ready, response.Data!.TalentDevelopmentNetworkReadinessState);
        Assert.NotNull(response.Data.LastEvaluatedAt);
    }

    [Fact]
    public async Task Non_activating_evaluation_preserves_deferred_metadata()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(tenantId, consentPreconditionState: TalentDevelopmentNetworkReadinessState.Deferred);
        var repository = new InMemoryTalentDevelopmentNetworkReadinessMetadataRepository(metadata);
        var handler = new EvaluateTalentDevelopmentNetworkReadinessHandler(repository, new FixedTenantContext(tenantId), PilotLegalEntityContext());

        var response = await handler.Handle(new EvaluateTalentDevelopmentNetworkReadinessCommand(metadata.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(TalentDevelopmentNetworkReadinessState.Deferred, response.Data!.TalentDevelopmentNetworkReadinessState);
        Assert.Contains("preconditions", response.Data.DeferredReason);
    }

    [Fact]
    public async Task Pool_highpotential_nomination_assessment_review_and_decision_boundaries_cannot_be_ready()
    {
        var tenantId = Guid.NewGuid();
        var handler = CreateHandler(new InMemoryTalentDevelopmentNetworkReadinessMetadataRepository(), tenantId);

        var requests = new[]
        {
            ValidRequest(pathwayCatalogBoundaryState: TalentDevelopmentNetworkReadinessState.Ready),
            ValidRequest(mentorshipLinkIntakeBoundaryState: TalentDevelopmentNetworkReadinessState.Ready),
            ValidRequest(progressionScopeBoundaryState: TalentDevelopmentNetworkReadinessState.Ready),
            ValidRequest(visibilityControlBoundaryState: TalentDevelopmentNetworkReadinessState.Ready),
            ValidRequest(networkReviewBoundaryState: TalentDevelopmentNetworkReadinessState.Ready),
            ValidRequest(automatedDecisionBoundaryState: TalentDevelopmentNetworkReadinessState.Ready)
        };

        foreach (var request in requests)
        {
            var response = await handler.Handle(new CreateTalentDevelopmentNetworkReadinessCommand(request), CancellationToken.None);

            Assert.False(response.IsSuccessful);
            Assert.Equal(400, response.StatusCode);
        }
    }

    [Fact]
    public async Task Audit_metadata_uses_audit_permission_surface()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(tenantId);
        var repository = new InMemoryTalentDevelopmentNetworkReadinessMetadataRepository(metadata);
        var response = await new GetTalentDevelopmentNetworkAuditMetadataHandler(repository, new FixedTenantContext(tenantId), PilotLegalEntityContext())
            .Handle(new GetTalentDevelopmentNetworkAuditMetadataQuery(metadata.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(metadata.Code, response.Data!.Code);
        Assert.Equal(TalentDevelopmentNetworkGuard.AuditReadPermission, PermissionFor(nameof(TalentDevelopmentNetworkController.GetAuditMetadata)));
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
        var handler = CreateHandler(new InMemoryTalentDevelopmentNetworkReadinessMetadataRepository(), tenantId);

        // Marker injected as a STANDALONE token (whitespace-delimited) so the word-boundary
        // (\b) matcher rejects a genuine forbidden token.
        var response = await handler.Handle(
            new CreateTalentDevelopmentNetworkReadinessCommand(ValidRequest(sourceContractVersion: $"v1 {marker}")),
            CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
    }

    [Theory]
    [InlineData("talent taxonomy")]
    [InlineData("talent-development-network pipeline")]
    [InlineData("nomination scorecard")]
    public async Task Word_boundary_matcher_accepts_legitimate_values_that_merely_contain_marker_substrings(string legitimateValue)
    {
        // Regression guard for the substring false-positive that previously broke LearningTraining:
        // "taxonomy" contains "tax", "scorecard" contains "score" — with the \b matcher these are
        // legitimate standalone tokens and MUST be accepted (create succeeds).
        var tenantId = Guid.NewGuid();
        var handler = CreateHandler(new InMemoryTalentDevelopmentNetworkReadinessMetadataRepository(), tenantId);

        var response = await handler.Handle(
            new CreateTalentDevelopmentNetworkReadinessCommand(ValidRequest(displayName: legitimateValue)),
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
            typeof(TalentDevelopmentNetworkReadinessCreateRequest),
            typeof(TalentDevelopmentNetworkReadinessDto),
            typeof(TalentDevelopmentNetworkReadinessMetadata)
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
        Assert.Null(typeof(TalentDevelopmentNetworkReadinessCreateRequest).GetProperty("TenantId"));
    }

    [Fact]
    public void Controller_permissions_match_pack()
    {
        Assert.Equal(TalentDevelopmentNetworkGuard.ReadPermission, PermissionFor(nameof(TalentDevelopmentNetworkController.GetAll)));
        Assert.Equal(TalentDevelopmentNetworkGuard.ReadPermission, PermissionFor(nameof(TalentDevelopmentNetworkController.GetById)));
        Assert.Equal(TalentDevelopmentNetworkGuard.ManagePermission, PermissionFor(nameof(TalentDevelopmentNetworkController.Create)));
        Assert.Equal(TalentDevelopmentNetworkGuard.EvaluatePermission, PermissionFor(nameof(TalentDevelopmentNetworkController.Evaluate)));
        Assert.Equal(TalentDevelopmentNetworkGuard.ManagePermission, PermissionFor(nameof(TalentDevelopmentNetworkController.Delete)));
        Assert.Equal(TalentDevelopmentNetworkGuard.AuditReadPermission, PermissionFor(nameof(TalentDevelopmentNetworkController.GetAuditMetadata)));
    }

    [Fact]
    public void Mongo_repository_contract_names_are_stable()
    {
        Assert.Equal("tep_talent_development_network_readiness", MongoTalentDevelopmentNetworkReadinessMetadataRepository.CollectionName);
        Assert.Equal("ux_tep_talent_development_network_tenant_code_active", MongoTalentDevelopmentNetworkReadinessMetadataRepository.ActiveCodeUniqueIndexName);
        Assert.Equal("ix_tep_talent_development_network_tenant_state", MongoTalentDevelopmentNetworkReadinessMetadataRepository.TenantStateIndexName);
    }

    [Fact]
    public void Runtime_literal_regression_guard_has_no_reserved_identity_literals()
    {
        var reserved = string.Join("-", "CAND", "CAP", "0049");
        var legacy = string.Join("-", "MOD", "0340");
        var runtimeStrings = new[]
        {
            TalentDevelopmentNetworkGuard.OwnerKey,
            TalentDevelopmentNetworkGuard.ReadPermission,
            TalentDevelopmentNetworkGuard.ManagePermission,
            TalentDevelopmentNetworkGuard.EvaluatePermission,
            TalentDevelopmentNetworkGuard.AuditReadPermission,
            MongoTalentDevelopmentNetworkReadinessMetadataRepository.CollectionName
        };

        Assert.DoesNotContain(runtimeStrings, value => value.Contains(reserved, StringComparison.Ordinal));
        Assert.DoesNotContain(runtimeStrings, value => value.Contains(legacy, StringComparison.Ordinal));
    }

    private static CreateTalentDevelopmentNetworkReadinessHandler CreateHandler(
        ITalentDevelopmentNetworkReadinessMetadataRepository repository,
        Guid tenantId) =>
        new(repository, new FixedTenantContext(tenantId == Guid.Empty ? null : tenantId), PilotLegalEntityContext());

    private static CreateTalentDevelopmentNetworkReadinessHandler CreateHandler(
        ITalentDevelopmentNetworkReadinessMetadataRepository repository,
        Guid tenantId,
        ILegalEntityContext legalEntityContext) =>
        new(repository, new FixedTenantContext(tenantId), legalEntityContext);

    private static TalentDevelopmentNetworkReadinessCreateRequest ValidRequest(
        string code = "SUCC-001",
        string displayName = "TalentDevelopmentNetwork readiness",
        TalentDevelopmentNetworkReadinessState readinessState = TalentDevelopmentNetworkReadinessState.Draft,
        string sourceContractVersion = "v1",
        TalentDevelopmentNetworkReadinessState pathwayCatalogBoundaryState = TalentDevelopmentNetworkReadinessState.NotRequired,
        TalentDevelopmentNetworkReadinessState mentorshipLinkIntakeBoundaryState = TalentDevelopmentNetworkReadinessState.NotRequired,
        TalentDevelopmentNetworkReadinessState progressionScopeBoundaryState = TalentDevelopmentNetworkReadinessState.NotRequired,
        TalentDevelopmentNetworkReadinessState visibilityControlBoundaryState = TalentDevelopmentNetworkReadinessState.NotRequired,
        TalentDevelopmentNetworkReadinessState networkReviewBoundaryState = TalentDevelopmentNetworkReadinessState.NotRequired,
        TalentDevelopmentNetworkReadinessState automatedDecisionBoundaryState = TalentDevelopmentNetworkReadinessState.NotRequired) =>
        new()
        {
            Code = code,
            DisplayName = displayName,
            TalentDevelopmentNetworkReadinessState = readinessState,
            PathwayCatalogBoundaryState = pathwayCatalogBoundaryState,
            MentorshipLinkIntakeBoundaryState = mentorshipLinkIntakeBoundaryState,
            ProgressionScopeBoundaryState = progressionScopeBoundaryState,
            VisibilityControlBoundaryState = visibilityControlBoundaryState,
            NetworkReviewBoundaryState = networkReviewBoundaryState,
            AutomatedDecisionBoundaryState = automatedDecisionBoundaryState,
            TalentDataSourceDependencyState = TalentDevelopmentNetworkReadinessState.NotRequired,
            ConsentPolicyDependencyState = TalentDevelopmentNetworkReadinessState.NotRequired,
            SkillPassportSourceDependencyState = TalentDevelopmentNetworkReadinessState.NotRequired,
            NotificationDependencyState = TalentDevelopmentNetworkReadinessState.NotRequired,
            ConsentPreconditionState = TalentDevelopmentNetworkReadinessState.Deferred,
            DataMinimizationState = TalentDevelopmentNetworkReadinessState.Deferred,
            RetentionPolicyState = TalentDevelopmentNetworkReadinessState.Deferred,
            ProgressionPolicyState = TalentDevelopmentNetworkReadinessState.Deferred,
            DependencyStates = new Dictionary<string, TalentDevelopmentNetworkReadinessState>
            {
                ["talentDataSource"] = TalentDevelopmentNetworkReadinessState.Ready,
                ["consentPolicy"] = TalentDevelopmentNetworkReadinessState.Ready,
                ["pathwayCatalog"] = TalentDevelopmentNetworkReadinessState.Ready
            },
            SourceContractVersion = sourceContractVersion,
            TalentDevelopmentNetworkReadinessVersion = 1
        };

    private static TalentDevelopmentNetworkReadinessMetadata Metadata(
        Guid tenantId,
        string code = "SUCC-001",
        TalentDevelopmentNetworkReadinessState consentPreconditionState = TalentDevelopmentNetworkReadinessState.Deferred,
        TalentDevelopmentNetworkReadinessState dataMinimizationState = TalentDevelopmentNetworkReadinessState.Deferred,
        TalentDevelopmentNetworkReadinessState retentionPolicyState = TalentDevelopmentNetworkReadinessState.Deferred,
        TalentDevelopmentNetworkReadinessState progressionPolicyState = TalentDevelopmentNetworkReadinessState.Deferred) =>
        new()
        {
            TenantId = tenantId,
            LegalEntityId = Holding,
            Code = code,
            DisplayName = "TalentDevelopmentNetwork readiness",
            TalentDevelopmentNetworkReadinessState = TalentDevelopmentNetworkReadinessState.Draft,
            PathwayCatalogBoundaryState = TalentDevelopmentNetworkReadinessState.NotRequired,
            MentorshipLinkIntakeBoundaryState = TalentDevelopmentNetworkReadinessState.NotRequired,
            ProgressionScopeBoundaryState = TalentDevelopmentNetworkReadinessState.NotRequired,
            VisibilityControlBoundaryState = TalentDevelopmentNetworkReadinessState.NotRequired,
            NetworkReviewBoundaryState = TalentDevelopmentNetworkReadinessState.NotRequired,
            AutomatedDecisionBoundaryState = TalentDevelopmentNetworkReadinessState.NotRequired,
            TalentDataSourceDependencyState = TalentDevelopmentNetworkReadinessState.NotRequired,
            ConsentPolicyDependencyState = TalentDevelopmentNetworkReadinessState.NotRequired,
            SkillPassportSourceDependencyState = TalentDevelopmentNetworkReadinessState.NotRequired,
            NotificationDependencyState = TalentDevelopmentNetworkReadinessState.NotRequired,
            ConsentPreconditionState = consentPreconditionState,
            DataMinimizationState = dataMinimizationState,
            RetentionPolicyState = retentionPolicyState,
            ProgressionPolicyState = progressionPolicyState,
            DependencyStates = new Dictionary<string, TalentDevelopmentNetworkReadinessState>
            {
                ["talentDataSource"] = TalentDevelopmentNetworkReadinessState.Ready
            },
            SourceContractVersion = "v1",
            TalentDevelopmentNetworkReadinessVersion = 1
        };

    private static IReadOnlyDictionary<Guid, TalentDevelopmentNetworkReadinessMetadata> RepositoryItems(
        InMemoryTalentDevelopmentNetworkReadinessMetadataRepository repository)
    {
        var field = typeof(InMemoryTalentDevelopmentNetworkReadinessMetadataRepository)
            .GetField("_items", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (IReadOnlyDictionary<Guid, TalentDevelopmentNetworkReadinessMetadata>)field.GetValue(repository)!;
    }

    private static string PermissionFor(string methodName)
    {
        var method = typeof(TalentDevelopmentNetworkController).GetMethod(methodName)!;
        var attribute = method.GetCustomAttribute<HasPermissionAttribute>()!;
        var field = typeof(HasPermissionAttribute).GetField("_permission", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (string)field.GetValue(attribute)!;
    }

    private static readonly Guid Holding = Guid.Parse("1e9a1000-0000-0000-0000-000000000001");
    private static readonly Guid Medikal = Guid.Parse("1e9a1000-0000-0000-0000-000000000002");
    private static readonly Guid Teknoloji = Guid.Parse("1e9a1000-0000-0000-0000-000000000003");

    private static FixedLegalEntityContext PilotLegalEntityContext() =>
        new(Holding, new[] { Holding, Medikal, Teknoloji });

    private static async Task<IReadOnlyList<TalentDevelopmentNetworkReadinessListItemDto>> ListWith(
        ITalentDevelopmentNetworkReadinessMetadataRepository repository,
        Guid tenantId,
        IReadOnlyCollection<Guid> effective)
    {
        var handler = new GetTalentDevelopmentNetworkReadinessListHandler(
            repository,
            new FixedTenantContext(tenantId),
            new FixedLegalEntityContext(effective.First(), effective));
        var response = await handler.Handle(new GetTalentDevelopmentNetworkReadinessListQuery(), CancellationToken.None);
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

    private sealed class InMemoryTalentDevelopmentNetworkReadinessMetadataRepository : ITalentDevelopmentNetworkReadinessMetadataRepository
    {
        private readonly Dictionary<Guid, TalentDevelopmentNetworkReadinessMetadata> _items;

        public InMemoryTalentDevelopmentNetworkReadinessMetadataRepository(params TalentDevelopmentNetworkReadinessMetadata[] items) =>
            _items = items.ToDictionary(x => x.Id);

        public Task<IReadOnlyList<TalentDevelopmentNetworkReadinessMetadata>> ListAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<TalentDevelopmentNetworkReadinessMetadata>>(
                _items.Values.Where(x => x.TenantId == tenantId && !x.IsDeleted && legalEntityIds.Contains(x.LegalEntityId)).OrderBy(x => x.Code).ToList());

        public Task<TalentDevelopmentNetworkReadinessMetadata?> GetByIdAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, Guid id, CancellationToken ct) =>
            Task.FromResult(_items.Values.FirstOrDefault(x => x.TenantId == tenantId && x.Id == id && !x.IsDeleted && legalEntityIds.Contains(x.LegalEntityId)));

        public Task<bool> ExistsActiveCodeAsync(Guid tenantId, Guid legalEntityId, string code, Guid? excludingId, CancellationToken ct) =>
            Task.FromResult(_items.Values.Any(x => x.TenantId == tenantId && x.LegalEntityId == legalEntityId && x.Code == code && !x.IsDeleted && x.Id != excludingId));

        public Task CreateAsync(TalentDevelopmentNetworkReadinessMetadata metadata, CancellationToken ct)
        {
            _items[metadata.Id] = metadata;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(TalentDevelopmentNetworkReadinessMetadata metadata, CancellationToken ct)
        {
            _items[metadata.Id] = metadata;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task LegalEntity_create_stamps_the_selected_legal_entity()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryTalentDevelopmentNetworkReadinessMetadataRepository();
        var handler = CreateHandler(repository, tenantId, new FixedLegalEntityContext(Medikal, new[] { Medikal }));

        var created = await handler.Handle(new CreateTalentDevelopmentNetworkReadinessCommand(ValidRequest()), CancellationToken.None);
        var stored = RepositoryItems(repository)[created.Data];

        Assert.True(created.IsSuccessful);
        Assert.Equal(Medikal, stored.LegalEntityId);
    }

    [Fact]
    public async Task LegalEntity_create_without_a_permitted_selection_is_forbidden()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryTalentDevelopmentNetworkReadinessMetadataRepository();
        var handler = CreateHandler(repository, tenantId, new FixedLegalEntityContext(Teknoloji, selectionAllowed: false));

        var response = await handler.Handle(new CreateTalentDevelopmentNetworkReadinessCommand(ValidRequest()), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(403, response.StatusCode);
        Assert.Empty(RepositoryItems(repository));
    }

    [Fact]
    public async Task LegalEntity_list_rolls_up_holding_and_isolates_siblings()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryTalentDevelopmentNetworkReadinessMetadataRepository();

        await CreateHandler(repository, tenantId, new FixedLegalEntityContext(Medikal, new[] { Medikal }))
            .Handle(new CreateTalentDevelopmentNetworkReadinessCommand(ValidRequest(code: "MED-01")), CancellationToken.None);
        await CreateHandler(repository, tenantId, new FixedLegalEntityContext(Teknoloji, new[] { Teknoloji }))
            .Handle(new CreateTalentDevelopmentNetworkReadinessCommand(ValidRequest(code: "TEK-01")), CancellationToken.None);

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
        var repository = new InMemoryTalentDevelopmentNetworkReadinessMetadataRepository();
        var medikal = new FixedLegalEntityContext(Medikal, new[] { Medikal });
        var teknoloji = new FixedLegalEntityContext(Teknoloji, new[] { Teknoloji });

        var first = await CreateHandler(repository, tenantId, medikal)
            .Handle(new CreateTalentDevelopmentNetworkReadinessCommand(ValidRequest(code: "SHARED-01")), CancellationToken.None);
        var duplicateSameEntity = await CreateHandler(repository, tenantId, medikal)
            .Handle(new CreateTalentDevelopmentNetworkReadinessCommand(ValidRequest(code: "SHARED-01")), CancellationToken.None);
        var sameCodeOtherEntity = await CreateHandler(repository, tenantId, teknoloji)
            .Handle(new CreateTalentDevelopmentNetworkReadinessCommand(ValidRequest(code: "SHARED-01")), CancellationToken.None);

        Assert.True(first.IsSuccessful);
        Assert.Equal(409, duplicateSameEntity.StatusCode);
        Assert.True(sameCodeOtherEntity.IsSuccessful);
    }
}
