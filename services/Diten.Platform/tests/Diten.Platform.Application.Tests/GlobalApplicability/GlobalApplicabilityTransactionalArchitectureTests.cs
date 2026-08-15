using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Features.ModuleCatalog.Commands;
using Diten.Platform.Application.Features.ModuleRegistration;
using Diten.Platform.Application.Features.SubscriptionPlans.Commands;
using Xunit;

namespace Diten.Platform.Application.Tests.GlobalApplicability;

public sealed class GlobalApplicabilityTransactionalArchitectureTests
{
    private static readonly Type[] ExactCommands =
    [
        typeof(CreateSubscriptionPlanCommand), typeof(UpdateSubscriptionPlanCommand),
        typeof(ActivateSubscriptionPlanCommand), typeof(DeactivateSubscriptionPlanCommand),
        typeof(SeedDefaultSubscriptionPlansCommand), typeof(CreateModuleCatalogItemCommand),
        typeof(UpdateModuleCatalogItemCommand), typeof(ActivateModuleCatalogItemCommand),
        typeof(DeactivateModuleCatalogItemCommand), typeof(DeleteModuleCatalogItemCommand),
        typeof(BulkDeleteModuleCatalogItemsCommand), typeof(RegisterModuleManifestCommand)
    ];

    [Fact]
    public void ExactAuthoritativeCommands_AreTransactionOwnedAuditCommands()
    {
        Assert.All(ExactCommands, command =>
            Assert.True(typeof(ITransactionOwnedAuditCommand).IsAssignableFrom(command), command.Name));
    }

    [Fact]
    public void CoordinatorPinsCounterProjectionIntegrationAuditAndNoDirectPublish()
    {
        var source = ReadApplication("Features", "GlobalApplicability", "GlobalApplicabilityTransactionCoordinator.cs");
        Assert.Contains("IncrementGlobalApplicabilityVersionAsync(session", source, StringComparison.Ordinal);
        Assert.Contains("writeProjectionAsync(session, version", source, StringComparison.Ordinal);
        Assert.Contains("_events.EnqueueAsync(session", source, StringComparison.Ordinal);
        Assert.Contains("_audit.TryEnqueueAsync(session", source, StringComparison.Ordinal);
        Assert.DoesNotContain("IEventBus", source, StringComparison.Ordinal);
        Assert.DoesNotContain("PublishAsync(", source, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("plan-participant", "SubscriptionPlans/Handlers/CommandHandlers/CreateSubscriptionPlanCommandHandler.cs", "_repository.CreateAsync(session")]
    [InlineData("plan-included-module-increment", "SubscriptionPlans/Handlers/CommandHandlers/UpdateSubscriptionPlanCommandHandler.cs", "IncludedModuleKeys.SequenceEqual")]
    [InlineData("module-participant", "ModuleCatalog/Handlers/CommandHandlers/CreateModuleCatalogItemCommandHandler.cs", "_repository.CreateAsync(session")]
    [InlineData("module-core-increment", "ModuleCatalog/Handlers/CommandHandlers/UpdateModuleCatalogItemCommandHandler.cs", "item.IsCoreModule =")]
    [InlineData("module-delete", "ModuleCatalog/Handlers/CommandHandlers/DeleteModuleCatalogItemCommandHandler.cs", "_repository.DeleteAsync(session")]
    [InlineData("bulk-atomic", "ModuleCatalog/Handlers/CommandHandlers/BulkDeleteModuleCatalogItemsCommandHandler.cs", "ExecuteBatchAsync")]
    [InlineData("self-registration-increment", "ModuleRegistration/RegisterModuleManifestCommandHandler.cs", "_transaction.ExecuteAsync")]
    [InlineData("seed-no-op", "SubscriptionPlans/Handlers/CommandHandlers/SeedDefaultSubscriptionPlansCommandHandler.cs", "GlobalApplicabilityMutation<bool>(false, false)")]
    public void RequiredMutationSeams_AreExecutableAndPinned(string disposition, string relativePath, string requiredToken)
    {
        var source = ReadApplication("Features", relativePath);
        Assert.Contains(requiredToken, source, StringComparison.Ordinal);
        Assert.DoesNotContain("PublishAsync(", source, StringComparison.Ordinal);
    }

    [Fact]
    public void UnsafeMutants_AreSecurityPolicyForbiddenByExecutableGuards()
    {
        var executor = ReadInfrastructure("Persistence", "PlatformTransactionExecutor.cs");
        Assert.Contains("UnknownTransactionCommitResult", executor, StringComparison.Ordinal);
        Assert.Contains("CommitTransactionAsync", executor, StringComparison.Ordinal);
        Assert.DoesNotContain("result = await body", executor[executor.IndexOf("for (var commitAttempt", StringComparison.Ordinal)..], StringComparison.Ordinal);
        Assert.Contains("catch (OperationCanceledException)", executor, StringComparison.Ordinal);

        var session = ReadInfrastructure("Persistence", "PlatformMongoTransactionSession.cs");
        Assert.Contains("ReferenceEquals(mongoSession.Owner, dbContext.Client)", session, StringComparison.Ordinal);
        Assert.Contains("mongoSession.Handle.IsInTransaction", session, StringComparison.Ordinal);

        foreach (var repository in new[] { "SubscriptionPlanRepository.cs", "ModuleCatalogRepository.cs" })
        {
            var source = ReadInfrastructure("Persistence", "Repositories", repository);
            Assert.Contains("PlatformTransactionUnavailableException", source, StringComparison.Ordinal);
            Assert.Contains("PlatformMongoTransactionSession.Require(session, _dbContext)", source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void CandidateGovernanceIdentity_IsAbsentFromRuntimeFiles()
    {
        var root = Path.Combine(GetRepoRoot(), "services", "Diten.Platform", "src");
        Assert.Empty(Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(path => File.ReadAllText(path).Contains("CAND" + "-CAP-", StringComparison.Ordinal)));
    }

    [Fact]
    public void StartupDoesNotBypassTransactionalSeedCommandWithDirectMongoPlanWrites()
    {
        var dependencyInjection = ReadInfrastructure("DependencyInjection.cs");
        Assert.DoesNotContain("SubscriptionPlanSeed.EnsureSeededAsync(database)", dependencyInjection, StringComparison.Ordinal);

        var seedHandler = ReadApplication("Features", "SubscriptionPlans", "Handlers", "CommandHandlers",
            "SeedDefaultSubscriptionPlansCommandHandler.cs");
        Assert.Contains("_transaction.ExecuteAsync", seedHandler, StringComparison.Ordinal);
        Assert.Contains("_repository.CreateAsync(session", seedHandler, StringComparison.Ordinal);
        Assert.Contains("GlobalApplicabilityMutation<bool>(false, false)", seedHandler, StringComparison.Ordinal);
    }

    private static string ReadApplication(params string[] segments) =>
        File.ReadAllText(Path.Combine(new[] { GetRepoRoot(), "services", "Diten.Platform", "src", "Diten.Platform.Application" }.Concat(segments).ToArray()));

    private static string ReadInfrastructure(params string[] segments) =>
        File.ReadAllText(Path.Combine(new[] { GetRepoRoot(), "services", "Diten.Platform", "src", "Diten.Platform.Infrastructure" }.Concat(segments).ToArray()));

    private static string GetRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AGENTS.md"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repo root could not be located.");
    }
}
