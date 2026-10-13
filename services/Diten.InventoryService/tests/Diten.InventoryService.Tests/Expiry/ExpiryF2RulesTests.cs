using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Expiry.Rules;
using Diten.InventoryService.Domain.Traceability.Entities;
using Diten.InventoryService.Domain.Traceability.Enums;
using Diten.InventoryService.Tests.TestInfrastructure;
using Xunit;
using YamlDotNet.Serialization;

namespace Diten.InventoryService.Tests.Expiry;

/// <summary>WP-0176-F2 — the pure FEFO / shelf-life / suggestion / scope rules (pack §8.2, §8.3) measured on the production functions.</summary>
public sealed class ExpiryF2RulesTests
{
    private static readonly DateTimeOffset AsOf = new(2026, 9, 30, 10, 0, 0, TimeSpan.Zero);

    private static Lot L(string number, string? expiry, LotStatus status = LotStatus.Released, string? manufacturing = null, string? retest = null)
        => new()
        {
            Id = Guid.NewGuid(),
            LotNumber = number,
            Status = status,
            ExpiryDate = expiry is null ? null : DateTimeOffset.Parse(expiry + "T00:00:00Z"),
            ManufacturingDate = manufacturing is null ? null : DateTimeOffset.Parse(manufacturing + "T00:00:00Z"),
            RetestDate = retest is null ? null : DateTimeOffset.Parse(retest + "T00:00:00Z")
        };

    private static Lot Held(Lot lot)
    {
        lot.RetestHeldAtUtc = AsOf.AddDays(-1); // the MOD-0175 retest hold
        return lot;
    }

    private static FefoCandidate C(Lot lot, decimal available = 5m, decimal held = 0m) => new(lot, available, held);

    [Fact]
    public void The_order_is_by_expiry_then_manufacturing_then_lot_number_with_nulls_last()
    {
        var noExpiry = L("A", null);
        var late = L("B", "2027-06-30");
        var tieLate = L("C", "2027-01-31", manufacturing: "2026-02-01");
        var tieEarly = L("D", "2027-01-31", manufacturing: "2026-01-01");
        var order = FefoOrderRule.Order([C(noExpiry), C(late), C(tieLate), C(tieEarly)], AsOf, null);
        Assert.Equal(["D", "C", "B", "A"], order.Eligible.Select(e => e.Lot.LotNumber));
    }

    [Fact]
    public void Exclusions_follow_the_pack_order()
    {
        var order = FefoOrderRule.Order(
        [
            C(L("blocked", "2026-10-01", LotStatus.Blocked)),
            C(L("expired", "2026-09-01")),
            C(L("retest", "2027-06-30", retest: "2026-09-01")),
            C(Held(L("held", "2027-06-30"))),
            C(L("short", "2026-10-20")),
            C(L("quarantined", "2026-10-15"), available: 0m, held: 5m), // below the minimum too: the earlier reason wins
            C(L("onlyHeldQty", "2027-06-30"), available: 0m, held: 5m),
            C(L("ok", "2027-06-30"))
        ], AsOf, minRemainingShelfLifeDays: 60);
        Assert.Equal(["ok"], order.Eligible.Select(e => e.Lot.LotNumber));
        var reasons = order.Excluded.ToDictionary(e => e.Lot.LotNumber, e => e.Reason);
        Assert.Equal(FefoExclusionReason.Blocked, reasons["blocked"]);
        Assert.Equal(FefoExclusionReason.Expired, reasons["expired"]);
        Assert.Equal(FefoExclusionReason.RetestDue, reasons["retest"]);
        Assert.Equal(FefoExclusionReason.Held, reasons["held"]);
        Assert.Equal(FefoExclusionReason.BelowMinRemaining, reasons["short"]);
        Assert.Equal(FefoExclusionReason.BelowMinRemaining, reasons["quarantined"]);
        Assert.Equal(FefoExclusionReason.Quarantined, reasons["onlyHeldQty"]);
    }

    [Fact]
    public void Only_a_strictly_earlier_expiry_makes_a_bypass()
    {
        var early = L("E", "2027-01-31");
        var same = L("S", "2027-01-31", manufacturing: "2026-03-01");
        var late = L("L", "2027-06-30");
        var order = FefoOrderRule.Order([C(early), C(same), C(late)], AsOf, null);
        Assert.True(FefoIssueRule.Evaluate(early, order).Compliant);
        Assert.True(FefoIssueRule.Evaluate(same, order).Compliant); // equal expiry: compliant (K3 sabotage (d))
        var bypass = FefoIssueRule.Evaluate(late, order);
        Assert.False(bypass.Compliant);
        Assert.Equal("S", bypass.FirstEligible!.LotNumber); // tie on expiry → the manufacturing date breaks it (nulls last)
        // an issued lot without expiry while an eligible lot has one is a bypass
        var none = L("N", null);
        Assert.False(FefoIssueRule.Evaluate(none, FefoOrderRule.Order([C(early), C(none)], AsOf, null)).Compliant);
    }

    [Theory]
    [InlineData(null, 90, MinRemainingOutcome.NotApplicable)]
    [InlineData("2027-06-30", null, MinRemainingOutcome.NotDefined)]
    [InlineData("2026-12-29", 90, MinRemainingOutcome.Ok)]   // 90 days left = the minimum
    [InlineData("2026-12-28", 90, MinRemainingOutcome.Below)] // 89
    public void Min_remaining_shelf_life(string? expiry, int? minimum, MinRemainingOutcome expected)
        => Assert.Equal(expected, MinRemainingShelfLifeRule.Evaluate(expiry is null ? null : DateTimeOffset.Parse(expiry + "T00:00:00Z"), minimum, AsOf).Outcome);

    [Fact]
    public void The_suggestion_is_manufacturing_plus_the_SKU_days()
    {
        var s = LotDateSuggestionRule.Suggest(new DateOnly(2026, 1, 1), 365, 180);
        Assert.Equal(new DateOnly(2027, 1, 1), s.Expiry);
        Assert.Equal(new DateOnly(2026, 6, 30), s.Retest);
        Assert.True(LotDateSuggestionRule.Suggest(null, 365, 180).IsEmpty);
        Assert.False(LotDateSuggestionRule.Differs(s, new DateOnly(2027, 1, 1), new DateOnly(2026, 6, 30)));
        Assert.True(LotDateSuggestionRule.Differs(s, new DateOnly(2026, 12, 1), new DateOnly(2026, 6, 30)));
        Assert.True(LotDateSuggestionRule.Differs(s, null, new DateOnly(2026, 6, 30))); // ASSUMPTION-0176-07
    }

    [Theory]
    [InlineData(MovementType.GoodsIssueSales, StockStatus.Available, null, "MOD-0183", ExpiryHookKind.Issue)]
    [InlineData(MovementType.GoodsIssueProd, StockStatus.Available, null, "MOD-TEST", ExpiryHookKind.Issue)]
    [InlineData(MovementType.GoodsIssueSales, StockStatus.Quarantine, null, "MOD-TEST", ExpiryHookKind.None)]
    [InlineData(MovementType.GoodsIssueSales, StockStatus.Available, null, "MOD-0175", ExpiryHookKind.Issue)] // FIX1: sourceModule never exempts
    [InlineData(MovementType.GoodsReceiptPo, null, StockStatus.Available, "MOD-0175", ExpiryHookKind.Receipt)]
    [InlineData(MovementType.GoodsReceiptPo, null, StockStatus.Available, "MOD-TEST", ExpiryHookKind.Receipt)]
    [InlineData(MovementType.GoodsReceiptPo, null, StockStatus.Quarantine, "MOD-TEST", ExpiryHookKind.Receipt)]
    [InlineData(MovementType.OpeningBalance, null, StockStatus.Available, "MOD-TEST", ExpiryHookKind.None)]
    [InlineData(MovementType.Reversal, StockStatus.Available, null, "MOD-0173", ExpiryHookKind.None)]
    [InlineData(MovementType.Scrap, StockStatus.Available, null, "MOD-TEST", ExpiryHookKind.None)]
    [InlineData(MovementType.WriteOff, StockStatus.Available, null, "MOD-TEST", ExpiryHookKind.None)]
    [InlineData(MovementType.StatusTransfer, StockStatus.Available, StockStatus.Quarantine, "MOD-TEST", ExpiryHookKind.None)]
    [InlineData(MovementType.LocationTransfer, StockStatus.Available, StockStatus.Available, "MOD-TEST", ExpiryHookKind.None)]
    [InlineData(MovementType.WarehouseTransfer, StockStatus.Available, StockStatus.Available, "MOD-TEST", ExpiryHookKind.None)]
    [InlineData(MovementType.CountGain, null, StockStatus.Available, "MOD-TEST", ExpiryHookKind.None)]
    [InlineData(MovementType.CountLoss, StockStatus.Available, null, "MOD-TEST", ExpiryHookKind.None)]
    [InlineData(MovementType.SupplierReturn, StockStatus.Available, null, "MOD-TEST", ExpiryHookKind.None)]
    public void The_hook_scope(MovementType type, StockStatus? from, StockStatus? to, string sourceModule, ExpiryHookKind expected)
        => Assert.Equal(expected, ExpiryHookScope.For(type, from, to, sourceModule, hasRegisteredLot: true));

    // ── FEFO-BUNDLE 1.0.0 → 1.0.1 is description only ────────────────────────────────────────────────────

    private const string FrozenFefoV100Sha256 = "bf0d1a2f65f6b58b6d0f603fce8c52500e5b3510614e4c8a867661ce4f29e9b6";

    private static string FixturePath => Path.Combine(RepoPaths.ServiceRoot, "tests", "Diten.InventoryService.Tests", "Contract", "Fixtures", "fefo-v1.0.0.openapi.yaml");

    private static JsonObject Load(string path)
    {
        var raw = new DeserializerBuilder().Build().Deserialize<object>(File.ReadAllText(path));
        return (JsonObject)JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(raw))!;
    }

    [Fact]
    public void The_fefo_v1_0_0_fixture_is_the_frozen_file_byte_for_byte()
    {
        var text = File.ReadAllText(FixturePath).Replace("\r\n", "\n");
        Assert.Equal(FrozenFefoV100Sha256, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant());
    }

    [Fact]
    public void The_1_0_1_change_is_the_implemented_by_slice_note_and_nothing_else()
    {
        var before = Load(FixturePath);
        var after = Load(ContractSchemas.FefoContractPath);
        Assert.Equal("1.0.0", before["info"]!["version"]!.GetValue<string>());
        Assert.Equal("1.0.1", after["info"]!["version"]!.GetValue<string>());

        var slices = after["info"]!["x-implemented-by-slice"]!.AsObject();
        Assert.Equal(["createDateOverride"], slices["F2"]!.AsArray().Select(v => v!.GetValue<string>()));
        Assert.DoesNotContain("createDateOverride", slices["not-yet-implemented"]!.AsArray().Select(v => v!.GetValue<string>()));
        var changelogBefore = before["info"]!["x-changelog"]!.AsArray().Select(v => v!.GetValue<string>()).ToList();
        var changelogAfter = after["info"]!["x-changelog"]!.AsArray().Select(v => v!.GetValue<string>()).ToList();
        Assert.Equal(changelogBefore, changelogAfter.Take(changelogBefore.Count));
        Assert.Single(changelogAfter.Skip(changelogBefore.Count));

        // everything else — paths, schemas, codes, permissions — is byte-equal
        foreach (var doc in new[] { before, after })
        {
            var info = doc["info"]!.AsObject();
            info.Remove("version");
            info.Remove("x-changelog");
            info.Remove("x-implemented-by-slice");
        }

        Assert.True(JsonNode.DeepEquals(before, after), "fefo.openapi.yaml 1.0.1 changes more than the version, the changelog and x-implemented-by-slice.");
    }
}
