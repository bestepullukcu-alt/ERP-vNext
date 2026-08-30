using System.Reflection;
using Diten.MdmService.Api.Services.ProductItemSkuMaster;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diten.MdmService.Application.Tests;

public sealed class ProductIdentityLifecycleScopeEnforcementTests
{
    public static TheoryData<string, string, string> Handlers => new()
    {
        { "Workflow/Handlers/CommandHandlers/StartGlobalProductIdentityWorkflowHandler.cs", "ProductIdentityLifecyclePermissions.GlobalProductSubmit", "processor.StartInteractiveAsync" },
        { "Workflow/Handlers/CommandHandlers/StartFirstGskuIdentityWorkflowHandler.cs", "FirstGskuIdentityLifecyclePermissions.Submit", "processor.StartInteractiveAsync" },
        { "Workflow/Handlers/CommandHandlers/StartLskuIdentityWorkflowHandler.cs", "LskuIdentityLifecyclePermissions.Submit", "processor.StartInteractiveAsync" },
        { "Workflow/Handlers/CommandHandlers/StartFinishedGoodIdentityWorkflowHandler.cs", "FinishedGoodIdentityLifecyclePermissions.Submit", "processor.StartInteractiveAsync" },
        { "Lifecycle/Handlers/CommandHandlers/RetireGlobalProductIdentityHandler.cs", "ProductIdentityLifecyclePermissions.GlobalProductRetire", "_products.RetireIdentityAsync" },
        { "Lifecycle/Handlers/CommandHandlers/RetireGskuIdentityPairHandler.cs", "GskuPairRetirementPermissions.Retire", "processor.StartAsync" },
        { "Lifecycle/Handlers/CommandHandlers/RetireLskuIdentityHandler.cs", "LskuIdentityLifecyclePermissions.Retire", "lskus.RetireIdentityAsync" },
        { "Lifecycle/Handlers/CommandHandlers/RetireFinishedGoodIdentityHandler.cs", "FinishedGoodIdentityLifecyclePermissions.Retire", "finishedGoods.RetireIdentityAsync" }
    };

    public static TheoryData<string, string> RecoveryRunners => new()
    {
        { "Diten.MdmService.Api/Services/ProductItemSkuMaster/ProductIdentityWorkflowRecoveryRunner.cs", "processor.RecoverAsync" },
        { "Diten.MdmService.Api/Services/ProductItemSkuMaster/FirstGskuIdentityWorkflowRecoveryRunner.cs", "processor.RecoverAsync" },
        { "Diten.MdmService.Api/Services/ProductItemSkuMaster/LskuIdentityWorkflowRecoveryRunner.cs", "processor.RecoverAsync" },
        { "Diten.MdmService.Api/Services/ProductItemSkuMaster/FinishedGoodIdentityWorkflowRecoveryRunner.cs", "processor.RecoverAsync" },
        { "Diten.MdmService.Application/Features/ProductItemSkuMaster/Lifecycle/FirstGskuIdentityRetirementRecoveryRunner.cs", "processor.RecoverAsync" }
    };

    public static TheoryData<Type> ApiRecoveryRunnerTypes => new()
    {
        typeof(ProductIdentityWorkflowRecoveryRunner),
        typeof(FirstGskuIdentityWorkflowRecoveryRunner),
        typeof(LskuIdentityWorkflowRecoveryRunner),
        typeof(FinishedGoodIdentityWorkflowRecoveryRunner)
    };

    [Theory]
    [MemberData(nameof(Handlers))]
    public void Exact_scope_permission_and_decision_precede_every_lifecycle_mutation(
        string relativePath,
        string permission,
        string mutationAnchor)
    {
        var root = RepositoryRoot();
        var path = Path.Combine(root, "services", "Diten.MdmService", "src",
            "Diten.MdmService.Application", "Features", "ProductItemSkuMaster", relativePath);
        var source = File.ReadAllText(path);
        var permissionIndex = source.IndexOf(permission, StringComparison.Ordinal);
        var decisionIndex = source.IndexOf("EvaluateAsync", StringComparison.Ordinal);
        var mutationIndex = source.IndexOf(mutationAnchor, StringComparison.Ordinal);

        Assert.True(permissionIndex >= 0, $"Missing exact lifecycle permission in {relativePath}.");
        Assert.True(decisionIndex > permissionIndex, $"Missing scope decision in {relativePath}.");
        Assert.True(mutationIndex > decisionIndex, $"Mutation precedes scope decision in {relativePath}.");
    }

    [Theory]
    [MemberData(nameof(RecoveryRunners))]
    public void Background_recovery_checks_rollout_and_defers_before_processor(
        string relativePath,
        string processorAnchor)
    {
        var source = File.ReadAllText(Path.Combine(
            RepositoryRoot(), "services", "Diten.MdmService", "src", relativePath));
        var rolloutIndex = source.IndexOf(
            "IProductLegalEntityScopeRolloutStateRepository", StringComparison.Ordinal);
        var gateIndex = source.IndexOf("IsBackgroundRecoveryAllowedAsync", StringComparison.Ordinal);
        var awaitingIndex = source.IndexOf(
            "AwaitingMakerReplay", StringComparison.Ordinal);
        var processorIndex = source.IndexOf(processorAnchor, StringComparison.Ordinal);

        Assert.True(rolloutIndex >= 0, $"Missing rollout dependency in {relativePath}.");
        Assert.True(gateIndex >= 0, $"Missing recovery scope gate in {relativePath}.");
        Assert.True(awaitingIndex >= 0 || source.Contains("deferred", StringComparison.Ordinal),
            $"Missing fail-closed deferral in {relativePath}.");
        Assert.True(processorIndex > gateIndex, $"Processor precedes recovery scope gate in {relativePath}.");
    }

    [Theory]
    [MemberData(nameof(ApiRecoveryRunnerTypes))]
    public async Task Enforced_rollout_denies_each_api_background_recovery_gate(Type runnerType)
    {
        var tenantId = Guid.NewGuid();
        var rollout = ProductLegalEntityScopeRolloutState.CreatePreparation(
            tenantId, Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow);
        rollout.Mode = ProductLegalEntityScopeRolloutMode.Enforced;
        await using var services = new ServiceCollection()
            .AddSingleton<IProductLegalEntityScopeRolloutStateRepository>(
                new ScopeRolloutRepository(rollout))
            .BuildServiceProvider();
        var method = runnerType.GetMethod(
            "IsBackgroundRecoveryAllowedAsync",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.NotNull(method);
        var task = Assert.IsType<Task<bool>>(method.Invoke(
            null, [services, tenantId, CancellationToken.None]));
        Assert.False(await task);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md"))
                && Directory.Exists(Path.Combine(directory.FullName, "services")))
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
