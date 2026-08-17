using System.Reflection;
using System.Security.Claims;
using Diten.TalentEcosystemService.Api.Controllers.Tep;
using Diten.TalentEcosystemService.Api.Security;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.AssociationMemberships;
using Diten.TalentEcosystemService.Application.Features.AssociationMemberships.Commands;
using Diten.TalentEcosystemService.Application.Features.AssociationMemberships.Handlers;
using Diten.TalentEcosystemService.Application.Features.AssociationMemberships.Queries;
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

public sealed class AssociationMembershipTests
{
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Fact]
    public async Task Create_rejects_duplicate_active_code_in_tenant_scope()
    {
        var registry = new InMemoryAssociationMembershipRegistryRepository();
        var policy = new InMemoryConsentVisibilityPolicyRepository();
        var handler = new CreateAssociationMembershipHandler(registry, policy, new FixedTenantContext(TenantA));
        var request = ValidRequest("ASSOC-A");

        var first = await handler.Handle(new CreateAssociationMembershipCommand(request), CancellationToken.None);
        var second = await handler.Handle(new CreateAssociationMembershipCommand(request), CancellationToken.None);

        Assert.True(first.IsSuccessful);
        Assert.False(second.IsSuccessful);
        Assert.Equal(409, second.StatusCode);
    }

    [Fact]
    public async Task Get_by_id_is_tenant_scoped_and_returns_404_for_cross_tenant()
    {
        var registry = new InMemoryAssociationMembershipRegistryRepository();
        var policy = new InMemoryConsentVisibilityPolicyRepository();
        var create = new CreateAssociationMembershipHandler(registry, policy, new FixedTenantContext(TenantA));
        var created = await create.Handle(new CreateAssociationMembershipCommand(ValidRequest("ASSOC-TENANT")), CancellationToken.None);

        var crossTenant = new GetAssociationMembershipByIdHandler(registry, new FixedTenantContext(TenantB));
        var result = await crossTenant.Handle(new(created.Data), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(404, result.StatusCode);
    }

    [Fact]
    public async Task Archive_sets_soft_delete_and_deleted_at()
    {
        var registry = new InMemoryAssociationMembershipRegistryRepository();
        var policy = new InMemoryConsentVisibilityPolicyRepository();
        var create = new CreateAssociationMembershipHandler(registry, policy, new FixedTenantContext(TenantA));
        var created = await create.Handle(new CreateAssociationMembershipCommand(ValidRequest("ASSOC-ARCH")), CancellationToken.None);
        var archive = new ArchiveAssociationMembershipHandler(registry, new FixedTenantContext(TenantA));

        var result = await archive.Handle(new(created.Data), CancellationToken.None);
        var stored = registry.Items.Single();

        Assert.True(result.IsSuccessful);
        Assert.True(stored.IsDeleted);
        Assert.NotNull(stored.DeletedAt);
        Assert.Equal(TepAssociationMembershipState.Archived, stored.AssociationMembershipState);
    }

    [Fact]
    public async Task Activation_fails_closed_without_policy_precondition()
    {
        var registry = new InMemoryAssociationMembershipRegistryRepository();
        var policy = new InMemoryConsentVisibilityPolicyRepository();
        var handler = new CreateAssociationMembershipHandler(registry, policy, new FixedTenantContext(TenantA));
        var request = ValidRequest("ASSOC-BLOCKED") with
        {
            AssociationMembershipState = TepAssociationMembershipState.Active,
            AssociationActivationState = TepAssociationActivationState.Active,
            ConsentVisibilityPolicyId = null
        };

        var result = await handler.Handle(new CreateAssociationMembershipCommand(request), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(404, result.StatusCode);
    }

    [Fact]
    public async Task Activation_requires_same_tenant_approved_policy()
    {
        var registry = new InMemoryAssociationMembershipRegistryRepository();
        var policy = new InMemoryConsentVisibilityPolicyRepository();
        var otherTenantPolicy = ApprovedPolicy(TenantB);
        await policy.CreateAsync(otherTenantPolicy, CancellationToken.None);
        var handler = new CreateAssociationMembershipHandler(registry, policy, new FixedTenantContext(TenantA));

        var result = await handler.Handle(new CreateAssociationMembershipCommand(ActivationRequest("ASSOC-CROSS", otherTenantPolicy.Id)), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(404, result.StatusCode);
    }

    [Fact]
    public async Task Activation_succeeds_with_same_tenant_approved_policy()
    {
        var registry = new InMemoryAssociationMembershipRegistryRepository();
        var policy = new InMemoryConsentVisibilityPolicyRepository();
        var sameTenantPolicy = ApprovedPolicy(TenantA);
        await policy.CreateAsync(sameTenantPolicy, CancellationToken.None);
        var handler = new CreateAssociationMembershipHandler(registry, policy, new FixedTenantContext(TenantA));

        var result = await handler.Handle(new CreateAssociationMembershipCommand(ActivationRequest("ASSOC-ACTIVE", sameTenantPolicy.Id)), CancellationToken.None);

        Assert.True(result.IsSuccessful);
    }

    [Fact]
    public async Task Evaluation_without_available_policy_returns_explicit_deferred_metadata()
    {
        var registry = new InMemoryAssociationMembershipRegistryRepository();
        var policy = new InMemoryConsentVisibilityPolicyRepository();
        var create = new CreateAssociationMembershipHandler(registry, policy, new FixedTenantContext(TenantA));
        var created = await create.Handle(new CreateAssociationMembershipCommand(ValidRequest("ASSOC-DEFER")), CancellationToken.None);
        var evaluate = new EvaluateAssociationMembershipHandler(registry, policy, new FixedTenantContext(TenantA));

        var result = await evaluate.Handle(new(created.Data, new(false)), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.True(result.Data!.EvaluationDeferred);
        Assert.False(result.Data.ActivationAllowed);
        Assert.Equal(TepPolicyEvaluationState.Deferred, registry.Items.Single().PolicyEvaluationState);
    }

    [Fact]
    public async Task Evaluation_activation_request_fails_closed_without_policy_approval()
    {
        var registry = new InMemoryAssociationMembershipRegistryRepository();
        var policy = new InMemoryConsentVisibilityPolicyRepository();
        var create = new CreateAssociationMembershipHandler(registry, policy, new FixedTenantContext(TenantA));
        var created = await create.Handle(new CreateAssociationMembershipCommand(ValidRequest("ASSOC-EVAL-BLOCK")), CancellationToken.None);
        var evaluate = new EvaluateAssociationMembershipHandler(registry, policy, new FixedTenantContext(TenantA));

        var result = await evaluate.Handle(new(created.Data, new(true)), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(404, result.StatusCode);
    }

    [Fact]
    public async Task Member_company_path_updates_only_member_company_metadata()
    {
        var registry = new InMemoryAssociationMembershipRegistryRepository();
        var policy = new InMemoryConsentVisibilityPolicyRepository();
        var create = new CreateAssociationMembershipHandler(registry, policy, new FixedTenantContext(TenantA));
        var created = await create.Handle(new CreateAssociationMembershipCommand(ValidRequest("ASSOC-MEMBER")), CancellationToken.None);
        var entity = registry.Items.Single();
        var originalCode = entity.Code;
        var originalActivation = entity.AssociationActivationState;
        var handler = new UpdateAssociationMemberCompanyHandler(registry, new FixedTenantContext(TenantA));

        var result = await handler.Handle(new(created.Data, new(TepMemberCompanyState.Verified, "member-company-updated", "v2")), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.Equal(originalCode, entity.Code);
        Assert.Equal(originalActivation, entity.AssociationActivationState);
        Assert.Equal("member-company-updated", entity.MemberCompanyReference);
        Assert.Equal("v2", entity.SourceContractVersion);
    }

    [Theory]
    [InlineData("raw_payload")]
    [InlineData("candidate_identity")]
    [InlineData("reference_exchange")]
    [InlineData("reputation")]
    [InlineData("pii_heavy")]
    public async Task Forbidden_runtime_markers_are_rejected(string marker)
    {
        var registry = new InMemoryAssociationMembershipRegistryRepository();
        var policy = new InMemoryConsentVisibilityPolicyRepository();
        var handler = new CreateAssociationMembershipHandler(registry, policy, new FixedTenantContext(TenantA));
        var request = ValidRequest("ASSOC-FORBIDDEN") with { DisplayName = marker };

        var result = await handler.Handle(new CreateAssociationMembershipCommand(request), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public void Controller_permissions_match_association_namespace()
    {
        Assert.NotNull(typeof(AssociationMembershipsController).GetCustomAttribute<AuthorizeAttribute>());
        AssertPermission(nameof(AssociationMembershipsController.GetAll), AssociationMembershipPermissions.Read);
        AssertPermission(nameof(AssociationMembershipsController.GetById), AssociationMembershipPermissions.Read);
        AssertPermission(nameof(AssociationMembershipsController.Create), AssociationMembershipPermissions.Manage);
        AssertPermission(nameof(AssociationMembershipsController.Update), AssociationMembershipPermissions.Manage);
        AssertPermission(nameof(AssociationMembershipsController.Archive), AssociationMembershipPermissions.Archive);
        AssertPermission(nameof(AssociationMembershipsController.Evaluate), AssociationMembershipPermissions.Evaluate);
        AssertPermission(nameof(AssociationMembershipsController.UpdateMemberCompany), AssociationMembershipPermissions.MemberCompanyManage);
    }

    [Fact]
    public async Task Has_permission_attribute_accepts_member_company_permission_from_comma_claim()
    {
        var attribute = new HasPermissionAttribute(AssociationMembershipPermissions.MemberCompanyManage);
        var context = AuthorizationContext($"{AssociationMembershipPermissions.Read},{AssociationMembershipPermissions.MemberCompanyManage}");

        await attribute.OnAuthorizationAsync(context);

        Assert.Null(context.Result);
    }

    [Fact]
    public void Mongo_repository_contract_names_are_tenant_aware()
    {
        Assert.Equal("tep_association_membership_registry", MongoTepAssociationMembershipRegistryRepository.CollectionName);
        Assert.Equal("ux_tep_association_memberships_tenant_code_active", MongoTepAssociationMembershipRegistryRepository.ActiveCodeUniqueIndexName);
        Assert.Equal("ix_tep_association_memberships_tenant_state", MongoTepAssociationMembershipRegistryRepository.TenantStateIndexName);
    }

    private static AssociationMembershipRegistryRequest ValidRequest(string code) =>
        new(
            code,
            "Association membership",
            TepAssociationMembershipState.Draft,
            TepMemberCompanyState.Draft,
            "member-company-ref",
            "hcm-foundation-ref",
            null,
            TepPolicyEvaluationState.NotEvaluated,
            TepVisibilityApprovalState.Pending,
            TepAssociationActivationState.Draft,
            [new TepAssociationDependencyStateDto("hcm.employee-projections", TepShellDependencyStatus.Available, null)],
            "v1",
            DateTimeOffset.UtcNow,
            1);

    private static AssociationMembershipRegistryRequest ActivationRequest(string code, Guid policyId) =>
        ValidRequest(code) with
        {
            AssociationMembershipState = TepAssociationMembershipState.Active,
            MemberCompanyState = TepMemberCompanyState.Verified,
            ConsentVisibilityPolicyId = policyId,
            PolicyEvaluationState = TepPolicyEvaluationState.Approved,
            VisibilityApprovalState = TepVisibilityApprovalState.Approved,
            AssociationActivationState = TepAssociationActivationState.Active
        };

    private static TepConsentVisibilityPolicy ApprovedPolicy(Guid tenantId) =>
        new()
        {
            TenantId = tenantId,
            Code = $"POLICY-{tenantId.ToString()[..8]}",
            DisplayName = "Consent visibility policy",
            PolicyState = TepPolicyState.Active,
            ConsentRequirementState = TepConsentRequirementState.Approved,
            VisibilityScope = TepVisibilityScope.AssociationVisible,
            DataScopeState = TepDataScopeState.Available,
            AccessPolicyState = TepAccessPolicyState.Approved,
            AssociationConsumptionState = TepAssociationConsumptionState.ActivationApproved,
            PolicyUnavailableBehavior = TepPolicyUnavailableBehavior.FailClosed,
            SourceContractVersion = "v1",
            LocalAuditEvidenceRetentionState = TepLocalAuditEvidenceRetentionState.LocalMetadata,
            PolicyVersion = 1
        };

    private static void AssertPermission(string methodName, string expected)
    {
        var method = typeof(AssociationMembershipsController)
            .GetMethods()
            .Single(method => method.Name == methodName);
        var attribute = method.GetCustomAttribute<HasPermissionAttribute>()!;
        var field = typeof(HasPermissionAttribute).GetField("_permission", BindingFlags.Instance | BindingFlags.NonPublic)!;

        Assert.Equal(expected, field.GetValue(attribute));
    }

    private static AuthorizationFilterContext AuthorizationContext(string permissions)
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("permissions", permissions)], "test"))
        };

        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        return new AuthorizationFilterContext(actionContext, []);
    }

    private sealed class FixedTenantContext : ITenantContext
    {
        public FixedTenantContext(Guid? tenantId) => TenantId = tenantId;
        public Guid? TenantId { get; }
    }

    private sealed class InMemoryAssociationMembershipRegistryRepository : ITepAssociationMembershipRegistryRepository
    {
        public List<TepAssociationMembershipRegistry> Items { get; } = [];

        public Task<IReadOnlyList<TepAssociationMembershipRegistry>> ListAsync(Guid tenantId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<TepAssociationMembershipRegistry>>(Items
                .Where(item => item.TenantId == tenantId && !item.IsDeleted)
                .OrderBy(item => item.Code)
                .ToList());

        public Task<TepAssociationMembershipRegistry?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct) =>
            Task.FromResult(Items.SingleOrDefault(item => item.TenantId == tenantId && item.Id == id && !item.IsDeleted));

        public Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct) =>
            Task.FromResult(Items.Any(item =>
                item.TenantId == tenantId
                && !item.IsDeleted
                && string.Equals(item.Code, code, StringComparison.OrdinalIgnoreCase)
                && item.Id != excludingId));

        public Task CreateAsync(TepAssociationMembershipRegistry registry, CancellationToken ct)
        {
            Items.Add(registry);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(TepAssociationMembershipRegistry registry, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class InMemoryConsentVisibilityPolicyRepository : ITepConsentVisibilityPolicyRepository
    {
        public List<TepConsentVisibilityPolicy> Items { get; } = [];

        public Task<IReadOnlyList<TepConsentVisibilityPolicy>> ListAsync(Guid tenantId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<TepConsentVisibilityPolicy>>(Items
                .Where(item => item.TenantId == tenantId && !item.IsDeleted)
                .OrderBy(item => item.Code)
                .ToList());

        public Task<TepConsentVisibilityPolicy?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct) =>
            Task.FromResult(Items.SingleOrDefault(item => item.TenantId == tenantId && item.Id == id && !item.IsDeleted));

        public Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct) =>
            Task.FromResult(Items.Any(item =>
                item.TenantId == tenantId
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
