using System.Reflection;
using Diten.HumanCapitalService.Api.Controllers.Hcm;
using Diten.HumanCapitalService.Api.Security;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Application.Features.SelfService;
using Diten.HumanCapitalService.Application.Features.SelfService.Commands;
using Diten.HumanCapitalService.Application.Features.SelfService.Handlers;
using Diten.HumanCapitalService.Application.Features.SelfService.Queries;
using Diten.HumanCapitalService.Domain.Entities;
using Diten.HumanCapitalService.Domain.Enums;
using Diten.HumanCapitalService.Domain.Repositories;
using Diten.HumanCapitalService.Persistence.Repositories;
using Xunit;

namespace Diten.HumanCapitalService.Application.Tests;

public sealed class SelfServiceTests
{
    [Fact]
    public async Task Create_list_and_get_metadata_contract()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemorySelfServiceReadinessMetadataRepository();
        var create = CreateHandler(repository, tenantId);

        var created = await create.Handle(new CreateSelfServiceReadinessCommand(ValidRequest()), CancellationToken.None);
        var list = await new GetSelfServiceReadinessListHandler(repository, new FixedTenantContext(tenantId))
            .Handle(new GetSelfServiceReadinessListQuery(), CancellationToken.None);
        var get = await new GetSelfServiceReadinessByIdHandler(repository, new FixedTenantContext(tenantId))
            .Handle(new GetSelfServiceReadinessByIdQuery(created.Data), CancellationToken.None);

        Assert.True(created.IsSuccessful);
        Assert.Equal(201, created.StatusCode);
        Assert.True(list.IsSuccessful);
        Assert.Single(list.Data!);
        Assert.True(get.IsSuccessful);
        Assert.Equal("SUCC-001", get.Data!.Code);
        Assert.Equal("SelfService readiness", get.Data.DisplayName);
        Assert.Equal(SelfServiceReadinessState.NotRequired, get.Data.RequestIntakeBoundaryState);
        Assert.Equal(SelfServiceReadinessState.NotRequired, get.Data.ApprovalRoutingBoundaryState);
        Assert.Equal(SelfServiceReadinessState.NotRequired, get.Data.InboxDeliveryBoundaryState);
        Assert.Equal(SelfServiceReadinessState.NotRequired, get.Data.ProfileSelfUpdateBoundaryState);
        Assert.Equal(SelfServiceReadinessState.NotRequired, get.Data.DelegationScopeBoundaryState);
        Assert.Equal(SelfServiceReadinessState.NotRequired, get.Data.AutomatedDecisionBoundaryState);
        Assert.Equal(SelfServiceReadinessState.NotRequired, get.Data.IdentityDirectoryDependencyState);
        Assert.Equal(SelfServiceReadinessState.NotRequired, get.Data.HcmCapabilityDependencyState);
        Assert.Equal(SelfServiceReadinessState.NotRequired, get.Data.DocumentDependencyState);
        Assert.Equal(SelfServiceReadinessState.NotRequired, get.Data.NotificationDependencyState);
    }

    [Fact]
    public async Task Cross_tenant_lookup_returns_not_found()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var metadata = Metadata(tenantA);
        var handler = new GetSelfServiceReadinessByIdHandler(
            new InMemorySelfServiceReadinessMetadataRepository(metadata),
            new FixedTenantContext(tenantB));

        var response = await handler.Handle(new GetSelfServiceReadinessByIdQuery(metadata.Id), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Missing_tenant_context_fails_closed()
    {
        var handler = CreateHandler(new InMemorySelfServiceReadinessMetadataRepository(), Guid.Empty);

        var response = await handler.Handle(new CreateSelfServiceReadinessCommand(ValidRequest()), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(401, response.StatusCode);
    }

    [Fact]
    public async Task Duplicate_active_code_returns_conflict_per_tenant()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemorySelfServiceReadinessMetadataRepository();
        var handler = CreateHandler(repository, tenantId);

        var first = await handler.Handle(new CreateSelfServiceReadinessCommand(ValidRequest(code: "succ-001")), CancellationToken.None);
        var second = await handler.Handle(new CreateSelfServiceReadinessCommand(ValidRequest(code: " SUCC-001 ")), CancellationToken.None);

        Assert.True(first.IsSuccessful);
        Assert.False(second.IsSuccessful);
        Assert.Equal(409, second.StatusCode);
    }

    [Fact]
    public async Task Soft_delete_hides_record_and_sets_deleted_at()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(tenantId);
        var repository = new InMemorySelfServiceReadinessMetadataRepository(metadata);
        var handler = new DeleteSelfServiceReadinessHandler(repository, new FixedTenantContext(tenantId));

        var response = await handler.Handle(new DeleteSelfServiceReadinessCommand(metadata.Id), CancellationToken.None);
        var hidden = await repository.GetByIdAsync(tenantId, metadata.Id, CancellationToken.None);
        var stored = RepositoryItems(repository)[metadata.Id];

        Assert.True(response.IsSuccessful);
        Assert.Equal(204, response.StatusCode);
        Assert.Null(hidden);
        Assert.True(stored.IsDeleted);
        Assert.NotNull(stored.DeletedAt);
        Assert.Equal(SelfServiceReadinessState.Archived, stored.SelfServiceReadinessState);
    }

    [Fact]
    public async Task Ready_state_fails_closed_when_preconditions_are_deferred()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemorySelfServiceReadinessMetadataRepository();
        var handler = CreateHandler(repository, tenantId);

        var response = await handler.Handle(
            new CreateSelfServiceReadinessCommand(ValidRequest(readinessState: SelfServiceReadinessState.Ready)),
            CancellationToken.None);
        var stored = RepositoryItems(repository)[response.Data];

        Assert.True(response.IsSuccessful);
        Assert.Equal(SelfServiceReadinessState.Deferred, stored.SelfServiceReadinessState);
        Assert.Contains("deferred", stored.DeferredReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Evaluation_promotes_ready_only_when_metadata_preconditions_are_satisfied()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(
            tenantId,
            consentPreconditionState: SelfServiceReadinessState.Ready,
            dataMinimizationState: SelfServiceReadinessState.Ready,
            retentionPolicyState: SelfServiceReadinessState.Ready,
            evidencePolicyState: SelfServiceReadinessState.Ready);
        var repository = new InMemorySelfServiceReadinessMetadataRepository(metadata);
        var handler = new EvaluateSelfServiceReadinessHandler(repository, new FixedTenantContext(tenantId));

        var response = await handler.Handle(new EvaluateSelfServiceReadinessCommand(metadata.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(SelfServiceReadinessState.Ready, response.Data!.SelfServiceReadinessState);
        Assert.NotNull(response.Data.LastEvaluatedAt);
    }

    [Fact]
    public async Task Non_activating_evaluation_preserves_deferred_metadata()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(tenantId, consentPreconditionState: SelfServiceReadinessState.Deferred);
        var repository = new InMemorySelfServiceReadinessMetadataRepository(metadata);
        var handler = new EvaluateSelfServiceReadinessHandler(repository, new FixedTenantContext(tenantId));

        var response = await handler.Handle(new EvaluateSelfServiceReadinessCommand(metadata.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(SelfServiceReadinessState.Deferred, response.Data!.SelfServiceReadinessState);
        Assert.Contains("preconditions", response.Data.DeferredReason);
    }

    [Fact]
    public async Task Pool_highpotential_nomination_assessment_review_and_decision_boundaries_cannot_be_ready()
    {
        var tenantId = Guid.NewGuid();
        var handler = CreateHandler(new InMemorySelfServiceReadinessMetadataRepository(), tenantId);

        var requests = new[]
        {
            ValidRequest(requestIntakeBoundaryState: SelfServiceReadinessState.Ready),
            ValidRequest(approvalRoutingBoundaryState: SelfServiceReadinessState.Ready),
            ValidRequest(inboxDeliveryBoundaryState: SelfServiceReadinessState.Ready),
            ValidRequest(profileSelfUpdateBoundaryState: SelfServiceReadinessState.Ready),
            ValidRequest(delegationScopeBoundaryState: SelfServiceReadinessState.Ready),
            ValidRequest(automatedDecisionBoundaryState: SelfServiceReadinessState.Ready)
        };

        foreach (var request in requests)
        {
            var response = await handler.Handle(new CreateSelfServiceReadinessCommand(request), CancellationToken.None);

            Assert.False(response.IsSuccessful);
            Assert.Equal(400, response.StatusCode);
        }
    }

    [Fact]
    public async Task Audit_metadata_uses_audit_permission_surface()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(tenantId);
        var repository = new InMemorySelfServiceReadinessMetadataRepository(metadata);
        var response = await new GetSelfServiceAuditMetadataHandler(repository, new FixedTenantContext(tenantId))
            .Handle(new GetSelfServiceAuditMetadataQuery(metadata.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(metadata.Code, response.Data!.Code);
        Assert.Equal(SelfServiceGuard.AuditReadPermission, PermissionFor(nameof(SelfServiceController.GetAuditMetadata)));
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
        var handler = CreateHandler(new InMemorySelfServiceReadinessMetadataRepository(), tenantId);

        // Marker injected as a STANDALONE token (whitespace-delimited) so the word-boundary
        // (\b) matcher rejects a genuine forbidden token.
        var response = await handler.Handle(
            new CreateSelfServiceReadinessCommand(ValidRequest(sourceContractVersion: $"v1 {marker}")),
            CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
    }

    [Theory]
    [InlineData("talent taxonomy")]
    [InlineData("self-service pipeline")]
    [InlineData("nomination scorecard")]
    public async Task Word_boundary_matcher_accepts_legitimate_values_that_merely_contain_marker_substrings(string legitimateValue)
    {
        // Regression guard for the substring false-positive that previously broke LearningTraining:
        // "taxonomy" contains "tax", "scorecard" contains "score" — with the \b matcher these are
        // legitimate standalone tokens and MUST be accepted (create succeeds).
        var tenantId = Guid.NewGuid();
        var handler = CreateHandler(new InMemorySelfServiceReadinessMetadataRepository(), tenantId);

        var response = await handler.Handle(
            new CreateSelfServiceReadinessCommand(ValidRequest(displayName: legitimateValue)),
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
            typeof(SelfServiceReadinessCreateRequest),
            typeof(SelfServiceReadinessDto),
            typeof(SelfServiceReadinessMetadata)
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
        Assert.Null(typeof(SelfServiceReadinessCreateRequest).GetProperty("TenantId"));
    }

    [Fact]
    public void Controller_permissions_match_pack()
    {
        Assert.Equal(SelfServiceGuard.ReadPermission, PermissionFor(nameof(SelfServiceController.GetAll)));
        Assert.Equal(SelfServiceGuard.ReadPermission, PermissionFor(nameof(SelfServiceController.GetById)));
        Assert.Equal(SelfServiceGuard.ManagePermission, PermissionFor(nameof(SelfServiceController.Create)));
        Assert.Equal(SelfServiceGuard.EvaluatePermission, PermissionFor(nameof(SelfServiceController.Evaluate)));
        Assert.Equal(SelfServiceGuard.ManagePermission, PermissionFor(nameof(SelfServiceController.Delete)));
        Assert.Equal(SelfServiceGuard.AuditReadPermission, PermissionFor(nameof(SelfServiceController.GetAuditMetadata)));
    }

    [Fact]
    public void Mongo_repository_contract_names_are_stable()
    {
        Assert.Equal("hcm_self_service_readiness", MongoSelfServiceReadinessMetadataRepository.CollectionName);
        Assert.Equal("ux_hcm_self_service_tenant_code_active", MongoSelfServiceReadinessMetadataRepository.ActiveCodeUniqueIndexName);
        Assert.Equal("ix_hcm_self_service_tenant_state", MongoSelfServiceReadinessMetadataRepository.TenantStateIndexName);
    }

    [Fact]
    public void Runtime_literal_regression_guard_has_no_reserved_identity_literals()
    {
        var reserved = string.Join("-", "CAND", "CAP", "0038");
        var legacy = string.Join("-", "MOD", "0319");
        var runtimeStrings = new[]
        {
            SelfServiceGuard.OwnerKey,
            SelfServiceGuard.ReadPermission,
            SelfServiceGuard.ManagePermission,
            SelfServiceGuard.EvaluatePermission,
            SelfServiceGuard.AuditReadPermission,
            MongoSelfServiceReadinessMetadataRepository.CollectionName
        };

        Assert.DoesNotContain(runtimeStrings, value => value.Contains(reserved, StringComparison.Ordinal));
        Assert.DoesNotContain(runtimeStrings, value => value.Contains(legacy, StringComparison.Ordinal));
    }

    private static CreateSelfServiceReadinessHandler CreateHandler(
        ISelfServiceReadinessMetadataRepository repository,
        Guid tenantId) =>
        new(repository, new FixedTenantContext(tenantId == Guid.Empty ? null : tenantId));

    private static SelfServiceReadinessCreateRequest ValidRequest(
        string code = "SUCC-001",
        string displayName = "SelfService readiness",
        SelfServiceReadinessState readinessState = SelfServiceReadinessState.Draft,
        string sourceContractVersion = "v1",
        SelfServiceReadinessState requestIntakeBoundaryState = SelfServiceReadinessState.NotRequired,
        SelfServiceReadinessState approvalRoutingBoundaryState = SelfServiceReadinessState.NotRequired,
        SelfServiceReadinessState inboxDeliveryBoundaryState = SelfServiceReadinessState.NotRequired,
        SelfServiceReadinessState profileSelfUpdateBoundaryState = SelfServiceReadinessState.NotRequired,
        SelfServiceReadinessState delegationScopeBoundaryState = SelfServiceReadinessState.NotRequired,
        SelfServiceReadinessState automatedDecisionBoundaryState = SelfServiceReadinessState.NotRequired) =>
        new()
        {
            Code = code,
            DisplayName = displayName,
            SelfServiceReadinessState = readinessState,
            RequestIntakeBoundaryState = requestIntakeBoundaryState,
            ApprovalRoutingBoundaryState = approvalRoutingBoundaryState,
            InboxDeliveryBoundaryState = inboxDeliveryBoundaryState,
            ProfileSelfUpdateBoundaryState = profileSelfUpdateBoundaryState,
            DelegationScopeBoundaryState = delegationScopeBoundaryState,
            AutomatedDecisionBoundaryState = automatedDecisionBoundaryState,
            IdentityDirectoryDependencyState = SelfServiceReadinessState.NotRequired,
            HcmCapabilityDependencyState = SelfServiceReadinessState.NotRequired,
            DocumentDependencyState = SelfServiceReadinessState.NotRequired,
            NotificationDependencyState = SelfServiceReadinessState.NotRequired,
            ConsentPreconditionState = SelfServiceReadinessState.Deferred,
            DataMinimizationState = SelfServiceReadinessState.Deferred,
            RetentionPolicyState = SelfServiceReadinessState.Deferred,
            EvidencePolicyState = SelfServiceReadinessState.Deferred,
            DependencyStates = new Dictionary<string, SelfServiceReadinessState>
            {
                ["identityDirectory"] = SelfServiceReadinessState.Ready,
                ["hcmCapability"] = SelfServiceReadinessState.Ready,
                ["requestIntake"] = SelfServiceReadinessState.Ready
            },
            SourceContractVersion = sourceContractVersion,
            SelfServiceReadinessVersion = 1
        };

    private static SelfServiceReadinessMetadata Metadata(
        Guid tenantId,
        string code = "SUCC-001",
        SelfServiceReadinessState consentPreconditionState = SelfServiceReadinessState.Deferred,
        SelfServiceReadinessState dataMinimizationState = SelfServiceReadinessState.Deferred,
        SelfServiceReadinessState retentionPolicyState = SelfServiceReadinessState.Deferred,
        SelfServiceReadinessState evidencePolicyState = SelfServiceReadinessState.Deferred) =>
        new()
        {
            TenantId = tenantId,
            Code = code,
            DisplayName = "SelfService readiness",
            SelfServiceReadinessState = SelfServiceReadinessState.Draft,
            RequestIntakeBoundaryState = SelfServiceReadinessState.NotRequired,
            ApprovalRoutingBoundaryState = SelfServiceReadinessState.NotRequired,
            InboxDeliveryBoundaryState = SelfServiceReadinessState.NotRequired,
            ProfileSelfUpdateBoundaryState = SelfServiceReadinessState.NotRequired,
            DelegationScopeBoundaryState = SelfServiceReadinessState.NotRequired,
            AutomatedDecisionBoundaryState = SelfServiceReadinessState.NotRequired,
            IdentityDirectoryDependencyState = SelfServiceReadinessState.NotRequired,
            HcmCapabilityDependencyState = SelfServiceReadinessState.NotRequired,
            DocumentDependencyState = SelfServiceReadinessState.NotRequired,
            NotificationDependencyState = SelfServiceReadinessState.NotRequired,
            ConsentPreconditionState = consentPreconditionState,
            DataMinimizationState = dataMinimizationState,
            RetentionPolicyState = retentionPolicyState,
            EvidencePolicyState = evidencePolicyState,
            DependencyStates = new Dictionary<string, SelfServiceReadinessState>
            {
                ["identityDirectory"] = SelfServiceReadinessState.Ready
            },
            SourceContractVersion = "v1",
            SelfServiceReadinessVersion = 1
        };

    private static IReadOnlyDictionary<Guid, SelfServiceReadinessMetadata> RepositoryItems(
        InMemorySelfServiceReadinessMetadataRepository repository)
    {
        var field = typeof(InMemorySelfServiceReadinessMetadataRepository)
            .GetField("_items", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (IReadOnlyDictionary<Guid, SelfServiceReadinessMetadata>)field.GetValue(repository)!;
    }

    private static string PermissionFor(string methodName)
    {
        var method = typeof(SelfServiceController).GetMethod(methodName)!;
        var attribute = method.GetCustomAttribute<HasPermissionAttribute>()!;
        var field = typeof(HasPermissionAttribute).GetField("_permission", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (string)field.GetValue(attribute)!;
    }

    private sealed class FixedTenantContext : ITenantContext
    {
        public FixedTenantContext(Guid? tenantId) => TenantId = tenantId;

        public Guid? TenantId { get; }
    }

    private sealed class InMemorySelfServiceReadinessMetadataRepository : ISelfServiceReadinessMetadataRepository
    {
        private readonly Dictionary<Guid, SelfServiceReadinessMetadata> _items;

        public InMemorySelfServiceReadinessMetadataRepository(params SelfServiceReadinessMetadata[] items) =>
            _items = items.ToDictionary(x => x.Id);

        public Task<IReadOnlyList<SelfServiceReadinessMetadata>> ListAsync(Guid tenantId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<SelfServiceReadinessMetadata>>(
                _items.Values.Where(x => x.TenantId == tenantId && !x.IsDeleted).OrderBy(x => x.Code).ToList());

        public Task<SelfServiceReadinessMetadata?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct) =>
            Task.FromResult(_items.Values.FirstOrDefault(x => x.TenantId == tenantId && x.Id == id && !x.IsDeleted));

        public Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct) =>
            Task.FromResult(_items.Values.Any(x => x.TenantId == tenantId && x.Code == code && !x.IsDeleted && x.Id != excludingId));

        public Task CreateAsync(SelfServiceReadinessMetadata metadata, CancellationToken ct)
        {
            _items[metadata.Id] = metadata;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(SelfServiceReadinessMetadata metadata, CancellationToken ct)
        {
            _items[metadata.Id] = metadata;
            return Task.CompletedTask;
        }
    }
}
