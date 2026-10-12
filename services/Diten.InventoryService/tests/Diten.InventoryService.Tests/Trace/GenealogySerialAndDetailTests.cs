using System.Text.Json;
using Diten.InventoryService.Tests.TestInfrastructure;
using MongoDB.Bson;
using Xunit;

namespace Diten.InventoryService.Tests.Trace;

/// <summary>
/// MOD-0174 P1 — linkGenealogy / getGenealogy (proposal §4.7, §13 #14), getSerial / listSerials (serials are written by
/// the ledger in slice P2, so they are seeded here as documents), validateTrace, getLotHistory and getLotDetail.
/// </summary>
public sealed class GenealogySerialAndDetailTests : IClassFixture<TraceApiFactory>
{
    private readonly TraceApiFactory _factory;

    public GenealogySerialAndDetailTests(TraceApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Genealogy_links_replay_refuse_self_and_cycles_and_read_two_levels()
    {
        var s = new TraceScenario(_factory);
        var api = (await s.CreateLotAsync("API-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant())).Id;
        var bulk = (await s.CreateLotAsync()).Id;
        var pack = (await s.CreateLotAsync()).Id;

        var link = await s.Expect(s.LinkAsync(api, bulk, "MO-55"), 201);
        Assert.Equal(api, link.GetProperty("parentLotId").GetString());
        var replay = await s.Expect(s.LinkAsync(api, bulk, "MO-55"), 200); // same triple, another key
        Assert.Equal(link.GetProperty("linkId").GetString(), replay.GetProperty("linkId").GetString());
        await s.Expect(s.LinkAsync(bulk, pack, "MO-77", key: "gl-fixed"), 201);
        await s.ExpectError(s.LinkAsync(bulk, pack, "MO-78", key: "gl-fixed"), 400, "IDEMPOTENCY_KEY_REUSED");

        await s.ExpectError(s.LinkAsync(api, api, "MO-1"), 422, "GENEALOGY_SELF_LINK");
        await s.ExpectError(s.LinkAsync(pack, api, "MO-99"), 422, "GENEALOGY_CYCLE");
        await s.ExpectError(s.LinkAsync(Guid.NewGuid().ToString("D"), bulk, "MO-1"), 404, "UNKNOWN_LOT");
        await s.Expect(s.LinkAsync(api, bulk, "MO-1", token: s.TokenWith("trace.lots.read")), 403);

        var oneLevel = await s.Expect(s.GetAsync($"/api/trace/lots/{bulk}/genealogy"), 200);
        Assert.Equal(api, Assert.Single(oneLevel.GetProperty("parents").EnumerateArray()).GetProperty("parentLotId").GetString());
        Assert.Equal(pack, Assert.Single(oneLevel.GetProperty("children").EnumerateArray()).GetProperty("childLotId").GetString());

        var twoLevels = await s.Expect(s.GetAsync($"/api/trace/lots/{pack}/genealogy?depth=2"), 200);
        var parents = twoLevels.GetProperty("parents").EnumerateArray().Select(p => (p.GetProperty("parentLotId").GetString(), p.GetProperty("childLotId").GetString())).ToList();
        Assert.Equal([(bulk, pack), (api, bulk)], parents);
        await s.ExpectError(s.GetAsync($"/api/trace/lots/{pack}/genealogy?depth=3"), 400, "INVALID_REQUEST");

        Assert.Single(await s.History(bulk), h => h.GetProperty("action").GetString() == "Linked");
    }

    [Fact]
    public async Task Serials_derive_Blocked_from_their_lot_and_list_filters_by_status()
    {
        var s = new TraceScenario(_factory, serialControlled: true);
        var (lotId, etag, _) = await s.CreateLotAsync();
        var active = SeedSerial(s, lotId, "SN-0001", "Active");
        var consumed = SeedSerial(s, lotId, "SN-0002", "Consumed");

        var serial = await s.Expect(s.GetAsync($"/api/trace/serials/{active}"), 200);
        Assert.Equal("Active", serial.GetProperty("status").GetString());
        Assert.Equal("SN-0001", serial.GetProperty("serialNumber").GetString());
        Assert.Equal(lotId, serial.GetProperty("lotId").GetString());

        await s.Expect(s.StatusAsync(lotId, "Released", "QA serbest bırakma onayı", etag), 200);
        await s.Expect(s.StatusAsync(lotId, "Blocked", "Sapma DEV-2026-018 incelemesi", await s.ETagOf(lotId), ack: true), 200);

        Assert.Equal("Blocked", (await s.Expect(s.GetAsync($"/api/trace/serials/{active}"), 200)).GetProperty("status").GetString());
        Assert.Equal("Consumed", (await s.Expect(s.GetAsync($"/api/trace/serials/{consumed}"), 200)).GetProperty("status").GetString());

        static List<string> Numbers(JsonElement j) => j.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("serialNumber").GetString()!).ToList();
        Assert.Equal(["SN-0001"], Numbers(await s.Expect(s.GetAsync($"/api/trace/serials?lotId={lotId}&status=Blocked"), 200)));
        Assert.Empty(Numbers(await s.Expect(s.GetAsync($"/api/trace/serials?lotId={lotId}&status=Active"), 200)));
        Assert.Equal(["SN-0002"], Numbers(await s.Expect(s.GetAsync($"/api/trace/serials?lotId={lotId}&status=Consumed"), 200)));
        Assert.Equal(["SN-0001", "SN-0002"], Numbers(await s.Expect(s.GetAsync($"/api/trace/serials?skuId={s.Sku:D}&q=sn-000"), 200)));

        await s.ExpectError(s.GetAsync($"/api/trace/serials/{Guid.NewGuid():D}"), 404, "UNKNOWN_SERIAL");
        await s.ExpectError(s.GetAsync("/api/trace/serials?status=Gone"), 400, "INVALID_REQUEST");

        var detail = await s.Expect(s.GetAsync($"/api/trace/lots/{lotId}/detail"), 200);
        Assert.Equal(2, detail.GetProperty("serialCount").GetInt32());
        Assert.Equal("LotAndSerial", detail.GetProperty("trackingMode").GetString());
    }

    [Fact]
    public async Task ValidateTrace_answers_each_lot_and_serial_in_request_order()
    {
        var s = new TraceScenario(_factory);
        var (lotId, etag, _) = await s.CreateLotAsync();
        var serial = SeedSerial(s, lotId, "SN-0100", "Active");
        await s.Expect(s.StatusAsync(lotId, "Released", "QA serbest bırakma onayı", etag), 200);
        var unknown = Guid.NewGuid().ToString("D");

        var json = await s.Expect(s.Send(HttpMethod.Post, "/api/trace/validate", new { lotIds = new[] { lotId, unknown }, serialIds = new[] { serial } }), 200);
        var results = json.GetProperty("results").EnumerateArray().ToList();
        Assert.Equal(3, results.Count);
        Assert.Equal(lotId, results[0].GetProperty("lotId").GetString());
        Assert.True(results[0].GetProperty("exists").GetBoolean());
        Assert.Equal("Released", results[0].GetProperty("status").GetString());
        Assert.True(results[0].GetProperty("usable").GetBoolean());
        Assert.False(results[1].GetProperty("exists").GetBoolean());
        Assert.Equal(serial, results[2].GetProperty("serialId").GetString());
        Assert.True(results[2].GetProperty("usable").GetBoolean());

        await s.ExpectError(s.Send(HttpMethod.Post, "/api/trace/validate", new { lotIds = "x" }), 400, "INVALID_REQUEST");
    }

    [Fact]
    public async Task LotDetail_reads_the_first_inbound_ledger_row_of_the_lot_in_the_callers_company()
    {
        var s = new TraceScenario(_factory);
        var (lotId, _, _) = await s.CreateLotAsync();

        var empty = await s.Expect(s.GetAsync($"/api/trace/lots/{lotId}/detail"), 200);
        Assert.False(empty.TryGetProperty("source", out _));
        Assert.Equal(0, empty.GetProperty("serialCount").GetInt32());

        _factory.Collection("inventory_transactions").InsertOne(new BsonDocument
        {
            ["_id"] = Guid.NewGuid().ToString(),
            ["TenantId"] = s.Tenant.ToString(),
            ["LegalEntityId"] = s.LegalEntity.ToString(),
            ["MovementType"] = "GoodsReceiptPo",
            ["LotId"] = lotId,
            ["SourceDocumentId"] = "MK-2026-0377",
            ["TransactionDate"] = new DateTime(2026, 9, 24, 10, 0, 0, DateTimeKind.Utc),
            ["CreatedAt"] = DateTime.UtcNow,
            ["IsDeleted"] = false
        });

        var detail = await s.Expect(s.GetAsync($"/api/trace/lots/{lotId}/detail"), 200);
        var source = detail.GetProperty("source");
        Assert.Equal("GoodsReceipt", source.GetProperty("kind").GetString());
        Assert.Equal("MK-2026-0377", source.GetProperty("documentNumber").GetString());

        // Another company of the tenant sees the lot but not this company's ledger row (stock reads stay company-scoped).
        var otherCompany = await s.Expect(s.GetAsync($"/api/trace/lots/{lotId}/detail", s.TokenForCompany(Guid.NewGuid())), 200);
        Assert.False(otherCompany.TryGetProperty("source", out _));
    }

    private string SeedSerial(TraceScenario s, string lotId, string number, string status)
    {
        var id = Guid.NewGuid().ToString("D");
        _factory.Collection("trace_serials").InsertOne(new BsonDocument
        {
            ["_id"] = id,
            ["TenantId"] = s.Tenant.ToString(),
            ["LegalEntityId"] = s.LegalEntity.ToString(),
            ["ItemId"] = s.Item.ToString(),
            ["SkuId"] = s.Sku.ToString(),
            ["SkuLevel"] = "Lsku",
            ["LotId"] = lotId,
            ["Number"] = number,
            ["IdentityKey"] = $"{s.Tenant:D}|Lsku|{s.Sku:D}|{number}",
            ["Status"] = status,
            ["CreatedAt"] = DateTime.UtcNow,
            ["Version"] = 1,
            ["IsDeleted"] = false
        });
        return id;
    }
}
