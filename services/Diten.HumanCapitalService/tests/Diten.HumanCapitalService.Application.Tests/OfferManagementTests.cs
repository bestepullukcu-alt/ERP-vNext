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
        var list = await new GetOfferReadinessListHandler(repository, new FixedTenantContext(tenantId), PilotLegalEntityContext())
            .Handle(new GetOfferReadinessListQuery(), CancellationToken.None);
        var get = await new GetOfferReadinessByIdHandler(repository, new FixedTenantContext(tenantId), PilotLegalEntityContext())
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
            new FixedTenantContext(tenantB), PilotLegalEntityContext());

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
        var handler = new DeleteOfferReadinessHandler(repository, new FixedTenantContext(tenantId), PilotLegalEntityContext());

        var response = await handler.Handle(new DeleteOfferReadinessCommand(metadata.Id), CancellationToken.None);
        var hidden = await repository.GetByIdAsync(tenantId, new[] { Holding }, metadata.Id, CancellationToken.None);
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
        var handler = new EvaluateOfferReadinessHandler(repository, new FixedTenantContext(tenantId), PilotLegalEntityContext());

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
        var handler = new EvaluateOfferReadinessHandler(repository, new FixedTenantContext(tenantId), PilotLegalEntityContext());

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
        var response = await new GetOfferAuditMetadataHandler(repository, new FixedTenantContext(tenantId), PilotLegalEntityContext())
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
            new CreateOfferReadinessCommand(ValidRequest(sourceContractVersion: $"v1 {marker}")),
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
        new(repository, new FixedTenantContext(tenantId == Guid.Empty ? null : tenantId), PilotLegalEntityContext());

    private static CreateOfferReadinessHandler CreateHandler(
        IOfferReadinessMetadataRepository repository,
        Guid tenantId,
        ILegalEntityContext legalEntityContext) =>
        new(repository, new FixedTenantContext(tenantId), legalEntityContext);

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
            LegalEntityId = Holding,
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

    private static readonly Guid Holding = Guid.Parse("1e9a1000-0000-0000-0000-000000000001");
    private static readonly Guid Medikal = Guid.Parse("1e9a1000-0000-0000-0000-000000000002");
    private static readonly Guid Teknoloji = Guid.Parse("1e9a1000-0000-0000-0000-000000000003");

    private static FixedLegalEntityContext PilotLegalEntityContext() =>
        new(Holding, new[] { Holding, Medikal, Teknoloji });

    private static async Task<IReadOnlyList<OfferReadinessListItemDto>> ListWith(
        IOfferReadinessMetadataRepository repository,
        Guid tenantId,
        IReadOnlyCollection<Guid> effective)
    {
        var handler = new GetOfferReadinessListHandler(
            repository,
            new FixedTenantContext(tenantId),
            new FixedLegalEntityContext(effective.First(), effective));
        var response = await handler.Handle(new GetOfferReadinessListQuery(), CancellationToken.None);
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

    private sealed class InMemoryOfferReadinessMetadataRepository : IOfferReadinessMetadataRepository
    {
        private readonly Dictionary<Guid, OfferReadinessMetadata> _items;

        public InMemoryOfferReadinessMetadataRepository(params OfferReadinessMetadata[] items) =>
            _items = items.ToDictionary(x => x.Id);

        public Task<IReadOnlyList<OfferReadinessMetadata>> ListAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<OfferReadinessMetadata>>(
                _items.Values.Where(x => x.TenantId == tenantId && !x.IsDeleted && legalEntityIds.Contains(x.LegalEntityId)).OrderBy(x => x.Code).ToList());

        public Task<OfferReadinessMetadata?> GetByIdAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, Guid id, CancellationToken ct) =>
            Task.FromResult(_items.Values.FirstOrDefault(x => x.TenantId == tenantId && x.Id == id && !x.IsDeleted && legalEntityIds.Contains(x.LegalEntityId)));

        public Task<bool> ExistsActiveCodeAsync(Guid tenantId, Guid legalEntityId, string code, Guid? excludingId, CancellationToken ct) =>
            Task.FromResult(_items.Values.Any(x => x.TenantId == tenantId && x.LegalEntityId == legalEntityId && x.Code == code && !x.IsDeleted && x.Id != excludingId));

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

    [Fact]
    public async Task LegalEntity_create_stamps_the_selected_legal_entity()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryOfferReadinessMetadataRepository();
        var handler = CreateHandler(repository, tenantId, new FixedLegalEntityContext(Medikal, new[] { Medikal }));

        var created = await handler.Handle(new CreateOfferReadinessCommand(ValidRequest()), CancellationToken.None);
        var stored = RepositoryItems(repository)[created.Data];

        Assert.True(created.IsSuccessful);
        Assert.Equal(Medikal, stored.LegalEntityId);
    }

    [Fact]
    public async Task LegalEntity_create_without_a_permitted_selection_is_forbidden()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryOfferReadinessMetadataRepository();
        var handler = CreateHandler(repository, tenantId, new FixedLegalEntityContext(Teknoloji, selectionAllowed: false));

        var response = await handler.Handle(new CreateOfferReadinessCommand(ValidRequest()), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(403, response.StatusCode);
        Assert.Empty(RepositoryItems(repository));
    }

    [Fact]
    public async Task LegalEntity_list_rolls_up_holding_and_isolates_siblings()
    {
        var tenantId = Guid.NewGuid();
        var repository = new InMemoryOfferReadinessMetadataRepository();

        await CreateHandler(repository, tenantId, new FixedLegalEntityContext(Medikal, new[] { Medikal }))
            .Handle(new CreateOfferReadinessCommand(ValidRequest(code: "MED-01")), CancellationToken.None);
        await CreateHandler(repository, tenantId, new FixedLegalEntityContext(Teknoloji, new[] { Teknoloji }))
            .Handle(new CreateOfferReadinessCommand(ValidRequest(code: "TEK-01")), CancellationToken.None);

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
        var repository = new InMemoryOfferReadinessMetadataRepository();
        var medikal = new FixedLegalEntityContext(Medikal, new[] { Medikal });
        var teknoloji = new FixedLegalEntityContext(Teknoloji, new[] { Teknoloji });

        var first = await CreateHandler(repository, tenantId, medikal)
            .Handle(new CreateOfferReadinessCommand(ValidRequest(code: "SHARED-01")), CancellationToken.None);
        var duplicateSameEntity = await CreateHandler(repository, tenantId, medikal)
            .Handle(new CreateOfferReadinessCommand(ValidRequest(code: "SHARED-01")), CancellationToken.None);
        var sameCodeOtherEntity = await CreateHandler(repository, tenantId, teknoloji)
            .Handle(new CreateOfferReadinessCommand(ValidRequest(code: "SHARED-01")), CancellationToken.None);

        Assert.True(first.IsSuccessful);
        Assert.Equal(409, duplicateSameEntity.StatusCode);
        Assert.True(sameCodeOtherEntity.IsSuccessful);
    }
}
