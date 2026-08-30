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

public sealed class FinishedGoodIdentityWorkflowRecoveryRunnerTests
{
    [Fact]
    public async Task Disabled_runner_and_worker_are_no_op_without_resolving_dependencies()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var options = Options.Create(new FinishedGoodIdentityWorkflowWorkerOptions());
        var runner = new FinishedGoodIdentityWorkflowRecoveryRunner(
            services.GetRequiredService<IServiceScopeFactory>(), options, TimeProvider.System,
            NullLogger<FinishedGoodIdentityWorkflowRecoveryRunner>.Instance);
        var worker = new FinishedGoodIdentityWorkflowRecoveryWorker(
            runner, options, NullLogger<FinishedGoodIdentityWorkflowRecoveryWorker>.Instance);

        Assert.Equal(new FinishedGoodIdentityWorkflowRecoveryRunResult(0, 0, 0, 0, 0),
            await runner.RunCycleAsync());
        await worker.StartAsync(CancellationToken.None);
        await worker.StopAsync(CancellationToken.None);
    }

    [Fact]
    public void Options_are_default_disabled_and_lease_budget_matches_repository_fence()
    {
        Assert.False(new FinishedGoodIdentityWorkflowOptions().Enabled);
        Assert.False(new FinishedGoodIdentityWorkflowWorkerOptions().Enabled);
        new FinishedGoodIdentityWorkflowWorkerOptions
        {
            Enabled = true, LeaseOwner = "worker", LeaseSeconds = 300
        }.EnsureValidWhenEnabled();
        Assert.Throws<InvalidOperationException>(() => new FinishedGoodIdentityWorkflowWorkerOptions
        {
            Enabled = true, LeaseOwner = "worker", LeaseSeconds = 301
        }.EnsureValidWhenEnabled());
    }

    [Theory]
    [InlineData("--run-finished-good-identity-workflow-recovery", true)]
    [InlineData("--other", false)]
    public void Command_line_accepts_only_exact_argument(string argument, bool expected) =>
        Assert.Equal(expected, FinishedGoodIdentityWorkflowRecoveryCommandLine.IsRequested([argument]));

    [Fact]
    public void Command_line_rejects_alias_suffix_and_duplicate()
    {
        Assert.Throws<InvalidOperationException>(() =>
            FinishedGoodIdentityWorkflowRecoveryCommandLine.IsRequested(
                ["--RUN-FINISHED-GOOD-IDENTITY-WORKFLOW-RECOVERY"]));
        Assert.Throws<InvalidOperationException>(() =>
            FinishedGoodIdentityWorkflowRecoveryCommandLine.IsRequested(
                ["--run-finished-good-identity-workflow-recovery=true"]));
        Assert.Throws<InvalidOperationException>(() =>
            FinishedGoodIdentityWorkflowRecoveryCommandLine.IsRequested([
                FinishedGoodIdentityWorkflowRecoveryCommandLine.ExactArgument,
                FinishedGoodIdentityWorkflowRecoveryCommandLine.ExactArgument]));
    }

    [Fact]
    public async Task Command_line_propagates_caller_cancellation()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var runner = new FinishedGoodIdentityWorkflowRecoveryRunner(
            services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new FinishedGoodIdentityWorkflowWorkerOptions()), TimeProvider.System,
            NullLogger<FinishedGoodIdentityWorkflowRecoveryRunner>.Instance);
        using var source = new CancellationTokenSource();
        source.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            FinishedGoodIdentityWorkflowRecoveryCommandLine.RunAsync(runner, source.Token));
    }

    [Fact]
    public async Task Explicit_command_fails_closed_when_recovery_is_disabled()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var runner = new FinishedGoodIdentityWorkflowRecoveryRunner(
            services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new FinishedGoodIdentityWorkflowWorkerOptions()), TimeProvider.System,
            NullLogger<FinishedGoodIdentityWorkflowRecoveryRunner>.Instance);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            FinishedGoodIdentityWorkflowRecoveryCommandLine.RunAsync(runner, CancellationToken.None));

        Assert.Equal("FINISHED_GOOD_IDENTITY_WORKFLOW_RECOVERY_DISABLED", exception.Message);
    }

    [Fact]
    public async Task Explicit_command_fails_when_any_recovery_operation_fails()
    {
        var tenantId = Guid.Parse("10000000-0000-0000-0000-000000000001");
        var operation = new FinishedGoodIdentityWorkflowOperation
        {
            Id = Guid.Parse("20000000-0000-0000-0000-000000000001"),
            TenantId = tenantId,
            OperationId = Guid.Parse("30000000-0000-0000-0000-000000000001"),
            FinishedGoodId = Guid.Parse("40000000-0000-0000-0000-000000000001"),
            ObjectType = FinishedGoodIdentityWorkflowStartRequestFactory.FinishedGoodObjectType,
            Checkpoint = FinishedGoodIdentityWorkflowCheckpoint.ManualReconciliationRequired,
            RecoveryDisposition = ProductIdentityWorkflowRecoveryDisposition.ManualReconciliationRequired
        };
        var operationRepository = Proxy<IFinishedGoodIdentityWorkflowOperationRepository>((method, _) =>
            method.Name == nameof(IFinishedGoodIdentityWorkflowOperationRepository.DiscoverRecoverableAsync)
                ? Task.FromResult(new FinishedGoodIdentityWorkflowRecoverablePage([operation], null))
                : throw new InvalidOperationException($"Unexpected operation repository call: {method.Name}"));
        var tenantDiscovery = Proxy<IFinishedGoodIdentityWorkflowTenantPartitionDiscovery>((method, _) =>
            method.Name == nameof(IFinishedGoodIdentityWorkflowTenantPartitionDiscovery.DiscoverAsync)
                ? Task.FromResult(new FinishedGoodIdentityWorkflowTenantPartitionPage([tenantId], null))
                : throw new InvalidOperationException($"Unexpected tenant discovery call: {method.Name}"));
        var processor = new FinishedGoodIdentityWorkflowProcessor(
            operationRepository,
            Throwing<IFinishedGoodRepository>(),
            Throwing<IGskuRepository>(),
            Throwing<IProductDefinitionRevisionRepository>(),
            Throwing<IProductIdentityWorkflowClient>(),
            new FinishedGoodIdentityWorkflowStartRequestFactory(
                new(null, "FINISHED-GOOD-IDENTITY", [], "FINISHED_GOOD_APPROVAL", true, true, null),
                TimeProvider.System),
            TimeProvider.System);
        var services = new ServiceCollection()
            .AddSingleton(tenantDiscovery)
            .AddSingleton(operationRepository)
            .AddSingleton(processor)
            .AddSingleton<IProductLegalEntityScopeRolloutStateRepository>(new ScopeRolloutRepository(null))
            .AddScoped<ITenantContext, TestTenantContext>()
            .BuildServiceProvider();
        await using var provider = services;
        var runner = new FinishedGoodIdentityWorkflowRecoveryRunner(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new FinishedGoodIdentityWorkflowWorkerOptions
            {
                Enabled = true,
                LeaseOwner = "test-worker",
                LeaseSeconds = 30,
                RetryDelaySeconds = 1,
                TenantPageSize = 10,
                OperationPageSize = 10
            }),
            TimeProvider.System,
            NullLogger<FinishedGoodIdentityWorkflowRecoveryRunner>.Instance);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            FinishedGoodIdentityWorkflowRecoveryCommandLine.RunAsync(runner, CancellationToken.None));

        Assert.Equal("FINISHED_GOOD_IDENTITY_WORKFLOW_RECOVERY_FAILED", exception.Message);
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
