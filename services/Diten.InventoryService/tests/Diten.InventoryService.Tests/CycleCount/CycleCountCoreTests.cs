using System.Text.Json;
using System.Text.RegularExpressions;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Persistence.CycleCounting;
using Diten.InventoryService.Tests.Warehouse;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;
using static Diten.InventoryService.Tests.CycleCounting.CycleCountScenario;

namespace Diten.InventoryService.Tests.CycleCounting;

/// <summary>
/// MOD-0181 DL-1 — the count record over the REAL host: create (AC-1), the record code (AC-12, SB3/F-1), scope members (AC-6), idempotency
/// (AC-8), concurrency (AC-9), cancel / bulk delete (AC-10) and the central record (AC-11).
/// </summary>
public sealed class CycleCountCoreTests(CycleCountApiFactory factory) : IClassFixture<CycleCountApiFactory>
{
    private static readonly Regex NumberShape = new("^GMG-CNT-\\d{6}$");

    // ── AC-1 ─────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AC1_create_answers_201_Draft_with_a_CNT_number_and_one_Created_event()
    {
        var s = new CycleCountScenario(factory);
        var count = await s.CreatedAsync(s.Form(note: "  aylik  "));

        Assert.Equal("Draft", count.GetProperty("status").GetString());
        Assert.Matches(NumberShape, count.GetProperty("number").GetString()!);
        Assert.Equal("aylik", count.GetProperty("note").GetString());
        Assert.Equal(1, count.GetProperty("version").GetInt32());
        Assert.False(count.TryGetProperty("snapshotAt", out _));
        var events = s.Events(Id(count));
        var created = Assert.Single(events);
        Assert.Equal("Created", created["Kind"].AsString);
        Assert.Equal(count.GetProperty("number").GetString(), created["Data"]["number"].AsString);
    }

    [Fact]
    public async Task AC1_the_record_and_its_Created_event_are_one_transaction()
    {
        var s = new CycleCountScenario(factory);
        factory.FailNextEventInsert[s.Tenant] = true;
        using (var failed = await s.CreateAsync())
        {
            Assert.False(failed.IsSuccessStatusCode);
        }

        Assert.Empty(s.Stored(CycleCountCollections.Counts));
        Assert.Empty(s.Stored(CycleCountCollections.Events));
        Assert.Empty(s.CentralRows("CreateCycleCountCommand"));
    }

    // ── AC-12 + SB3/F-1 ──────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AC12_the_number_is_not_an_api_member_and_two_concurrent_creates_get_two_numbers()
    {
        var s = new CycleCountScenario(factory);
        var body = new Dictionary<string, object?>(s.Form()) { ["number"] = "GMG-CNT-999999" };
        Assert.Equal("INVALID_REQUEST", await ErrorCode(s.CreateAsync(body), 400));

        var both = await Task.WhenAll(s.CreatedAsync(), s.CreatedAsync());
        Assert.NotEqual(both[0].GetProperty("number").GetString(), both[1].GetProperty("number").GetString());
    }

    [Fact]
    public async Task AC12_an_unreachable_counter_is_503_and_opens_no_count()
    {
        var s = new CycleCountScenario(factory);
        factory.CounterDown[s.Tenant] = true;
        try
        {
            Assert.Equal("COUNT_RECORD_CODE_UNAVAILABLE", await ErrorCode(s.CreateAsync(), 503));
        }
        finally
        {
            factory.CounterDown.TryRemove(s.Tenant, out _);
        }

        Assert.Empty(s.Stored(CycleCountCollections.Counts));
    }

    [Fact]
    public async Task AC12_an_invalid_company_code_is_422_and_takes_no_number()
    {
        var s = new CycleCountScenario(factory, companyCode: "gmg1");
        Assert.Equal("COUNT_COMPANY_CODE_INVALID", await ErrorCode(s.CreateAsync(), 422));
        Assert.Empty(Counters(s));
    }

    /// <summary>
    /// SB3/F-1 (final look 03): the replay is looked up BEFORE the number (a replay burns none), a business refusal burns none, the number is taken
    /// before the transaction and outside it — a write that fails after the number skips it, the next create takes the one after.
    /// </summary>
    [Fact]
    public async Task SB3_F1_replay_and_refusal_burn_no_number_a_failed_write_skips_its_number()
    {
        var s = new CycleCountScenario(factory);
        var key = NewKey();
        var first = await Expect(s.CreateAsync(key: key), 201);
        Assert.EndsWith("-CNT-000001", first.GetProperty("number").GetString());

        var replay = await Expect(s.CreateAsync(key: key), 200);
        Assert.Equal(first.GetProperty("number").GetString(), replay.GetProperty("number").GetString());
        Assert.Equal(1, Counter(s));

        Assert.Equal("UNKNOWN_LOCATION", await ErrorCode(s.CreateAsync(s.Form(warehouse: Guid.NewGuid())), 422));
        Assert.Equal(1, Counter(s));

        factory.FailNextEventInsert[s.Tenant] = true;
        using (var failed = await s.CreateAsync()) Assert.False(failed.IsSuccessStatusCode);
        Assert.Equal(2, Counter(s)); // taken, never written

        var next = await s.CreatedAsync();
        Assert.EndsWith("-CNT-000003", next.GetProperty("number").GetString());
        Assert.Equal(2, s.Stored(CycleCountCollections.Counts).Count);
    }

    private static List<BsonDocument> Counters(CycleCountScenario s)
        => s.Factory.Collection("wms_number_counters").Find(new BsonDocument("TenantId", s.Tenant.ToString("D"))).ToList()
            .Where(c => c.Elements.Any(e => e.Value.IsString && e.Value.AsString == "CNT")).ToList();

    private static long Counter(CycleCountScenario s)
    {
        var counter = Assert.Single(Counters(s));
        return counter.Elements.Where(e => e.Value.IsNumeric).Select(e => e.Value.ToInt64()).Max();
    }

    // ── form rules (pack §12) ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_form_is_checked_shape_400_business_422()
    {
        var s = new CycleCountScenario(factory);
        Assert.Equal("INVALID_REQUEST", await ErrorCode(s.CreateAsync(s.Form(countType: "Spot")), 400));
        Assert.Equal("INVALID_REQUEST", await ErrorCode(s.CreateAsync(s.Form(scope: [])), 400));
        Assert.Equal("INVALID_REQUEST", await ErrorCode(s.CreateAsync(s.Form(countType: "Full", scope: [s.BinA])), 400));
        Assert.Equal("INVALID_REQUEST", await ErrorCode(s.CreateAsync(s.Form(plannedDate: DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd"))), 400));
        Assert.Equal("INVALID_REQUEST", await ErrorCode(s.CreateAsync(s.Form(note: new string('x', 501))), 400));
        Assert.Equal("COUNT_SCOPE_NODE_INVALID", await ErrorCode(s.CreateAsync(s.Form(scope: [s.Inventory.Warehouse2Node.Id])), 422));
        Assert.Equal("COUNT_SCOPE_NODE_INVALID", await ErrorCode(s.CreateAsync(s.Form(scope: [Guid.Parse(s.Inventory.LocationC)])), 422));
        Assert.Equal("UNKNOWN_LOCATION", await ErrorCode(s.CreateAsync(s.Form(warehouse: s.Storage)), 422));

        var stranger = Guid.NewGuid();
        Assert.Equal("COUNT_ASSIGNEE_INVALID", await ErrorCode(s.CreateAsync(s.Form(assignedTo: stranger)), 422));
        factory.ActiveUsers[(s.Tenant, stranger)] = true;
        var assigned = await s.CreatedAsync(s.Form(assignedTo: stranger));
        Assert.Equal(stranger.ToString("D"), assigned.GetProperty("assignedTo").GetString());
        Assert.Single(s.Stored(CycleCountCollections.Counts)); // every refusal above wrote nothing
    }

    // ── AC-6 ─────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AC6_a_scope_member_in_the_body_or_the_query_is_400_and_the_record_takes_the_resolved_scope()
    {
        var s = new CycleCountScenario(factory);
        foreach (var member in new[] { "legalEntityId", "tenantId" })
        {
            var body = new Dictionary<string, object?>(s.Form()) { [member] = Guid.NewGuid().ToString("D") };
            var error = await Expect(s.CreateAsync(body), 400);
            Assert.Equal("INVALID_REQUEST", error.GetProperty("error").GetProperty("code").GetString());
            Assert.Contains("never accepted from a body", error.GetProperty("error").GetProperty("message").GetString());
        }

        Assert.Equal("INVALID_REQUEST", await ErrorCode(s.GetAsync($"/api/warehouse/cycle-counts?legalEntityId={s.LegalEntity}"), 400));
        var count = await s.CreatedAsync();
        var stored = s.StoredCount(Id(count));
        Assert.Equal(s.Tenant.ToString("D"), stored["TenantId"].AsString);
        Assert.Equal(s.LegalEntity.ToString("D"), stored["LegalEntityId"].AsString);
    }

    // ── AC-8 ─────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AC8_create_replays_same_body_and_refuses_another_body_under_the_key()
    {
        var s = new CycleCountScenario(factory);
        var key = NewKey();
        var first = await Expect(s.CreateAsync(key: key), 201);
        var replay = await Expect(s.CreateAsync(key: key), 200);
        Assert.Equal(Id(first), Id(replay));
        Assert.Equal("IDEMPOTENCY_KEY_REUSED", await ErrorCode(s.CreateAsync(s.Form(blind: true), key), 400));
        Assert.Single(s.Stored(CycleCountCollections.Counts));
        Assert.Single(s.Stored(CycleCountCollections.Events));
    }

    [Fact]
    public async Task AC8_update_replays_and_refuses_another_body()
    {
        var s = new CycleCountScenario(factory);
        var count = await s.CreatedAsync();
        var key = NewKey();
        var updated = await Expect(s.UpdateAsync(Id(count), s.Form(blind: true), ETag(count), key), 200);
        Assert.Equal(2, updated.GetProperty("version").GetInt32());
        var replay = await Expect(s.UpdateAsync(Id(count), s.Form(blind: true), ETag(count), key), 200);
        Assert.Equal(2, replay.GetProperty("version").GetInt32());
        Assert.Equal("IDEMPOTENCY_KEY_REUSED", await ErrorCode(s.UpdateAsync(Id(count), s.Form(note: "x"), ETag(count), key), 400));
        Assert.Equal(2, s.Events(Id(count)).Count);
    }

    [Fact]
    public async Task AC8_release_replays_and_refuses_another_body()
    {
        var s = new CycleCountScenario(factory);
        await s.ReceiveAsync(5, s.BinA);
        var count = await s.CreatedAsync();
        var key = NewKey();
        var released = await Expect(s.ReleaseAsync(Id(count), ETag(count), key), 200);
        var replay = await Expect(s.ReleaseAsync(Id(count), ETag(count), key), 200);
        Assert.Equal(released.GetProperty("version").GetInt32(), replay.GetProperty("version").GetInt32());
        Assert.Equal(2, s.Events(Id(count)).Count);
        Assert.Single(s.Stored(CycleCountCollections.Lines));
        // the release body is the key alone: a new key on the Released count is a new request (409), the other count is untouched
        var other = await s.CreatedAsync(s.Form(scope: [s.BinB]));
        Assert.Equal("COUNT_INVALID_STATE", await ErrorCode(s.ReleaseAsync(Id(count), ETag(released)), 409));
        Assert.Equal(1, s.CentralRows("ReleaseCycleCountCommand").Count);
        Assert.Equal("Draft", (await Expect(s.GetAsync($"/api/warehouse/cycle-counts/{Id(other)}"), 200)).GetProperty("status").GetString());
    }

    [Fact]
    public async Task AC8_cancel_replays_on_Draft_soft_delete_and_on_Released_and_refuses_another_reason()
    {
        var s = new CycleCountScenario(factory);
        var draft = await s.CreatedAsync();
        var key = NewKey();
        await Expect(s.CancelAsync(Id(draft), ETag(draft), key: key), 200);
        var replay = await Expect(s.CancelAsync(Id(draft), ETag(draft), key: key), 200);
        Assert.Equal("Cancelled", replay.GetProperty("status").GetString());
        Assert.Equal("IDEMPOTENCY_KEY_REUSED", await ErrorCode(s.CancelAsync(Id(draft), ETag(draft), "Baska gerekce", key), 400));
        Assert.Equal(2, s.Events(Id(draft)).Count);

        await s.ReceiveAsync(2, s.BinB);
        var released = await s.ReleasedAsync(await s.CreatedAsync(s.Form(scope: [s.BinB])));
        var key2 = NewKey();
        var cancelled = await Expect(s.CancelAsync(Id(released), ETag(released), key: key2), 200);
        var replay2 = await Expect(s.CancelAsync(Id(released), ETag(released), key: key2), 200);
        Assert.Equal(cancelled.GetProperty("version").GetInt32(), replay2.GetProperty("version").GetInt32());
        Assert.Equal(3, s.Events(Id(released)).Count);
    }

    [Fact]
    public async Task AC8_bulk_delete_replays_written_rows_and_refuses_another_body()
    {
        var s = new CycleCountScenario(factory);
        var a = await s.CreatedAsync();
        var b = await s.CreatedAsync(s.Form(scope: [s.BinB]));
        var key = NewKey();
        var items = new[] { (Id(a), 1), (Id(b), 1) };
        var first = await Expect(s.BulkDeleteAsync(items, key: key), 200);
        Assert.All(first.GetProperty("results").EnumerateArray(), r => Assert.Equal("Deleted", r.GetProperty("outcome").GetString()));
        var replay = await Expect(s.BulkDeleteAsync(items, key: key), 200);
        Assert.All(replay.GetProperty("results").EnumerateArray(), r => Assert.Equal("Deleted", r.GetProperty("outcome").GetString()));
        Assert.Equal("IDEMPOTENCY_KEY_REUSED", await ErrorCode(s.BulkDeleteAsync(items, "Baska gerekce", key), 400));
        Assert.Equal(2, s.Stored(CycleCountCollections.Events).Count(e => e["Kind"].AsString == "Cancelled"));
        Assert.Single(s.CentralRows("BulkDeleteCycleCountCommand"));
    }

    // ── AC-9 ─────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AC9_a_stale_IfMatch_is_409_COUNT_CONFLICT_and_writes_nothing()
    {
        var s = new CycleCountScenario(factory);
        var count = await s.CreatedAsync();
        await Expect(s.UpdateAsync(Id(count), s.Form(blind: true), ETag(count)), 200);
        Assert.Equal("COUNT_CONFLICT", await ErrorCode(s.UpdateAsync(Id(count), s.Form(note: "eski"), ETag(count)), 409));
        Assert.Equal("COUNT_CONFLICT", await ErrorCode(s.ReleaseAsync(Id(count), ETag(count)), 409));
        Assert.Equal("COUNT_CONFLICT", await ErrorCode(s.CancelAsync(Id(count), ETag(count)), 409));
        Assert.Equal(2, s.Events(Id(count)).Count);
    }

    /// <summary>AC-9 at the store: two updates with the same If-Match pass the handler's check together — only one compare-and-set wins.</summary>
    [Fact]
    public async Task AC9_two_concurrent_updates_with_one_IfMatch_one_wins_the_other_is_409()
    {
        var s = new CycleCountScenario(factory);
        var count = await s.CreatedAsync();
        var barrier = new AsyncBarrier(2);
        factory.BeforeCountTransaction[s.Tenant] = barrier.SignalAndWaitAsync;
        HttpResponseMessage[] answers;
        try
        {
            answers = await Task.WhenAll(s.UpdateAsync(Id(count), s.Form(note: "bir"), ETag(count)), s.UpdateAsync(Id(count), s.Form(note: "iki"), ETag(count)));
        }
        finally
        {
            factory.BeforeCountTransaction.TryRemove(s.Tenant, out _);
        }

        Assert.Equal([200, 409], answers.Select(a => (int)a.StatusCode).Order());
        Assert.Equal(2, s.Events(Id(count)).Count);
        Assert.Equal(2, s.StoredCount(Id(count))["Version"].AsInt32);
    }

    // ── AC-10 ────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AC10_a_Draft_cancel_is_a_soft_delete_out_of_the_list_and_404()
    {
        var s = new CycleCountScenario(factory);
        var count = await s.CreatedAsync();
        Assert.Equal("COUNT_REASON_REQUIRED", await ErrorCode(s.CancelAsync(Id(count), ETag(count), reason: null), 400));
        Assert.Equal("COUNT_REASON_REQUIRED", await ErrorCode(s.CancelAsync(Id(count), ETag(count), reason: "   "), 400));
        var cancelled = await Expect(s.CancelAsync(Id(count), ETag(count)), 200);
        Assert.Equal("Cancelled", cancelled.GetProperty("status").GetString());

        var stored = s.StoredCount(Id(count));
        Assert.True(stored["IsDeleted"].AsBoolean);
        Assert.False(stored["DeletedAt"].IsBsonNull);
        Assert.Equal("COUNT_NOT_FOUND", await ErrorCode(s.GetAsync($"/api/warehouse/cycle-counts/{Id(count)}"), 404));
        Assert.Equal(0, (await Expect(s.GetAsync("/api/warehouse/cycle-counts"), 200)).GetProperty("total").GetInt64());
        var e = s.Events(Id(count)).Last();
        Assert.Equal("Cancelled", e["Kind"].AsString);
        Assert.Equal("true", e["Data"]["softDeleted"].AsString);
    }

    [Fact]
    public async Task AC10_a_Released_cancel_keeps_the_record_and_releases_its_claims_Submitted_is_409()
    {
        var s = new CycleCountScenario(factory);
        await s.ReceiveAsync(3, s.BinA);
        var released = await s.ReleasedAsync(await s.CreatedAsync());
        Assert.All(s.Claims(Id(released)), c => Assert.True(c["Open"].AsBoolean));

        var cancelled = await Expect(s.CancelAsync(Id(released), ETag(released)), 200);
        Assert.Equal("Cancelled", cancelled.GetProperty("status").GetString());
        Assert.False(s.StoredCount(Id(released))["IsDeleted"].AsBoolean);
        Assert.Equal(200, (int)(await s.GetAsync($"/api/warehouse/cycle-counts/{Id(released)}")).StatusCode);
        Assert.All(s.Claims(Id(released)), c => Assert.False(c["Open"].AsBoolean));
        Assert.Equal("false", s.Events(Id(released)).Last()["Data"]["softDeleted"].AsString);
        Assert.Equal("COUNT_INVALID_STATE", await ErrorCode(s.CancelAsync(Id(released), ETag(cancelled)), 409));

        var submitted = await s.CreatedAsync(s.Form(scope: [s.BinB]));
        await s.Factory.Collection(CycleCountCollections.Counts).UpdateOneAsync(new BsonDocument("_id", Id(submitted)),
            new BsonDocument("$set", new BsonDocument("Status", BsonValue.Create(StoredStatus(s, "Submitted")))));
        Assert.Equal("COUNT_INVALID_STATE", await ErrorCode(s.CancelAsync(Id(submitted), ETag(submitted)), 409));
    }

    /// <summary>Enums are stored by name (the service's Domain convention).</summary>
    private static BsonValue StoredStatus(CycleCountScenario s, string status) => status;

    [Fact]
    public async Task AC10_bulk_delete_soft_deletes_Draft_rows_only_with_a_result_per_row()
    {
        var s = new CycleCountScenario(factory);
        var draft = await s.CreatedAsync();
        var stale = await s.CreatedAsync(s.Form(scope: [s.BinB]));
        await s.ReceiveAsync(1, s.Node());
        var releasedBin = s.Node();
        await s.ReceiveAsync(1, releasedBin);
        var released = await s.ReleasedAsync(await s.CreatedAsync(s.Form(scope: [releasedBin])));
        var missing = Guid.NewGuid().ToString("D");

        Assert.Equal("COUNT_REASON_REQUIRED", await ErrorCode(s.BulkDeleteAsync([(Id(draft), 1)], reason: null), 400));
        var result = await Expect(s.BulkDeleteAsync([(Id(draft), 1), (Id(stale), 9), (Id(released), 2), (missing, 1)]), 200);
        var outcomes = result.GetProperty("results").EnumerateArray().ToDictionary(r => r.GetProperty("countId").GetString()!, r => r.GetProperty("outcome").GetString());
        Assert.Equal("Deleted", outcomes[Id(draft)]);
        Assert.Equal("Conflict", outcomes[Id(stale)]);
        Assert.Equal("InvalidState", outcomes[Id(released)]);
        Assert.Equal("NotFound", outcomes[missing]);

        Assert.True(s.StoredCount(Id(draft))["IsDeleted"].AsBoolean);
        Assert.False(s.StoredCount(Id(stale))["IsDeleted"].AsBoolean);
        var e = s.Events(Id(draft)).Last();
        Assert.Equal("Cancelled", e["Kind"].AsString);
        Assert.Equal("true", e["Data"]["softDeleted"].AsString);
        Assert.Equal("true", e["Data"]["bulk"].AsString);
    }

    // ── AC-11 ────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AC11_a_successful_write_files_one_central_record_a_replay_and_a_refusal_none()
    {
        var s = new CycleCountScenario(factory);
        var key = NewKey();
        var count = await Expect(s.CreateAsync(key: key), 201);
        await Expect(s.CreateAsync(key: key), 200);
        Assert.Equal("UNKNOWN_LOCATION", await ErrorCode(s.CreateAsync(s.Form(warehouse: Guid.NewGuid())), 422));

        var rows = s.CentralRows("CreateCycleCountCommand");
        var row = Assert.Single(rows);
        Assert.Equal("CYCLE-COUNTING", row["SourceModule"].AsString);
        Assert.Equal(Id(count), row["EntityId"].ToString());

        var updated = await Expect(s.UpdateAsync(Id(count), s.Form(blind: true), ETag(count)), 200);
        Assert.Equal("COUNT_CONFLICT", await ErrorCode(s.UpdateAsync(Id(count), s.Form(), ETag(count)), 409));
        Assert.Single(s.CentralRows("UpdateCycleCountCommand"));
        await Expect(s.CancelAsync(Id(count), ETag(updated)), 200);
        Assert.Single(s.CentralRows("CancelCycleCountCommand"));
    }
}
