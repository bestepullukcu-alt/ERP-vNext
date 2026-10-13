using System.Text.Json;
using Diten.InventoryService.Tests.TestInfrastructure;
using Xunit;

namespace Diten.InventoryService.Tests.Trace;

/// <summary>
/// MOD-0174 P1 — updateLot (K-C, CT-D8) and changeLotStatus (K-E) against the REAL service (pack §12 R / E / S / C,
/// §13 #2–#6). Every refusal leaves the lot, its version and its history untouched.
/// </summary>
public sealed class LotEditAndStatusTests : IClassFixture<TraceApiFactory>
{
    private const string Reason10 = "CoA AR-2026-0142 sonrası";

    private readonly TraceApiFactory _factory;

    public LotEditAndStatusTests(TraceApiFactory factory) => _factory = factory;

    // ── K-C ──────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_Draft_lot_is_edited_with_trace_lots_update_one_history_row_per_changed_field_version_plus_one()
    {
        var s = new TraceScenario(_factory);
        var (id, etag, _) = await s.CreateLotAsync(extra: new() { ["supplierLot"] = "S-1" });
        var draftEditor = s.TokenWith("trace.lots.read", "trace.lots.update");

        using var response = await s.PatchAsync(id, new Dictionary<string, object?> { ["supplierLot"] = "S-2", ["coaReference"] = "COA-1", ["manufacturerLot"] = null }, etag, draftEditor);
        var json = await TraceScenario.Read(response);
        Assert.Equal(200, (int)response.StatusCode);
        Assert.Equal("W/\"2\"", response.Headers.ETag!.ToString());
        Assert.Equal("S-2", json.GetProperty("supplierLot").GetString());

        var updated = (await s.History(id)).Where(h => h.GetProperty("action").GetString() == "Updated").ToList();
        Assert.Equal(2, updated.Count); // manufacturerLot was already empty: no change, no row
        var supplier = Assert.Single(updated, h => h.GetProperty("field").GetString() == "supplierLot");
        Assert.Equal("S-1", supplier.GetProperty("oldValue").GetString());
        Assert.Equal("S-2", supplier.GetProperty("newValue").GetString());
        Assert.False(supplier.TryGetProperty("reason", out _));
    }

    [Fact]
    public async Task The_edit_key_follows_the_lot_status()
    {
        var s = new TraceScenario(_factory);
        var (id, etag, _) = await s.CreateLotAsync();
        var releasedOnly = s.TokenWith("trace.lots.read", "trace.lots.update-released");
        var draftOnly = s.TokenWith("trace.lots.read", "trace.lots.update");
        var neither = s.TokenWith("trace.lots.read");

        await s.Expect(s.PatchAsync(id, new { supplierLot = "X" }, etag, releasedOnly), 403);
        await s.Expect(s.PatchAsync(id, new { supplierLot = "X" }, etag, neither), 403);

        etag = await Release(s, id, etag);
        await s.Expect(s.PatchAsync(id, new { supplierLot = "X", reason = Reason10 }, etag, draftOnly), 403);
        await s.Expect(s.PatchAsync(id, new { supplierLot = "X", reason = Reason10 }, etag, releasedOnly), 200);
    }

    [Fact]
    public async Task K_C_a_Released_lot_needs_a_reason_of_ten_and_writes_old_new_reason_actor_correlation()
    {
        var s = new TraceScenario(_factory);
        var (id, etag, _) = await s.CreateLotAsync(extra: new() { ["manufacturingDate"] = "2026-03-10", ["expiryDate"] = "2028-03-31" });
        etag = await Release(s, id, etag);

        await s.ExpectError(s.PatchAsync(id, new { expiryDate = "2029-03-31" }, etag), 400, "REASON_REQUIRED");
        await s.ExpectError(s.PatchAsync(id, new { expiryDate = "2029-03-31", reason = "123456789" }, etag), 400, "REASON_REQUIRED");
        Assert.Equal(etag, await s.ETagOf(id));

        using var response = await s.PatchAsync(id, new { expiryDate = "2029-03-31T00:00:00Z", reason = "Stabilite STB-2026-004 SKT uzatma" }, etag);
        Assert.Equal(200, (int)response.StatusCode);
        var correlation = response.Headers.GetValues("X-Correlation-Id").Single();

        var row = Assert.Single(await s.History(id), h => h.TryGetProperty("field", out var field) && field.GetString() == "expiryDate");
        Assert.Equal("Updated", row.GetProperty("action").GetString());
        Assert.Equal("2028-03-31T00:00:00Z", row.GetProperty("oldValue").GetString());
        Assert.Equal("2029-03-31T00:00:00Z", row.GetProperty("newValue").GetString());
        Assert.Equal("Stabilite STB-2026-004 SKT uzatma", row.GetProperty("reason").GetString());
        Assert.Equal(s.User.ToString("D"), row.GetProperty("actorUserId").GetString());
        Assert.Equal(correlation, row.GetProperty("correlationId").GetString());
    }

    [Theory]
    [InlineData("lotNumber")]
    [InlineData("itemId")]
    [InlineData("skuId")]
    [InlineData("skuLevel")]
    [InlineData("status")]
    public async Task Lot_number_item_sku_and_status_are_immutable_on_a_Draft_lot_too(string member)
    {
        var s = new TraceScenario(_factory);
        var (id, etag, _) = await s.CreateLotAsync();
        await s.ExpectError(s.PatchAsync(id, new Dictionary<string, object?> { [member] = "X" }, etag), 400, "LOT_KEY_IMMUTABLE");
        Assert.Equal(etag, await s.ETagOf(id));
    }

    [Fact]
    public async Task If_Match_no_change_and_result_date_order_are_checked()
    {
        var s = new TraceScenario(_factory);
        var (id, etag, _) = await s.CreateLotAsync(extra: new() { ["manufacturingDate"] = "2026-01-01", ["supplierLot"] = "S" });

        await s.ExpectError(s.PatchAsync(id, new { supplierLot = "T" }, null), 400, "INVALID_REQUEST");
        await s.ExpectError(s.PatchAsync(id, new { supplierLot = "T" }, "W/\"9\""), 409, "LOT_CONFLICT");
        await s.ExpectError(s.PatchAsync(id, new { supplierLot = "S" }, etag), 422, "LOT_NO_CHANGES");
        await s.ExpectError(s.PatchAsync(id, new { expiryDate = "2025-12-31" }, etag), 400, "LOT_DATES_INVALID");
        await s.ExpectError(s.PatchAsync($"{Guid.NewGuid():D}", new { supplierLot = "T" }, etag), 404, "UNKNOWN_LOT");
        Assert.Equal(etag, await s.ETagOf(id));
        Assert.Single(await s.History(id));
    }

    // ── K-E ──────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task K_E_Draft_Released_Blocked_Released_with_reason_each_step_one_history_row()
    {
        var s = new TraceScenario(_factory);
        var (id, etag, _) = await s.CreateLotAsync();

        var released = await s.Expect(s.StatusAsync(id, "Released", "QA serbest bırakma onayı", etag), 200);
        Assert.Equal("Released", released.GetProperty("status").GetString());
        etag = await s.ETagOf(id);

        await s.ExpectError(s.StatusAsync(id, "Blocked", "Sapma DEV-2026-018 incelemesi", etag), 400, "ACKNOWLEDGEMENT_REQUIRED");
        await s.ExpectError(s.StatusAsync(id, "Blocked", "Sapma DEV-2026-018 incelemesi", etag, ack: false), 400, "ACKNOWLEDGEMENT_REQUIRED");
        await s.Expect(s.StatusAsync(id, "Blocked", "Sapma DEV-2026-018 incelemesi", etag, ack: true), 200);
        etag = await s.ETagOf(id);
        await s.Expect(s.StatusAsync(id, "Released", "Sapma kapandı, lot serbest", etag), 200);

        var changes = (await s.History(id)).Where(h => h.GetProperty("action").GetString() == "StatusChanged")
            .Select(h => (h.GetProperty("oldValue").GetString(), h.GetProperty("newValue").GetString())).Reverse().ToList();
        Assert.Equal([("Draft", "Released"), ("Released", "Blocked"), ("Blocked", "Released")], changes);
    }

    [Theory]
    [InlineData("Draft", "Blocked")]
    [InlineData("Draft", "Expired")]
    [InlineData("Draft", "Draft")]
    public async Task Disallowed_transitions_are_422_and_Expired_is_never_manual(string from, string to)
    {
        var s = new TraceScenario(_factory);
        var (id, etag, _) = await s.CreateLotAsync();
        Assert.Equal("Draft", from);
        await s.ExpectError(s.StatusAsync(id, to, "Elle durum denemesi yapıldı", etag, ack: true), 422, "LOT_STATUS_TRANSITION_INVALID");

        etag = await Release(s, id, etag);
        await s.ExpectError(s.StatusAsync(id, "Expired", "Elle durum denemesi yapıldı", etag), 422, "LOT_STATUS_TRANSITION_INVALID");
    }

    [Fact]
    public async Task Status_shape_concurrency_and_permission()
    {
        var s = new TraceScenario(_factory);
        var (id, etag, _) = await s.CreateLotAsync();

        await s.ExpectError(s.StatusAsync(id, "Released", "123456789", etag), 400, "REASON_REQUIRED");
        await s.ExpectError(s.StatusAsync(id, "Gone", "Geçersiz durum adı yazıldı", etag), 400, "INVALID_REQUEST");
        await s.ExpectError(s.StatusAsync(id, "Released", "QA serbest bırakma onayı", null), 400, "INVALID_REQUEST");
        await s.ExpectError(s.StatusAsync(id, "Released", "QA serbest bırakma onayı", "W/\"7\""), 409, "LOT_CONFLICT");
        await s.Expect(s.StatusAsync(id, "Released", "QA serbest bırakma onayı", etag, token: s.TokenWith("trace.lots.read", "trace.lots.update")), 403);
        Assert.Single(await s.History(id));
    }

    [Fact]
    public async Task A_repeated_status_request_replays_without_a_second_row_and_a_reused_key_is_400()
    {
        var s = new TraceScenario(_factory);
        var (id, etag, _) = await s.CreateLotAsync();
        await s.Expect(s.StatusAsync(id, "Released", "QA serbest bırakma onayı", etag, key: "st-1"), 200);
        var replay = await s.Expect(s.StatusAsync(id, "Released", "QA serbest bırakma onayı", etag, key: "st-1"), 200);
        Assert.Equal("Released", replay.GetProperty("status").GetString());
        await s.ExpectError(s.StatusAsync(id, "Released", "Başka bir gerekçe yazıldı", etag, key: "st-1"), 400, "IDEMPOTENCY_KEY_REUSED");
        Assert.Single(await s.History(id), h => h.GetProperty("action").GetString() == "StatusChanged");
    }

    private static async Task<string> Release(TraceScenario s, string id, string etag)
    {
        await s.Expect(s.StatusAsync(id, "Released", "QA serbest bırakma onayı", etag), 200);
        return await s.ETagOf(id);
    }
}
