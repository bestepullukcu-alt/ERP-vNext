using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Diten.BuildingBlocks.Eventing;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Infrastructure.Eventing;
using Xunit;

namespace Diten.Platform.Application.Tests.Eventing;

public sealed class Mod0355PerformanceImprovementAuditEvidenceTests
{
    private const string Checkpoint = "fa39a659ae58568bfa0a76dcb70a27980e19e0ca";
    private const string Root = "execution/domains/management-governance/module-packs/fixtures/MOD-0355/performance-improvement-audit";
    private const string Manifest = "execution/domains/platform-shared-services/module-packs/fixtures/MOD-0021/gate-i/mod-0355-performance-improvement-bilateral-acceptance-v1.json";
    private const string Producer = "Diten.ManagementGovernanceService";
    private const string EventName = "management-governance.process-performance-improvement-audit-intent.submitted.v1";

    [Fact]
    public void Owner_checkpoint_and_consumer_manifest_bind_exact_bytes_without_copying()
    {
        var payload = GitBytes("show", $"{Checkpoint}:{Root}/canonical-payload-v1.json");
        using var vector = JsonDocument.Parse(GitBytes("show", $"{Checkpoint}:{Root}/signing-vector-v1.json"));
        using var manifest = JsonDocument.Parse(File.ReadAllBytes(RepositoryPath(Manifest)));
        var expected = manifest.RootElement;

        Assert.Equal(271, payload.Length);
        Assert.Equal(expected.GetProperty("canonicalSha256").GetString(), Sha256(payload));
        Assert.Equal(expected.GetProperty("testOnlyHmacSha256").GetString(),
            Convert.ToHexString(HMACSHA256.HashData(
                Convert.FromHexString(vector.RootElement.GetProperty("keyHex").GetString()!), payload)).ToLowerInvariant());
        Assert.Equal(Checkpoint, expected.GetProperty("producerCheckpoint").GetString());
        Assert.False(expected.GetProperty("productionKeyProvisioned").GetBoolean());
        Assert.False(expected.GetProperty("publisherActivation").GetBoolean());
    }

    [Fact]
    public void Exact_seven_positive_and_eleven_negative_pairs_are_closed_and_projected()
    {
        using var contract = JsonDocument.Parse(GitBytes("show", $"{Checkpoint}:{Root}/contract-v1.json"));
        var root = contract.RootElement;
        var positive = Pairs(root, "positivePairs");
        var negative = Pairs(root, "negativePairs");
        Assert.Equal(7, positive.Count);
        Assert.Equal(11, negative.Count);
        Assert.Empty(positive.Intersect(negative));
        Assert.Equal(18, positive.Concat(negative).Distinct().Count());

        var registry = new GateIAuditAdapterRegistry();
        Assert.Equal(7, registry.ForProducer(Producer, EventName, 1).Count);
        foreach (var pair in positive)
        {
            var mapping = registry.Resolve(Producer, EventName, 1, pair.Entity, pair.Mutation);
            Assert.Equal("MOD-0355", mapping.ModuleCode);
            Assert.Equal(AuditCategory.Integration, mapping.Category);
            Assert.StartsWith("ProcessModeling.", mapping.AuditEntityType);
        }
        foreach (var pair in negative)
            Assert.Throws<EventUnsupportedException>(() =>
                registry.Resolve(Producer, EventName, 1, pair.Entity, pair.Mutation));
    }

    [Fact]
    public void Identity_version_schema_and_test_material_are_exact_and_non_production()
    {
        using var contract = JsonDocument.Parse(GitBytes("show", $"{Checkpoint}:{Root}/contract-v1.json"));
        using var vector = JsonDocument.Parse(GitBytes("show", $"{Checkpoint}:{Root}/signing-vector-v1.json"));
        Assert.Equal("ProcessPerformanceImprovementAuditIntentSubmittedV1", contract.RootElement.GetProperty("eventType").GetString());
        Assert.Equal(EventName, contract.RootElement.GetProperty("eventName").GetString());
        Assert.Equal(1, contract.RootElement.GetProperty("eventVersion").GetInt32());
        Assert.Equal("MOD-0355", contract.RootElement.GetProperty("moduleCode").GetString());
        Assert.Equal("urn:diten:management-governance:process-performance-improvement-audit-intent-submitted:v1",
            contract.RootElement.GetProperty("schemaId").GetString());
        Assert.Equal("none", vector.RootElement.GetProperty("productionAuthority").GetString());
        Assert.Equal("HMAC-SHA256-TEST-ONLY", vector.RootElement.GetProperty("scheme").GetString());
        Assert.Equal(new[] { "identity", "keyId", "signature" },
            contract.RootElement.GetProperty("productionReject").EnumerateArray().Select(x => x.GetString()));
    }

    [Fact]
    public void Existing_process_modeling_profile_remains_exact_twenty_and_separate()
    {
        var registry = new GateIAuditAdapterRegistry();
        Assert.Equal(20, registry.ForProducer(Producer,
            "management-governance.process-modeling-audit-intent.submitted.v1", 1).Count);
        Assert.Throws<EventUnsupportedException>(() => registry.Resolve(Producer,
            "management-governance.process-modeling-audit-intent.submitted.v1", 1,
            "ProcessImprovementOpportunity", "Qualified"));
    }

    private static HashSet<(string Entity, string Mutation)> Pairs(JsonElement root, string property) =>
        root.GetProperty(property).EnumerateArray()
            .Select(x => (x[0].GetString()!, x[1].GetString()!)).ToHashSet();

    private static string Sha256(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static string RepositoryPath(string relative)
    {
        var cursor = new DirectoryInfo(AppContext.BaseDirectory);
        while (cursor is not null && !Directory.Exists(Path.Combine(cursor.FullName, "execution"))) cursor = cursor.Parent;
        return Path.Combine(cursor?.FullName ?? throw new InvalidOperationException("repository_root_missing"), relative);
    }

    private static byte[] GitBytes(params string[] arguments)
    {
        using var process = Process.Start(new ProcessStartInfo("git", arguments)
        {
            WorkingDirectory = RepositoryPath(string.Empty), RedirectStandardOutput = true,
            RedirectStandardError = true, UseShellExecute = false
        }) ?? throw new InvalidOperationException("git_start_failed");
        using var output = new MemoryStream();
        process.StandardOutput.BaseStream.CopyTo(output);
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0) throw new InvalidOperationException(error);
        return output.ToArray();
    }
}
