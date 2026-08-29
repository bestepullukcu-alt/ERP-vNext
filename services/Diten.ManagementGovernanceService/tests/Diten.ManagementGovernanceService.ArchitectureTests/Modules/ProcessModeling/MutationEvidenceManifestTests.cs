using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Diten.ManagementGovernanceService.Persistence.Modules.ProcessModeling;

namespace Diten.ManagementGovernanceService.ArchitectureTests.Modules.ProcessModeling;

public sealed class MutationEvidenceManifestTests
{
    [Fact]
    public void Safe_physical_campaign_recomputes_restored_files_and_binds_expected_red_tests()
    {
        using var manifest=LoadManifest();var rows=manifest.RootElement.GetProperty("safePhysicalMutations").EnumerateArray().ToArray();Assert.Equal(5,rows.Length);
        foreach(var row in rows)
        {
            var path=Path.Combine(RepositoryRoot(),row.GetProperty("sourcePath").GetString()!);Assert.True(File.Exists(path),path);
            Assert.Equal(row.GetProperty("restoredSha256").GetString(),Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant());
            Assert.Equal(0,row.GetProperty("compileExitCode").GetInt32());Assert.NotEqual(0,row.GetProperty("targetedExitCode").GetInt32());Assert.False(string.IsNullOrWhiteSpace(row.GetProperty("expectedFailure").GetString()));
            AssertTestExists(row.GetProperty("expectedRedTest").GetString()!);
        }
    }

    [Fact]
    public void Guardian_dispositions_bind_exact_executable_behavior_test()
    {
        using var manifest=LoadManifest();var rows=manifest.RootElement.GetProperty("guardianDispositions").EnumerateArray().ToArray();Assert.Equal(2,rows.Length);
        Assert.Equal(new[]{"expected-version-cas","tenant-predicate"},rows.Select(x=>x.GetProperty("id").GetString()).Order().ToArray());
        foreach(var row in rows){Assert.Equal("guardian-rejected",row.GetProperty("disposition").GetString());Assert.False(string.IsNullOrWhiteSpace(row.GetProperty("rationale").GetString()));Assert.False(string.IsNullOrWhiteSpace(row.GetProperty("behavior").GetString()));AssertTestExists(row.GetProperty("compensatingTest").GetString()!);}
    }

    [Fact]
    public void Four_test_owned_fault_seams_bind_typed_production_participants_and_executable_test()
    {
        using var manifest=LoadManifest();var expected=manifest.RootElement.GetProperty("faultParticipants").EnumerateArray().Select(x=>x.GetString()).ToArray();
        Assert.Equal(Enum.GetNames<ProcessModelingMutationParticipant>(),expected);AssertTestExists(manifest.RootElement.GetProperty("faultSeamTest").GetString()!);
    }

    private static JsonDocument LoadManifest()=>JsonDocument.Parse(File.ReadAllText(Path.Combine(ServiceRoot(),"tests","Diten.ManagementGovernanceService.ArchitectureTests","Modules","ProcessModeling","mutation-evidence-v1.json")));
    private static void AssertTestExists(string identity)
    {
        var split=identity.Split('.',2);Assert.Equal(2,split.Length);
        var assemblies=Directory.GetFiles(Path.Combine(ServiceRoot(),"tests"),"Diten.ManagementGovernanceService.*Tests.dll",SearchOption.AllDirectories).Where(x=>x.Contains(Path.Combine("bin","Debug","net8.0"),StringComparison.Ordinal)).Select(Assembly.LoadFrom).ToArray();
        Assert.Contains(assemblies.SelectMany(x=>x.GetTypes()),t=>t.Name==split[0]&&t.GetMethod(split[1],BindingFlags.Public|BindingFlags.Instance|BindingFlags.Static) is not null);
    }
    private static string ServiceRoot(){var cursor=new DirectoryInfo(AppContext.BaseDirectory);while(cursor is not null&&!string.Equals(cursor.Name,"Diten.ManagementGovernanceService",StringComparison.Ordinal))cursor=cursor.Parent;return cursor?.FullName??throw new InvalidOperationException();}
    private static string RepositoryRoot(){var cursor=new DirectoryInfo(ServiceRoot());while(cursor.Parent is not null&&!Directory.Exists(Path.Combine(cursor.FullName,".git"))&&!File.Exists(Path.Combine(cursor.FullName,".git")))cursor=cursor.Parent;return cursor.FullName;}
}
