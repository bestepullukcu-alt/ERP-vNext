using System.Text.Json;
using Diten.InventoryService.Tests.TestInfrastructure;
using Xunit;

namespace Diten.InventoryService.Tests.Quarantine;

/// <summary>
/// MOD-0175 Q4 (O-Q2-6 → Q4, ASSUMPTION-0175-Q4-02): "Karantinaya Al" / "Stoğu bloke et" on a serial-tracked bucket. The serials are
/// chosen by the SYSTEM — the Active registered serials of the SKU / lot at the bin, minus those an open 0175 record there names, in
/// intake order: the whole bucket takes them all, a part takes the first ones. They ride the movement (the ledger validates them —
/// without them a serial-tracked status transfer is refused) and are stored on the hold / blocked record, so a later decision moves
/// exactly them (FU-0175-Q1-SER). K3 sabotage (e): dropping the serials in the manual action → these tests are red.
/// </summary>
public sealed class QuarantineManualSerialTests : IClassFixture<QuarantineApiFactory>
{
    private readonly QuarantineApiFactory _factory;

    public QuarantineManualSerialTests(QuarantineApiFactory factory) => _factory = factory;

    private static string Id(JsonElement e, string name) => e.GetProperty(name).GetString()!;

    private static object Bucket(QuarantineScenario s, string lot, decimal quantity)
        => new { itemId = s.Ledger.Item, skuId = s.Ledger.Sku, skuLevel = "Lsku", warehouseId = s.Ledger.Warehouse, locationId = s.Ledger.LocationA, quantity, lotId = lot };

    private static List<string> Serials(MongoDB.Bson.BsonDocument record) => record["SerialIds"].AsBsonArray.Select(v => v.AsString).ToList();

    private static List<string> MovementSerials(QuarantineScenario s, string transactionId)
        => Assert.Single(s.Ledger.Persisted("inventory_transactions"), t => t["_id"].AsString == transactionId)["SerialIds"].AsBsonArray.Select(v => v.AsString).ToList();

    [Fact]
    public async Task Manual_quarantine_of_part_of_a_serial_bucket_takes_the_first_serials_and_the_release_moves_exactly_them()
    {
        var s = new QuarantineScenario(_factory, serialControlled: true);
        var lot = await s.ReleasedLotAsync();
        await s.ReceiveAsync("4", lot, "AVAILABLE", serials: ["MS-0001", "MS-0002", "MS-0003", "MS-0004"]);

        var key = QuarantineScenario.Key("hold");
        var hold = Assert.Single((await s.ExpectAsync(HttpMethod.Post, "/api/quarantine/holds",
            new { mode = "Manual", reason = QuarantineScenario.Reason, idempotencyKey = key, buckets = new[] { Bucket(s, lot, 2m) } }, 201, key)).EnumerateArray());
        var record = Assert.Single(s.Holds());
        Assert.Equal(["MS-0001", "MS-0002"], Serials(record));
        Assert.Equal(["MS-0001", "MS-0002"], MovementSerials(s, record["SourceMovementId"].AsString));
        Assert.Equal(2m, await s.OnHandAsync("QUARANTINE"));

        var applied = await s.DecideAndApproveAsync(s.DecideAsync(Id(hold, "holdId"), [QuarantineScenario.Line(1, "Release", 2m)]));
        Assert.Equal("Applied", Id(applied, "state"));
        var release = Assert.Single(await s.LedgerMovementsAsync(Id(applied, "decisionNumber")));
        Assert.Equal(["MS-0001", "MS-0002"], MovementSerials(s, Id(release, "transactionId")));
        Assert.Equal(4m, await s.OnHandAsync("AVAILABLE"));
    }

    [Fact]
    public async Task Manual_block_leaves_out_the_serials_an_open_hold_names_and_the_whole_rest_takes_them_all()
    {
        var s = new QuarantineScenario(_factory, serialControlled: true);
        var lot = await s.ReleasedLotAsync();
        await s.ReceiveAsync("3", lot, "AVAILABLE", serials: ["MB-0001", "MB-0002", "MB-0003"]);
        var holdKey = QuarantineScenario.Key("hold");
        await s.ExpectAsync(HttpMethod.Post, "/api/quarantine/holds",
            new { mode = "Manual", reason = QuarantineScenario.Reason, idempotencyKey = holdKey, buckets = new[] { Bucket(s, lot, 1m) } }, 201, holdKey);
        Assert.Equal(["MB-0001"], Serials(Assert.Single(s.Holds())));

        var blockKey = QuarantineScenario.Key("blk");
        await s.ExpectAsync(HttpMethod.Post, "/api/quarantine/blocked",
            new { buckets = new[] { Bucket(s, lot, 2m) }, blockReasonCode = "DEVIATION", reason = QuarantineScenario.Reason, idempotencyKey = blockKey }, 201, blockKey);
        var blocked = Assert.Single(s.Blocked());
        Assert.Equal(["MB-0002", "MB-0003"], Serials(blocked)); // MB-0001 is in QUARANTINE (open hold), not AVAILABLE
        Assert.Equal(["MB-0002", "MB-0003"], MovementSerials(s, blocked["SourceMovementId"].AsString));
        Assert.Equal(2m, await s.OnHandAsync("BLOCKED"));
        Assert.Equal(0m, await s.OnHandAsync("AVAILABLE"));
    }

    [Fact]
    public async Task A_non_serial_bucket_carries_no_serials()
    {
        var s = new QuarantineScenario(_factory);
        var lot = await s.ReleasedLotAsync();
        await s.ReceiveAsync("5", lot, "AVAILABLE");
        var key = QuarantineScenario.Key("hold");
        await s.ExpectAsync(HttpMethod.Post, "/api/quarantine/holds",
            new { mode = "Manual", reason = QuarantineScenario.Reason, idempotencyKey = key, buckets = new[] { Bucket(s, lot, 5m) } }, 201, key);
        var record = Assert.Single(s.Holds());
        Assert.True(!record.Contains("SerialIds") || record["SerialIds"].IsBsonNull);
    }
}
