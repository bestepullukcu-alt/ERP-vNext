using System.Reflection;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Xunit;
using Mode = Diten.MdmService.Application.Features.ProductLegalEntityScopes.ProductIdentityBackgroundScope.TenantMode;

namespace Diten.MdmService.Application.Tests;

/// <summary>
/// The worker scope rule (S1, WP-MDM-GSKU-ACCEPT-01 — "RUN" in the review's list of untested rules): which product
/// identity operations a recovery worker may carry on in a tenant with a legal-entity scope rollout. The HTTP host
/// test B6 runs the same rule through the real GSKU runner and Mongo.
/// </summary>
public sealed class ProductIdentityBackgroundScopeTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid Product = Guid.NewGuid();

    [Fact]
    public async Task RUN_A_tenant_without_a_rollout_recovers_everything()
    {
        var scope = Scope(rollout: null);

        Assert.Equal(Mode.Everything, await scope.ResolveAsync(default));
        Assert.True(await scope.InScopeAsync(Mode.Everything, Guid.NewGuid(), default));
    }

    [Theory]
    [InlineData(ProductLegalEntityScopeRolloutMode.Preparation, Mode.Everything)]
    [InlineData(ProductLegalEntityScopeRolloutMode.Enforced, Mode.PerProduct)]
    [InlineData(ProductLegalEntityScopeRolloutMode.FailClosedSuspended, Mode.Nothing)]
    public async Task RUN_The_rollout_mode_decides_the_tenant_mode(ProductLegalEntityScopeRolloutMode rolloutMode, Mode expected)
    {
        Assert.Equal(expected, await Scope(Rollout(rolloutMode)).ResolveAsync(default));
    }

    // Before FIX1 an Enforced tenant got no background recovery at all (GP included): a decision taken in the Task
    // Center never reached MDM there. Now an operation runs when its product is in force, and only then.
    [Fact]
    public async Task RUN_In_an_enforced_tenant_only_a_product_with_a_policy_in_force_is_recovered()
    {
        var policy = ProductLegalEntityScopePolicy.Create(TenantId, Product, Guid.NewGuid(),
            ProductLegalEntityScopeMode.GroupWide, [], Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(-1));
        var scope = Scope(Rollout(ProductLegalEntityScopeRolloutMode.Enforced), policy);

        Assert.True(await scope.InScopeAsync(Mode.PerProduct, Product, default));
        Assert.False(await scope.InScopeAsync(Mode.PerProduct, Guid.NewGuid(), default));
        Assert.False(await scope.InScopeAsync(Mode.Nothing, Product, default));
    }

    [Fact]
    public async Task RUN_A_rollout_that_cannot_be_read_stops_the_worker_for_the_tenant()
    {
        var scope = Scope(rollout: null, rolloutThrows: true);

        Assert.Equal(Mode.Nothing, await scope.ResolveAsync(default));
    }

    private static ProductLegalEntityScopeRolloutState Rollout(ProductLegalEntityScopeRolloutMode mode)
    {
        var rollout = ProductLegalEntityScopeRolloutState.CreatePreparation(TenantId, Guid.NewGuid(), Guid.NewGuid(),
            DateTimeOffset.UtcNow.AddMinutes(-1));
        rollout.Mode = mode;
        return rollout;
    }

    private static ProductIdentityBackgroundScope Scope(
        ProductLegalEntityScopeRolloutState? rollout, ProductLegalEntityScopePolicy? policy = null, bool rolloutThrows = false)
    {
        var rollouts = DispatchProxy.Create<IProductLegalEntityScopeRolloutStateRepository,
            FirstGskuIdentityWorkflowProcessorTests.RepositoryProxy>();
        ((FirstGskuIdentityWorkflowProcessorTests.RepositoryProxy)(object)rollouts).Handler = (method, _) =>
            method.Name == "GetAsync"
                ? rolloutThrows
                    ? Task.FromException<ProductLegalEntityScopeRolloutState?>(new InvalidOperationException("unreadable"))
                    : Task.FromResult(rollout)
                : throw new NotSupportedException(method.Name);
        var policies = DispatchProxy.Create<IProductLegalEntityScopePolicyRepository,
            FirstGskuIdentityWorkflowProcessorTests.RepositoryProxy>();
        ((FirstGskuIdentityWorkflowProcessorTests.RepositoryProxy)(object)policies).Handler = (method, args) =>
            method.Name == "GetByGlobalProductIdAsync"
                ? Task.FromResult(policy is not null && (Guid)args![0]! == policy.GlobalProductId ? policy : null)
                : throw new NotSupportedException(method.Name);
        return new(rollouts, policies, new Tenant(), TimeProvider.System);
    }

    private sealed class Tenant : ITenantContext
    {
        public Guid TenantId { get; private set; } = ProductIdentityBackgroundScopeTests.TenantId;
        public bool IsResolved => true;
        public void SetTenant(Guid tenantId) => TenantId = tenantId;
    }
}
