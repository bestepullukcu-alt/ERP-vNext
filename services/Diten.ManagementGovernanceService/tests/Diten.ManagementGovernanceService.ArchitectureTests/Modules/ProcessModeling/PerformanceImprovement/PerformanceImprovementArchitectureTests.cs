using System.Security.Cryptography;
using System.Text.Json;
using Diten.ManagementGovernanceService.Application.Modules.ProcessModeling.PerformanceImprovement;
using Diten.ManagementGovernanceService.Persistence.Modules.ProcessModeling.PerformanceImprovement;

namespace Diten.ManagementGovernanceService.ArchitectureTests.Modules.ProcessModeling.PerformanceImprovement;

public sealed class PerformanceImprovementArchitectureTests
{
    [Fact]
    public void Local_test_slice_is_composed_only_on_existing_host_and_has_exact_route()
    {
        var service = ServiceRoot();
        var hostFiles = new[]
        {
            Path.Combine(service, "src", "Diten.ManagementGovernanceService.Api", "Program.cs"),
            Path.Combine(service, "src", "Diten.ManagementGovernanceService.Application", "DependencyInjection.cs"),
            Path.Combine(service, "src", "Diten.ManagementGovernanceService.Persistence", "DependencyInjection.cs"),
            Path.Combine(service, "src", "Diten.ManagementGovernanceService.Infrastructure", "DependencyInjection.cs")
        };
        Assert.Contains("PerformanceImprovement", File.ReadAllText(hostFiles[0]), StringComparison.Ordinal);
        Assert.Contains("PerformanceImprovement", File.ReadAllText(hostFiles[1]), StringComparison.Ordinal);
        Assert.Contains("PerformanceImprovement", File.ReadAllText(hostFiles[2]), StringComparison.Ordinal);
        var controller = File.ReadAllText(Path.Combine(service, "src", "Diten.ManagementGovernanceService.Api", "Controllers", "PerformanceImprovementLocalTestController.cs"));
        Assert.Contains("internal/local-test/v1/process-modeling/improvements", controller, StringComparison.Ordinal);
        Assert.Contains("LocalTestAuthenticationHandler.SchemeName", controller, StringComparison.Ordinal);
        Assert.Contains("ProcessModelingLocalRequestHash.Compute", controller, StringComparison.Ordinal);
        Assert.Contains("ExactResolvedPath", controller, StringComparison.Ordinal);
        Assert.Contains("ExactCommandName", controller, StringComparison.Ordinal);
        Assert.Contains(": CustomBaseController", controller, StringComparison.Ordinal);
        Assert.DoesNotContain("AllowAnonymous", controller, StringComparison.Ordinal);
        Assert.DoesNotContain("SHA256.HashData(raw)", controller, StringComparison.Ordinal);
        Assert.DoesNotContain("IEventBus", controller, StringComparison.Ordinal);
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
    public void Local_test_mutation_evidence_is_bound_to_restored_query_and_forbidden_browser_boundary()
    {
        var path = Path.Combine(TestRoot(), "local-test-mutation-evidence-v1.json");
        using var manifest = JsonDocument.Parse(File.ReadAllBytes(path));
        var mutation = Assert.Single(manifest.RootElement.GetProperty("physicalMutations").EnumerateArray());
        var source = Path.Combine(RepositoryRoot(), mutation.GetProperty("sourcePath").GetString()!);
        Assert.Equal(mutation.GetProperty("restoredSha256").GetString(),
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(source))).ToLowerInvariant());
        Assert.Equal(0, mutation.GetProperty("compileExitCode").GetInt32());
        Assert.NotEqual(0, mutation.GetProperty("targetedExitCode").GetInt32());

        var forbidden = Assert.Single(manifest.RootElement.GetProperty("securityPolicyForbiddenDispositions").EnumerateArray());
        Assert.False(forbidden.GetProperty("materialized").GetBoolean());
        var testSource = Path.Combine(RepositoryRoot(), forbidden.GetProperty("testSourcePath").GetString()!);
        Assert.Contains(forbidden.GetProperty("executableInvariantTest").GetString()!, File.ReadAllText(testSource), StringComparison.Ordinal);
    }

    [Fact]
    public void Additive_permissions_and_event_identity_are_exact_and_host_is_non_production()
    {
        Assert.Equal(9, PerformanceImprovementPermissions.ExactOperationMap.Count);
        Assert.Equal("MOD-0355", PerformanceImprovementContract.ModuleCode);
        Assert.Equal(1, PerformanceImprovementContract.EventVersion);
        var program = File.ReadAllText(Path.Combine(ServiceRoot(), "src", "Diten.ManagementGovernanceService.Api", "Program.cs"));
        Assert.Contains("--local-test", program, StringComparison.Ordinal);
        Assert.Contains("UseUrls(\"http://127.0.0.1:5017\")", program, StringComparison.Ordinal);
        Assert.True(program.IndexOf("--local-test", StringComparison.Ordinal) < program.IndexOf("UseUrls", StringComparison.Ordinal));
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
