using System.Security.Cryptography;
using System.Text.Json;
using Diten.ManagementGovernanceService.Application.Modules.ProcessModeling;
using Diten.ManagementGovernanceService.Application.Modules.ProcessModeling.PerformanceImprovement;

namespace Diten.ManagementGovernanceService.Tests.Modules.ProcessModeling.PerformanceImprovement;

public sealed class PerformanceImprovementFixtureTests
{
    [Fact]
    public void Producer_contract_is_exact_seven_positive_eleven_negative_and_closed_cartesian()
    {
        using var contract = JsonDocument.Parse(File.ReadAllBytes(Fixture("contract-v1.json")));
        var root = contract.RootElement;
        Assert.Equal(PerformanceImprovementContract.SchemaIdentity, root.GetProperty("schemaId").GetString());
        Assert.Equal(PerformanceImprovementContract.EventType, root.GetProperty("eventType").GetString());
        Assert.Equal(PerformanceImprovementContract.EventName, root.GetProperty("eventName").GetString());
        Assert.Equal(1, root.GetProperty("eventVersion").GetInt32());
        Assert.Equal("MOD-0355", root.GetProperty("moduleCode").GetString());
        Assert.Equal(new[] { "auditIntentId", "actorId", "entityType", "entityId", "mutation", "occurredAtUtc" },
            root.GetProperty("canonicalPropertyOrder").EnumerateArray().Select(x => x.GetString()));

        var positive = Pairs(root, "positivePairs");
        var negative = Pairs(root, "negativePairs");
        Assert.Equal(7, positive.Count);
        Assert.Equal(11, negative.Count);
        Assert.Empty(positive.Intersect(negative));
        var entities = positive.Concat(negative).Select(x => x.Entity).Distinct(StringComparer.Ordinal).ToArray();
        var mutations = positive.Concat(negative).Select(x => x.Mutation).Distinct(StringComparer.Ordinal).ToArray();
        Assert.Equal(3, entities.Length);
        Assert.Equal(6, mutations.Length);
        Assert.Equal(18, positive.Concat(negative).Distinct().Count());
    }

    [Fact]
    public void Canonical_payload_checksum_and_test_only_signature_are_exact()
    {
        var payload = File.ReadAllBytes(Fixture("canonical-payload-v1.json"));
        Assert.Equal(271, payload.Length);
        Assert.Equal("b1aa7e80335cda8e82676852f3fed5c20cbbed2af3e10f954086f957a4a78531", Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant());
        Assert.Equal(File.ReadAllText(Fixture("canonical-payload-v1.sha256")).Trim(), Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant());
        using var vector = JsonDocument.Parse(File.ReadAllBytes(Fixture("signing-vector-v1.json")));
        var key = Convert.FromHexString(vector.RootElement.GetProperty("keyHex").GetString()!);
        Assert.Equal(vector.RootElement.GetProperty("expectedSignature").GetString(), Convert.ToHexString(HMACSHA256.HashData(key, payload)).ToLowerInvariant());
        Assert.Equal("none", vector.RootElement.GetProperty("productionAuthority").GetString());
        Assert.Equal(new[] { "identity", "keyId", "signature" },
            JsonDocument.Parse(File.ReadAllBytes(Fixture("contract-v1.json"))).RootElement.GetProperty("productionReject").EnumerateArray().Select(x => x.GetString()));
    }

    [Fact]
    public void Current_sixteen_entry_fu16_owner_manifest_rejects_every_additive_operation()
    {
        Assert.Equal(16, ProcessModelingPermissions.ExactPermissions.Count);
        Assert.Equal(20, ProcessModelingPermissions.ExactCommandMap.Count);
        Assert.All(PerformanceImprovementPermissions.ExactOperationMap.Keys,
            permission => Assert.DoesNotContain(permission, ProcessModelingPermissions.ExactPermissions));
    }

    private static HashSet<(string Entity, string Mutation)> Pairs(JsonElement root, string property) =>
        root.GetProperty(property).EnumerateArray().Select(x => (x[0].GetString()!, x[1].GetString()!)).ToHashSet();

    private static string Fixture(string name)
    {
        var cursor = new DirectoryInfo(AppContext.BaseDirectory);
        while (cursor is not null && !Directory.Exists(Path.Combine(cursor.FullName, "execution"))) cursor = cursor.Parent;
        return Path.Combine(cursor?.FullName ?? throw new InvalidOperationException("repository_root_missing"),
            "execution", "domains", "management-governance", "module-packs", "fixtures", "MOD-0355", "performance-improvement-audit", name);
    }
}
