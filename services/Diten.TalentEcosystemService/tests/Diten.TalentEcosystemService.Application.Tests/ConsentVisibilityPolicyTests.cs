using System.Reflection;
using System.Security.Claims;
using Diten.TalentEcosystemService.Api.Controllers.Tep;
using Diten.TalentEcosystemService.Api.Security;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.ConsentVisibilityPolicies;
using Diten.TalentEcosystemService.Application.Features.ConsentVisibilityPolicies.Commands;
using Diten.TalentEcosystemService.Application.Features.ConsentVisibilityPolicies.Handlers;
using Diten.TalentEcosystemService.Application.Features.ConsentVisibilityPolicies.Queries;
using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Enums;
using Diten.TalentEcosystemService.Domain.Repositories;
using Diten.TalentEcosystemService.Persistence.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Xunit;

namespace Diten.TalentEcosystemService.Application.Tests;

public sealed class ConsentVisibilityPolicyTests
{
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private static readonly Guid Holding = Guid.Parse("1e9a1000-0000-0000-0000-000000000001");
    private static readonly Guid Medikal = Guid.Parse("1e9a1000-0000-0000-0000-000000000002");
    private static readonly Guid Teknoloji = Guid.Parse("1e9a1000-0000-0000-0000-000000000003");

    private static FixedLegalEntityContext PilotLegalEntityContext() =>
        new(Holding, new[] { Holding, Medikal, Teknoloji });

    [Fact]
    public async Task Create_rejects_duplicate_active_code_in_tenant_scope()
    {
        var repository = new InMemoryConsentVisibilityPolicyRepository();
        var handler = new CreateConsentVisibilityPolicyHandler(repository, new FixedTenantContext(TenantA), PilotLegalEntityContext());
        var request = ValidRequest("POLICY-A");

        var first = await handler.Handle(new CreateConsentVisibilityPolicyCommand(request), CancellationToken.None);
        var second = await handler.Handle(new CreateConsentVisibilityPolicyCommand(request), CancellationToken.None);

        Assert.True(first.IsSuccessful);
        Assert.False(second.IsSuccessful);
        Assert.Equal(409, second.StatusCode);
    }

    [Fact]
    public async Task Get_by_id_is_tenant_scoped_and_returns_404_for_cross_tenant()
    {
        var repository = new InMemoryConsentVisibilityPolicyRepository();
        var create = new CreateConsentVisibilityPolicyHandler(repository, new FixedTenantContext(TenantA), PilotLegalEntityContext());
        var created = await create.Handle(new CreateConsentVisibilityPolicyCommand(ValidRequest("POLICY-TENANT")), CancellationToken.None);

        var crossTenant = new GetConsentVisibilityPolicyByIdHandler(repository, new FixedTenantContext(TenantB), PilotLegalEntityContext());
        var result = await crossTenant.Handle(new(created.Data), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(404, result.StatusCode);
    }

    [Fact]
    public async Task Archive_sets_soft_delete_and_deleted_at()
    {
        var repository = new InMemoryConsentVisibilityPolicyRepository();
        var create = new CreateConsentVisibilityPolicyHandler(repository, new FixedTenantContext(TenantA), PilotLegalEntityContext());
        var created = await create.Handle(new CreateConsentVisibilityPolicyCommand(ValidRequest("POLICY-ARCH")), CancellationToken.None);
        var archive = new ArchiveConsentVisibilityPolicyHandler(repository, new FixedTenantContext(TenantA), PilotLegalEntityContext());

        var result = await archive.Handle(new(created.Data), CancellationToken.None);
        var stored = repository.Items.Single();

        Assert.True(result.IsSuccessful);
        Assert.True(stored.IsDeleted);
        Assert.NotNull(stored.DeletedAt);
        Assert.Equal(TepPolicyState.Archived, stored.PolicyState);
    }

    [Fact]
    public async Task Association_activation_fails_closed_without_approved_policy_metadata()
    {
        var repository = new InMemoryConsentVisibilityPolicyRepository();
        var handler = new CreateConsentVisibilityPolicyHandler(repository, new FixedTenantContext(TenantA), PilotLegalEntityContext());
        var request = ValidRequest("POLICY-BLOCKED") with
        {
            PolicyState = TepPolicyState.Active,
            ConsentRequirementState = TepConsentRequirementState.Required
        };

        var result = await handler.Handle(new CreateConsentVisibilityPolicyCommand(request), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(404, result.StatusCode);
    }

    [Fact]
    public async Task Association_activation_is_allowed_only_with_complete_approved_policy_metadata()
    {
        var repository = new InMemoryConsentVisibilityPolicyRepository();
        var handler = new CreateConsentVisibilityPolicyHandler(repository, new FixedTenantContext(TenantA), PilotLegalEntityContext());

        var result = await handler.Handle(new CreateConsentVisibilityPolicyCommand(ValidRequest("POLICY-ACTIVE") with
        {
            PolicyState = TepPolicyState.Active,
            AssociationConsumptionState = TepAssociationConsumptionState.ActivationApproved
        }), CancellationToken.None);

        Assert.True(result.IsSuccessful);
    }

    [Fact]
    public async Task Policy_unavailable_deferred_behavior_defers_evaluation_but_not_activation()
    {
        var repository = new InMemoryConsentVisibilityPolicyRepository();
        var create = new CreateConsentVisibilityPolicyHandler(repository, new FixedTenantContext(TenantA), PilotLegalEntityContext());
        var created = await create.Handle(new CreateConsentVisibilityPolicyCommand(ValidRequest("POLICY-DEFERRED") with
        {
            PolicyState = TepPolicyState.Deferred,
            PolicyUnavailableBehavior = TepPolicyUnavailableBehavior.DeferredEvaluation,
            DataScopeState = TepDataScopeState.Deferred
        }), CancellationToken.None);

        var evaluate = new EvaluateConsentVisibilityPolicyHandler(repository, new FixedTenantContext(TenantA), PilotLegalEntityContext());
        var result = await evaluate.Handle(new(created.Data, new(false)), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.True(result.Data!.EvaluationDeferred);
        Assert.False(result.Data.ActivationAllowed);
    }

    [Theory]
    [InlineData("raw_payload")]
    [InlineData("provider_payload")]
    [InlineData("credential")]
    [InlineData("preference_center")]
    [InlineData("reference_exchange")]
    public async Task Forbidden_runtime_markers_are_rejected(string marker)
    {
        var repository = new InMemoryConsentVisibilityPolicyRepository();
        var handler = new CreateConsentVisibilityPolicyHandler(repository, new FixedTenantContext(TenantA), PilotLegalEntityContext());
        var request = ValidRequest("POLICY-FORBIDDEN") with { DisplayName = marker };

        var result = await handler.Handle(new CreateConsentVisibilityPolicyCommand(request), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task Audit_metadata_returns_local_deferred_metadata_only()
    {
        var repository = new InMemoryConsentVisibilityPolicyRepository();
        var create = new CreateConsentVisibilityPolicyHandler(repository, new FixedTenantContext(TenantA), PilotLegalEntityContext());
        var created = await create.Handle(new CreateConsentVisibilityPolicyCommand(ValidRequest("POLICY-AUDIT") with
        {
            LocalAuditEvidenceRetentionState = TepLocalAuditEvidenceRetentionState.Deferred
        }), CancellationToken.None);

        var handler = new GetConsentVisibilityPolicyAuditMetadataHandler(repository, new FixedTenantContext(TenantA), PilotLegalEntityContext());
        var result = await handler.Handle(new(created.Data), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.Equal(TepLocalAuditEvidenceRetentionState.Deferred, result.Data!.LocalAuditEvidenceRetentionState);
    }

    [Fact]
    public void Controller_permissions_match_policy_namespace()
    {
        Assert.NotNull(typeof(ConsentVisibilityPoliciesController).GetCustomAttribute<AuthorizeAttribute>());
        AssertPermission(nameof(ConsentVisibilityPoliciesController.GetAll), ConsentVisibilityPolicyPermissions.Read);
        AssertPermission(nameof(ConsentVisibilityPoliciesController.GetById), ConsentVisibilityPolicyPermissions.Read);
        AssertPermission(nameof(ConsentVisibilityPoliciesController.Create), ConsentVisibilityPolicyPermissions.Manage);
        AssertPermission(nameof(ConsentVisibilityPoliciesController.Update), ConsentVisibilityPolicyPermissions.Manage);
        AssertPermission(nameof(ConsentVisibilityPoliciesController.Archive), ConsentVisibilityPolicyPermissions.Manage);
        AssertPermission(nameof(ConsentVisibilityPoliciesController.Evaluate), ConsentVisibilityPolicyPermissions.Evaluate);
        AssertPermission(nameof(ConsentVisibilityPoliciesController.GetAuditMetadata), ConsentVisibilityPolicyPermissions.AuditRead);
    }

    [Fact]
    public async Task Has_permission_attribute_accepts_evaluate_permission_from_scope_claim()
    {
        var attribute = new HasPermissionAttribute(ConsentVisibilityPolicyPermissions.Evaluate);
        var context = AuthorizationContext($"{ConsentVisibilityPolicyPermissions.Read} {ConsentVisibilityPolicyPermissions.Evaluate}", claimType: "scope");

        await attribute.OnAuthorizationAsync(context);

        Assert.Null(context.Result);
    }

    [Fact]
    public async Task Has_permission_attribute_forbids_missing_audit_permission()
    {
        var attribute = new HasPermissionAttribute(ConsentVisibilityPolicyPermissions.AuditRead);
        var context = AuthorizationContext(ConsentVisibilityPolicyPermissions.Read);

        await attribute.OnAuthorizationAsync(context);

        Assert.IsType<ForbidResult>(context.Result);
    }

    [Fact]
    public void Mongo_repository_contract_names_are_tenant_aware()
    {
        Assert.Equal("tep_consent_visibility_policies", MongoTepConsentVisibilityPolicyRepository.CollectionName);
        Assert.Equal("ux_tep_consent_visibility_policies_tenant_code_active", MongoTepConsentVisibilityPolicyRepository.ActiveCodeUniqueIndexName);
        Assert.Equal("ix_tep_consent_visibility_policies_tenant_state", MongoTepConsentVisibilityPolicyRepository.TenantStateIndexName);
    }

    [Fact]
    public async Task LegalEntity_create_stamps_the_selected_legal_entity()
    {
        var repository = new InMemoryConsentVisibilityPolicyRepository();
        var handler = new CreateConsentVisibilityPolicyHandler(
            repository, new FixedTenantContext(TenantA), new FixedLegalEntityContext(Medikal, new[] { Medikal }));

        var created = await handler.Handle(new CreateConsentVisibilityPolicyCommand(ValidRequest("POLICY-LE")), CancellationToken.None);

        Assert.True(created.IsSuccessful);
        Assert.Equal(Medikal, repository.Items.Single().LegalEntityId);
    }

    [Fact]
    public async Task LegalEntity_create_without_a_permitted_selection_is_forbidden()
    {
        var repository = new InMemoryConsentVisibilityPolicyRepository();
        var handler = new CreateConsentVisibilityPolicyHandler(
            repository, new FixedTenantContext(TenantA), new FixedLegalEntityContext(Teknoloji, selectionAllowed: false));

        var response = await handler.Handle(new CreateConsentVisibilityPolicyCommand(ValidRequest("POLICY-403")), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(403, response.StatusCode);
        Assert.Empty(repository.Items);
    }

    private static ConsentVisibilityPolicyRequest ValidRequest(string code) =>
        new(
            code,
            "Consent visibility policy",
            TepPolicyState.Draft,
            TepConsentRequirementState.Approved,
            TepVisibilityScope.AssociationVisible,
            TepDataScopeState.Available,
            TepAccessPolicyState.Approved,
            TepAssociationConsumptionState.Draft,
            TepPolicyUnavailableBehavior.FailClosed,
            "v1",
            [new TepPolicyDependencyStateDto("hcm.sensitive-access", TepShellDependencyStatus.Available, null)],
            TepLocalAuditEvidenceRetentionState.LocalMetadata,
            DateTimeOffset.UtcNow,
            1);

    private static void AssertPermission(string methodName, string expected)
    {
        var method = typeof(ConsentVisibilityPoliciesController)
            .GetMethods()
            .Single(method => method.Name == methodName);
        var attribute = method.GetCustomAttribute<HasPermissionAttribute>()!;
        var field = typeof(HasPermissionAttribute).GetField("_permission", BindingFlags.Instance | BindingFlags.NonPublic)!;

        Assert.Equal(expected, field.GetValue(attribute));
    }

    private static AuthorizationFilterContext AuthorizationContext(
        string permissions,
        bool authenticated = true,
        string claimType = "permissions")
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                authenticated ? [new Claim(claimType, permissions)] : [],
                authenticated ? "test" : null))
        };

        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        return new AuthorizationFilterContext(actionContext, []);
    }

    private sealed class FixedTenantContext : ITenantContext
    {
        public FixedTenantContext(Guid? tenantId) => TenantId = tenantId;
        public Guid? TenantId { get; }
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

    private sealed class InMemoryConsentVisibilityPolicyRepository : ITepConsentVisibilityPolicyRepository
    {
        public List<TepConsentVisibilityPolicy> Items { get; } = [];

        public Task<IReadOnlyList<TepConsentVisibilityPolicy>> ListAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<TepConsentVisibilityPolicy>>(Items
                .Where(item => item.TenantId == tenantId && !item.IsDeleted && legalEntityIds.Contains(item.LegalEntityId))
                .OrderBy(item => item.Code)
                .ToList());

        public Task<TepConsentVisibilityPolicy?> GetByIdAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, Guid id, CancellationToken ct) =>
            Task.FromResult(Items.SingleOrDefault(item => item.TenantId == tenantId && item.Id == id && !item.IsDeleted && legalEntityIds.Contains(item.LegalEntityId)));

        public Task<bool> ExistsActiveCodeAsync(Guid tenantId, Guid legalEntityId, string code, Guid? excludingId, CancellationToken ct) =>
            Task.FromResult(Items.Any(item =>
                item.TenantId == tenantId
                && item.LegalEntityId == legalEntityId
                && !item.IsDeleted
                && string.Equals(item.Code, code, StringComparison.OrdinalIgnoreCase)
                && item.Id != excludingId));

        public Task CreateAsync(TepConsentVisibilityPolicy policy, CancellationToken ct)
        {
            Items.Add(policy);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(TepConsentVisibilityPolicy policy, CancellationToken ct) => Task.CompletedTask;
    }
}
