using System.Reflection;
using Diten.HumanCapitalService.Api.Controllers.Hcm;
using Diten.HumanCapitalService.Api.Security;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Application.Features.CandidatePipeline;
using Diten.HumanCapitalService.Application.Features.CandidatePipeline.Commands;
using Diten.HumanCapitalService.Application.Features.CandidatePipeline.Handlers;
using Diten.HumanCapitalService.Application.Features.CandidatePipeline.Queries;
using Diten.HumanCapitalService.Domain.Entities;
using Diten.HumanCapitalService.Domain.Enums;
using Diten.HumanCapitalService.Domain.Repositories;
using Diten.HumanCapitalService.Persistence.Repositories;
using Xunit;

namespace Diten.HumanCapitalService.Application.Tests;

public sealed class CandidatePipelineTests
{
    [Fact]
    public async Task Create_list_and_get_metadata_contract()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryCandidatePipelineReadinessMetadataRepository();
        var create = CreateHandler(repository, tenantId);

        var created = await create.Handle(new CreateCandidatePipelineReadinessCommand(ValidRequest()), CancellationToken.None);
        var list = await new GetCandidatePipelineReadinessListHandler(repository, new FixedTenantContext(tenantId))
            .Handle(new GetCandidatePipelineReadinessListQuery(), CancellationToken.None);
        var get = await new GetCandidatePipelineReadinessByIdHandler(repository, new FixedTenantContext(tenantId))
            .Handle(new GetCandidatePipelineReadinessByIdQuery(created.Data), CancellationToken.None);

        Assert.True(created.IsSuccessful);
        Assert.Equal(201, created.StatusCode);
        Assert.True(list.IsSuccessful);
        Assert.Single(list.Data!);
        Assert.True(get.IsSuccessful);
        Assert.Equal("PIPELINE-001", get.Data!.Code);
        Assert.Equal("Candidate pipeline readiness", get.Data.DisplayName);
        Assert.Equal(CandidatePipelineReadinessState.NotRequired, get.Data.CandidateCommunicationBoundaryState);
        Assert.Equal(CandidatePipelineReadinessState.NotRequired, get.Data.AutomatedDecisionBoundaryState);
    }

    [Fact]
    public async Task Cross_tenant_lookup_returns_not_found()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var metadata = Metadata(tenantA);
        var handler = new GetCandidatePipelineReadinessByIdHandler(
            new InMemoryCandidatePipelineReadinessMetadataRepository(metadata),
            new FixedTenantContext(tenantB));

        var response = await handler.Handle(new GetCandidatePipelineReadinessByIdQuery(metadata.Id), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Missing_tenant_context_fails_closed()
    {
        var handler = CreateHandler(new InMemoryCandidatePipelineReadinessMetadataRepository(), Guid.Empty);

        var response = await handler.Handle(new CreateCandidatePipelineReadinessCommand(ValidRequest()), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(401, response.StatusCode);
    }

    [Fact]
    public async Task Duplicate_active_code_returns_conflict_per_tenant()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryCandidatePipelineReadinessMetadataRepository();
        var handler = CreateHandler(repository, tenantId);

        var first = await handler.Handle(new CreateCandidatePipelineReadinessCommand(ValidRequest(code: "pipeline-001")), CancellationToken.None);
        var second = await handler.Handle(new CreateCandidatePipelineReadinessCommand(ValidRequest(code: " PIPELINE-001 ")), CancellationToken.None);

        Assert.True(first.IsSuccessful);
        Assert.False(second.IsSuccessful);
        Assert.Equal(409, second.StatusCode);
    }

    [Fact]
    public async Task Soft_delete_hides_record_and_sets_deleted_at()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(tenantId);
        var repository = new InMemoryCandidatePipelineReadinessMetadataRepository(metadata);
        var handler = new DeleteCandidatePipelineReadinessHandler(repository, new FixedTenantContext(tenantId));

        var response = await handler.Handle(new DeleteCandidatePipelineReadinessCommand(metadata.Id), CancellationToken.None);
        var hidden = await repository.GetByIdAsync(tenantId, metadata.Id, CancellationToken.None);
        var stored = RepositoryItems(repository)[metadata.Id];

        Assert.True(response.IsSuccessful);
        Assert.Equal(204, response.StatusCode);
        Assert.Null(hidden);
        Assert.True(stored.IsDeleted);
        Assert.NotNull(stored.DeletedAt);
        Assert.Equal(CandidatePipelineReadinessState.Archived, stored.PipelineReadinessState);
    }

    [Fact]
    public async Task Ready_state_fails_closed_when_preconditions_are_deferred()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryCandidatePipelineReadinessMetadataRepository();
        var handler = CreateHandler(repository, tenantId);

        var response = await handler.Handle(
            new CreateCandidatePipelineReadinessCommand(ValidRequest(pipelineState: CandidatePipelineReadinessState.Ready)),
            CancellationToken.None);
        var stored = RepositoryItems(repository)[response.Data];

        Assert.True(response.IsSuccessful);
        Assert.Equal(CandidatePipelineReadinessState.Deferred, stored.PipelineReadinessState);
        Assert.Contains("deferred", stored.DeferredReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Evaluation_promotes_ready_only_when_metadata_preconditions_are_satisfied()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(
            tenantId,
            pipelineStageGovernanceState: CandidatePipelineReadinessState.Ready,
            interviewSchedulingReadinessState: CandidatePipelineReadinessState.Ready,
            interviewerAssignmentReadinessState: CandidatePipelineReadinessState.Ready,
            evaluationGovernanceState: CandidatePipelineReadinessState.Ready,
            candidateCommunicationBoundaryState: CandidatePipelineReadinessState.NotRequired,
            consentPreconditionState: CandidatePipelineReadinessState.Ready,
            dataMinimizationState: CandidatePipelineReadinessState.Ready,
            retentionPolicyState: CandidatePipelineReadinessState.Ready,
            evidencePolicyState: CandidatePipelineReadinessState.Ready,
            calendarDependencyState: CandidatePipelineReadinessState.NotRequired,
            notificationDependencyState: CandidatePipelineReadinessState.NotRequired,
            documentDependencyState: CandidatePipelineReadinessState.NotRequired,
            automatedDecisionBoundaryState: CandidatePipelineReadinessState.NotRequired);
        var repository = new InMemoryCandidatePipelineReadinessMetadataRepository(metadata);
        var handler = new EvaluateCandidatePipelineReadinessHandler(repository, new FixedTenantContext(tenantId));

        var response = await handler.Handle(new EvaluateCandidatePipelineReadinessCommand(metadata.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(CandidatePipelineReadinessState.Ready, response.Data!.PipelineReadinessState);
        Assert.NotNull(response.Data.LastEvaluatedAt);
    }

    [Fact]
    public async Task Non_activating_evaluation_preserves_deferred_metadata()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(tenantId, consentPreconditionState: CandidatePipelineReadinessState.Deferred);
        var repository = new InMemoryCandidatePipelineReadinessMetadataRepository(metadata);
        var handler = new EvaluateCandidatePipelineReadinessHandler(repository, new FixedTenantContext(tenantId));

        var response = await handler.Handle(new EvaluateCandidatePipelineReadinessCommand(metadata.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(CandidatePipelineReadinessState.Deferred, response.Data!.PipelineReadinessState);
        Assert.Contains("preconditions", response.Data.DeferredReason);
    }

    [Fact]
    public async Task Audit_metadata_uses_audit_permission_surface()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(tenantId);
        var repository = new InMemoryCandidatePipelineReadinessMetadataRepository(metadata);
        var response = await new GetCandidatePipelineAuditMetadataHandler(repository, new FixedTenantContext(tenantId))
            .Handle(new GetCandidatePipelineAuditMetadataQuery(metadata.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(metadata.Code, response.Data!.Code);
        Assert.Equal(CandidatePipelineGuard.AuditReadPermission, PermissionFor(nameof(CandidatePipelineController.GetAuditMetadata)));
    }

    [Theory]
    [InlineData("interview_note")]
    [InlineData("free_text")]
    [InlineData("resume")]
    [InlineData("attachment")]
    [InlineData("score")]
    [InlineData("automated_decision")]
    public async Task Forbidden_interview_body_scoring_and_sensitive_markers_are_rejected(string marker)
    {
        var tenantId = Guid.NewGuid();
        var handler = CreateHandler(new InMemoryCandidatePipelineReadinessMetadataRepository(), tenantId);

        var response = await handler.Handle(
            new CreateCandidatePipelineReadinessCommand(ValidRequest(sourceContractVersion: $"v1_{marker}")),
            CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
    }

    [Fact]
    public void Public_and_persisted_contract_excludes_forbidden_body_scoring_and_sensitive_fields()
    {
        var forbiddenFragments = new[] { "Note", "FreeText", "Resume", "Cv", "Attachment", "Payload", "Score", "Rank", "ModelOutput", "AutomatedDecisionResult" };
        var contractTypes = new[]
        {
            typeof(CandidatePipelineCreateRequest),
            typeof(CandidatePipelineReadinessDto),
            typeof(CandidatePipelineReadinessMetadata)
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
        Assert.Null(typeof(CandidatePipelineCreateRequest).GetProperty("TenantId"));
    }

    [Fact]
    public void Controller_permissions_match_pack()
    {
        Assert.Equal(CandidatePipelineGuard.ReadPermission, PermissionFor(nameof(CandidatePipelineController.GetAll)));
        Assert.Equal(CandidatePipelineGuard.ReadPermission, PermissionFor(nameof(CandidatePipelineController.GetById)));
        Assert.Equal(CandidatePipelineGuard.ManagePermission, PermissionFor(nameof(CandidatePipelineController.Create)));
        Assert.Equal(CandidatePipelineGuard.EvaluatePermission, PermissionFor(nameof(CandidatePipelineController.Evaluate)));
        Assert.Equal(CandidatePipelineGuard.ManagePermission, PermissionFor(nameof(CandidatePipelineController.Delete)));
        Assert.Equal(CandidatePipelineGuard.AuditReadPermission, PermissionFor(nameof(CandidatePipelineController.GetAuditMetadata)));
    }

    [Fact]
    public void Mongo_repository_contract_names_are_stable()
    {
        Assert.Equal("hcm_candidate_pipeline_readiness", MongoCandidatePipelineReadinessMetadataRepository.CollectionName);
        Assert.Equal("ux_hcm_candidate_pipeline_tenant_code_active", MongoCandidatePipelineReadinessMetadataRepository.ActiveCodeUniqueIndexName);
    }

    [Fact]
    public void Runtime_literal_regression_guard_has_no_reserved_identity_literals()
    {
        var reserved = string.Join("-", "CAND", "CAP", "0023");
        var legacy = string.Join("-", "MOD", "0301");
        var runtimeStrings = new[]
        {
            CandidatePipelineGuard.OwnerKey,
            CandidatePipelineGuard.ReadPermission,
            CandidatePipelineGuard.ManagePermission,
            CandidatePipelineGuard.EvaluatePermission,
            CandidatePipelineGuard.AuditReadPermission,
            MongoCandidatePipelineReadinessMetadataRepository.CollectionName
        };

        Assert.DoesNotContain(runtimeStrings, value => value.Contains(reserved, StringComparison.Ordinal));
        Assert.DoesNotContain(runtimeStrings, value => value.Contains(legacy, StringComparison.Ordinal));
    }

    private static CreateCandidatePipelineReadinessHandler CreateHandler(
        ICandidatePipelineReadinessMetadataRepository repository,
        Guid tenantId) =>
        new(repository, new FixedTenantContext(tenantId == Guid.Empty ? null : tenantId));

    private static CandidatePipelineCreateRequest ValidRequest(
        string code = "PIPELINE-001",
        CandidatePipelineReadinessState pipelineState = CandidatePipelineReadinessState.Draft,
        string sourceContractVersion = "v1") =>
        new()
        {
            Code = code,
            DisplayName = "Candidate pipeline readiness",
            PipelineReadinessState = pipelineState,
            PipelineStageGovernanceState = CandidatePipelineReadinessState.Deferred,
            InterviewSchedulingReadinessState = CandidatePipelineReadinessState.Deferred,
            InterviewerAssignmentReadinessState = CandidatePipelineReadinessState.Deferred,
            EvaluationGovernanceState = CandidatePipelineReadinessState.Deferred,
            CandidateCommunicationBoundaryState = CandidatePipelineReadinessState.NotRequired,
            ConsentPreconditionState = CandidatePipelineReadinessState.Deferred,
            DataMinimizationState = CandidatePipelineReadinessState.Deferred,
            RetentionPolicyState = CandidatePipelineReadinessState.Deferred,
            EvidencePolicyState = CandidatePipelineReadinessState.Deferred,
            CalendarDependencyState = CandidatePipelineReadinessState.NotRequired,
            NotificationDependencyState = CandidatePipelineReadinessState.NotRequired,
            DocumentDependencyState = CandidatePipelineReadinessState.NotRequired,
            AutomatedDecisionBoundaryState = CandidatePipelineReadinessState.NotRequired,
            DependencyStates = new Dictionary<string, CandidatePipelineReadinessState>
            {
                ["applicantIntake"] = CandidatePipelineReadinessState.Ready,
                ["employeeProjection"] = CandidatePipelineReadinessState.Ready,
                ["sensitiveAccess"] = CandidatePipelineReadinessState.Ready
            },
            SourceContractVersion = sourceContractVersion,
            PipelineReadinessVersion = 1
        };

    private static CandidatePipelineReadinessMetadata Metadata(
        Guid tenantId,
        string code = "PIPELINE-001",
        CandidatePipelineReadinessState pipelineStageGovernanceState = CandidatePipelineReadinessState.Deferred,
        CandidatePipelineReadinessState interviewSchedulingReadinessState = CandidatePipelineReadinessState.Deferred,
        CandidatePipelineReadinessState interviewerAssignmentReadinessState = CandidatePipelineReadinessState.Deferred,
        CandidatePipelineReadinessState evaluationGovernanceState = CandidatePipelineReadinessState.Deferred,
        CandidatePipelineReadinessState candidateCommunicationBoundaryState = CandidatePipelineReadinessState.NotRequired,
        CandidatePipelineReadinessState consentPreconditionState = CandidatePipelineReadinessState.Deferred,
        CandidatePipelineReadinessState dataMinimizationState = CandidatePipelineReadinessState.Deferred,
        CandidatePipelineReadinessState retentionPolicyState = CandidatePipelineReadinessState.Deferred,
        CandidatePipelineReadinessState evidencePolicyState = CandidatePipelineReadinessState.Deferred,
        CandidatePipelineReadinessState calendarDependencyState = CandidatePipelineReadinessState.NotRequired,
        CandidatePipelineReadinessState notificationDependencyState = CandidatePipelineReadinessState.NotRequired,
        CandidatePipelineReadinessState documentDependencyState = CandidatePipelineReadinessState.NotRequired,
        CandidatePipelineReadinessState automatedDecisionBoundaryState = CandidatePipelineReadinessState.NotRequired) =>
        new()
        {
            TenantId = tenantId,
            Code = code,
            DisplayName = "Candidate pipeline readiness",
            PipelineReadinessState = CandidatePipelineReadinessState.Draft,
            PipelineStageGovernanceState = pipelineStageGovernanceState,
            InterviewSchedulingReadinessState = interviewSchedulingReadinessState,
            InterviewerAssignmentReadinessState = interviewerAssignmentReadinessState,
            EvaluationGovernanceState = evaluationGovernanceState,
            CandidateCommunicationBoundaryState = candidateCommunicationBoundaryState,
            ConsentPreconditionState = consentPreconditionState,
            DataMinimizationState = dataMinimizationState,
            RetentionPolicyState = retentionPolicyState,
            EvidencePolicyState = evidencePolicyState,
            CalendarDependencyState = calendarDependencyState,
            NotificationDependencyState = notificationDependencyState,
            DocumentDependencyState = documentDependencyState,
            AutomatedDecisionBoundaryState = automatedDecisionBoundaryState,
            DependencyStates = new Dictionary<string, CandidatePipelineReadinessState>
            {
                ["applicantIntake"] = CandidatePipelineReadinessState.Ready
            },
            SourceContractVersion = "v1",
            PipelineReadinessVersion = 1
        };

    private static IReadOnlyDictionary<Guid, CandidatePipelineReadinessMetadata> RepositoryItems(
        InMemoryCandidatePipelineReadinessMetadataRepository repository)
    {
        var field = typeof(InMemoryCandidatePipelineReadinessMetadataRepository)
            .GetField("_items", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (IReadOnlyDictionary<Guid, CandidatePipelineReadinessMetadata>)field.GetValue(repository)!;
    }

    private static string PermissionFor(string methodName)
    {
        var method = typeof(CandidatePipelineController).GetMethod(methodName)!;
        var attribute = method.GetCustomAttribute<HasPermissionAttribute>()!;
        var field = typeof(HasPermissionAttribute).GetField("_permission", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (string)field.GetValue(attribute)!;
    }

    private sealed class FixedTenantContext : ITenantContext
    {
        public FixedTenantContext(Guid? tenantId) => TenantId = tenantId;

        public Guid? TenantId { get; }
    }

    private sealed class InMemoryCandidatePipelineReadinessMetadataRepository : ICandidatePipelineReadinessMetadataRepository
    {
        private readonly Dictionary<Guid, CandidatePipelineReadinessMetadata> _items;

        public InMemoryCandidatePipelineReadinessMetadataRepository(params CandidatePipelineReadinessMetadata[] items) =>
            _items = items.ToDictionary(x => x.Id);

        public Task<IReadOnlyList<CandidatePipelineReadinessMetadata>> ListAsync(Guid tenantId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<CandidatePipelineReadinessMetadata>>(
                _items.Values.Where(x => x.TenantId == tenantId && !x.IsDeleted).OrderBy(x => x.Code).ToList());

        public Task<CandidatePipelineReadinessMetadata?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct) =>
            Task.FromResult(_items.Values.FirstOrDefault(x => x.TenantId == tenantId && x.Id == id && !x.IsDeleted));

        public Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct) =>
            Task.FromResult(_items.Values.Any(x => x.TenantId == tenantId && x.Code == code && !x.IsDeleted && x.Id != excludingId));

        public Task CreateAsync(CandidatePipelineReadinessMetadata metadata, CancellationToken ct)
        {
            _items[metadata.Id] = metadata;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(CandidatePipelineReadinessMetadata metadata, CancellationToken ct)
        {
            _items[metadata.Id] = metadata;
            return Task.CompletedTask;
        }
    }
}
