using System.Diagnostics.Metrics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Diten.BuildingBlocks.CentralAudit;
using Diten.InventoryService.Tests.TestInfrastructure;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.InventoryService.Tests.Api;

/// <summary>
/// MOD-0173-FU03 AC-14 (success audit + K2) and §13 "Ret denetim satırı yazılamaz". A lot reservation writes, in ITS transaction, one
/// <c>InventoryReservationCreated</c> row with the scope and one central-audit outbox row; a failure of either write inside the transaction
/// leaves no reservation at all. Faults are injected by a tenant-scoped collection validator (<see cref="CollectionValidatorFault"/>), so the
/// production write itself fails — no seam in production code.
/// </summary>
[Collection(FaultInjectionCollection.Name)]
public sealed class ScopedReservationAuditTests : IClassFixture<TraceApiFactory>
{
    private readonly TraceApiFactory _factory;

    public ScopedReservationAuditTests(TraceApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Lot_reservation_writes_one_created_row_with_its_scope_and_one_central_event()
    {
        var w = new ScopedReservationWorld(_factory);
        var lot = await w.LotAsync();
        await w.ReceiveAsync("50", lot);
        using var response = await w.Ledger.ReserveAsync(w.Reservation("30", w.W, lot), token: w.Lots.Token);
        Assert.Equal(201, (int)response.StatusCode);
        var id = (await w.Ledger.ReadAsync(response)).GetProperty("reservationId").GetString()!;
        var correlation = Guid.Parse(response.Headers.GetValues("X-Correlation-Id").Single());

        var row = Assert.Single(w.Persisted("inventory_audit"), d => d.GetValue("Action", "").AsString == "InventoryReservationCreated");
        Assert.Equal((id, w.W, lot, "Succeeded"), (row["ReservationId"].AsString, row["WarehouseId"].AsString, row["LotId"].AsString, row["Outcome"].AsString));
        Assert.Equal((w.Ledger.User.ToString(), correlation.ToString(), "token"), (row["ActorUserId"].AsString, row["CorrelationId"].AsString, row["LeSource"].AsString));

        var central = CentralRows(w.Ledger.Tenant).Where(r => r.RequestType == "CreateReservationCommand").ToList();
        var single = Assert.Single(central);
        Assert.Equal(Guid.Parse(id), single.EntityId);
    }

    /// <summary>S-8: the central enlistment moved out of the transaction body to after the commit → the reservation stays, red.</summary>
    [Fact]
    public async Task Central_enlist_failure_leaves_no_reservation_and_no_audit_row()
    {
        var w = new ScopedReservationWorld(_factory);
        var lot = await w.LotAsync();
        await w.ReceiveAsync("50", lot);
        var audit = w.Count("inventory_audit");

        await using (await CollectionValidatorFault.RefuseAsync(_factory.Database, CentralAuditOutboxRecord.CollectionName, w.Ledger.Tenant,
                         "RequestType", "CreateReservationCommand"))
        {
            using var response = await RawReserveAsync(w, w.Reservation("30", w.W, lot));
            Assert.NotEqual(201, (int)response.StatusCode);
        }

        Assert.Empty(w.Reservations());
        Assert.Equal(audit, w.Count("inventory_audit"));
        Assert.Equal((50m, 0m), w.Scope(w.W, lot));
        Assert.DoesNotContain(CentralRows(w.Ledger.Tenant), r => r.RequestType == "CreateReservationCommand");
        w.AssertInvariant("K2");
    }

    /// <summary>
    /// S-8b: the local audit insert made without the transaction session → the row is written outside the transaction: when the write AFTER
    /// it (the outbox event) fails, the reservation is gone but its Created row stays, red.
    /// </summary>
    [Fact]
    public async Task Local_audit_failure_leaves_no_reservation()
    {
        var w = new ScopedReservationWorld(_factory);
        var lot = await w.LotAsync();
        await w.ReceiveAsync("50", lot);

        await using (await CollectionValidatorFault.RefuseAsync(_factory.Database, "inventory_audit", w.Ledger.Tenant,
                         "Action", "InventoryReservationCreated"))
        {
            using var response = await RawReserveAsync(w, w.Reservation("30", w.W, lot));
            Assert.NotEqual(201, (int)response.StatusCode);
        }

        Assert.Empty(w.Reservations());
        Assert.Equal((50m, 0m), w.Scope(w.W, lot));
        Assert.Equal((50m, 0m), w.SkuPosition());

        // the audit row belongs to the transaction: a failure of the next write takes it away with the reservation
        await using (await CollectionValidatorFault.RefuseAsync(_factory.Database, "inventory_outbox", w.Ledger.Tenant, "Payload.Change", "Created"))
        {
            using var response = await RawReserveAsync(w, w.Reservation("30", w.W, lot));
            Assert.NotEqual(201, (int)response.StatusCode);
        }

        Assert.Empty(w.Reservations());
        Assert.DoesNotContain(w.Persisted("inventory_audit"), d => d.GetValue("Action", "").AsString == "InventoryReservationCreated");
    }

    /// <summary>§13: a refusal row that cannot be written changes nothing in the answer, and is not silent (the counter moves).</summary>
    [Fact]
    public async Task Refusal_row_write_failure_keeps_the_answer_and_is_counted()
    {
        var w = new ScopedReservationWorld(_factory);
        var lot = await w.LotAsync();
        await w.ReceiveAsync("5", lot);
        long failed = 0;
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Name == "inventory_refusal_audit_failed_total") l.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((_, value, _, _) => Interlocked.Add(ref failed, value));
        listener.Start();

        await using (await CollectionValidatorFault.RefuseAsync(_factory.Database, "inventory_audit", w.Ledger.Tenant,
                         "Action", "InventoryReservationRefused"))
        {
            var refused = await w.ReserveAsync(w.Reservation("6", w.W, lot), 422);
            Assert.Equal("INSUFFICIENT_AVAILABLE", ScopedReservationWorld.Code(refused));
        }

        Assert.True(Interlocked.Read(ref failed) >= 1, "the failed refusal row was not counted");
        Assert.DoesNotContain(w.Persisted("inventory_audit"), d => d.GetValue("Action", "").AsString == "InventoryReservationRefused");
    }

    /// <summary>AC-16: a fault after the 2nd insert of a 3-line batch (the 3rd reservation document is refused) → 0 reservations remain.</summary>
    [Fact]
    public async Task Batch_fault_after_the_second_insert_leaves_no_reservation()
    {
        var w = new ScopedReservationWorld(_factory);
        var lot = await w.LotAsync();
        await w.ReceiveAsync("90", lot);
        var lines = Enumerable.Range(0, 3).Select(_ => PortWorld.Line(w, "10", w.W, lot)).ToList();
        var port = new PortWorld(_factory, w);

        await using (await CollectionValidatorFault.RefuseAsync(_factory.Database, "inventory_reservations", w.Ledger.Tenant,
                         "IdempotencyKey", lines[2].IdempotencyKey!))
        {
            await Assert.ThrowsAnyAsync<MongoException>(() => port.Use(p => p.CreateBatchAsync(lines, w.Ledger.LegalEntity, w.Ledger.User)));
        }

        Assert.Empty(w.Reservations());
        Assert.Equal((90m, 0m), w.Scope(w.W, lot));
        Assert.DoesNotContain(w.Persisted("inventory_audit"), d => d.GetValue("Action", "").AsString == "InventoryReservationCreated");
    }

    private async Task<HttpResponseMessage> RawReserveAsync(ScopedReservationWorld w, Dictionary<string, object?> body)
    {
        var client = _factory.CreateClient(); // no contract validation: a failed transaction answers outside the contract (500)
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/inventory/reservations") { Content = JsonContent.Create(body) };
        request.Headers.TryAddWithoutValidation("Idempotency-Key", (string)body["idempotencyKey"]!);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", w.Lots.Token);
        return await client.SendAsync(request);
    }

    private List<CentralAuditOutboxRecord> CentralRows(Guid tenant)
        => CentralAudit.CentralAuditInventoryTests.Outbox(_factory).Find(x => x.TenantId == tenant).ToList();
}
