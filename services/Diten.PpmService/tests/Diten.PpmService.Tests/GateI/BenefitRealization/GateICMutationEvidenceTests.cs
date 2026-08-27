using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Xunit;

namespace Diten.PpmService.Tests.GateI.BenefitRealization;

public sealed class GateICMutationEvidenceTests
{
    [Fact]
    public void Durable_mutation_evidence_binds_restored_source_and_compiled_expected_red_tests()
    {
        var root = FindRepositoryRoot();
        var evidencePath = Path.Combine(root,
            "services/Diten.PpmService/tests/Diten.PpmService.Tests/GateI/BenefitRealization/gate-ic-mutation-evidence-v1.json");
        using var document = JsonDocument.Parse(File.ReadAllBytes(evidencePath));
        var evidence = document.RootElement;
        Assert.Equal(1, evidence.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("MOD-0117-GATE-I-C", evidence.GetProperty("lane").GetString());

        var source = evidence.GetProperty("source").GetString()!;
        var actualHash = Convert.ToHexString(SHA256.HashData(GitShow(
                root, evidence.GetProperty("sourceCheckpoint").GetString()!, source)))
            .ToLowerInvariant();
        Assert.Equal(evidence.GetProperty("restoredSha256").GetString(), actualHash);

        var assembly = typeof(GateICMutationEvidenceTests).Assembly;
        var dispositions = evidence.GetProperty("securityPolicyDispositions").EnumerateArray().ToArray();
        Assert.Equal(2, dispositions.Length);
        foreach (var disposition in dispositions)
        {
            Assert.Equal("SECURITY-POLICY-FORBIDDEN", disposition.GetProperty("disposition").GetString());
            Assert.False(string.IsNullOrWhiteSpace(disposition.GetProperty("rationale").GetString()));
            AssertCompiledTest(assembly, disposition.GetProperty("compensatingTest").GetString()!);
        }

        var campaign = evidence.GetProperty("campaign").EnumerateArray().ToArray();
        Assert.Equal(4, campaign.Length);
        Assert.Equal(4, campaign.Select(item => item.GetProperty("id").GetString()).Distinct(StringComparer.Ordinal).Count());
        foreach (var item in campaign)
        {
            Assert.Equal(0, item.GetProperty("compileExit").GetInt32());
            Assert.NotEqual(0, item.GetProperty("targetedExit").GetInt32());
            Assert.False(string.IsNullOrWhiteSpace(item.GetProperty("failureIdentity").GetString()));
            AssertCompiledTest(assembly, item.GetProperty("test").GetString()!);
        }
    }

    private static byte[] GitShow(string root, string checkpoint, string source)
    {
        using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("git")
        {
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            ArgumentList = { "show", $"{checkpoint}:{source}" }
        }) ?? throw new InvalidOperationException("git_start_failed");
        using var stream = new MemoryStream();
        process.StandardOutput.BaseStream.CopyTo(stream);
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, error);
        return stream.ToArray();
    }

    private static void AssertCompiledTest(Assembly assembly, string identity)
    {
        var separator = identity.LastIndexOf('.');
        var type = assembly.GetType(identity[..separator], throwOnError: true)!;
        var method = type.GetMethod(identity[(separator + 1)..], BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(method);
        Assert.True(method!.GetCustomAttribute<FactAttribute>() is not null ||
                    method.GetCustomAttribute<TheoryAttribute>() is not null);
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, ".git")) ||
                File.Exists(Path.Combine(current.FullName, ".git"))) return current.FullName;
            current = current.Parent;
        }
        throw new InvalidOperationException("repository root unavailable");
    }
}
