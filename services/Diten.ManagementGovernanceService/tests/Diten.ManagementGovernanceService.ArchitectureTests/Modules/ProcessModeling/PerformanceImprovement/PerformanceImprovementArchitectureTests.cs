using System.Security.Cryptography;
using System.Text.Json;
using Diten.ManagementGovernanceService.Application.Modules.ProcessModeling.PerformanceImprovement;
using Diten.ManagementGovernanceService.Persistence.Modules.ProcessModeling.PerformanceImprovement;

namespace Diten.ManagementGovernanceService.ArchitectureTests.Modules.ProcessModeling.PerformanceImprovement;

public sealed class PerformanceImprovementArchitectureTests
{
    [Fact]
    public void Slice_is_default_off_and_has_zero_host_reachability()
    {
        var service = ServiceRoot();
        var hostFiles = new[]
        {
            Path.Combine(service, "src", "Diten.ManagementGovernanceService.Api", "Program.cs"),
            Path.Combine(service, "src", "Diten.ManagementGovernanceService.Application", "DependencyInjection.cs"),
            Path.Combine(service, "src", "Diten.ManagementGovernanceService.Persistence", "DependencyInjection.cs"),
            Path.Combine(service, "src", "Diten.ManagementGovernanceService.Infrastructure", "DependencyInjection.cs")
        };
        Assert.All(hostFiles, path => Assert.DoesNotContain("PerformanceImprovement", File.ReadAllText(path), StringComparison.Ordinal));
        Assert.DoesNotContain(
            Directory.GetFiles(Path.Combine(service, "src", "Diten.ManagementGovernanceService.Api", "Controllers"), "*.cs"),
            path => File.ReadAllText(path).Contains("PerformanceImprovement", StringComparison.Ordinal));
    }

    [Fact]
    public void Domain_is_mongo_free_and_module_does_not_reference_Dws()
    {
        var roots = SourceRoots();
        var domain = Directory.GetFiles(roots.Domain, "*.cs", SearchOption.AllDirectories);
        Assert.All(domain, path =>
        {
            var text = File.ReadAllText(path);
            Assert.DoesNotContain("MongoDB.", text, StringComparison.Ordinal);
            Assert.DoesNotContain("Modules.Dws", text, StringComparison.Ordinal);
        });
        Assert.All(Directory.GetFiles(roots.Application, "*.cs", SearchOption.AllDirectories),
            path => Assert.DoesNotContain("Modules.Dws", File.ReadAllText(path), StringComparison.Ordinal));
        Assert.All(Directory.GetFiles(roots.Persistence, "*.cs", SearchOption.AllDirectories),
            path => Assert.DoesNotContain("Modules.Dws", File.ReadAllText(path), StringComparison.Ordinal));
    }

    [Fact]
    public void Persistence_manifest_is_exact_two_collection_two_tenant_first_index_and_no_kpi_collection()
    {
        Assert.Equal(new[] { "mg_process_improvement_opportunities", "mg_process_improvement_outcomes" },
            PerformanceImprovementMongoPersistence.BusinessCollections);
        Assert.Equal(2, PerformanceImprovementMongoPersistence.BusinessIndexes.Count);
        Assert.All(PerformanceImprovementMongoPersistence.BusinessIndexes, index =>
        {
            Assert.Equal("TenantId", index.Keys[0]);
            Assert.False(index.Unique);
            Assert.DoesNotContain("kpi", index.Collection, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void Mutation_evidence_is_bound_to_restored_sources_and_existing_tests()
    {
        var path = Path.Combine(TestRoot(), "mutation-evidence-v1.json");
        using var manifest = JsonDocument.Parse(File.ReadAllBytes(path));
        var rows = manifest.RootElement.GetProperty("physicalMutations").EnumerateArray().ToArray();
        Assert.Equal(11, rows.Length);
        foreach (var row in rows)
        {
            var source = Path.Combine(RepositoryRoot(), row.GetProperty("sourcePath").GetString()!);
            Assert.True(File.Exists(source), source);
            Assert.Equal(row.GetProperty("restoredSha256").GetString(), Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(source))).ToLowerInvariant());
            Assert.Equal(0, row.GetProperty("compileExitCode").GetInt32());
            Assert.NotEqual(0, row.GetProperty("targetedExitCode").GetInt32());
            Assert.False(string.IsNullOrWhiteSpace(row.GetProperty("expectedFailure").GetString()));
        }

        var forbidden = manifest.RootElement.GetProperty("securityPolicyForbiddenDispositions").EnumerateArray().ToArray();
        Assert.Equal(3, forbidden.Length);
        Assert.All(forbidden, disposition =>
        {
            Assert.False(disposition.GetProperty("materialized").GetBoolean());
            Assert.False(disposition.GetProperty("compiled").GetBoolean());
            Assert.False(disposition.GetProperty("executed").GetBoolean());
            var testSource = Path.Combine(RepositoryRoot(), disposition.GetProperty("testSourcePath").GetString()!);
            Assert.Contains(disposition.GetProperty("executableInvariantTest").GetString()!, File.ReadAllText(testSource), StringComparison.Ordinal);
            Assert.False(string.IsNullOrWhiteSpace(disposition.GetProperty("reason").GetString()));
        });
    }

    [Fact]
    public void Additive_permissions_and_event_identity_are_not_in_current_host_composition()
    {
        Assert.Equal(9, PerformanceImprovementPermissions.ExactOperationMap.Count);
        Assert.Equal("MOD-0355", PerformanceImprovementContract.ModuleCode);
        Assert.Equal(1, PerformanceImprovementContract.EventVersion);
        var api = Path.Combine(ServiceRoot(), "src", "Diten.ManagementGovernanceService.Api");
        Assert.DoesNotContain(Directory.GetFiles(api, "*.cs", SearchOption.AllDirectories),
            path => File.ReadAllText(path).Contains(PerformanceImprovementContract.EventName, StringComparison.Ordinal));
    }

    private static (string Domain, string Application, string Persistence) SourceRoots()
    {
        var service = ServiceRoot();
        const string relative = "Modules/ProcessModeling/PerformanceImprovement";
        return (
            Path.Combine(service, "src", "Diten.ManagementGovernanceService.Domain", relative),
            Path.Combine(service, "src", "Diten.ManagementGovernanceService.Application", relative),
            Path.Combine(service, "src", "Diten.ManagementGovernanceService.Persistence", relative));
    }

    private static string TestRoot() => Path.Combine(ServiceRoot(), "tests", "Diten.ManagementGovernanceService.ArchitectureTests", "Modules", "ProcessModeling", "PerformanceImprovement");
    private static string ServiceRoot()
    {
        var cursor = new DirectoryInfo(AppContext.BaseDirectory);
        while (cursor is not null && !string.Equals(cursor.Name, "Diten.ManagementGovernanceService", StringComparison.Ordinal)) cursor = cursor.Parent;
        return cursor?.FullName ?? throw new InvalidOperationException("service_root_missing");
    }
    private static string RepositoryRoot()
    {
        var cursor = new DirectoryInfo(ServiceRoot());
        while (cursor.Parent is not null && !Directory.Exists(Path.Combine(cursor.FullName, ".git")) && !File.Exists(Path.Combine(cursor.FullName, ".git"))) cursor = cursor.Parent;
        return cursor.FullName;
    }
}
