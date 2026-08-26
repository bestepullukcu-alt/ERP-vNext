using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Xunit;

namespace Diten.ManagementGovernanceService.ArchitectureTests.Modules.Dws;

public sealed class DwsMutationEvidenceTests
{
    [Fact]
    public void Durable_external_mutation_evidence_is_hash_bound_expected_red_and_reflection_verified()
    {
        var root=FindRoot();var path=Path.Combine(root,"services/Diten.ManagementGovernanceService/tests/Diten.ManagementGovernanceService.ArchitectureTests/Modules/Dws/dws-mutation-evidence-v1.json");using var document=JsonDocument.Parse(File.ReadAllText(path));Assert.Equal("dws-mutation-evidence-v1",document.RootElement.GetProperty("schemaVersion").GetString());var entries=document.RootElement.GetProperty("mutations").EnumerateArray().ToArray();Assert.Equal(5,entries.Length);Assert.Equal(5,entries.Select(x=>x.GetProperty("id").GetString()).Distinct(StringComparer.Ordinal).Count());Assert.Equal(3,entries.Count(x=>x.GetProperty("disposition").GetString()=="expected-red"));Assert.Equal(2,entries.Count(x=>x.GetProperty("disposition").GetString()=="security-policy-forbidden"));
        var configuration=new DirectoryInfo(AppContext.BaseDirectory).Parent?.Name??throw new InvalidOperationException("configuration_not_found");
        foreach(var entry in entries)
        {
            var source=Path.Combine(root,entry.GetProperty("sourcePath").GetString()!);Assert.True(File.Exists(source));var hash=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(source))).ToLowerInvariant();Assert.Equal(entry.GetProperty("restoredSha256").GetString(),hash);Assert.False(string.IsNullOrWhiteSpace(entry.GetProperty("failureText").GetString()));
            if(entry.GetProperty("disposition").GetString()=="expected-red"){Assert.Equal(0,entry.GetProperty("compileExit").GetInt32());Assert.NotEqual(0,entry.GetProperty("targetedExit").GetInt32());}
            var typeName=entry.GetProperty("testType").GetString()!;var project=typeName.Contains(".IntegrationTests.",StringComparison.Ordinal)?"Diten.ManagementGovernanceService.IntegrationTests":typeName.Contains(".ArchitectureTests.",StringComparison.Ordinal)?"Diten.ManagementGovernanceService.ArchitectureTests":"Diten.ManagementGovernanceService.Tests";var assembly=Assembly.LoadFrom(Path.Combine(root,"services/Diten.ManagementGovernanceService/tests",project,"bin",configuration,"net8.0",project+".dll"));var type=assembly.GetType(typeName,true)!;Assert.NotNull(type.GetMethod(entry.GetProperty("testMethod").GetString()!,BindingFlags.Public|BindingFlags.Instance));
        }
    }
    private static string FindRoot(){var cursor=new DirectoryInfo(AppContext.BaseDirectory);while(cursor is not null&&!File.Exists(Path.Combine(cursor.FullName,"AGENTS.md")))cursor=cursor.Parent;return cursor?.FullName??throw new InvalidOperationException("repo_root_not_found");}
}
