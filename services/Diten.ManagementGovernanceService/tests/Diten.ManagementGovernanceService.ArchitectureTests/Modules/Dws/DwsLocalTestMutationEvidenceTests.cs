using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Xunit;

namespace Diten.ManagementGovernanceService.ArchitectureTests.Modules.Dws;

public sealed class DwsLocalTestMutationEvidenceTests
{
    [Fact]
    public void Ten_physical_mutants_are_expected_red_hash_restored_and_test_bound()
    {
        var root=FindRoot();
        var path=Path.Combine(root,"services/Diten.ManagementGovernanceService/tests/Diten.ManagementGovernanceService.ArchitectureTests/Modules/Dws/dws-local-test-mutation-evidence-v1.json");
        using var json=JsonDocument.Parse(File.ReadAllText(path));
        Assert.Equal("dws-local-test-mutation-evidence-v1",json.RootElement.GetProperty("schemaVersion").GetString());
        var entries=json.RootElement.GetProperty("mutations").EnumerateArray().ToArray();
        Assert.Equal(10,entries.Length);
        Assert.Equal(10,entries.Select(x=>x.GetProperty("id").GetString()).Distinct(StringComparer.Ordinal).Count());
        var configuration=new DirectoryInfo(AppContext.BaseDirectory).Parent?.Name??throw new InvalidOperationException("configuration_not_found");
        foreach(var entry in entries)
        {
            Assert.Equal(0,entry.GetProperty("compileExit").GetInt32());
            Assert.NotEqual(0,entry.GetProperty("targetedExit").GetInt32());
            Assert.False(string.IsNullOrWhiteSpace(entry.GetProperty("failureText").GetString()));
            var source=Path.Combine(root,entry.GetProperty("sourcePath").GetString()!);
            Assert.Equal(entry.GetProperty("restoredSha256").GetString(),Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(source))).ToLowerInvariant());
            var typeName=entry.GetProperty("testType").GetString()!;
            var project=typeName.Contains(".IntegrationTests.",StringComparison.Ordinal)?"Diten.ManagementGovernanceService.IntegrationTests":typeName.Contains(".ArchitectureTests.",StringComparison.Ordinal)?"Diten.ManagementGovernanceService.ArchitectureTests":"Diten.ManagementGovernanceService.Tests";
            var assembly=Assembly.LoadFrom(Path.Combine(root,"services/Diten.ManagementGovernanceService/tests",project,"bin",configuration,"net8.0",project+".dll"));
            Assert.NotNull(assembly.GetType(typeName,true)!.GetMethod(entry.GetProperty("testMethod").GetString()!,BindingFlags.Instance|BindingFlags.Public));
        }
    }
    private static string FindRoot(){var cursor=new DirectoryInfo(AppContext.BaseDirectory);while(cursor is not null&&!File.Exists(Path.Combine(cursor.FullName,"AGENTS.md")))cursor=cursor.Parent;return cursor?.FullName??throw new InvalidOperationException("repo_root_not_found");}
}
