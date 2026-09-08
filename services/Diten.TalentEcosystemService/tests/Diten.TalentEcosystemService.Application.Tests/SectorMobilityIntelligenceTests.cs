using System.Reflection;
using Diten.TalentEcosystemService.Api.Controllers.Tep;
using Diten.TalentEcosystemService.Api.Security;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.SectorMobilityIntelligence;
using Diten.TalentEcosystemService.Application.Features.SectorMobilityIntelligence.Commands;
using Diten.TalentEcosystemService.Application.Features.SectorMobilityIntelligence.Handlers;
using Diten.TalentEcosystemService.Application.Features.SectorMobilityIntelligence.Queries;
using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Enums;
using Diten.TalentEcosystemService.Domain.Repositories;
using Diten.TalentEcosystemService.Persistence.Repositories;
using Xunit;

namespace Diten.TalentEcosystemService.Application.Tests;

public sealed class SectorMobilityIntelligenceTests
{
    [Fact]
    public async Task Create_list_and_get_metadata_contract()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemorySectorMobilityIntelligenceReadinessMetadataRepository();
        var create = CreateHandler(repository, tenantId);

        var created = await create.Handle(new CreateSectorMobilityIntelligenceReadinessCommand(ValidRequest()), CancellationToken.None);
        var list = await new GetSectorMobilityIntelligenceReadinessListHandler(repository, new FixedTenantContext(tenantId))
            .Handle(new GetSectorMobilityIntelligenceReadinessListQuery(), CancellationToken.None);
        var get = await new GetSectorMobilityIntelligenceReadinessByIdHandler(repository, new FixedTenantContext(tenantId))
            .Handle(new GetSectorMobilityIntelligenceReadinessByIdQuery(created.Data), CancellationToken.None);

        Assert.True(created.IsSuccessful);
        Assert.Equal(201, created.StatusCode);
        Assert.True(list.IsSuccessful);
        Assert.Single(list.Data!);
        Assert.True(get.IsSuccessful);
        Assert.Equal("SUCC-001", get.Data!.Code);
        Assert.Equal("SectorMobilityIntelligence readiness", get.Data.DisplayName);
        Assert.Equal(SectorMobilityIntelligenceReadinessState.NotRequired, get.Data.MobilityCatalogBoundaryState);
        Assert.Equal(SectorMobilityIntelligenceReadinessState.NotRequired, get.Data.FlowBindingIntakeBoundaryState);
        Assert.Equal(SectorMobilityIntelligenceReadinessState.NotRequired, get.Data.CorridorScopeBoundaryState);
        Assert.Equal(SectorMobilityIntelligenceReadinessState.NotRequired, get.Data.VisibilityControlBoundaryState);
        Assert.Equal(SectorMobilityIntelligenceReadinessState.NotRequired, get.Data.MobilityReviewBoundaryState);
        Assert.Equal(SectorMobilityIntelligenceReadinessState.NotRequired, get.Data.AutomatedDecisionBoundaryState);
        Assert.Equal(SectorMobilityIntelligenceReadinessState.NotRequired, get.Data.SectorTrendSourceDependencyState);
        Assert.Equal(SectorMobilityIntelligenceReadinessState.NotRequired, get.Data.WorkforceAnalyticsSourceDependencyState);
        Assert.Equal(SectorMobilityIntelligenceReadinessState.NotRequired, get.Data.SkillsTaxonomySourceDependencyState);
        Assert.Equal(SectorMobilityIntelligenceReadinessState.NotRequired, get.Data.NotificationDependencyState);
    }

    [Fact]
    public async Task Cross_tenant_lookup_returns_not_found()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var metadata = Metadata(tenantA);
        var handler = new GetSectorMobilityIntelligenceReadinessByIdHandler(
            new InMemorySectorMobilityIntelligenceReadinessMetadataRepository(metadata),
            new FixedTenantContext(tenantB));

        var response = await handler.Handle(new GetSectorMobilityIntelligenceReadinessByIdQuery(metadata.Id), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Missing_tenant_context_fails_closed()
    {
        var handler = CreateHandler(new InMemorySectorMobilityIntelligenceReadinessMetadataRepository(), Guid.Empty);

        var response = await handler.Handle(new CreateSectorMobilityIntelligenceReadinessCommand(ValidRequest()), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(401, response.StatusCode);
    }

    [Fact]
    public async Task Duplicate_active_code_returns_conflict_per_tenant()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemorySectorMobilityIntelligenceReadinessMetadataRepository();
        var handler = CreateHandler(repository, tenantId);

        var first = await handler.Handle(new CreateSectorMobilityIntelligenceReadinessCommand(ValidRequest(code: "succ-001")), CancellationToken.None);
        var second = await handler.Handle(new CreateSectorMobilityIntelligenceReadinessCommand(ValidRequest(code: " SUCC-001 ")), CancellationToken.None);

        Assert.True(first.IsSuccessful);
        Assert.False(second.IsSuccessful);
        Assert.Equal(409, second.StatusCode);
    }

    [Fact]
    public async Task Soft_delete_hides_record_and_sets_deleted_at()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(tenantId);
        var repository = new InMemorySectorMobilityIntelligenceReadinessMetadataRepository(metadata);
        var handler = new DeleteSectorMobilityIntelligenceReadinessHandler(repository, new FixedTenantContext(tenantId));

        var response = await handler.Handle(new DeleteSectorMobilityIntelligenceReadinessCommand(metadata.Id), CancellationToken.None);
        var hidden = await repository.GetByIdAsync(tenantId, metadata.Id, CancellationToken.None);
        var stored = RepositoryItems(repository)[metadata.Id];

        Assert.True(response.IsSuccessful);
        Assert.Equal(204, response.StatusCode);
        Assert.Null(hidden);
        Assert.True(stored.IsDeleted);
        Assert.NotNull(stored.DeletedAt);
        Assert.Equal(SectorMobilityIntelligenceReadinessState.Archived, stored.SectorMobilityIntelligenceReadinessState);
    }

    [Fact]
    public async Task Ready_state_fails_closed_when_preconditions_are_deferred()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemorySectorMobilityIntelligenceReadinessMetadataRepository();
        var handler = CreateHandler(repository, tenantId);

        var response = await handler.Handle(
            new CreateSectorMobilityIntelligenceReadinessCommand(ValidRequest(readinessState: SectorMobilityIntelligenceReadinessState.Ready)),
            CancellationToken.None);
        var stored = RepositoryItems(repository)[response.Data];

        Assert.True(response.IsSuccessful);
        Assert.Equal(SectorMobilityIntelligenceReadinessState.Deferred, stored.SectorMobilityIntelligenceReadinessState);
        Assert.Contains("deferred", stored.DeferredReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Evaluation_promotes_ready_only_when_metadata_preconditions_are_satisfied()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(
            tenantId,
            consentPreconditionState: SectorMobilityIntelligenceReadinessState.Ready,
            dataMinimizationState: SectorMobilityIntelligenceReadinessState.Ready,
            retentionPolicyState: SectorMobilityIntelligenceReadinessState.Ready,
            publicationPolicyState: SectorMobilityIntelligenceReadinessState.Ready);
        var repository = new InMemorySectorMobilityIntelligenceReadinessMetadataRepository(metadata);
        var handler = new EvaluateSectorMobilityIntelligenceReadinessHandler(repository, new FixedTenantContext(tenantId));

        var response = await handler.Handle(new EvaluateSectorMobilityIntelligenceReadinessCommand(metadata.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(SectorMobilityIntelligenceReadinessState.Ready, response.Data!.SectorMobilityIntelligenceReadinessState);
        Assert.NotNull(response.Data.LastEvaluatedAt);
    }

    [Fact]
    public async Task Non_activating_evaluation_preserves_deferred_metadata()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(tenantId, consentPreconditionState: SectorMobilityIntelligenceReadinessState.Deferred);
        var repository = new InMemorySectorMobilityIntelligenceReadinessMetadataRepository(metadata);
        var handler = new EvaluateSectorMobilityIntelligenceReadinessHandler(repository, new FixedTenantContext(tenantId));

        var response = await handler.Handle(new EvaluateSectorMobilityIntelligenceReadinessCommand(metadata.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(SectorMobilityIntelligenceReadinessState.Deferred, response.Data!.SectorMobilityIntelligenceReadinessState);
        Assert.Contains("preconditions", response.Data.DeferredReason);
    }

    [Fact]
    public async Task Pool_highpotential_nomination_assessment_review_and_decision_boundaries_cannot_be_ready()
    {
        var tenantId = Guid.NewGuid();
        var handler = CreateHandler(new InMemorySectorMobilityIntelligenceReadinessMetadataRepository(), tenantId);

        var requests = new[]
        {
            ValidRequest(mobilityCatalogBoundaryState: SectorMobilityIntelligenceReadinessState.Ready),
            ValidRequest(flowBindingIntakeBoundaryState: SectorMobilityIntelligenceReadinessState.Ready),
            ValidRequest(corridorScopeBoundaryState: SectorMobilityIntelligenceReadinessState.Ready),
            ValidRequest(visibilityControlBoundaryState: SectorMobilityIntelligenceReadinessState.Ready),
            ValidRequest(mobilityReviewBoundaryState: SectorMobilityIntelligenceReadinessState.Ready),
            ValidRequest(automatedDecisionBoundaryState: SectorMobilityIntelligenceReadinessState.Ready)
        };

        foreach (var request in requests)
        {
            var response = await handler.Handle(new CreateSectorMobilityIntelligenceReadinessCommand(request), CancellationToken.None);

            Assert.False(response.IsSuccessful);
            Assert.Equal(400, response.StatusCode);
        }
    }

    [Fact]
    public async Task Audit_metadata_uses_audit_permission_surface()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(tenantId);
        var repository = new InMemorySectorMobilityIntelligenceReadinessMetadataRepository(metadata);
        var response = await new GetSectorMobilityIntelligenceAuditMetadataHandler(repository, new FixedTenantContext(tenantId))
            .Handle(new GetSectorMobilityIntelligenceAuditMetadataQuery(metadata.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(metadata.Code, response.Data!.Code);
        Assert.Equal(SectorMobilityIntelligenceGuard.AuditReadPermission, PermissionFor(nameof(SectorMobilityIntelligenceController.GetAuditMetadata)));
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
        var handler = CreateHandler(new InMemorySectorMobilityIntelligenceReadinessMetadataRepository(), tenantId);

        // Marker injected as a STANDALONE token (whitespace-delimited) so the word-boundary
        // (\b) matcher rejects a genuine forbidden token.
        var response = await handler.Handle(
            new CreateSectorMobilityIntelligenceReadinessCommand(ValidRequest(sourceContractVersion: $"v1 {marker}")),
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
        var handler = CreateHandler(new InMemorySectorMobilityIntelligenceReadinessMetadataRepository(), tenantId);

        var response = await handler.Handle(
            new CreateSectorMobilityIntelligenceReadinessCommand(ValidRequest(displayName: legitimateValue)),
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
            typeof(SectorMobilityIntelligenceReadinessCreateRequest),
            typeof(SectorMobilityIntelligenceReadinessDto),
            typeof(SectorMobilityIntelligenceReadinessMetadata)
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
        Assert.Null(typeof(SectorMobilityIntelligenceReadinessCreateRequest).GetProperty("TenantId"));
    }

    [Fact]
    public void Controller_permissions_match_pack()
    {
        Assert.Equal(SectorMobilityIntelligenceGuard.ReadPermission, PermissionFor(nameof(SectorMobilityIntelligenceController.GetAll)));
        Assert.Equal(SectorMobilityIntelligenceGuard.ReadPermission, PermissionFor(nameof(SectorMobilityIntelligenceController.GetById)));
        Assert.Equal(SectorMobilityIntelligenceGuard.ManagePermission, PermissionFor(nameof(SectorMobilityIntelligenceController.Create)));
        Assert.Equal(SectorMobilityIntelligenceGuard.EvaluatePermission, PermissionFor(nameof(SectorMobilityIntelligenceController.Evaluate)));
        Assert.Equal(SectorMobilityIntelligenceGuard.ManagePermission, PermissionFor(nameof(SectorMobilityIntelligenceController.Delete)));
        Assert.Equal(SectorMobilityIntelligenceGuard.AuditReadPermission, PermissionFor(nameof(SectorMobilityIntelligenceController.GetAuditMetadata)));
    }

    [Fact]
    public void Mongo_repository_contract_names_are_stable()
    {
        Assert.Equal("tep_sector_mobility_intelligence_readiness", MongoSectorMobilityIntelligenceReadinessMetadataRepository.CollectionName);
        Assert.Equal("ux_tep_sector_mobility_intelligence_tenant_code_active", MongoSectorMobilityIntelligenceReadinessMetadataRepository.ActiveCodeUniqueIndexName);
        Assert.Equal("ix_tep_sector_mobility_intelligence_tenant_state", MongoSectorMobilityIntelligenceReadinessMetadataRepository.TenantStateIndexName);
    }

    [Fact]
    public void Runtime_literal_regression_guard_has_no_reserved_identity_literals()
    {
        var reserved = string.Join("-", "CAND", "CAP", "0058");
        var legacy = string.Join("-", "MOD", "0349");
        var runtimeStrings = new[]
        {
            SectorMobilityIntelligenceGuard.OwnerKey,
            SectorMobilityIntelligenceGuard.ReadPermission,
            SectorMobilityIntelligenceGuard.ManagePermission,
            SectorMobilityIntelligenceGuard.EvaluatePermission,
            SectorMobilityIntelligenceGuard.AuditReadPermission,
            MongoSectorMobilityIntelligenceReadinessMetadataRepository.CollectionName
        };

        Assert.DoesNotContain(runtimeStrings, value => value.Contains(reserved, StringComparison.Ordinal));
        Assert.DoesNotContain(runtimeStrings, value => value.Contains(legacy, StringComparison.Ordinal));
    }

    private static CreateSectorMobilityIntelligenceReadinessHandler CreateHandler(
        ISectorMobilityIntelligenceReadinessMetadataRepository repository,
        Guid tenantId) =>
        new(repository, new FixedTenantContext(tenantId == Guid.Empty ? null : tenantId));

    private static SectorMobilityIntelligenceReadinessCreateRequest ValidRequest(
        string code = "SUCC-001",
        string displayName = "SectorMobilityIntelligence readiness",
        SectorMobilityIntelligenceReadinessState readinessState = SectorMobilityIntelligenceReadinessState.Draft,
        string sourceContractVersion = "v1",
        SectorMobilityIntelligenceReadinessState mobilityCatalogBoundaryState = SectorMobilityIntelligenceReadinessState.NotRequired,
        SectorMobilityIntelligenceReadinessState flowBindingIntakeBoundaryState = SectorMobilityIntelligenceReadinessState.NotRequired,
        SectorMobilityIntelligenceReadinessState corridorScopeBoundaryState = SectorMobilityIntelligenceReadinessState.NotRequired,
        SectorMobilityIntelligenceReadinessState visibilityControlBoundaryState = SectorMobilityIntelligenceReadinessState.NotRequired,
        SectorMobilityIntelligenceReadinessState mobilityReviewBoundaryState = SectorMobilityIntelligenceReadinessState.NotRequired,
        SectorMobilityIntelligenceReadinessState automatedDecisionBoundaryState = SectorMobilityIntelligenceReadinessState.NotRequired) =>
        new()
        {
            Code = code,
            DisplayName = displayName,
            SectorMobilityIntelligenceReadinessState = readinessState,
            MobilityCatalogBoundaryState = mobilityCatalogBoundaryState,
            FlowBindingIntakeBoundaryState = flowBindingIntakeBoundaryState,
            CorridorScopeBoundaryState = corridorScopeBoundaryState,
            VisibilityControlBoundaryState = visibilityControlBoundaryState,
            MobilityReviewBoundaryState = mobilityReviewBoundaryState,
            AutomatedDecisionBoundaryState = automatedDecisionBoundaryState,
            SectorTrendSourceDependencyState = SectorMobilityIntelligenceReadinessState.NotRequired,
            WorkforceAnalyticsSourceDependencyState = SectorMobilityIntelligenceReadinessState.NotRequired,
            SkillsTaxonomySourceDependencyState = SectorMobilityIntelligenceReadinessState.NotRequired,
            NotificationDependencyState = SectorMobilityIntelligenceReadinessState.NotRequired,
            ConsentPreconditionState = SectorMobilityIntelligenceReadinessState.Deferred,
            DataMinimizationState = SectorMobilityIntelligenceReadinessState.Deferred,
            RetentionPolicyState = SectorMobilityIntelligenceReadinessState.Deferred,
            PublicationPolicyState = SectorMobilityIntelligenceReadinessState.Deferred,
            DependencyStates = new Dictionary<string, SectorMobilityIntelligenceReadinessState>
            {
                ["talentDataSource"] = SectorMobilityIntelligenceReadinessState.Ready,
                ["consentPolicy"] = SectorMobilityIntelligenceReadinessState.Ready,
                ["mobilityCatalog"] = SectorMobilityIntelligenceReadinessState.Ready
            },
            SourceContractVersion = sourceContractVersion,
            SectorMobilityIntelligenceReadinessVersion = 1
        };

    private static SectorMobilityIntelligenceReadinessMetadata Metadata(
        Guid tenantId,
        string code = "SUCC-001",
        SectorMobilityIntelligenceReadinessState consentPreconditionState = SectorMobilityIntelligenceReadinessState.Deferred,
        SectorMobilityIntelligenceReadinessState dataMinimizationState = SectorMobilityIntelligenceReadinessState.Deferred,
        SectorMobilityIntelligenceReadinessState retentionPolicyState = SectorMobilityIntelligenceReadinessState.Deferred,
        SectorMobilityIntelligenceReadinessState publicationPolicyState = SectorMobilityIntelligenceReadinessState.Deferred) =>
        new()
        {
            TenantId = tenantId,
            Code = code,
            DisplayName = "SectorMobilityIntelligence readiness",
            SectorMobilityIntelligenceReadinessState = SectorMobilityIntelligenceReadinessState.Draft,
            MobilityCatalogBoundaryState = SectorMobilityIntelligenceReadinessState.NotRequired,
            FlowBindingIntakeBoundaryState = SectorMobilityIntelligenceReadinessState.NotRequired,
            CorridorScopeBoundaryState = SectorMobilityIntelligenceReadinessState.NotRequired,
            VisibilityControlBoundaryState = SectorMobilityIntelligenceReadinessState.NotRequired,
            MobilityReviewBoundaryState = SectorMobilityIntelligenceReadinessState.NotRequired,
            AutomatedDecisionBoundaryState = SectorMobilityIntelligenceReadinessState.NotRequired,
            SectorTrendSourceDependencyState = SectorMobilityIntelligenceReadinessState.NotRequired,
            WorkforceAnalyticsSourceDependencyState = SectorMobilityIntelligenceReadinessState.NotRequired,
            SkillsTaxonomySourceDependencyState = SectorMobilityIntelligenceReadinessState.NotRequired,
            NotificationDependencyState = SectorMobilityIntelligenceReadinessState.NotRequired,
            ConsentPreconditionState = consentPreconditionState,
            DataMinimizationState = dataMinimizationState,
            RetentionPolicyState = retentionPolicyState,
            PublicationPolicyState = publicationPolicyState,
            DependencyStates = new Dictionary<string, SectorMobilityIntelligenceReadinessState>
            {
                ["talentDataSource"] = SectorMobilityIntelligenceReadinessState.Ready
            },
            SourceContractVersion = "v1",
            SectorMobilityIntelligenceReadinessVersion = 1
        };

    private static IReadOnlyDictionary<Guid, SectorMobilityIntelligenceReadinessMetadata> RepositoryItems(
        InMemorySectorMobilityIntelligenceReadinessMetadataRepository repository)
    {
        var field = typeof(InMemorySectorMobilityIntelligenceReadinessMetadataRepository)
            .GetField("_items", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (IReadOnlyDictionary<Guid, SectorMobilityIntelligenceReadinessMetadata>)field.GetValue(repository)!;
    }

    private static string PermissionFor(string methodName)
    {
        var method = typeof(SectorMobilityIntelligenceController).GetMethod(methodName)!;
        var attribute = method.GetCustomAttribute<HasPermissionAttribute>()!;
        var field = typeof(HasPermissionAttribute).GetField("_permission", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (string)field.GetValue(attribute)!;
    }

    private sealed class FixedTenantContext : ITenantContext
    {
        public FixedTenantContext(Guid? tenantId) => TenantId = tenantId;

        public Guid? TenantId { get; }
    }

    private sealed class InMemorySectorMobilityIntelligenceReadinessMetadataRepository : ISectorMobilityIntelligenceReadinessMetadataRepository
    {
        private readonly Dictionary<Guid, SectorMobilityIntelligenceReadinessMetadata> _items;

        public InMemorySectorMobilityIntelligenceReadinessMetadataRepository(params SectorMobilityIntelligenceReadinessMetadata[] items) =>
            _items = items.ToDictionary(x => x.Id);

        public Task<IReadOnlyList<SectorMobilityIntelligenceReadinessMetadata>> ListAsync(Guid tenantId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<SectorMobilityIntelligenceReadinessMetadata>>(
                _items.Values.Where(x => x.TenantId == tenantId && !x.IsDeleted).OrderBy(x => x.Code).ToList());

        public Task<SectorMobilityIntelligenceReadinessMetadata?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct) =>
            Task.FromResult(_items.Values.FirstOrDefault(x => x.TenantId == tenantId && x.Id == id && !x.IsDeleted));

        public Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct) =>
            Task.FromResult(_items.Values.Any(x => x.TenantId == tenantId && x.Code == code && !x.IsDeleted && x.Id != excludingId));

        public Task CreateAsync(SectorMobilityIntelligenceReadinessMetadata metadata, CancellationToken ct)
        {
            _items[metadata.Id] = metadata;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(SectorMobilityIntelligenceReadinessMetadata metadata, CancellationToken ct)
        {
            _items[metadata.Id] = metadata;
            return Task.CompletedTask;
        }
    }
}
