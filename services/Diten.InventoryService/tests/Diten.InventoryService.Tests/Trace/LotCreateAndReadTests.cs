using System.Net.Http.Headers;
using System.Text.Json;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Tests.TestInfrastructure;
using Xunit;

namespace Diten.InventoryService.Tests.Trace;

/// <summary>
/// MOD-0174 P1 — createLot / getLot / listLots against the REAL service (pack §12 rows 1–9, §13 #1 #15 #16, §16 P1):
/// Draft on create, K-G uniqueness and the measured B-1 behaviour change, B-2 (= CT-D2), fail-closed PRODUCT-MASTER,
/// idempotent replay, persistence across a restart, tenant-wide identity (CT-D3), filters / paging. Every response is
/// validated against <c>trace.openapi.yaml</c> 1.1.0 by the contract-validating client.
/// </summary>
public sealed class LotCreateAndReadTests : IClassFixture<TraceApiFactory>
{
    private readonly TraceApiFactory _factory;

    public LotCreateAndReadTests(TraceApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Create_is_201_Draft_with_ETag_every_v1_member_and_one_Created_history_row()
    {
        var s = new TraceScenario(_factory);
        var number = TraceScenario.LotNo("PRS");
        var (id, etag, json) = await s.CreateLotAsync(number, extra: new()
        {
            ["manufacturerLot"] = "M-99", ["supplierLot"] = "S-12", ["manufacturingDate"] = "2026-01-10T00:00:00Z",
            ["expiryDate"] = "2029-01-10", ["retestDate"] = "2027-01-10T00:00:00Z", ["countryOfOrigin"] = "tr", ["coaReference"] = "COA-777"
        });

        Assert.Equal("W/\"1\"", etag);
        Assert.Equal(number, json.GetProperty("lotNumber").GetString());
        Assert.Equal(s.Item.ToString("D"), json.GetProperty("itemId").GetString());
        Assert.Equal(s.Sku.ToString("D"), json.GetProperty("skuId").GetString());
        Assert.Equal("Lsku", json.GetProperty("skuLevel").GetString());
        Assert.Equal("Draft", json.GetProperty("status").GetString());
        Assert.Equal("2029-01-10T00:00:00Z", json.GetProperty("expiryDate").GetString());
        Assert.Equal("TR", json.GetProperty("countryOfOrigin").GetString());
        Assert.Equal("Ok", json.GetProperty("expiryState").GetString());
        Assert.Equal("Lot", json.GetProperty("trackingMode").GetString());
        Assert.Equal(s.LegalEntity.ToString("D"), json.GetProperty("legalEntityId").GetString());
        Assert.Equal("v1", json.GetProperty("contractVersion").GetString());

        var history = await s.History(id);
        var created = Assert.Single(history);
        Assert.Equal("Created", created.GetProperty("action").GetString());
        Assert.Equal(number, created.GetProperty("newValue").GetString());
        Assert.Equal(s.User.ToString("D"), created.GetProperty("actorUserId").GetString());
        Assert.Single(s.Persisted("trace_lots"));
    }

    [Fact]
    public async Task A_created_lot_is_read_back_by_a_restarted_host()
    {
        var s = new TraceScenario(_factory);
        var (id, _, _) = await s.CreateLotAsync();

        using var restarted = new TraceApiFactory(); // a new host process on the same database
        using var client = restarted.CreateContractClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/trace/lots/{id}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", s.Token);
        using var response = await client.SendAsync(request);
        var json = await TraceScenario.Read(response);

        Assert.Equal(200, (int)response.StatusCode);
        Assert.Equal(id, json.GetProperty("lotId").GetString());
        Assert.Equal("Draft", json.GetProperty("status").GetString());
    }

    [Fact]
    public async Task B1_same_sku_same_number_is_409_while_the_second_sku_of_the_same_item_takes_it_and_listLots_by_item_returns_both()
    {
        var s = new TraceScenario(_factory);
        var number = TraceScenario.LotNo("DUP");
        var (first, _, _) = await s.CreateLotAsync(number);

        await s.ExpectError(s.CreateAsync(s.LotBody(number)), 409, "LOT_ALREADY_EXISTS");
        await s.ExpectError(s.CreateAsync(s.LotBody(" " + number.ToLowerInvariant() + " ")), 409, "LOT_ALREADY_EXISTS");

        var secondSku = s.AddSecondSku();
        var (second, _, json) = await s.CreateLotAsync(number, secondSku, SkuLevel.Gsku);
        Assert.NotEqual(first, second);
        Assert.Equal(number, json.GetProperty("lotNumber").GetString());

        var list = await s.Expect(s.GetAsync($"/api/trace/lots?itemId={s.Item:D}"), 200);
        var numbers = list.GetProperty("items").EnumerateArray().Select(i => (i.GetProperty("lotNumber").GetString(), i.GetProperty("skuId").GetString())).ToList();
        Assert.Equal(2, list.GetProperty("total").GetInt32());
        Assert.Equal(2, numbers.Count(n => n.Item1 == number));
        Assert.Contains((number, s.Sku.ToString("D")), numbers);
        Assert.Contains((number, secondSku.ToString("D")), numbers);
        Assert.False(list.TryGetProperty("page", out _)); // v1 unpaged shape
        Assert.Equal(2, s.Persisted("trace_lots").Count);
    }

    [Fact]
    public async Task B2_without_skuId_and_skuLevel_is_422_LOT_SKU_REQUIRED_and_nothing_is_written()
    {
        var s = new TraceScenario(_factory);
        var body = s.LotBody(TraceScenario.LotNo());
        body.Remove("skuId");
        body.Remove("skuLevel");
        await s.ExpectError(s.CreateAsync(body), 422, "LOT_SKU_REQUIRED");

        var noLevel = s.LotBody(TraceScenario.LotNo());
        noLevel.Remove("skuLevel");
        await s.ExpectError(s.CreateAsync(noLevel), 422, "LOT_SKU_REQUIRED");

        Assert.Empty(s.Persisted("trace_lots"));
        Assert.Empty(s.Persisted("trace_lot_history"));
    }

    [Fact]
    public async Task Product_master_rules_refuse_before_any_write()
    {
        var notControlled = new TraceScenario(_factory, lotControlled: false);
        await notControlled.ExpectError(notControlled.CreateAsync(notControlled.LotBody(TraceScenario.LotNo())), 422, "PRODUCT_NOT_LOT_CONTROLLED");

        var s = new TraceScenario(_factory);
        await s.ExpectError(s.CreateAsync(s.LotBody(TraceScenario.LotNo(), Guid.NewGuid())), 422, "UNKNOWN_ITEM");

        var foreignSku = Guid.NewGuid();
        _factory.Skus.Add(foreignSku, SkuLevel.Lsku, Guid.NewGuid(), null);
        await s.ExpectError(s.CreateAsync(s.LotBody(TraceScenario.LotNo(), foreignSku)), 422, "LOT_SKU_MISMATCH");

        _factory.Skus.MarkUnavailable(s.Sku);
        await s.ExpectError(s.CreateAsync(s.LotBody(TraceScenario.LotNo())), 503, "PRODUCT_MASTER_UNAVAILABLE");

        Assert.Empty(s.Persisted("trace_lots"));
        Assert.Empty(notControlled.Persisted("trace_lots"));
    }

    [Fact]
    public async Task The_same_key_replays_200_the_same_lot_and_a_reused_key_with_another_body_is_400()
    {
        var s = new TraceScenario(_factory);
        var body = s.LotBody(TraceScenario.LotNo());
        var first = await s.Expect(s.CreateAsync(body), 201);
        var replay = await s.Expect(s.CreateAsync(body), 200);
        Assert.Equal(first.GetProperty("lotId").GetString(), replay.GetProperty("lotId").GetString());

        var changed = s.LotBody(TraceScenario.LotNo(), key: (string)body["idempotencyKey"]!);
        await s.ExpectError(s.CreateAsync(changed), 400, "IDEMPOTENCY_KEY_REUSED");
        Assert.Single(s.Persisted("trace_lots"));
        Assert.Single(s.Persisted("trace_lot_history"));
    }

    [Theory]
    [InlineData("lotNumber", "MCC 2602", "LOT_NUMBER_INVALID")]
    [InlineData("lotNumber", "", "LOT_NUMBER_INVALID")]
    [InlineData("expiryDate", "2029-01-10T15:00:00Z", "LOT_DATES_INVALID")]
    [InlineData("expiryDate", "10.01.2029", "LOT_DATES_INVALID")]
    [InlineData("manufacturingDate", "2099-01-01", "LOT_DATES_INVALID")]
    [InlineData("countryOfOrigin", "Turkey", "INVALID_REQUEST")]
    [InlineData("itemId", "not-a-uuid", "INVALID_REQUEST")]
    [InlineData("legalEntityId", "x", "INVALID_REQUEST")]
    [InlineData("unknownMember", "x", "INVALID_REQUEST")]
    public async Task Shape_refusals_are_400_and_write_nothing(string member, string value, string code)
    {
        var s = new TraceScenario(_factory);
        var body = s.LotBody(TraceScenario.LotNo());
        body[member] = value;
        await s.ExpectError(s.CreateAsync(body), 400, code);
        Assert.Empty(s.Persisted("trace_lots"));
    }

    [Fact]
    public async Task Date_order_is_checked_and_the_key_header_must_equal_the_body_key()
    {
        var s = new TraceScenario(_factory);
        var body = s.LotBody(TraceScenario.LotNo());
        body["manufacturingDate"] = "2026-01-10";
        body["expiryDate"] = "2026-01-10";
        await s.ExpectError(s.CreateAsync(body), 400, "LOT_DATES_INVALID");

        await s.ExpectError(s.CreateAsync(s.LotBody(TraceScenario.LotNo()), headerKey: "other-key"), 400, "INVALID_REQUEST");
    }

    [Fact]
    public async Task Identity_is_tenant_wide_another_company_reads_it_another_tenant_does_not()
    {
        var s = new TraceScenario(_factory);
        var (id, _, _) = await s.CreateLotAsync();

        await s.Expect(s.GetAsync($"/api/trace/lots/{id}", s.TokenForCompany(Guid.NewGuid())), 200);

        var other = new TraceScenario(_factory);
        await other.ExpectError(other.GetAsync($"/api/trace/lots/{id}"), 404, "UNKNOWN_LOT");
        await s.ExpectError(s.GetAsync($"/api/trace/lots/{Guid.NewGuid():D}"), 404, "UNKNOWN_LOT");
        await s.ExpectError(s.GetAsync("/api/trace/lots/NOT-A-LOT"), 404, "UNKNOWN_LOT");
    }

    [Fact]
    public async Task ListLots_filters_paging_sort_and_warn()
    {
        var s = new TraceScenario(_factory);
        var soon = DateTimeOffset.UtcNow.UtcDateTime.Date.AddDays(30).ToString("yyyy-MM-dd");
        var late = DateTimeOffset.UtcNow.UtcDateTime.Date.AddDays(900).ToString("yyyy-MM-dd");
        var (a, _, _) = await s.CreateLotAsync("AAA-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(), extra: new() { ["expiryDate"] = soon, ["manufacturerLot"] = "MFR-XYZ" });
        var (b, _, _) = await s.CreateLotAsync("BBB-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(), extra: new() { ["expiryDate"] = late });
        var (c, _, _) = await s.CreateLotAsync("CCC-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant());

        static List<string> Ids(JsonElement j) => j.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("lotId").GetString()!).ToList();

        Assert.Equal([a], Ids(await s.Expect(s.GetAsync($"/api/trace/lots?skuId={s.Sku:D}&warn=true"), 200)));
        Assert.Equal([a], Ids(await s.Expect(s.GetAsync($"/api/trace/lots?skuId={s.Sku:D}&q=mfr-x"), 200)));
        Assert.Equal([a], Ids(await s.Expect(s.GetAsync($"/api/trace/lots?itemId={s.Item:D}&expiryBefore={late}"), 200)));
        Assert.Equal([a, b, c], Ids(await s.Expect(s.GetAsync($"/api/trace/lots?itemId={s.Item:D}&status=Draft"), 200)));
        Assert.Empty(Ids(await s.Expect(s.GetAsync($"/api/trace/lots?itemId={s.Item:D}&status=Released"), 200)));

        var page = await s.Expect(s.GetAsync($"/api/trace/lots?itemId={s.Item:D}&sort=-lotNumber&page=2&pageSize=2"), 200);
        Assert.Equal([a], Ids(page));
        Assert.Equal(3, page.GetProperty("total").GetInt32());
        Assert.Equal(2, page.GetProperty("page").GetInt32());

        await s.ExpectError(s.GetAsync("/api/trace/lots?status=Gone"), 400, "INVALID_REQUEST");
        await s.ExpectError(s.GetAsync("/api/trace/lots?pageSize=201"), 400, "INVALID_REQUEST");
        await s.ExpectError(s.GetAsync("/api/trace/lots?sort=name"), 400, "INVALID_REQUEST");
        await s.ExpectError(s.GetAsync("/api/trace/lots?legalEntityId=" + s.LegalEntity), 400, "INVALID_REQUEST");
    }

    [Fact]
    public async Task Every_operation_refuses_without_a_token_and_each_key_gates_its_operation()
    {
        var s = new TraceScenario(_factory);
        var (id, _, _) = await s.CreateLotAsync();

        using (var anonymous = await s.GetAsync($"/api/trace/lots/{id}", token: "")) Assert.Equal(401, (int)anonymous.StatusCode);
        var readOnly = s.TokenWith("trace.lots.read");
        using (var create = await s.CreateAsync(s.LotBody(TraceScenario.LotNo()), readOnly)) Assert.Equal(403, (int)create.StatusCode);
        var createOnly = s.TokenWith("trace.lots.create");
        using (var read = await s.GetAsync($"/api/trace/lots/{id}", createOnly)) Assert.Equal(403, (int)read.StatusCode);
        using (var serials = await s.GetAsync("/api/trace/serials", readOnly)) Assert.Equal(403, (int)serials.StatusCode);
        var legacy = s.TokenWith("inventory.stock.read", "inventory.movements.create");
        using (var list = await s.GetAsync("/api/trace/lots", legacy)) Assert.Equal(403, (int)list.StatusCode);
    }
}
