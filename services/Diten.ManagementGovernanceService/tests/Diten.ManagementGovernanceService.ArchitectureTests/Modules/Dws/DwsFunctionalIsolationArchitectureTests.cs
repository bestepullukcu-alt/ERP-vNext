using Diten.ManagementGovernanceService.Application.Modules.Dws;
using Diten.ManagementGovernanceService.Persistence.Modules.Dws;
using Xunit;

namespace Diten.ManagementGovernanceService.ArchitectureTests.Modules.Dws;

public sealed class DwsFunctionalIsolationArchitectureTests
{
    [Fact]
    public void Functional_surface_preserves_exact_sibling_isolation_and_owned_storage()
    {
        Assert.Equal(24, DwsIsolationEvidenceManifest.ExactEvidence.Count);
        Assert.Equal(["ProcessModeling", "DecisionRegistry"],
            DwsIsolationEvidenceManifest.Siblings.Select(sibling => sibling.Name));

        Assert.All(DwsAuthorizationManifest.Entries, entry =>
        {
            Assert.StartsWith("management-governance.dws.", entry.Permission, StringComparison.Ordinal);
            Assert.DoesNotContain("process-modeling", entry.Permission, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("decision-registry", entry.Permission, StringComparison.OrdinalIgnoreCase);
        });

        Assert.All(DwsPersistenceOwnershipManifest.Collections, collection =>
        {
            Assert.StartsWith("mg_dws_", collection.Name, StringComparison.Ordinal);
            Assert.DoesNotContain("process", collection.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("decision_registry", collection.Name, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void Functional_source_has_no_foreign_module_namespace_or_collection_dependency()
    {
        var source = FunctionalSources();
        Assert.NotEmpty(source);
        var forbidden = new[]
        {
            "Modules.ProcessModeling", "Domain.DecisionRegistry", "mg_process_",
            "decision_registry_", "Diten.PpmService", "Diten.AuthService"
        };

        foreach (var text in source.Select(File.ReadAllText))
            Assert.All(forbidden, token => Assert.DoesNotContain(token, text, StringComparison.Ordinal));
    }

    private static IReadOnlyList<string> FunctionalSources()
    {
        var root = FindRoot();
        return Directory.GetFiles(
                Path.Combine(root, "services/Diten.ManagementGovernanceService/src"),
                "*.cs",
                SearchOption.AllDirectories)
            .Where(path => path.Contains("/Features/Dws/", StringComparison.Ordinal)
                || path.Contains("/Persistence/Modules/Dws/DwsTyped", StringComparison.Ordinal)
                || path.Contains("/Persistence/Modules/Dws/DwsFunctional", StringComparison.Ordinal))
            .ToArray();
    }

    private static string FindRoot()
    {
        var cursor = new DirectoryInfo(AppContext.BaseDirectory);
        while (cursor is not null && !File.Exists(Path.Combine(cursor.FullName, "AGENTS.md"))) cursor = cursor.Parent;
        return cursor?.FullName ?? throw new InvalidOperationException("repo_root_not_found");
    }
}
