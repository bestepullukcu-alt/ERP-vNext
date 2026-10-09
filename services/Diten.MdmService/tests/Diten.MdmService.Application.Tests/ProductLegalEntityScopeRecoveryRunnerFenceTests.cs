using System.Reflection;
using Diten.MdmService.Api.Services.ProductItemSkuMaster;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diten.MdmService.Application.Tests;

/// <summary>
/// WP-MDM-SCOPE-ACCEPT-01 FIX2 / WP-MDM-SCOPE-INT-01 (1) — the recovery runners that must not recover while a fence is up:
/// the first-GSKU retirement runner (its own fence check) and both LSKU runners (through LskuBackgroundScope →
/// ProductIdentityBackgroundScope, since LSKU FIX1 B4). Their files belong to the GSKU / LSKU rounds and are not touched
/// here; these tests only drive them. A fence up means: do not recover now (the worker leaves the operation for later).
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

    /// <summary>
    /// WP-MDM-SCOPE-INT-01 (1) — measured on the PRODUCTION path after LSKU FIX1 B4: the LSKU runners ask
    /// <c>LskuBackgroundScope</c>, which asks <c>ProductIdentityBackgroundScope</c> (FIX1 B3: a fence up → TenantMode.Nothing).
    /// The real runner is driven over one tenant and one recoverable operation: with a fence up the operation is deferred
    /// and never reaches the processor; in Preparation without a fence it does (the processor here is an empty shell, so
    /// reaching it is counted as a failed attempt — what is measured is that recovery was ATTEMPTED).
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task The_lsku_identity_runner_recovers_only_without_a_fence(bool fenced)
    {
        var lskuOperation = new LskuIdentityWorkflowOperation { OperationId = Guid.NewGuid(), TenantId = Tenant, GskuId = Guid.NewGuid() };
        using var provider = Services(fenced, services =>
        {
            services.AddScoped<ILskuIdentityWorkflowTenantPartitionDiscovery>(_ => Fake<ILskuIdentityWorkflowTenantPartitionDiscovery>(
                ("DiscoverAsync", _ => Task.FromResult(new LskuIdentityWorkflowTenantPartitionPage([Tenant], null)))));
            services.AddScoped<ILskuIdentityWorkflowOperationRepository>(_ => Fake<ILskuIdentityWorkflowOperationRepository>(
                ("DiscoverRecoverableAsync", _ => Task.FromResult(new LskuIdentityWorkflowRecoverablePage([lskuOperation], null)))));
            services.AddScoped(_ => (LskuIdentityWorkflowProcessor)System.Runtime.CompilerServices.RuntimeHelpers
                .GetUninitializedObject(typeof(LskuIdentityWorkflowProcessor)));
        });
        var runner = new LskuIdentityWorkflowRecoveryRunner(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Microsoft.Extensions.Options.Options.Create(new Diten.MdmService.Api.Configuration.LskuIdentityWorkflowWorkerOptions
            {
                Enabled = true, LeaseOwner = "lsku-fence-test"
            }),
            TimeProvider.System,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<LskuIdentityWorkflowRecoveryRunner>.Instance);

        var result = await runner.RunCycleAsync();

        Assert.Equal(1, result.OperationCount);
        if (fenced)
        {
            Assert.Equal(1, result.DeferredCount);
            Assert.Equal(0, result.CompletedCount + result.FailedCount);
        }
        else
        {
            Assert.Equal(0, result.DeferredCount);
            Assert.Equal(1, result.CompletedCount + result.FailedCount);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task The_lsku_retirement_runner_recovers_only_without_a_fence(bool fenced)
    {
        var retirement = new LskuRetirementRequestOperation { OperationId = Guid.NewGuid(), TenantId = Tenant, LskuId = Guid.NewGuid() };
        using var provider = Services(fenced, services =>
        {
            services.AddScoped<ILskuRetirementRequestOperationRepository>(_ => Fake<ILskuRetirementRequestOperationRepository>(
                ("DiscoverTenantPartitionsAsync", _ => Task.FromResult(new GlobalProductIdentityWorkflowTenantPartitionPage([Tenant], null))),
                ("DiscoverRecoverableAsync", _ => Task.FromResult(new LskuRetirementRequestPage([retirement], null)))));
            services.AddScoped(_ => (LskuRetirementRequestWorkflowProcessor)System.Runtime.CompilerServices.RuntimeHelpers
                .GetUninitializedObject(typeof(LskuRetirementRequestWorkflowProcessor)));
        });
        var runner = new LskuRetirementRequestRecoveryRunner(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Microsoft.Extensions.Options.Options.Create(new Diten.MdmService.Api.Configuration.LskuRetirementRequestWorkflowWorkerOptions { Enabled = true }),
            TimeProvider.System,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<LskuRetirementRequestRecoveryRunner>.Instance);

        var result = await runner.RunCycleAsync();

        Assert.Equal(1, result.OperationCount);
        if (fenced)
        {
            Assert.Equal(1, result.DeferredCount);
            Assert.Equal(0, result.CompletedCount + result.FailedCount);
        }
        else
        {
            Assert.Equal(0, result.DeferredCount);
            Assert.Equal(1, result.CompletedCount + result.FailedCount);
        }
    }

    /// <summary>The production scope chain (ProductIdentityBackgroundScope → LskuBackgroundScope) over this rollout.</summary>
    private static ServiceProvider Services(bool fenced, Action<ServiceCollection> add)
    {
        var services = new ServiceCollection();
        services.AddScoped<Diten.MdmService.Application.Common.ITenantContext, Diten.MdmService.Application.Common.TenantContext>();
        services.AddSingleton<IProductLegalEntityScopeRolloutStateRepository>(new Rollouts(Rollout(fenced)));
        services.AddSingleton(Fake<IProductLegalEntityScopePolicyRepository>());
        services.AddSingleton(Fake<ILskuRepository>());
        services.AddSingleton(Fake<IGskuRepository>());
        services.AddSingleton(Fake<IProductDefinitionRevisionRepository>());
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<Diten.MdmService.Application.Features.ProductLegalEntityScopes.ProductIdentityBackgroundScope>();
        services.AddScoped<Diten.MdmService.Application.Features.ProductLegalEntityScopes.LskuBackgroundScope>();
        add(services);
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// An interface answering only the calls named here; any other call fails the test loudly (it was not expected),
    /// except the optional counts (a Task&lt;long&gt; answers 0).
    /// </summary>
    private static T Fake<T>(params (string Method, Func<object?[]?, object> Answer)[] answers) where T : class
    {
        var proxy = DispatchProxy.Create<T, FakeProxy>();
        ((FakeProxy)(object)proxy).Answers = answers.ToDictionary(item => item.Method, item => item.Answer);
        return proxy;
    }

    public class FakeProxy : DispatchProxy
    {
        public Dictionary<string, Func<object?[]?, object>> Answers { get; set; } = [];

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod is not null && Answers.TryGetValue(targetMethod.Name, out var answer)) return answer(args);
            if (targetMethod?.ReturnType == typeof(Task<long>)) return Task.FromResult(0L);
            throw new NotSupportedException($"Unexpected call: {targetMethod?.DeclaringType?.Name}.{targetMethod?.Name}");
        }
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
