using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Diten.PpmService.IntegrationTests.GateI.BenefitRealization;

public sealed class Mod0072ImmutableFixtureTests
{
    private const string Checkpoint = "b4589139e8c9db544de5b66300640b214db3acf4";
    private const string Root = "execution/domains/enterprise-strategy-business-performance/module-packs/fixtures/MOD-0072/audit/";

    [Fact]
    public void Canonical_fixture_is_read_binary_safe_from_exact_checkpoint()
    {
        var bytes = GitShowBytes(Root + "outcome-tracking-audit-intent-submitted-v1.canonical.json");
        Assert.Equal(246, bytes.Length);
        Assert.NotEqual(0xEF, bytes[0]);
        Assert.NotEqual((byte)'\n', bytes[^1]);
        Assert.Equal("6e1e750ffddc6f65d45556e703b0aa282b8469dec00bcc516a7a0a5f823cc2a3", Hex(SHA256.HashData(bytes)));
        Assert.Equal("{\"auditIntentId\":\"72000000-0000-4000-8000-000000000001\",\"actorId\":\"72000000-0000-4000-8000-000000000002\",\"entityType\":\"Outcome\",\"entityId\":\"72000000-0000-4000-8000-000000000003\",\"mutation\":\"Created\",\"occurredAtUtc\":\"2026-08-04T12:00:00.0000000Z\"}", Encoding.UTF8.GetString(bytes));
    }

    [Fact]
    public void Compatibility_matrix_binds_exact_identity_projection_and_negative_counts()
    {
        using var json = JsonDocument.Parse(GitShowBytes(Root + "outcome-tracking-audit-intent-submitted-v1.compatibility-matrix.json"));
        var root = json.RootElement;
        Assert.Equal("MOD-0072", root.GetProperty("contractIdentity").GetProperty("moduleCode").GetString());
        Assert.Equal(1, root.GetProperty("contractIdentity").GetProperty("eventVersion").GetInt32());
        Assert.Equal(246, root.GetProperty("pairBinding").GetProperty("canonicalByteLength").GetInt32());
        Assert.Equal(5, root.GetProperty("positiveAllowlist").GetArrayLength());
        Assert.Equal(10, root.GetProperty("negativeCrossPairs").GetArrayLength());
        Assert.Equal(31, root.GetProperty("negativeContractCases").GetArrayLength());
        Assert.True(root.GetProperty("negativeExpectedDisposition").GetProperty("terminal").GetBoolean());
        Assert.False(root.GetProperty("negativeExpectedDisposition").GetProperty("retry").GetBoolean());
    }

    [Fact]
    public void Delegated_and_nondelegated_provenance_are_exact_and_no_copy()
    {
        using var positive = JsonDocument.Parse(GitShowBytes(Root + "outcome-tracking-audit-intent-submitted-v1.delegated-provenance.positive.json"));
        using var negative = JsonDocument.Parse(GitShowBytes(Root + "outcome-tracking-audit-intent-submitted-v1.delegated-provenance.negative.json"));
        using var nondelegated = JsonDocument.Parse(GitShowBytes(Root + "outcome-tracking-audit-intent-submitted-v1.nondelegated-provenance.positive.json"));
        var input = positive.RootElement.GetProperty("input");
        Assert.Equal("diten-decision-intelligence-service", input.GetProperty("audience").GetString());
        Assert.Equal("diten.decision-intelligence", input.GetProperty("clientId").GetString());
        Assert.Equal("diten.s2s.delegated.invoke", input.GetProperty("scope").GetString());
        Assert.Equal(15, negative.RootElement.GetProperty("cases").GetArrayLength());
        Assert.Equal(6, nondelegated.RootElement.GetProperty("mustOmitMetadataFields").GetArrayLength());
        Assert.Contains("requestBody", positive.RootElement.GetProperty("forbiddenProjectionFields").EnumerateArray().Select(x => x.GetString()));
    }

    [Fact]
    public void All_nine_test_identity_production_rejections_are_terminal()
    {
        using var json = JsonDocument.Parse(GitShowBytes(Root + "outcome-tracking-audit-intent-submitted-v1.production-validator-rejection.json"));
        var root = json.RootElement;
        Assert.Equal(9, root.GetProperty("cases").GetArrayLength());
        Assert.True(root.GetProperty("expectedDispositionForEveryCase").GetProperty("terminalSecurityFailure").GetBoolean());
        Assert.False(root.GetProperty("expectedDispositionForEveryCase").GetProperty("retry").GetBoolean());
        Assert.False(root.GetProperty("expectedDispositionForEveryCase").GetProperty("productionSlotMutationApplied").GetBoolean());
        Assert.False(root.GetProperty("prohibitions").GetProperty("productionSecretOrCredentialMaterial").GetBoolean());
    }

    [Fact]
    public void Test_only_hmac_vector_recomputes_but_never_becomes_runtime_identity()
    {
        using var vectorDoc = JsonDocument.Parse(GitShowBytes(Root + "outcome-tracking-audit-intent-submitted-v1.signing-vector.json"));
        using var expectedDoc = JsonDocument.Parse(GitShowBytes(Root + "outcome-tracking-audit-intent-submitted-v1.expected-signature.json"));
        var vector = vectorDoc.RootElement;
        var payload = GitShowBytes(Root + vector.GetProperty("canonicalPayloadPath").GetString()!);
        var fields = new[]
        {
            vector.GetProperty("scheme").GetString(), vector.GetProperty("eventId").GetString(),
            vector.GetProperty("eventName").GetString(), vector.GetProperty("eventVersion").GetInt32().ToString(),
            vector.GetProperty("tenantId").GetString(), vector.GetProperty("correlationId").GetString(),
            vector.GetProperty("producer").GetString(), vector.GetProperty("causationId").GetString(),
            vector.GetProperty("occurredAtUtc").GetString(), payload.Length.ToString()
        };
        var prefix = Encoding.UTF8.GetBytes(string.Join("\n", fields) + "\n");
        var signingInput = new byte[prefix.Length + payload.Length];
        prefix.CopyTo(signingInput, 0);
        payload.CopyTo(signingInput, prefix.Length);
        var key = Encoding.UTF8.GetBytes(vector.GetProperty("testOnlyKeyUtf8").GetString()!);
        var signature = Hex(HMACSHA256.HashData(key, signingInput));
        Assert.Equal(expectedDoc.RootElement.GetProperty("expectedSignature").GetString(), signature);
        Assert.Contains("test-only", vector.GetProperty("keyId").GetString(), StringComparison.Ordinal);
    }

    private static byte[] GitShowBytes(string path)
    {
        var repo = FindRepositoryRoot();
        var start = new ProcessStartInfo("git")
        {
            WorkingDirectory = repo,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        start.ArgumentList.Add("show");
        start.ArgumentList.Add($"{Checkpoint}:{path}");
        using var process = Process.Start(start) ?? throw new InvalidOperationException("git process unavailable");
        using var stream = new MemoryStream();
        process.StandardOutput.BaseStream.CopyTo(stream);
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, error);
        return stream.ToArray();
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, ".git")) || File.Exists(Path.Combine(current.FullName, ".git")))
                return current.FullName;
            current = current.Parent;
        }
        throw new InvalidOperationException("repository root unavailable");
    }

    private static string Hex(ReadOnlySpan<byte> bytes) => Convert.ToHexString(bytes).ToLowerInvariant();
}
