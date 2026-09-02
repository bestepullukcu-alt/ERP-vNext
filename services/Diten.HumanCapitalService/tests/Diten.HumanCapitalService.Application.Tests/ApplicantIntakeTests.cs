using System.Reflection;
using Diten.HumanCapitalService.Api.Controllers.Hcm;
using Diten.HumanCapitalService.Api.Security;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Application.Features.ApplicantIntake;
using Diten.HumanCapitalService.Application.Features.ApplicantIntake.Commands;
using Diten.HumanCapitalService.Application.Features.ApplicantIntake.Handlers;
using Diten.HumanCapitalService.Application.Features.ApplicantIntake.Queries;
using Diten.HumanCapitalService.Domain.Entities;
using Diten.HumanCapitalService.Domain.Enums;
using Diten.HumanCapitalService.Domain.Repositories;
using Diten.HumanCapitalService.Persistence.Repositories;
using Xunit;

namespace Diten.HumanCapitalService.Application.Tests;

public sealed class ApplicantIntakeTests
{
    [Fact]
    public async Task Create_list_and_get_metadata_contract()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryApplicantIntakeReadinessMetadataRepository();
        var create = CreateHandler(repository, tenantId);

        var created = await create.Handle(new CreateApplicantIntakeReadinessCommand(ValidRequest()), CancellationToken.None);
        var list = await new GetApplicantIntakeReadinessListHandler(repository, new FixedTenantContext(tenantId))
            .Handle(new GetApplicantIntakeReadinessListQuery(), CancellationToken.None);
        var get = await new GetApplicantIntakeReadinessByIdHandler(repository, new FixedTenantContext(tenantId))
            .Handle(new GetApplicantIntakeReadinessByIdQuery(created.Data), CancellationToken.None);

        Assert.True(created.IsSuccessful);
        Assert.Equal(201, created.StatusCode);
        Assert.True(list.IsSuccessful);
        Assert.Single(list.Data!);
        Assert.True(get.IsSuccessful);
        Assert.Equal("INTAKE-001", get.Data!.Code);
        Assert.Equal("Applicant intake readiness", get.Data.DisplayName);
        Assert.Equal(ApplicantIntakeReadinessState.NotRequired, get.Data.PublicUxBoundaryState);
        Assert.Equal(ApplicantIntakeReadinessState.Deferred, get.Data.NotificationDependencyState);
    }

    [Fact]
    public async Task Cross_tenant_lookup_returns_not_found()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var metadata = Metadata(tenantA);
        var handler = new GetApplicantIntakeReadinessByIdHandler(
            new InMemoryApplicantIntakeReadinessMetadataRepository(metadata),
            new FixedTenantContext(tenantB));

        var response = await handler.Handle(new GetApplicantIntakeReadinessByIdQuery(metadata.Id), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Duplicate_active_code_returns_conflict_per_tenant()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryApplicantIntakeReadinessMetadataRepository();
        var handler = CreateHandler(repository, tenantId);

        var first = await handler.Handle(new CreateApplicantIntakeReadinessCommand(ValidRequest(code: "intake-001")), CancellationToken.None);
        var second = await handler.Handle(new CreateApplicantIntakeReadinessCommand(ValidRequest(code: " INTAKE-001 ")), CancellationToken.None);

        Assert.True(first.IsSuccessful);
        Assert.False(second.IsSuccessful);
        Assert.Equal(409, second.StatusCode);
    }

    [Fact]
    public async Task Soft_delete_hides_record_and_sets_deleted_at()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(tenantId);
        var repository = new InMemoryApplicantIntakeReadinessMetadataRepository(metadata);
        var handler = new DeleteApplicantIntakeReadinessHandler(repository, new FixedTenantContext(tenantId));

        var response = await handler.Handle(new DeleteApplicantIntakeReadinessCommand(metadata.Id), CancellationToken.None);
        var hidden = await repository.GetByIdAsync(tenantId, metadata.Id, CancellationToken.None);
        var stored = RepositoryItems(repository)[metadata.Id];

        Assert.True(response.IsSuccessful);
        Assert.Equal(204, response.StatusCode);
        Assert.Null(hidden);
        Assert.True(stored.IsDeleted);
        Assert.NotNull(stored.DeletedAt);
        Assert.Equal(ApplicantIntakeReadinessState.Archived, stored.IntakeState);
    }

    [Fact]
    public async Task Ready_state_fails_closed_when_preconditions_are_deferred()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryApplicantIntakeReadinessMetadataRepository();
        var handler = CreateHandler(repository, tenantId);

        var response = await handler.Handle(
            new CreateApplicantIntakeReadinessCommand(ValidRequest(intakeState: ApplicantIntakeReadinessState.Ready)),
            CancellationToken.None);
        var stored = RepositoryItems(repository)[response.Data];

        Assert.True(response.IsSuccessful);
        Assert.Equal(ApplicantIntakeReadinessState.Deferred, stored.IntakeState);
        Assert.Contains("deferred", stored.DeferredReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Evaluation_promotes_ready_only_when_metadata_preconditions_are_satisfied()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(
            tenantId,
            sourceChannelState: ApplicantIntakeReadinessState.Ready,
            consentPreconditionState: ApplicantIntakeReadinessState.Ready,
            dataMinimizationState: ApplicantIntakeReadinessState.Ready,
            duplicateHandlingState: ApplicantIntakeReadinessState.Ready,
            retentionPolicyState: ApplicantIntakeReadinessState.Ready,
            evidencePolicyState: ApplicantIntakeReadinessState.Ready,
            applicantIdentityBoundaryState: ApplicantIntakeReadinessState.Ready,
            publicUxBoundaryState: ApplicantIntakeReadinessState.NotRequired,
            documentDependencyState: ApplicantIntakeReadinessState.NotRequired,
            notificationDependencyState: ApplicantIntakeReadinessState.NotRequired);
        var repository = new InMemoryApplicantIntakeReadinessMetadataRepository(metadata);
        var handler = new EvaluateApplicantIntakeReadinessHandler(repository, new FixedTenantContext(tenantId));

        var response = await handler.Handle(new EvaluateApplicantIntakeReadinessCommand(metadata.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(ApplicantIntakeReadinessState.Ready, response.Data!.IntakeState);
        Assert.NotNull(response.Data.LastEvaluatedAt);
    }

    [Fact]
    public async Task Non_activating_evaluation_preserves_deferred_metadata()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(tenantId, consentPreconditionState: ApplicantIntakeReadinessState.Deferred);
        var repository = new InMemoryApplicantIntakeReadinessMetadataRepository(metadata);
        var handler = new EvaluateApplicantIntakeReadinessHandler(repository, new FixedTenantContext(tenantId));

        var response = await handler.Handle(new EvaluateApplicantIntakeReadinessCommand(metadata.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(ApplicantIntakeReadinessState.Deferred, response.Data!.IntakeState);
        Assert.Contains("preconditions", response.Data.DeferredReason);
    }

    [Fact]
    public async Task Audit_metadata_uses_audit_permission_surface()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(tenantId);
        var repository = new InMemoryApplicantIntakeReadinessMetadataRepository(metadata);
        var response = await new GetApplicantIntakeAuditMetadataHandler(repository, new FixedTenantContext(tenantId))
            .Handle(new GetApplicantIntakeAuditMetadataQuery(metadata.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(metadata.Code, response.Data!.Code);
        Assert.Equal(ApplicantIntakeGuard.AuditReadPermission, PermissionFor(nameof(ApplicantIntakeController.GetAuditMetadata)));
    }

    [Theory]
    [InlineData("resume")]
    [InlineData("cover_letter")]
    [InlineData("free_text")]
    [InlineData("attachment")]
    [InlineData("provider_payload")]
    public async Task Forbidden_application_body_and_provider_markers_are_rejected(string marker)
    {
        var tenantId = Guid.NewGuid();
        var handler = CreateHandler(new InMemoryApplicantIntakeReadinessMetadataRepository(), tenantId);

        var response = await handler.Handle(
            new CreateApplicantIntakeReadinessCommand(ValidRequest(sourceContractVersion: $"v1_{marker}")),
            CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
    }

    [Fact]
    public void Public_and_persisted_contract_excludes_forbidden_body_fields()
    {
        var forbiddenFragments = new[] { "Resume", "Cv", "CoverLetter", "FreeText", "Narrative", "Attachment", "Payload" };
        var contractTypes = new[]
        {
            typeof(ApplicantIntakeCreateRequest),
            typeof(ApplicantIntakeReadinessDto),
            typeof(ApplicantIntakeReadinessMetadata)
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
        Assert.Null(typeof(ApplicantIntakeCreateRequest).GetProperty("TenantId"));
    }

    [Fact]
    public void Controller_permissions_match_pack()
    {
        Assert.Equal(ApplicantIntakeGuard.ReadPermission, PermissionFor(nameof(ApplicantIntakeController.GetAll)));
        Assert.Equal(ApplicantIntakeGuard.ReadPermission, PermissionFor(nameof(ApplicantIntakeController.GetById)));
        Assert.Equal(ApplicantIntakeGuard.ManagePermission, PermissionFor(nameof(ApplicantIntakeController.Create)));
        Assert.Equal(ApplicantIntakeGuard.EvaluatePermission, PermissionFor(nameof(ApplicantIntakeController.Evaluate)));
        Assert.Equal(ApplicantIntakeGuard.ManagePermission, PermissionFor(nameof(ApplicantIntakeController.Delete)));
        Assert.Equal(ApplicantIntakeGuard.AuditReadPermission, PermissionFor(nameof(ApplicantIntakeController.GetAuditMetadata)));
    }

    [Fact]
    public void Mongo_repository_contract_names_are_stable()
    {
        Assert.Equal("hcm_applicant_intake_readiness", MongoApplicantIntakeReadinessMetadataRepository.CollectionName);
        Assert.Equal("ux_hcm_applicant_intake_tenant_code_active", MongoApplicantIntakeReadinessMetadataRepository.ActiveCodeUniqueIndexName);
    }

    [Fact]
    public void Runtime_literal_regression_guard_has_no_reserved_identity_literals()
    {
        var reserved = string.Join("-", "CAND", "CAP", "0022");
        var legacy = string.Join("-", "MOD", "0300");
        var runtimeStrings = new[]
        {
            ApplicantIntakeGuard.OwnerKey,
            ApplicantIntakeGuard.ReadPermission,
            ApplicantIntakeGuard.ManagePermission,
            ApplicantIntakeGuard.EvaluatePermission,
            ApplicantIntakeGuard.AuditReadPermission,
            MongoApplicantIntakeReadinessMetadataRepository.CollectionName
        };

        Assert.DoesNotContain(runtimeStrings, value => value.Contains(reserved, StringComparison.Ordinal));
        Assert.DoesNotContain(runtimeStrings, value => value.Contains(legacy, StringComparison.Ordinal));
    }

    private static CreateApplicantIntakeReadinessHandler CreateHandler(
        IApplicantIntakeReadinessMetadataRepository repository,
        Guid tenantId) =>
        new(repository, new FixedTenantContext(tenantId));

    private static ApplicantIntakeCreateRequest ValidRequest(
        string code = "INTAKE-001",
        ApplicantIntakeReadinessState intakeState = ApplicantIntakeReadinessState.Draft,
        string sourceContractVersion = "v1") =>
        new()
        {
            Code = code,
            DisplayName = "Applicant intake readiness",
            IntakeState = intakeState,
            SourceChannelState = ApplicantIntakeReadinessState.Deferred,
            ConsentPreconditionState = ApplicantIntakeReadinessState.Deferred,
            DataMinimizationState = ApplicantIntakeReadinessState.Deferred,
            DuplicateHandlingState = ApplicantIntakeReadinessState.Deferred,
            RetentionPolicyState = ApplicantIntakeReadinessState.Deferred,
            EvidencePolicyState = ApplicantIntakeReadinessState.Deferred,
            ApplicantIdentityBoundaryState = ApplicantIntakeReadinessState.Deferred,
            PublicUxBoundaryState = ApplicantIntakeReadinessState.NotRequired,
            DocumentDependencyState = ApplicantIntakeReadinessState.Deferred,
            NotificationDependencyState = ApplicantIntakeReadinessState.Deferred,
            DependencyStates = new Dictionary<string, ApplicantIntakeReadinessState>
            {
                ["employeeProjection"] = ApplicantIntakeReadinessState.Ready,
                ["sensitiveAccess"] = ApplicantIntakeReadinessState.Ready,
                ["offboardingContext"] = ApplicantIntakeReadinessState.NotRequired
            },
            SourceContractVersion = sourceContractVersion,
            ApplicantIntakeVersion = 1
        };

    private static ApplicantIntakeReadinessMetadata Metadata(
        Guid tenantId,
        string code = "INTAKE-001",
        ApplicantIntakeReadinessState sourceChannelState = ApplicantIntakeReadinessState.Deferred,
        ApplicantIntakeReadinessState consentPreconditionState = ApplicantIntakeReadinessState.Deferred,
        ApplicantIntakeReadinessState dataMinimizationState = ApplicantIntakeReadinessState.Deferred,
        ApplicantIntakeReadinessState duplicateHandlingState = ApplicantIntakeReadinessState.Deferred,
        ApplicantIntakeReadinessState retentionPolicyState = ApplicantIntakeReadinessState.Deferred,
        ApplicantIntakeReadinessState evidencePolicyState = ApplicantIntakeReadinessState.Deferred,
        ApplicantIntakeReadinessState applicantIdentityBoundaryState = ApplicantIntakeReadinessState.Deferred,
        ApplicantIntakeReadinessState publicUxBoundaryState = ApplicantIntakeReadinessState.NotRequired,
        ApplicantIntakeReadinessState documentDependencyState = ApplicantIntakeReadinessState.Deferred,
        ApplicantIntakeReadinessState notificationDependencyState = ApplicantIntakeReadinessState.Deferred) =>
        new()
        {
            TenantId = tenantId,
            Code = code,
            DisplayName = "Applicant intake readiness",
            IntakeState = ApplicantIntakeReadinessState.Draft,
            SourceChannelState = sourceChannelState,
            ConsentPreconditionState = consentPreconditionState,
            DataMinimizationState = dataMinimizationState,
            DuplicateHandlingState = duplicateHandlingState,
            RetentionPolicyState = retentionPolicyState,
            EvidencePolicyState = evidencePolicyState,
            ApplicantIdentityBoundaryState = applicantIdentityBoundaryState,
            PublicUxBoundaryState = publicUxBoundaryState,
            DocumentDependencyState = documentDependencyState,
            NotificationDependencyState = notificationDependencyState,
            DependencyStates = new Dictionary<string, ApplicantIntakeReadinessState>
            {
                ["employeeProjection"] = ApplicantIntakeReadinessState.Ready
            },
            SourceContractVersion = "v1",
            ApplicantIntakeVersion = 1
        };

    private static IReadOnlyDictionary<Guid, ApplicantIntakeReadinessMetadata> RepositoryItems(
        InMemoryApplicantIntakeReadinessMetadataRepository repository)
    {
        var field = typeof(InMemoryApplicantIntakeReadinessMetadataRepository)
            .GetField("_items", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (IReadOnlyDictionary<Guid, ApplicantIntakeReadinessMetadata>)field.GetValue(repository)!;
    }

    private static string PermissionFor(string methodName)
    {
        var method = typeof(ApplicantIntakeController).GetMethod(methodName)!;
        var attribute = method.GetCustomAttribute<HasPermissionAttribute>()!;
        var field = typeof(HasPermissionAttribute).GetField("_permission", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (string)field.GetValue(attribute)!;
    }

    private sealed class FixedTenantContext : ITenantContext
    {
        public FixedTenantContext(Guid? tenantId) => TenantId = tenantId;

        public Guid? TenantId { get; }
    }

    private sealed class InMemoryApplicantIntakeReadinessMetadataRepository : IApplicantIntakeReadinessMetadataRepository
    {
        private readonly Dictionary<Guid, ApplicantIntakeReadinessMetadata> _items;

        public InMemoryApplicantIntakeReadinessMetadataRepository(params ApplicantIntakeReadinessMetadata[] items) =>
            _items = items.ToDictionary(x => x.Id);

        public Task<IReadOnlyList<ApplicantIntakeReadinessMetadata>> ListAsync(Guid tenantId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<ApplicantIntakeReadinessMetadata>>(
                _items.Values.Where(x => x.TenantId == tenantId && !x.IsDeleted).OrderBy(x => x.Code).ToList());

        public Task<ApplicantIntakeReadinessMetadata?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct) =>
            Task.FromResult(_items.Values.FirstOrDefault(x => x.TenantId == tenantId && x.Id == id && !x.IsDeleted));

        public Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct) =>
            Task.FromResult(_items.Values.Any(x => x.TenantId == tenantId && x.Code == code && !x.IsDeleted && x.Id != excludingId));

        public Task CreateAsync(ApplicantIntakeReadinessMetadata metadata, CancellationToken ct)
        {
            _items[metadata.Id] = metadata;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(ApplicantIntakeReadinessMetadata metadata, CancellationToken ct)
        {
            _items[metadata.Id] = metadata;
            return Task.CompletedTask;
        }
    }
}
