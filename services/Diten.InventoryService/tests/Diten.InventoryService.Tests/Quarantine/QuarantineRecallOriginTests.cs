using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Features.Quarantine.Commands;
using Diten.InventoryService.Application.Features.Recall.Ports;
using Diten.InventoryService.Tests.TestInfrastructure;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diten.InventoryService.Tests.Quarantine;

/// <summary>
/// MOD-0177 R3 owner step MOD-0175 (QUARANTINE 1.3.0, CT-R2 / CT-R3): the recall's in-process port runs the REAL <c>QuarantineStockHandler</c>
/// (mode Manual) — the hold gets <c>Source = Recall</c> and <c>sourceDocument = MOD-0177 / RECALL_CASE / caseId / RC-…</c>; the same key
/// replays (no second posting); the reservation check refuses with the reservation lines and leaves the reservation alone; an origin is never
/// taken in mode Retest. The API reads the hold back through the contract-validating client (the 1.3.0 enum value is on the wire).
/// </summary>
public sealed class QuarantineRecallOriginTests : IClassFixture<QuarantineApiFactory>
{
    private readonly QuarantineApiFactory _factory;

    public QuarantineRecallOriginTests(QuarantineApiFactory factory) => _factory = factory;

    private IServiceScope ScopeOf(QuarantineScenario s)
    {
        var scope = _factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().SetScope(s.Tenant, s.Ledger.LegalEntity, s.Ledger.User);
        scope.ServiceProvider.GetRequiredService<ICorrelationContext>().Set(Guid.NewGuid());
        return scope;
    }

    private static RecallQuarantineCall Call(QuarantineScenario s, string lot, decimal quantity, Guid caseId, string key)
        => new([new RecallQuarantineBucket(s.Ledger.Item, s.Ledger.Sku, s.Ledger.Level, s.Ledger.Warehouse, s.Ledger.LocationA, quantity, lot)],
            "Geri çağırma RC-2026-0001 · Tedarikci sapma bildirimi", key, caseId, "RC-2026-0001");

    [Fact]
    public async Task The_port_holds_with_source_Recall_and_the_case_as_source_document_and_replays_on_the_same_key()
    {
        var s = new QuarantineScenario(_factory);
        var lot = await s.ReleasedLotAsync();
        await s.ReceiveAsync("10", lot, "AVAILABLE");
        var caseId = Guid.NewGuid();
        var key = $"recall:{caseId:D}:r1:Stock:{lot}";

        RecallQuarantineAnswer first, again;
        using (var scope = ScopeOf(s)) first = await scope.ServiceProvider.GetRequiredService<IRecallQuarantineRequester>().QuarantineAsync(Call(s, lot, 10m, caseId, key));
        using (var scope = ScopeOf(s)) again = await scope.ServiceProvider.GetRequiredService<IRecallQuarantineRequester>().QuarantineAsync(Call(s, lot, 10m, caseId, key));

        Assert.True(first.Held, first.ErrorCode);
        Assert.Equal(201, first.Status);
        Assert.Equal(200, again.Status);
        Assert.Equal(first.Holds.Select(h => h.HoldId), again.Holds.Select(h => h.HoldId));
        Assert.Equal(10m, await s.OnHandAsync("QUARANTINE"));
        Assert.Single(s.Holds());

        var stored = Assert.Single(s.Holds());
        Assert.Equal("Recall", stored["Source"].AsString);
        Assert.Equal(("MOD-0177", "RECALL_CASE", caseId.ToString("D"), "RC-2026-0001"),
            (stored["SourceDocument"]["Module"].AsString, stored["SourceDocument"]["Type"].AsString, stored["SourceDocument"]["_id"].AsString, stored["SourceDocument"]["Number"].AsString));
        Assert.Equal(s.Ledger.User.ToString("D"), stored["CreatedBy"].ToString());

        var hold = await s.GetHoldAsync(first.Holds[0].HoldId); // contract-validated: Recall is a QUARANTINE 1.3.0 HoldSource
        Assert.Equal("Recall", hold.GetProperty("source").GetString());

        using (var scope = ScopeOf(s))
        {
            var port = scope.ServiceProvider.GetRequiredService<IRecallQuarantineRequester>();
            Assert.Equal("Open", Assert.Single(await port.HoldsAsync([first.Holds[0].HoldId])).Status);
            Assert.Single(await port.OpenHoldsOfLotAsync(lot));
        }
    }

    [Fact]
    public async Task Reserved_stock_is_refused_with_the_reservation_lines_and_the_reservation_stays()
    {
        var s = new QuarantineScenario(_factory);
        var lot = await s.ReleasedLotAsync();
        await s.ReceiveAsync("10", lot, "AVAILABLE");
        await s.Lots.ReserveAsync("8", 201);
        var reservationsBefore = s.Ledger.Persisted("inventory_reservations").Select(r => r.ToString()).ToList();

        RecallQuarantineAnswer answer;
        using (var scope = ScopeOf(s))
        {
            answer = await scope.ServiceProvider.GetRequiredService<IRecallQuarantineRequester>()
                .QuarantineAsync(Call(s, lot, 10m, Guid.NewGuid(), "recall:" + Guid.NewGuid().ToString("N")));
        }

        Assert.False(answer.Held);
        Assert.Equal((409, "QUARANTINE_STOCK_RESERVED"), (answer.Status, answer.ErrorCode));
        Assert.NotNull(answer.Details!["reservations"]);
        Assert.Empty(s.Holds());
        Assert.Equal(reservationsBefore, s.Ledger.Persisted("inventory_reservations").Select(r => r.ToString()).ToList());
    }

    [Fact]
    public async Task An_origin_is_taken_in_mode_Manual_only()
    {
        var s = new QuarantineScenario(_factory);
        using var scope = ScopeOf(s);
        var answer = await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new QuarantineStockCommand("Retest", Guid.NewGuid().ToString("D"), null,
            null, null, "k-" + Guid.NewGuid().ToString("N"), new HoldOrigin("MOD-0177", "RECALL_CASE", Guid.NewGuid().ToString("D"), "RC-2026-0001")));
        Assert.Equal((400, "INVALID_REQUEST"), (answer.StatusCode, answer.ErrorCode));
    }
}
