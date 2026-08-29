using System.Reflection;
using Diten.MdmService.Api.Configuration;
using Diten.MdmService.Api.Services.ProductItemSkuMaster;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts.ReferenceData;
using Diten.MdmService.Application.Contracts.Workflow;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.MdmService.Application.Tests;

public sealed class LskuIdentityWorkflowRecoveryRunnerTests
{
    [Fact]
    public async Task Disabled_runner_and_worker_are_no_op_without_resolving_dependencies()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var options = Options.Create(new LskuIdentityWorkflowWorkerOptions());
        var runner = new LskuIdentityWorkflowRecoveryRunner(
            services.GetRequiredService<IServiceScopeFactory>(), options, TimeProvider.System,
            NullLogger<LskuIdentityWorkflowRecoveryRunner>.Instance);
        var worker = new LskuIdentityWorkflowRecoveryWorker(
            runner, options, NullLogger<LskuIdentityWorkflowRecoveryWorker>.Instance);

        Assert.Equal(new LskuIdentityWorkflowRecoveryRunResult(0, 0, 0, 0, 0),
            await runner.RunCycleAsync());
        await worker.StartAsync(CancellationToken.None);
        await worker.StopAsync(CancellationToken.None);
    }

    [Fact]
    public void Options_are_default_disabled_and_lease_budget_matches_repository_fence()
    {
        Assert.False(new LskuIdentityWorkflowOptions().Enabled);
        Assert.False(new LskuIdentityWorkflowWorkerOptions().Enabled);
        new LskuIdentityWorkflowWorkerOptions
        {
            Enabled = true, LeaseOwner = "worker", LeaseSeconds = 300
        }.EnsureValidWhenEnabled();
        Assert.Throws<InvalidOperationException>(() => new LskuIdentityWorkflowWorkerOptions
        {
            Enabled = true, LeaseOwner = "worker", LeaseSeconds = 301
        }.EnsureValidWhenEnabled());
    }

    [Theory]
    [InlineData("--run-lsku-identity-workflow-recovery", true)]
    [InlineData("--other", false)]
    public void Command_line_accepts_only_exact_argument(string argument, bool expected) =>
        Assert.Equal(expected, LskuIdentityWorkflowRecoveryCommandLine.IsRequested([argument]));

    [Fact]
    public void Command_line_rejects_alias_suffix_and_duplicate()
    {
        Assert.Throws<InvalidOperationException>(() =>
            LskuIdentityWorkflowRecoveryCommandLine.IsRequested(
                ["--RUN-LSKU-IDENTITY-WORKFLOW-RECOVERY"]));
        Assert.Throws<InvalidOperationException>(() =>
            LskuIdentityWorkflowRecoveryCommandLine.IsRequested(
                ["--run-lsku-identity-workflow-recovery=true"]));
        Assert.Throws<InvalidOperationException>(() =>
            LskuIdentityWorkflowRecoveryCommandLine.IsRequested([
                LskuIdentityWorkflowRecoveryCommandLine.ExactArgument,
                LskuIdentityWorkflowRecoveryCommandLine.ExactArgument]));
    }

    [Fact]
    public async Task Command_line_propagates_caller_cancellation()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var runner = new LskuIdentityWorkflowRecoveryRunner(
            services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new LskuIdentityWorkflowWorkerOptions()), TimeProvider.System,
            NullLogger<LskuIdentityWorkflowRecoveryRunner>.Instance);
        using var source = new CancellationTokenSource();
        source.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            LskuIdentityWorkflowRecoveryCommandLine.RunAsync(runner, source.Token));
    }

    [Fact]
    public async Task Explicit_command_fails_closed_when_recovery_is_disabled()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var runner = new LskuIdentityWorkflowRecoveryRunner(
            services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new LskuIdentityWorkflowWorkerOptions()), TimeProvider.System,
            NullLogger<LskuIdentityWorkflowRecoveryRunner>.Instance);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            LskuIdentityWorkflowRecoveryCommandLine.RunAsync(runner, CancellationToken.None));

        Assert.Equal("LSKU_IDENTITY_WORKFLOW_RECOVERY_DISABLED", exception.Message);
    }

    [Fact]
    public async Task Explicit_command_fails_when_any_recovery_operation_fails()
    {
        var tenantId = Guid.Parse("10000000-0000-0000-0000-000000000001");
        var operation = new LskuIdentityWorkflowOperation
        {
            Id = Guid.Parse("20000000-0000-0000-0000-000000000001"),
            TenantId = tenantId,
            OperationId = Guid.Parse("30000000-0000-0000-0000-000000000001"),
            LskuId = Guid.Parse("40000000-0000-0000-0000-000000000001"),
            ObjectType = LskuIdentityWorkflowStartRequestFactory.LskuObjectType,
            Checkpoint = LskuIdentityWorkflowCheckpoint.ManualReconciliationRequired,
            RecoveryDisposition = ProductIdentityWorkflowRecoveryDisposition.ManualReconciliationRequired
        };
        var operationRepository = Proxy<ILskuIdentityWorkflowOperationRepository>((method, _) =>
            method.Name == nameof(ILskuIdentityWorkflowOperationRepository.DiscoverRecoverableAsync)
                ? Task.FromResult(new LskuIdentityWorkflowRecoverablePage([operation], null))
                : throw new InvalidOperationException($"Unexpected operation repository call: {method.Name}"));
        var tenantDiscovery = Proxy<ILskuIdentityWorkflowTenantPartitionDiscovery>((method, _) =>
            method.Name == nameof(ILskuIdentityWorkflowTenantPartitionDiscovery.DiscoverAsync)
                ? Task.FromResult(new LskuIdentityWorkflowTenantPartitionPage([tenantId], null))
                : throw new InvalidOperationException($"Unexpected tenant discovery call: {method.Name}"));
        var processor = new LskuIdentityWorkflowProcessor(
            operationRepository,
            Throwing<ILskuRepository>(),
            Throwing<IGskuRepository>(),
            Throwing<IProductDefinitionRevisionRepository>(),
            Throwing<IWorkflowVerifiedMarketReferenceResolver>(),
            Throwing<IProductIdentityWorkflowClient>(),
            new LskuIdentityWorkflowStartRequestFactory(
                new(null, "LSKU-IDENTITY", [], "LSKU_APPROVAL", true, true, null),
                TimeProvider.System),
            TimeProvider.System);
        var services = new ServiceCollection()
            .AddSingleton(tenantDiscovery)
            .AddSingleton(operationRepository)
            .AddSingleton(processor)
            .AddScoped<ITenantContext, TestTenantContext>()
            .BuildServiceProvider();
        await using var provider = services;
        var runner = new LskuIdentityWorkflowRecoveryRunner(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new LskuIdentityWorkflowWorkerOptions
            {
                Enabled = true,
                LeaseOwner = "test-worker",
                LeaseSeconds = 30,
                RetryDelaySeconds = 1,
                TenantPageSize = 10,
                OperationPageSize = 10
            }),
            TimeProvider.System,
            NullLogger<LskuIdentityWorkflowRecoveryRunner>.Instance);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            LskuIdentityWorkflowRecoveryCommandLine.RunAsync(runner, CancellationToken.None));

        Assert.Equal("LSKU_IDENTITY_WORKFLOW_RECOVERY_FAILED", exception.Message);
    }

    private static T Throwing<T>() where T : class =>
        Proxy<T>((method, _) => throw new InvalidOperationException(
            $"Dependency must not be called from terminal recovery: {method.Name}"));

    private static T Proxy<T>(Func<MethodInfo, object?[]?, object?> handler) where T : class
    {
        var proxy = DispatchProxy.Create<T, TestProxy>();
        ((TestProxy)(object)proxy).Handler = handler;
        return proxy;
    }

    public class TestProxy : DispatchProxy
    {
        public Func<MethodInfo, object?[]?, object?> Handler { get; set; } = null!;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            Handler(targetMethod!, args);
    }

    private sealed class TestTenantContext : ITenantContext
    {
        public Guid TenantId { get; private set; }
        public bool IsResolved => TenantId != Guid.Empty;
        public void SetTenant(Guid tenantId) => TenantId = tenantId;
    }
}
