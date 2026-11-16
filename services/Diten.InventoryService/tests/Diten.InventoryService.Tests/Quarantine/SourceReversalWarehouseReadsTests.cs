using System.Text.Json;
using Diten.InventoryService.Tests.Warehouse;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;
using static Diten.InventoryService.Tests.Warehouse.PutawayScenario;
using static Diten.InventoryService.Tests.Warehouse.WarehouseScenario;

namespace Diten.InventoryService.Tests.Quarantine;

/// <summary>
/// MOD-0175-FU01 DL-2 — owner decision XMC-001 MOD-0178 (Beste 2026-11-04, <c>docs/records/decisions/2026-11/mod0175-fu01-owner-decisions-01.md</c>):
/// MOD-0178's two hold reads ask <c>QuarantineRecordStates.IsOpen</c> instead of <c>!= HoldStatus.Decided</c>; the ONLY behaviour change is that a
/// Voided hold is no longer open. Measured on MOD-0178's own API and fixtures (<see cref="WarehouseApiFactory"/>, <see cref="PutawayScenario"/>),
/// the hold made Voided in the store the way MOD-0178's AC62 makes it Decided (the reversal flow itself is DL-1's subject, AC-1 / AC-9).
/// Each test keeps its control: the same read with the hold still open answers as before.
/// </summary>
public sealed class SourceReversalWarehouseReadsTests : IClassFixture<WarehouseApiFactory>
{
    private readonly WarehouseApiFactory _factory;

    public SourceReversalWarehouseReadsTests(WarehouseApiFactory factory) => _factory = factory;

    private static string[] Serials(int n) => Enumerable.Range(1, n).Select(_ => "SN-" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant()).ToArray();

    private static void SetHoldStatus(PutawayScenario s, string status)
        => s.Factory.Collection("quarantine_holds").UpdateMany(new BsonDocument("TenantId", s.Tenant.ToString("D")),
            new BsonDocument("$set", new BsonDocument("Status", status)));

    private static JsonElement Row(JsonElement page, string status)
        => page.GetProperty("items").EnumerateArray().Single(c => c.GetProperty("stockStatus").GetString() == status);

    private static string? BlockingKind(JsonElement row)
        => row.TryGetProperty("blockingDocument", out var d) && d.ValueKind == JsonValueKind.Object ? d.GetProperty("kind").GetString() : null;

    /// <summary>
    /// <c>GetPutawayCandidateListHandler</c> (the blocking document of a held row, MOD-0178 §8.15): an open intake hold is named as the blocking
    /// document; once that hold is Voided it is not — the row is still not eligible (its stock is held), but no void hold is shown as the reason.
    /// </summary>
    [Fact]
    public async Task XMC0178a_the_putaway_candidate_list_names_no_voided_hold_as_the_blocking_document()
    {
        var s = await new PutawayScenario(_factory).ReadyAsync();
        await s.ReceiveAsync(10, status: "QUARANTINE");
        var hold = Assert.Single(s.Factory.Collection("quarantine_holds").Find(new BsonDocument("TenantId", s.Tenant.ToString("D"))).ToList());

        var open = Row(await s.CandidatesAsync(), "QUARANTINE");                              // control: the open hold blocks and is named
        Assert.Equal("StockHeld", open.GetProperty("notEligibleReason").GetString());
        Assert.Equal("QuarantineHold", BlockingKind(open));
        Assert.Equal(hold["HoldNumber"].AsString, open.GetProperty("blockingDocument").GetProperty("number").GetString());

        SetHoldStatus(s, "Voided");
        var voided = Row(await s.CandidatesAsync(), "QUARANTINE");
        Assert.False(voided.GetProperty("eligible").GetBoolean());
        Assert.Equal("StockHeld", voided.GetProperty("notEligibleReason").GetString());
        Assert.NotEqual("QuarantineHold", BlockingKind(voided));

        SetHoldStatus(s, "DecisionPending");                                                   // DecisionPending is open: named again
        Assert.Equal("QuarantineHold", BlockingKind(Row(await s.CandidatesAsync(), "QUARANTINE")));
    }

    /// <summary>
    /// <c>WarehouseSerialReader</c> (the AVAILABLE serials of a bucket minus the serials of an open hold, MOD-0178 §8.16): MOD-0178's AC62 bucket —
    /// 5 serials received AVAILABLE, 3 received QUALITY_INSPECTION (an intake hold) and moved to AVAILABLE with the hold still open. While the hold is
    /// open its 3 serials stay out (422 WMS_SERIAL_BUCKET_CHANGED, found 5); once it is Voided they count — all 8 move.
    /// </summary>
    [Fact]
    public async Task XMC0178b_the_warehouse_serial_reader_counts_the_serials_of_a_voided_hold_as_available()
    {
        var s = await new PutawayScenario(_factory, lotControlled: false, serialControlled: true).ReadyAsync();
        var available = Serials(5);
        await s.ReceiveAsync(5, serials: available);
        var held = Serials(3);
        await s.ReceiveAsync(3, status: "QUALITY_INSPECTION", serials: held);
        await s.PostLedgerAsync(s.StatusTransfer(3, "QUALITY_INSPECTION", "AVAILABLE", serials: held));
        var task = await s.TaskAsync(8);

        var changed = await Expect(s.ConfirmAsync(task, 8, s.A01), 422);                       // control: the open hold keeps its 3 serials out
        Assert.Equal("WMS_SERIAL_BUCKET_CHANGED", Code(changed));
        Assert.Equal(5m, D(changed.GetProperty("error").GetProperty("details"), "found"));

        SetHoldStatus(s, "Voided");
        var posted = await Expect(s.ConfirmAsync(task, 8, s.A01), 201);
        Assert.Equal(available.Concat(held).Order(), Strings(Confirmation(posted, 1).GetProperty("serialIds")).Order());
    }
}
