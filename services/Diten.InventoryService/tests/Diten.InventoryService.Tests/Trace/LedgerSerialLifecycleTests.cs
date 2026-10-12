using System.Diagnostics;
using Diten.InventoryService.Tests.TestInfrastructure;
using MongoDB.Bson;
using Xunit;

namespace Diten.InventoryService.Tests.Trace;

/// <summary>
/// WP-0174-P2 — the serial registry is written by the ledger posting, in the posting transaction (pack §16 P2): inbound →
/// <c>SerialNumber</c> Active (with lot, company, location, receiving transaction); outbound → Consumed; transfer → moved;
/// reversal → back (ASSUMPTION-0174-05). The ledger keeps the serial NUMBERS (ASSUMPTION-0174-04). Pack §12 serial rows:
/// <c>SERIAL_ALREADY_EXISTS</c>, <c>SERIAL_NOT_AVAILABLE</c>, <c>SERIAL_LOT_MISMATCH</c>; <c>SERIAL_COUNT_MISMATCH</c>
/// unchanged. Uniqueness is Tenant + SKU + serial number (CT-D7).
/// </summary>
public sealed class LedgerSerialLifecycleTests : IClassFixture<TraceApiFactory>
{
    private readonly TraceApiFactory _factory;

    public LedgerSerialLifecycleTests(TraceApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Receive_issue_and_reverse_walk_the_serial_through_Active_Consumed_Active()
    {
        var s = new LotLedgerScenario(_factory, lotControlled: true, serialControlled: true);
        var lot = await s.CreateLotAsync();
        var receipt = s.Receipt("3", lot, serials: ["SN-0001", "SN-0002", "sn-0003"]);
        var receiptId = await s.PostOkAsync(receipt);

        var serials = s.Serials();
        Assert.Equal(3, serials.Count);
        Assert.All(serials, d =>
        {
            Assert.Equal("Active", d["Status"].AsString);
            Assert.Equal(lot, d["LotId"].AsString);
            Assert.Equal(s.Ledger.LegalEntity.ToString(), d["LegalEntityId"].AsString);
            Assert.Equal(s.Ledger.LocationA, d["LocationId"].AsString);
            Assert.Equal(receiptId, d["ReceivedTransactionId"].AsString);
        });
        Assert.Contains(serials, d => d["Number"].AsString == "SN-0003"); // normalised by LotIdentityPolicy
        var ledgerSerials = Assert.Single(s.Persisted("inventory_transactions"))["SerialIds"].AsBsonArray.Select(v => v.AsString);
        Assert.Equal(["SN-0001", "SN-0002", "sn-0003"], ledgerSerials); // the ledger keeps what was posted

        var issue = s.Issue("2", lot, serials: ["SN-0001", "SN-0002"]);
        var issueId = await s.PostOkAsync(issue);
        Assert.Equal("Consumed", s.Serial("SN-0001")["Status"].AsString);
        Assert.Equal(issueId, s.Serial("SN-0002")["LastTransactionId"].AsString);
        Assert.Equal("Active", s.Serial("SN-0003")["Status"].AsString);

        // TRACE read side: the serial explorer sees the ledger's writes.
        var listed = await s.GetTraceAsync($"/api/trace/serials?lotId={lot}&pageSize=50");
        Assert.Equal(3, listed.GetProperty("total").GetInt32());

        await s.PostOkAsync(s.Reversal(issue, issueId)); // reversal of an issue → Active again
        Assert.Equal("Active", s.Serial("SN-0001")["Status"].AsString);
        Assert.Equal("Active", s.Serial("SN-0002")["Status"].AsString);

        await s.PostOkAsync(s.Reversal(receipt, receiptId)); // reversal of the receipt → all three Consumed
        Assert.All(s.Serials(), d => Assert.Equal("Consumed", d["Status"].AsString));
        Assert.Equal(0m, s.LotPositionOnHand(lot));
    }

    [Fact]
    public async Task A_transfer_moves_the_serial_and_a_serial_elsewhere_is_SERIAL_NOT_AVAILABLE()
    {
        var s = new LotLedgerScenario(_factory, lotControlled: true, serialControlled: true);
        var lot = await s.CreateLotAsync();
        await s.PostOkAsync(s.Receipt("2", lot, serials: ["TR-0001", "TR-0002"]));

        await s.PostOkAsync(s.Transfer("1", lot, serials: ["TR-0001"]));
        Assert.Equal(s.Ledger.LocationB, s.Serial("TR-0001")["LocationId"].AsString);
        Assert.Equal("Active", s.Serial("TR-0001")["Status"].AsString);

        // TR-0001 is at B now: issuing it from A is refused, nothing written.
        var before = s.Persisted("inventory_transactions").Count;
        await s.RefusedAsync(s.Issue("1", lot, serials: ["TR-0001"]), "SERIAL_NOT_AVAILABLE");
        Assert.Equal(before, s.Persisted("inventory_transactions").Count);
        await s.PostOkAsync(s.Issue("1", lot, location: s.Ledger.LocationB, serials: ["TR-0001"]));
    }

    [Fact]
    public async Task SERIAL_ALREADY_EXISTS_for_an_Active_serial_and_for_the_same_serial_twice_in_one_receipt()
    {
        var s = new LotLedgerScenario(_factory, lotControlled: true, serialControlled: true);
        var lot = await s.CreateLotAsync();
        await s.PostOkAsync(s.Receipt("1", lot, serials: ["DUP-0001"]));

        await s.RefusedAsync(s.Receipt("1", lot, serials: ["DUP-0001"]), "SERIAL_ALREADY_EXISTS");
        await s.RefusedAsync(s.Receipt("1", lot, serials: ["dup-0001"]), "SERIAL_ALREADY_EXISTS"); // the policy normalises
        await s.RefusedAsync(s.Receipt("2", lot, serials: ["NEW-0001", "new-0001"]), "SERIAL_ALREADY_EXISTS");
        Assert.Single(s.Serials());
        Assert.Single(s.Persisted("inventory_transactions"));
    }

    [Fact]
    public async Task SERIAL_NOT_AVAILABLE_for_a_Consumed_serial_and_for_an_unknown_serial_of_a_registered_lot()
    {
        var s = new LotLedgerScenario(_factory, lotControlled: true, serialControlled: true);
        var lot = await s.CreateLotAsync();
        await s.PostOkAsync(s.Receipt("3", lot, serials: ["NA-0001", "NA-0002", "NA-0003"]));
        await s.PostOkAsync(s.Issue("1", lot, serials: ["NA-0001"]));

        await s.RefusedAsync(s.Issue("1", lot, serials: ["NA-0001"]), "SERIAL_NOT_AVAILABLE"); // §13 #9: consumed
        await s.RefusedAsync(s.Issue("1", lot, serials: ["NA-9999"]), "SERIAL_NOT_AVAILABLE"); // never received
        Assert.Equal("Active", s.Serial("NA-0002")["Status"].AsString);
    }

    [Fact]
    public async Task A_serial_of_another_lot_is_SERIAL_LOT_MISMATCH()
    {
        var s = new LotLedgerScenario(_factory, lotControlled: true, serialControlled: true);
        var lot1 = await s.CreateLotAsync();
        var lot2 = await s.CreateLotAsync();
        await s.PostOkAsync(s.Receipt("1", lot1, serials: ["LM-0001"]));
        await s.PostOkAsync(s.Receipt("1", lot2, serials: ["LM-0002"]));

        await s.RefusedAsync(s.Issue("1", lot2, serials: ["LM-0001"]), "SERIAL_LOT_MISMATCH");
        Assert.Equal("Active", s.Serial("LM-0001")["Status"].AsString);
        await s.PostOkAsync(s.Issue("1", lot1, serials: ["LM-0001"]));
    }

    [Fact]
    public async Task SERIAL_COUNT_MISMATCH_is_unchanged_and_comes_before_the_registry_rules()
    {
        var s = new LotLedgerScenario(_factory, lotControlled: true, serialControlled: true);
        var lot = await s.CreateLotAsync();
        await s.PostOkAsync(s.Receipt("1", lot, serials: ["CM-0001"]));
        await s.RefusedAsync(s.Receipt("2", lot, serials: ["CM-0001"]), "SERIAL_COUNT_MISMATCH");
    }

    [Fact]
    public async Task Switch_off_a_pre_P2_serial_unknown_to_the_registry_still_leaves_with_a_typed_lot_but_not_with_a_registered_lot()
    {
        var s = new LotLedgerScenario(_factory, lotControlled: true, serialControlled: true);
        var lot = await s.CreateLotAsync();
        await s.PostOkAsync(s.Receipt("1", "TYPED-OLD-1", serials: ["OLD-0001"]));
        await s.PostOkAsync(s.Receipt("1", lot, serials: ["REG-0001"]));
        // Stock received before P2 has no serial rows: model it by removing this tenant's rows (test database only).
        s.Factory.Collection("trace_serials").DeleteMany(new BsonDocument("TenantId", s.Ledger.Tenant.ToString()));

        await s.PostOkAsync(s.Issue("1", "TYPED-OLD-1", serials: ["OLD-0001"]));
        await s.RefusedAsync(s.Issue("1", lot, serials: ["REG-0001"]), "SERIAL_NOT_AVAILABLE");
        Assert.Empty(s.Serials()); // the legacy issue wrote no registry row
    }

    [Fact]
    public async Task Five_thousand_serials_are_received_and_issued_in_one_posting_each()
    {
        var s = new LotLedgerScenario(_factory, lotControlled: true, serialControlled: true);
        var lot = await s.CreateLotAsync();
        var serials = Enumerable.Range(1, 5000).Select(i => $"BULK-{i:000000}").ToArray();

        var watch = Stopwatch.StartNew();
        await s.PostOkAsync(s.Receipt("5000", lot, serials: serials));
        await s.PostOkAsync(s.Issue("5000", lot, serials: serials));
        watch.Stop();

        Assert.Equal(5000, s.Serials().Count(d => d["Status"].AsString == "Consumed"));
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(60), $"5,000 serials took {watch.Elapsed}");
    }
}

/// <summary>Serials with <c>Trace:EnforceRegisteredLots = true</c>: an outbound serial must be known to the registry.</summary>
public sealed class LedgerEnforcedSerialTests : IClassFixture<EnforcedLotsTraceApiFactory>
{
    private readonly EnforcedLotsTraceApiFactory _factory;

    public LedgerEnforcedSerialTests(EnforcedLotsTraceApiFactory factory) => _factory = factory;

    [Fact]
    public async Task With_the_switch_on_an_unknown_outbound_serial_is_SERIAL_NOT_AVAILABLE_even_without_a_lot()
    {
        var s = new LotLedgerScenario(_factory, lotControlled: false, serialControlled: true);
        await s.PostOkAsync(s.Receipt("1", lotId: null, serials: ["ENF-0001"]));
        s.Factory.Collection("trace_serials").DeleteMany(new BsonDocument("TenantId", s.Ledger.Tenant.ToString()));

        await s.RefusedAsync(s.Issue("1", lotId: null, serials: ["ENF-0001"]), "SERIAL_NOT_AVAILABLE");
    }
}
