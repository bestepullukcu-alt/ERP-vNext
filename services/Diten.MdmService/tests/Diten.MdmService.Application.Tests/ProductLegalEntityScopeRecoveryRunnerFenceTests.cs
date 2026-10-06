using System.Reflection;
using Diten.MdmService.Api.Services.ProductItemSkuMaster;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diten.MdmService.Application.Tests;

/// <summary>
/// WP-MDM-SCOPE-ACCEPT-01 FIX2 — test gap: the two recovery runners that read the fence themselves
/// (<c>LskuIdentityWorkflowRecoveryRunner</c>, <c>FirstGskuIdentityRetirementRecoveryRunner</c>; FIX1 B3) had no test of that
/// line. Their files belong to the LSKU / GSKU rounds and are not touched here; these tests only drive them. A fence up
/// means: do not recover now (the worker leaves the operation for later / for its maker).
/// </summary>
public sealed class ProductLegalEntityScopeRecoveryRunnerFenceTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly DateTimeOffset At = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task The_first_gsku_retirement_runner_does_not_recover_while_a_fence_is_up()
    {
        var runner = new FirstGskuIdentityRetirementRecoveryRunner(new Operations(), new Rollouts(Rollout(fenced: true)), null!);

        var result = await runner.RunAsync(Guid.NewGuid(), "worker", TimeSpan.FromMinutes(1));

        Assert.False(result.Succeeded);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal("FIRST_GSKU_RETIREMENT_MAKER_REPLAY_REQUIRED", result.ErrorCode);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task The_lsku_identity_runner_recovers_only_without_a_fence(bool fenced, bool allowed)
    {
        var method = typeof(LskuIdentityWorkflowRecoveryRunner).GetMethod(
            "IsBackgroundRecoveryAllowedAsync", BindingFlags.NonPublic | BindingFlags.Static)!;
        var services = new ServiceCollection()
            .AddSingleton<IProductLegalEntityScopeRolloutStateRepository>(new Rollouts(Rollout(fenced)))
            .BuildServiceProvider();

        var answer = await (Task<bool>)method.Invoke(null, [services, Tenant, CancellationToken.None])!;

        Assert.Equal(allowed, answer);
    }

    private static ProductLegalEntityScopeRolloutState Rollout(bool fenced)
    {
        var rollout = ProductLegalEntityScopeRolloutState.CreatePreparation(Tenant, Guid.NewGuid(), Guid.NewGuid(), At);
        if (fenced)
            rollout.ActiveFence = new ProductLegalEntityScopeActivationFence
            {
                Token = new string('D', 64), State = ProductLegalEntityScopeAdmissionState.Closing, Action = "ActivateEnforced",
                CommandId = Guid.NewGuid(), ActorId = Guid.NewGuid(), ReasonCode = "OWNER_APPROVED", AcquiredAtUtc = At
            };
        return rollout;
    }

    private sealed class Rollouts(ProductLegalEntityScopeRolloutState rollout) : IProductLegalEntityScopeRolloutStateRepository
    {
        public Task<ProductLegalEntityScopeRolloutState?> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult<ProductLegalEntityScopeRolloutState?>(rollout);
        public Task<ProductLegalEntityScopeRolloutState?> GetByCreationCommandIdAsync(Guid creationCommandId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ProductLegalEntityScopeRolloutStateWriteResult> CreateAsync(ProductLegalEntityScopeRolloutState state, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ProductLegalEntityScopeRolloutStateWriteResult> UpdateAsync(ProductLegalEntityScopeRolloutState state, int expectedVersion, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    /// <summary>Finds every operation; the runner must stop at the fence before anything else is asked of it.</summary>
    private sealed class Operations : IFirstGskuIdentityRetirementOperationRepository
    {
        public Task<FirstGskuIdentityRetirementOperation?> GetByOperationIdAsync(Guid operationId, CancellationToken cancellationToken = default)
            => Task.FromResult<FirstGskuIdentityRetirementOperation?>(new FirstGskuIdentityRetirementOperation { OperationId = operationId, TenantId = Tenant });
        public Task<FirstGskuIdentityRetirementReserveResult> ReserveAsync(FirstGskuIdentityRetirementOperation operation, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<FirstGskuIdentityRetirementClaim?> TryClaimAsync(FirstGskuIdentityRetirementClaimRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> AdvanceAsync(FirstGskuIdentityRetirementClaim claim, FirstGskuIdentityRetirementMutation mutation, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<FirstGskuIdentityRetirementRecoverablePage> DiscoverRecoverableAsync(long nowUtcTicks, int limit, FirstGskuIdentityRetirementRecoveryCursor? after = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
