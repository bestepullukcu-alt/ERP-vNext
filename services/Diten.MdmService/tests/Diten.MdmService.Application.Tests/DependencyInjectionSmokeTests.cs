using Diten.MdmService.Application.Contracts;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes;
using Diten.MdmService.Domain.Repositories;
using Xunit;

namespace Diten.MdmService.Application.Tests;

public sealed class DependencyInjectionSmokeTests
{
    [Fact]
    public void Fu03_scope_dependencies_are_registered_without_audit_transport_or_operational_runner()
    {
        var application = ReadRepoFile(
            "services/Diten.MdmService/src/Diten.MdmService.Application/DependencyInjection.cs");
        var infrastructure = ReadRepoFile(
            "services/Diten.MdmService/src/Diten.MdmService.Infrastructure/DependencyInjection.cs");
        var persistence = ReadRepoFile(
            "services/Diten.MdmService/src/Diten.MdmService.Persistence/DependencyInjection.cs");

        Assert.Contains(nameof(IProductLegalEntityScopeEvaluator), application, StringComparison.Ordinal);
        Assert.Contains(nameof(ProductLegalEntityScopeCandidateFacade), infrastructure, StringComparison.Ordinal);
        Assert.Contains(nameof(IProductLegalEntityScopePolicyRepository), persistence, StringComparison.Ordinal);
        Assert.Contains(nameof(IProductLegalEntityScopeRolloutStateRepository), persistence, StringComparison.Ordinal);
        Assert.DoesNotContain("AuditIntentDeliveryWorker", application, StringComparison.Ordinal);
        Assert.DoesNotContain("ProductLegalEntityScopeOperationalRunner", application, StringComparison.Ordinal);
    }

    private static string ReadRepoFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
            {
                return File.ReadAllText(Path.Combine(
                    directory.FullName,
                    relativePath.Replace('/', Path.DirectorySeparatorChar)));
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("REPO_ROOT_NOT_FOUND");
    }
}
