using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Diten.InventoryService.Domain.Expiry.Rules;
using Diten.InventoryService.Domain.Traceability.Entities;
using Diten.InventoryService.Domain.Traceability.Enums;
using Diten.InventoryService.Tests.TestInfrastructure;
using Xunit;
using YamlDotNet.Serialization;

namespace Diten.InventoryService.Tests.Expiry;

/// <summary>
/// WP-0177-R3 owner step MOD-0176 (CT-R12) — FEFO-BUNDLE 1.2.0 → 1.3.0 is ADDITIVE: <c>ExcludedReason</c> gains <c>RECALLED</c> (appended) and one
/// changelog line; nothing else of 1.2.0 changes. The rule: a candidate under an active recall is excluded with that reason (never first).
/// </summary>
public sealed class FefoRecalledContractTests
{
    private const string FrozenFefoV120Sha256 = "a1969228480d9bd4a06884c058ad5762bf9f3d9db0089daa1d6343a16d53ed79";

    internal static string FixtureV120Path => Path.Combine(RepoPaths.ServiceRoot, "tests", "Diten.InventoryService.Tests", "Contract", "Fixtures", "fefo-v1.2.0.openapi.yaml");

    private static JsonObject Load(string path)
        => (JsonObject)JsonNode.Parse(JsonSerializer.Serialize(new DeserializerBuilder().Build().Deserialize<object>(File.ReadAllText(path))))!;

    private static List<string> Strings(JsonNode? node) => node!.AsArray().Select(v => v!.GetValue<string>()).ToList();

    [Fact]
    public void The_fefo_v1_2_0_fixture_is_the_frozen_file_byte_for_byte()
        => Assert.Equal(FrozenFefoV120Sha256,
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(File.ReadAllText(FixtureV120Path).Replace("\r\n", "\n")))).ToLowerInvariant());

    [Fact]
    public void The_1_3_0_change_is_RECALLED_on_ExcludedReason_and_one_changelog_line()
    {
        var before = Load(FixtureV120Path);
        var after = Load(ContractSchemas.FefoContractPath);
        Assert.Equal(("1.2.0", "1.3.0"), (before["info"]!["version"]!.GetValue<string>(), after["info"]!["version"]!.GetValue<string>()));
        Assert.Equal([.. Strings(before["components"]!["schemas"]!["ExcludedReason"]!["enum"]), "RECALLED"], Strings(after["components"]!["schemas"]!["ExcludedReason"]!["enum"]));
        var oldLog = Strings(before["info"]!["x-changelog"]);
        var newLog = Strings(after["info"]!["x-changelog"]);
        Assert.Equal(oldLog, newLog.Take(oldLog.Count));
        Assert.StartsWith("1.3.0 — additive (WP-0177-R3", Assert.Single(newLog.Skip(oldLog.Count)));

        foreach (var doc in new[] { before, after })
        {
            doc["info"]!.AsObject().Remove("version");
            doc["info"]!.AsObject().Remove("x-changelog");
            doc["components"]!["schemas"]!["ExcludedReason"]!.AsObject().Remove("enum");
        }

        Assert.True(JsonNode.DeepEquals(before, after), "fefo.openapi.yaml 1.3.0 changes more than RECALLED, the version and the changelog.");
    }

    [Fact]
    public void A_recalled_candidate_is_excluded_with_RECALLED_and_the_next_lot_is_first()
    {
        var sku = Guid.NewGuid();
        Lot L(string number, int days) => new()
        {
            Id = Guid.NewGuid(), LotNumber = number, SkuId = sku, Status = LotStatus.Released, ExpiryDate = new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero).AddDays(days)
        };
        var recalled = L("RC-FIRST", 0);
        var next = L("NEXT", 30);

        var order = FefoOrderRule.Order([new FefoCandidate(recalled, 10m, 0m, Recalled: true), new FefoCandidate(next, 5m, 0m)],
            new DateTimeOffset(2026, 10, 14, 0, 0, 0, TimeSpan.Zero), null);

        Assert.Equal(next.Id, Assert.Single(order.Eligible).Lot.Id);
        var excluded = Assert.Single(order.Excluded);
        Assert.Equal((recalled.Id, FefoExclusionReason.Recalled), (excluded.Lot.Id, excluded.Reason));
        Assert.Equal("RECALLED", Diten.InventoryService.Application.Features.Expiry.Handlers.ExpiryLotView.Reason(FefoExclusionReason.Recalled));
        Assert.Equal(Enum.GetValues<FefoExclusionReason>().Length, Enum.GetValues<FefoExclusionReason>().Select(r =>
            Diten.InventoryService.Application.Features.Expiry.Handlers.ExpiryLotView.Reason(r)).Distinct().Count());
    }
}
