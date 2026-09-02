using System.Reflection;
using Diten.HumanCapitalService.Api.Controllers.Hcm;
using Diten.HumanCapitalService.Api.Security;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Application.Features.OfferManagement;
using Diten.HumanCapitalService.Application.Features.OfferManagement.Commands;
using Diten.HumanCapitalService.Application.Features.OfferManagement.Handlers;
using Diten.HumanCapitalService.Application.Features.OfferManagement.Queries;
using Diten.HumanCapitalService.Domain.Entities;
using Diten.HumanCapitalService.Domain.Enums;
using Diten.HumanCapitalService.Domain.Repositories;
using Diten.HumanCapitalService.Persistence.Repositories;
using Xunit;

namespace Diten.HumanCapitalService.Application.Tests;

public sealed class OfferManagementTests
{
    [Fact]
    public async Task Create_list_and_get_metadata_contract()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryOfferReadinessMetadataRepository();
        var create = CreateHandler(repository, tenantId);

        var created = await create.Handle(new CreateOfferReadinessCommand(ValidRequest()), CancellationToken.None);
        var list = await new GetOfferReadinessListHandler(repository, new FixedTenantContext(tenantId))
            .Handle(new GetOfferReadinessListQuery(), CancellationToken.None);
        var get = await new GetOfferReadinessByIdHandler(repository, new FixedTenantContext(tenantId))
            .Handle(new GetOfferReadinessByIdQuery(created.Data), CancellationToken.None);

        Assert.True(created.IsSuccessful);
        Assert.Equal(201, created.StatusCode);
        Assert.True(list.IsSuccessful);
        Assert.Single(list.Data!);
        Assert.True(get.IsSuccessful);
        Assert.Equal("OFFER-001", get.Data!.Code);
        Assert.Equal("Offer readiness", get.Data.DisplayName);
        Assert.Equal(OfferReadinessState.NotRequired, get.Data.OfferWorkflowBoundaryState);
        Assert.Equal(OfferReadinessState.NotRequired, get.Data.ApprovalWorkflowBoundaryState);
        Assert.Equal(OfferReadinessState.NotRequired, get.Data.CandidateAcceptanceBoundaryState);
        Assert.Equal(OfferReadinessState.NotRequired, get.Data.OfferDocumentBoundaryState);
    }

    [Fact]
    public async Task Cross_tenant_lookup_returns_not_found()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var metadata = Metadata(tenantA);
        var handler = new GetOfferReadinessByIdHandler(
            new InMemoryOfferReadinessMetadataRepository(metadata),
            new FixedTenantContext(tenantB));

        var response = await handler.Handle(new GetOfferReadinessByIdQuery(metadata.Id), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Missing_tenant_context_fails_closed()
    {
        var handler = CreateHandler(new InMemoryOfferReadinessMetadataRepository(), Guid.Empty);

        var response = await handler.Handle(new CreateOfferReadinessCommand(ValidRequest()), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(401, response.StatusCode);
    }

    [Fact]
    public async Task Duplicate_active_code_returns_conflict_per_tenant()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryOfferReadinessMetadataRepository();
        var handler = CreateHandler(repository, tenantId);

        var first = await handler.Handle(new CreateOfferReadinessCommand(ValidRequest(code: "offer-001")), CancellationToken.None);
        var second = await handler.Handle(new CreateOfferReadinessCommand(ValidRequest(code: " OFFER-001 ")), CancellationToken.None);

        Assert.True(first.IsSuccessful);
        Assert.False(second.IsSuccessful);
        Assert.Equal(409, second.StatusCode);
    }

    [Fact]
    public async Task Soft_delete_hides_record_and_sets_deleted_at()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(tenantId);
        var repository = new InMemoryOfferReadinessMetadataRepository(metadata);
        var handler = new DeleteOfferReadinessHandler(repository, new FixedTenantContext(tenantId));

        var response = await handler.Handle(new DeleteOfferReadinessCommand(metadata.Id), CancellationToken.None);
        var hidden = await repository.GetByIdAsync(tenantId, metadata.Id, CancellationToken.None);
        var stored = RepositoryItems(repository)[metadata.Id];

        Assert.True(response.IsSuccessful);
        Assert.Equal(204, response.StatusCode);
        Assert.Null(hidden);
        Assert.True(stored.IsDeleted);
        Assert.NotNull(stored.DeletedAt);
        Assert.Equal(OfferReadinessState.Archived, stored.OfferReadinessState);
    }

    [Fact]
    public async Task Ready_state_fails_closed_when_preconditions_are_deferred()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryOfferReadinessMetadataRepository();
        var handler = CreateHandler(repository, tenantId);

        var response = await handler.Handle(
            new CreateOfferReadinessCommand(ValidRequest(offerState: OfferReadinessState.Ready)),
            CancellationToken.None);
        var stored = RepositoryItems(repository)[response.Data];

        Assert.True(response.IsSuccessful);
        Assert.Equal(OfferReadinessState.Deferred, stored.OfferReadinessState);
        Assert.Contains("deferred", stored.DeferredReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Evaluation_promotes_ready_only_when_metadata_preconditions_are_satisfied()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(
            tenantId,
            offerWorkflowBoundaryState: OfferReadinessState.NotRequired,
            approvalWorkflowBoundaryState: OfferReadinessState.NotRequired,
            candidateAcceptanceBoundaryState: OfferReadinessState.NotRequired,
            offerDocumentBoundaryState: OfferReadinessState.NotRequired,
            compensationDataBoundaryState: OfferReadinessState.NotRequired,
            benefitsDataBoundaryState: OfferReadinessState.NotRequired,
            payrollDataBoundaryState: OfferReadinessState.NotRequired,
            consentPreconditionState: OfferReadinessState.Ready,
            dataMinimizationState: OfferReadinessState.Ready,
            retentionPolicyState: OfferReadinessState.Ready,
            evidencePolicyState: OfferReadinessState.Ready,
            notificationDependencyState: OfferReadinessState.NotRequired,
            documentDependencyState: OfferReadinessState.NotRequired);
        var repository = new InMemoryOfferReadinessMetadataRepository(metadata);
        var handler = new EvaluateOfferReadinessHandler(repository, new FixedTenantContext(tenantId));

        var response = await handler.Handle(new EvaluateOfferReadinessCommand(metadata.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(OfferReadinessState.Ready, response.Data!.OfferReadinessState);
        Assert.NotNull(response.Data.LastEvaluatedAt);
    }

    [Fact]
    public async Task Non_activating_evaluation_preserves_deferred_metadata()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(tenantId, consentPreconditionState: OfferReadinessState.Deferred);
        var repository = new InMemoryOfferReadinessMetadataRepository(metadata);
        var handler = new EvaluateOfferReadinessHandler(repository, new FixedTenantContext(tenantId));

        var response = await handler.Handle(new EvaluateOfferReadinessCommand(metadata.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(OfferReadinessState.Deferred, response.Data!.OfferReadinessState);
        Assert.Contains("preconditions", response.Data.DeferredReason);
    }

    [Fact]
    public async Task Audit_metadata_uses_audit_permission_surface()
    {
        var tenantId = Guid.NewGuid();
        var metadata = Metadata(tenantId);
        var repository = new InMemoryOfferReadinessMetadataRepository(metadata);
        var response = await new GetOfferAuditMetadataHandler(repository, new FixedTenantContext(tenantId))
            .Handle(new GetOfferAuditMetadataQuery(metadata.Id), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(metadata.Code, response.Data!.Code);
        Assert.Equal(OfferManagementGuard.AuditReadPermission, PermissionFor(nameof(OfferManagementController.GetAuditMetadata)));
    }

    [Theory]
    [InlineData("offer_letter")]
    [InlineData("compensation_amount")]
    [InlineData("benefits_election")]
    [InlineData("payroll")]
    [InlineData("bank")]
    [InlineData("tax")]
    [InlineData("provider_payload")]
    [InlineData("credential")]
    public async Task Forbidden_offer_compensation_and_sensitive_markers_are_rejected(string marker)
    {
        var tenantId = Guid.NewGuid();
        var handler = CreateHandler(new InMemoryOfferReadinessMetadataRepository(), tenantId);

        var response = await handler.Handle(
            new CreateOfferReadinessCommand(ValidRequest(sourceContractVersion: $"v1_{marker}")),
            CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
    }

    [Fact]
    public void Public_and_persisted_contract_excludes_forbidden_offer_compensation_and_sensitive_fields()
    {
        var forbiddenFragments = new[]
        {
            "LetterBody",
            "Amount",
            "Salary",
            "Wage",
            "Bonus",
            "Election",
            "Bank",
            "Tax",
            "PayrollDetail",
            "Payload",
            "Credential",
            "Token",
            "Secret",
            "Password"
        };
        var contractTypes = new[]
        {
            typeof(OfferReadinessCreateRequest),
            typeof(OfferReadinessDto),
            typeof(OfferReadinessMetadata)
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
        Assert.Null(typeof(OfferReadinessCreateRequest).GetProperty("TenantId"));
    }

    [Fact]
    public void Controller_permissions_match_pack()
    {
        Assert.Equal(OfferManagementGuard.ReadPermission, PermissionFor(nameof(OfferManagementController.GetAll)));
        Assert.Equal(OfferManagementGuard.ReadPermission, PermissionFor(nameof(OfferManagementController.GetById)));
        Assert.Equal(OfferManagementGuard.ManagePermission, PermissionFor(nameof(OfferManagementController.Create)));
        Assert.Equal(OfferManagementGuard.EvaluatePermission, PermissionFor(nameof(OfferManagementController.Evaluate)));
        Assert.Equal(OfferManagementGuard.ManagePermission, PermissionFor(nameof(OfferManagementController.Delete)));
        Assert.Equal(OfferManagementGuard.AuditReadPermission, PermissionFor(nameof(OfferManagementController.GetAuditMetadata)));
    }

    [Fact]
    public void Mongo_repository_contract_names_are_stable()
    {
        Assert.Equal("hcm_offer_readiness", MongoOfferReadinessMetadataRepository.CollectionName);
        Assert.Equal("ux_hcm_offer_management_tenant_code_active", MongoOfferReadinessMetadataRepository.ActiveCodeUniqueIndexName);
    }

    [Fact]
    public void Runtime_literal_regression_guard_has_no_reserved_identity_literals()
    {
        var reserved = string.Join("-", "CAND", "CAP", "0024");
        var legacy = string.Join("-", "MOD", "0302");
        var runtimeStrings = new[]
        {
            OfferManagementGuard.OwnerKey,
            OfferManagementGuard.ReadPermission,
            OfferManagementGuard.ManagePermission,
            OfferManagementGuard.EvaluatePermission,
            OfferManagementGuard.AuditReadPermission,
            MongoOfferReadinessMetadataRepository.CollectionName
        };

        Assert.DoesNotContain(runtimeStrings, value => value.Contains(reserved, StringComparison.Ordinal));
        Assert.DoesNotContain(runtimeStrings, value => value.Contains(legacy, StringComparison.Ordinal));
    }

    private static CreateOfferReadinessHandler CreateHandler(
        IOfferReadinessMetadataRepository repository,
        Guid tenantId) =>
        new(repository, new FixedTenantContext(tenantId == Guid.Empty ? null : tenantId));

    private static OfferReadinessCreateRequest ValidRequest(
        string code = "OFFER-001",
        OfferReadinessState offerState = OfferReadinessState.Draft,
        string sourceContractVersion = "v1") =>
        new()
        {
            Code = code,
            DisplayName = "Offer readiness",
            OfferReadinessState = offerState,
            OfferWorkflowBoundaryState = OfferReadinessState.NotRequired,
            ApprovalWorkflowBoundaryState = OfferReadinessState.NotRequired,
            CandidateAcceptanceBoundaryState = OfferReadinessState.NotRequired,
            OfferDocumentBoundaryState = OfferReadinessState.NotRequired,
            CompensationDataBoundaryState = OfferReadinessState.Deferred,
            BenefitsDataBoundaryState = OfferReadinessState.Deferred,
            PayrollDataBoundaryState = OfferReadinessState.Deferred,
            ConsentPreconditionState = OfferReadinessState.Deferred,
            DataMinimizationState = OfferReadinessState.Deferred,
            RetentionPolicyState = OfferReadinessState.Deferred,
            EvidencePolicyState = OfferReadinessState.Deferred,
            NotificationDependencyState = OfferReadinessState.NotRequired,
            DocumentDependencyState = OfferReadinessState.NotRequired,
            DependencyStates = new Dictionary<string, OfferReadinessState>
            {
                ["employeeProjection"] = OfferReadinessState.Ready,
                ["sensitiveAccess"] = OfferReadinessState.Ready,
                ["candidatePipeline"] = OfferReadinessState.Ready
            },
            SourceContractVersion = sourceContractVersion,
            OfferReadinessVersion = 1
        };

    private static OfferReadinessMetadata Metadata(
        Guid tenantId,
        string code = "OFFER-001",
        OfferReadinessState offerWorkflowBoundaryState = OfferReadinessState.NotRequired,
        OfferReadinessState approvalWorkflowBoundaryState = OfferReadinessState.NotRequired,
        OfferReadinessState candidateAcceptanceBoundaryState = OfferReadinessState.NotRequired,
        OfferReadinessState offerDocumentBoundaryState = OfferReadinessState.NotRequired,
        OfferReadinessState compensationDataBoundaryState = OfferReadinessState.Deferred,
        OfferReadinessState benefitsDataBoundaryState = OfferReadinessState.Deferred,
        OfferReadinessState payrollDataBoundaryState = OfferReadinessState.Deferred,
        OfferReadinessState consentPreconditionState = OfferReadinessState.Deferred,
        OfferReadinessState dataMinimizationState = OfferReadinessState.Deferred,
        OfferReadinessState retentionPolicyState = OfferReadinessState.Deferred,
        OfferReadinessState evidencePolicyState = OfferReadinessState.Deferred,
        OfferReadinessState notificationDependencyState = OfferReadinessState.NotRequired,
        OfferReadinessState documentDependencyState = OfferReadinessState.NotRequired) =>
        new()
        {
            TenantId = tenantId,
            Code = code,
            DisplayName = "Offer readiness",
            OfferReadinessState = OfferReadinessState.Draft,
            OfferWorkflowBoundaryState = offerWorkflowBoundaryState,
            ApprovalWorkflowBoundaryState = approvalWorkflowBoundaryState,
            CandidateAcceptanceBoundaryState = candidateAcceptanceBoundaryState,
            OfferDocumentBoundaryState = offerDocumentBoundaryState,
            CompensationDataBoundaryState = compensationDataBoundaryState,
            BenefitsDataBoundaryState = benefitsDataBoundaryState,
            PayrollDataBoundaryState = payrollDataBoundaryState,
            ConsentPreconditionState = consentPreconditionState,
            DataMinimizationState = dataMinimizationState,
            RetentionPolicyState = retentionPolicyState,
            EvidencePolicyState = evidencePolicyState,
            NotificationDependencyState = notificationDependencyState,
            DocumentDependencyState = documentDependencyState,
            DependencyStates = new Dictionary<string, OfferReadinessState>
            {
                ["candidatePipeline"] = OfferReadinessState.Ready
            },
            SourceContractVersion = "v1",
            OfferReadinessVersion = 1
        };

    private static IReadOnlyDictionary<Guid, OfferReadinessMetadata> RepositoryItems(
        InMemoryOfferReadinessMetadataRepository repository)
    {
        var field = typeof(InMemoryOfferReadinessMetadataRepository)
            .GetField("_items", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (IReadOnlyDictionary<Guid, OfferReadinessMetadata>)field.GetValue(repository)!;
    }

    private static string PermissionFor(string methodName)
    {
        var method = typeof(OfferManagementController).GetMethod(methodName)!;
        var attribute = method.GetCustomAttribute<HasPermissionAttribute>()!;
        var field = typeof(HasPermissionAttribute).GetField("_permission", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (string)field.GetValue(attribute)!;
    }

    private sealed class FixedTenantContext : ITenantContext
    {
        public FixedTenantContext(Guid? tenantId) => TenantId = tenantId;

        public Guid? TenantId { get; }
    }

    private sealed class InMemoryOfferReadinessMetadataRepository : IOfferReadinessMetadataRepository
    {
        private readonly Dictionary<Guid, OfferReadinessMetadata> _items;

        public InMemoryOfferReadinessMetadataRepository(params OfferReadinessMetadata[] items) =>
            _items = items.ToDictionary(x => x.Id);

        public Task<IReadOnlyList<OfferReadinessMetadata>> ListAsync(Guid tenantId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<OfferReadinessMetadata>>(
                _items.Values.Where(x => x.TenantId == tenantId && !x.IsDeleted).OrderBy(x => x.Code).ToList());

        public Task<OfferReadinessMetadata?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct) =>
            Task.FromResult(_items.Values.FirstOrDefault(x => x.TenantId == tenantId && x.Id == id && !x.IsDeleted));

        public Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct) =>
            Task.FromResult(_items.Values.Any(x => x.TenantId == tenantId && x.Code == code && !x.IsDeleted && x.Id != excludingId));

        public Task CreateAsync(OfferReadinessMetadata metadata, CancellationToken ct)
        {
            _items[metadata.Id] = metadata;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(OfferReadinessMetadata metadata, CancellationToken ct)
        {
            _items[metadata.Id] = metadata;
            return Task.CompletedTask;
        }
    }
}
