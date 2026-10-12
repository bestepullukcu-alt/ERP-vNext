using System.Text.Json;
using Diten.InventoryService.Tests.TestInfrastructure;
using Xunit;

namespace Diten.InventoryService.Tests.Trace;

/// <summary>
/// K-B — checkGs1Code (proposal §4.6, pack §13 #10): (01) GTIN, (21) serial, (17) SKT, (10) lot of a scanned code against
/// the selected lot and its SKU. GTIN is skipped ONLY when PRODUCT-MASTER answers gtin null, with a visible reason; the
/// lot and the SKT are checked in every case — a code with another lot is refused even when the GTIN check is skipped.
/// </summary>
public sealed class Gs1CheckTests : IClassFixture<TraceApiFactory>
{
    private readonly TraceApiFactory _factory;

    public Gs1CheckTests(TraceApiFactory factory) => _factory = factory;

    [Fact]
    public async Task A_matching_code_is_accepted_with_every_check_Ok()
    {
        var (s, lotId, lot) = await LotAsync(TraceScenario.Gtin);
        var result = await s.Expect(s.Gs1Async($"(01){TraceScenario.Gtin}(21)SN0001(17)280630(10){lot}", lotId), 200);

        Assert.True(result.GetProperty("accepted").GetBoolean());
        Assert.Equal(["Format:Ok", "Gtin:Ok", "Lot:Ok", "Expiry:Ok"], Checks(result));
        Assert.Equal("SN0001", result.GetProperty("serialNumber").GetString());
        Assert.Equal("2028-06-30", result.GetProperty("expiry").GetString());
        Assert.False(result.TryGetProperty("gtinSkippedReason", out _));
    }

    [Fact]
    public async Task A_raw_scanner_code_with_FNC1_and_an_end_of_month_expiry_is_read_the_same()
    {
        var (s, lotId, lot) = await LotAsync(TraceScenario.Gtin);
        var raw = "]d201" + TraceScenario.Gtin + "21SN0002\u001D17280600" + "10" + lot;
        var result = await s.Expect(s.Gs1Async(raw, lotId), 200);
        Assert.True(result.GetProperty("accepted").GetBoolean());
    }

    [Theory]
    [InlineData("(01)08699512340018(21)SN0001(17)280630(10){lot}", "Gtin:Mismatch")]
    [InlineData("(01)08699512340407(21)SN0001(17)280630(10)OTHER-LOT", "Lot:Mismatch")]
    [InlineData("(01)08699512340407(21)SN0001(17)290630(10){lot}", "Expiry:Mismatch")]
    [InlineData("(01)08699512340407(21)SN0001(10){lot}", "Expiry:Missing")]
    [InlineData("(01)08699512340407(17)280630(10){lot}", "Format:Missing")]
    [InlineData("(21)SN0001(17)280630(10){lot}", "Gtin:Missing")]
    public async Task A_code_that_does_not_match_is_not_accepted_and_names_the_field(string template, string failing)
    {
        var (s, lotId, lot) = await LotAsync(TraceScenario.Gtin);
        var result = await s.Expect(s.Gs1Async(template.Replace("{lot}", lot), lotId), 200);
        Assert.False(result.GetProperty("accepted").GetBoolean());
        Assert.Contains(failing, Checks(result));
    }

    [Fact]
    public async Task GTIN_null_skips_only_the_GTIN_check_with_a_reason_and_still_accepts_a_matching_code()
    {
        var (s, lotId, lot) = await LotAsync(gtin: null);
        var result = await s.Expect(s.Gs1Async($"(01)00000000000000(21)SN0001(17)280630(10){lot}", lotId), 200);

        Assert.True(result.GetProperty("accepted").GetBoolean());
        Assert.Contains("Gtin:Skipped", Checks(result));
        Assert.Equal("PRODUCT_MASTER_GTIN_NULL", result.GetProperty("gtinSkippedReason").GetString());
    }

    [Fact]
    public async Task GTIN_null_still_refuses_a_code_of_another_lot_or_another_expiry()
    {
        var (s, lotId, lot) = await LotAsync(gtin: null);

        var otherLot = await s.Expect(s.Gs1Async("(01)08699512340407(21)SN0001(17)280630(10)DKS-26P003", lotId), 200);
        Assert.False(otherLot.GetProperty("accepted").GetBoolean());
        Assert.Equal(["Format:Ok", "Gtin:Skipped", "Lot:Mismatch", "Expiry:Ok"], Checks(otherLot));

        var otherExpiry = await s.Expect(s.Gs1Async($"(01)08699512340407(21)SN0001(17)300101(10){lot}", lotId), 200);
        Assert.False(otherExpiry.GetProperty("accepted").GetBoolean());
        Assert.Contains("Expiry:Mismatch", Checks(otherExpiry));
    }

    [Fact]
    public async Task Refusals_unreadable_wrong_sku_unknown_lot_and_product_master_down()
    {
        var (s, lotId, lot) = await LotAsync(TraceScenario.Gtin);
        await s.ExpectError(s.Gs1Async("SN-0001", lotId), 400, "GS1_UNREADABLE");
        await s.ExpectError(s.Gs1Async($"(10){lot}", Guid.NewGuid().ToString("D")), 404, "UNKNOWN_LOT");
        var secondSku = s.AddSecondSku();
        await s.ExpectError(s.Gs1Async($"(10){lot}", lotId, secondSku, Diten.InventoryService.Domain.Enums.SkuLevel.Gsku), 422, "LOT_SKU_MISMATCH");
        _factory.Skus.MarkUnavailable(s.Sku);
        await s.ExpectError(s.Gs1Async($"(21)SN0001(10){lot}", lotId), 503, "PRODUCT_MASTER_UNAVAILABLE");
    }

    private async Task<(TraceScenario S, string LotId, string LotNumber)> LotAsync(string? gtin)
    {
        var s = new TraceScenario(_factory, serialControlled: true, gtin: gtin);
        var number = TraceScenario.LotNo("DKS");
        var (id, _, _) = await s.CreateLotAsync(number, extra: new() { ["manufacturingDate"] = "2026-06-10", ["expiryDate"] = "2028-06-30" });
        return (s, id, number);
    }

    private static List<string> Checks(JsonElement result)
        => result.GetProperty("checks").EnumerateArray().Select(c => c.GetProperty("field").GetString() + ":" + c.GetProperty("result").GetString()).ToList();
}
