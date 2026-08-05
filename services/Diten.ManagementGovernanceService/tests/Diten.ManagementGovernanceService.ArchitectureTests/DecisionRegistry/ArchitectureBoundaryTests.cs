using System.Reflection;
using Diten.ManagementGovernanceService.Persistence.DecisionRegistry.Mongo;
using Xunit;

namespace Diten.ManagementGovernanceService.ArchitectureTests.DecisionRegistry;

public sealed class ArchitectureBoundaryTests
{
    [Fact] public void Domain_has_only_platform_common_external_project_reference()
    { var root=FindRoot(); var text=File.ReadAllText(Path.Combine(root,"src/Diten.ManagementGovernanceService.Domain/Diten.ManagementGovernanceService.Domain.csproj")); Assert.Contains("Diten.Platform.Common",text); Assert.DoesNotContain("Persistence",text); Assert.DoesNotContain("Infrastructure",text); }

    [Fact] public void No_runtime_or_forbidden_dependency_is_present()
    { var root=FindRoot(); var files=Directory.GetFiles(Path.Combine(root,"src"),"*",SearchOption.AllDirectories).Where(x=>x.EndsWith(".cs")||x.EndsWith(".csproj")); var text=string.Join('\n',files.Select(File.ReadAllText)); foreach(var forbidden in new[]{"MassTransit","RabbitMQ.Client","Diten.EnterpriseStrategy","Diten.PpmService","WorkCenter","AddHostedService","WebApplication.Run","MapControllers"}) Assert.DoesNotContain(forbidden,text,StringComparison.Ordinal); }

    [Fact] public void Sole_manifest_owns_nine_collections_and_seventeen_indexes()
    { Assert.Single(typeof(DecisionRegistryPersistenceManifest).Assembly.GetTypes(), x=>x.Name==nameof(DecisionRegistryPersistenceManifest)); DecisionRegistryPersistenceManifest.Instance.Validate(); }

    private static string FindRoot()
    { var path=new DirectoryInfo(AppContext.BaseDirectory); while(path is not null && !Directory.Exists(Path.Combine(path.FullName,"src"))) path=path.Parent; return path?.FullName??throw new InvalidOperationException("Service root not found."); }
}
