using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Xunit;

namespace Diten.ManagementGovernanceService.ArchitectureTests.Modules.Dws;

public sealed class DwsFunctionalMutationEvidenceTests
{
    [Fact]
    public void Functional_B11B_evidence_has_twenty_three_killed_mutants_three_forbidden_dispositions_and_byte_restores()
    {
        var root = FindRoot();
        var evidenceRoot = Path.Combine(
            root,
            "services/Diten.ManagementGovernanceService/tests/Diten.ManagementGovernanceService.ArchitectureTests/Modules/Dws");
        var path = Path.Combine(evidenceRoot, "dws-functional-mutation-evidence-v1.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        Assert.Equal("dws-functional-mutation-evidence-v1", document.RootElement.GetProperty("schemaVersion").GetString());
        var entries = document.RootElement.GetProperty("mutations").EnumerateArray().ToArray();
        Assert.Equal(26, entries.Length);
        Assert.Equal(26, entries.Select(entry => entry.GetProperty("id").GetString()).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(23, entries.Count(entry => entry.GetProperty("disposition").GetString() == "expected-red"));
        Assert.Equal(3, entries.Count(entry => entry.GetProperty("disposition").GetString() == "security-policy-forbidden"));
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent?.Name ?? throw new InvalidOperationException("configuration_not_found");
        foreach (var entry in entries)
        {
            Assert.False(string.IsNullOrWhiteSpace(entry.GetProperty("failureText").GetString()));
            var source = Path.Combine(root, entry.GetProperty("sourcePath").GetString()!);
            Assert.Equal(entry.GetProperty("restoredSha256").GetString(), Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(source))).ToLowerInvariant());
            if (entry.GetProperty("disposition").GetString() == "expected-red")
            {
                Assert.False(string.IsNullOrWhiteSpace(entry.GetProperty("runId").GetString()));
                Assert.Equal(0, entry.GetProperty("compileExit").GetInt32());
                Assert.NotEqual(0, entry.GetProperty("targetedExit").GetInt32());
            }
            var typeName = entry.GetProperty("testType").GetString()!;
            var project = typeName.Contains(".IntegrationTests.", StringComparison.Ordinal)
                ? "Diten.ManagementGovernanceService.IntegrationTests"
                : typeName.Contains(".ArchitectureTests.", StringComparison.Ordinal)
                    ? "Diten.ManagementGovernanceService.ArchitectureTests"
                    : "Diten.ManagementGovernanceService.Tests";
            var assembly = Assembly.LoadFrom(Path.Combine(root, "services/Diten.ManagementGovernanceService/tests", project, "bin", configuration, "net8.0", project + ".dll"));
            Assert.NotNull(assembly.GetType(typeName, true)!.GetMethod(entry.GetProperty("testMethod").GetString()!, BindingFlags.Instance | BindingFlags.Public));
        }
    }

    private static string FindRoot()
    {
        var cursor = new DirectoryInfo(AppContext.BaseDirectory);
        while (cursor is not null && !File.Exists(Path.Combine(cursor.FullName, "AGENTS.md"))) cursor = cursor.Parent;
        return cursor?.FullName ?? throw new InvalidOperationException("repo_root_not_found");
    }
}
