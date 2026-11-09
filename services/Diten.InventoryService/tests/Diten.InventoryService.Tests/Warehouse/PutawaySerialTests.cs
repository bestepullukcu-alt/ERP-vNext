using System.Text.Json;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;
using static Diten.InventoryService.Tests.Warehouse.PutawayScenario;
using static Diten.InventoryService.Tests.Warehouse.WarehouseScenario;

namespace Diten.InventoryService.Tests.Warehouse;

/// <summary>
/// MOD-0178 DL-2 — a serial-controlled SKU is put away whole (pack §8.16; AC-4 F-2, AC-51, AC-52, AC-53, AC-62): the planned quantity is the
/// whole AVAILABLE bucket, the system reads the serials (MOD-0175 <c>SerialsAtAsync</c> minus held ones) and puts them on the movement, one open
/// task per bucket (partial unique index on <c>OpenSerialSlot</c>), a changed or mixed bucket is refused.
/// </summary>
public sealed class PutawaySerialTests : IClassFixture<WarehouseApiFactory>
{
    private readonly WarehouseApiFactory _factory;

    public PutawaySerialTests(WarehouseApiFactory factory) => _factory = factory;

    private static string[] Serials(int n) => Enumerable.Range(1, n).Select(_ => "SN-" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant()).ToArray();

    private async Task<(PutawayScenario S, string[] Serials)> BucketAsync(int n = 5)
    {
        var s = await new PutawayScenario(_factory, lotControlled: false, serialControlled: true).ReadyAsync();
        var serials = Serials(n);
        await s.ReceiveAsync(n, serials: serials);
        return (s, serials);
    }

    [Fact]
    public async Task AC51_the_whole_bucket_moves_with_its_serials_and_a_second_receipt_opens_a_new_task()
    {
        var (s, serials) = await BucketAsync();
        Assert.Equal("WMS_SERIAL_PARTIAL_NOT_SUPPORTED", await ErrorCode(s.CreateTaskAsync(s.TaskBody(4)), 422));
        var task = await s.TaskAsync(5);
        Assert.True(task.GetProperty("serialControlled").GetBoolean());

        var posted = await Expect(s.ConfirmAsync(task, 5, s.A01), 201);
        Assert.Equal("Completed", Status(posted));
        Assert.Equal(serials.Order(), Strings(Confirmation(posted, 1).GetProperty("serialIds")).Order());
        var movement = Assert.Single(s.TaskMovements(TaskId(task)));
        Assert.Equal(serials.Order(), movement["SerialIds"].AsBsonArray.Select(v => v.AsString).Order());
        Assert.All(s.Inventory.Persisted("trace_serials").Where(d => serials.Contains(d["Number"].AsString)),
            d => Assert.Equal(s.A01.ToString("D"), d["LocationId"].ToString()));
        Assert.False(s.StoredTask(TaskId(task)).Contains("OpenSerialSlot") && !s.StoredTask(TaskId(task))["OpenSerialSlot"].IsBsonNull);

        var more = Serials(3);
        await s.ReceiveAsync(3, serials: more);
        Assert.Equal(3m, D(await s.TaskAsync(3), "plannedQuantity"));
    }

    [Fact]
    public async Task AC52_partial_confirm_close_short_and_quantity_change_are_refused_and_one_open_task_per_bucket()
    {
        var (s, _) = await BucketAsync();
        var task = await s.TaskAsync(5);
        Assert.Equal("WMS_SERIAL_PARTIAL_NOT_SUPPORTED", await ErrorCode(s.ConfirmAsync(task, 3, s.A01), 422));
        Assert.Equal("WMS_SERIAL_PARTIAL_NOT_SUPPORTED", await ErrorCode(s.CloseShortAsync(TaskId(task), ETagOf(task)), 422));
        Assert.Equal("WMS_SERIAL_PARTIAL_NOT_SUPPORTED",
            await ErrorCode(s.UpdateTaskAsync(TaskId(task), new() { ["plannedQuantity"] = "4" }, ETagOf(task)), 422));
        Assert.Equal("WMS_QUANTITY_EXCEEDS_OPEN", await ErrorCode(s.CreateTaskAsync(s.TaskBody(5)), 422));
    }

    [Fact]
    public async Task AC52_two_concurrent_creates_with_different_keys_one_is_written_the_other_hits_the_serial_slot()
    {
        var (s, _) = await BucketAsync();
        var barrier = new AsyncBarrier(2);
        _factory.BeforeTaskWrite[s.Tenant] = barrier.SignalAndWaitAsync;
        HttpResponseMessage[] answers;
        try
        {
            answers = await Task.WhenAll(s.CreateTaskAsync(s.TaskBody(5)), s.CreateTaskAsync(s.TaskBody(5)));
        }
        finally
        {
            _factory.BeforeTaskWrite.TryRemove(s.Tenant, out _);
        }

        Assert.Equal([201, 422], answers.Select(a => (int)a.StatusCode).Order());
        Assert.Contains("WMS_QUANTITY_EXCEEDS_OPEN", await answers.Single(a => (int)a.StatusCode == 422).Content.ReadAsStringAsync());
        Assert.Single(s.W.Stored("wms_tasks"));
    }

    [Fact]
    public async Task AC4_F2_two_identical_concurrent_serial_creates_one_201_the_twin_200()
    {
        var (s, _) = await BucketAsync();
        var body = s.TaskBody(5);
        var barrier = new AsyncBarrier(2);
        _factory.BeforeTaskWrite[s.Tenant] = barrier.SignalAndWaitAsync;
        HttpResponseMessage[] answers;
        try
        {
            answers = await Task.WhenAll(s.CreateTaskAsync(body), s.CreateTaskAsync(body));
        }
        finally
        {
            _factory.BeforeTaskWrite.TryRemove(s.Tenant, out _);
        }

        Assert.Equal([200, 201], answers.Select(a => (int)a.StatusCode).Order());
        Assert.Single(s.W.Stored("wms_tasks"));
    }

    [Fact]
    public async Task AC53_a_serial_leaving_the_bucket_refuses_the_confirmation_and_the_pending_retry()
    {
        var (s, serials) = await BucketAsync();
        var task = await s.TaskAsync(5);
        await s.PostLedgerAsync(s.Issue(1, serials: [serials[0]]));

        var error = await Expect(s.ConfirmAsync(task, 5, s.A01), 422);
        Assert.Equal("WMS_SERIAL_BUCKET_CHANGED", Code(error));
        var details = error.GetProperty("error").GetProperty("details");
        Assert.Equal(5m, D(details, "expected"));
        Assert.Equal(4m, D(details, "found"));
        Assert.Empty(s.TaskMovements(TaskId(task)));

        // the retry of a Pending confirmation meets the same rule
        var (p, pSerials) = await BucketAsync();
        var pending = await p.TaskAsync(5);
        p.LedgerAnswersBefore(503);
        await Expect(p.ConfirmAsync(pending, 5, p.A01), 503);
        p.LedgerHealthy();
        await p.PostLedgerAsync(p.Issue(1, serials: [pSerials[4]]));
        _factory.Clock.Advance(TimeSpan.FromMinutes(3));
        Assert.Equal("WMS_SERIAL_BUCKET_CHANGED", await ErrorCode(p.RetryAsync(TaskId(pending), 1), 422));
        Assert.Empty(p.TaskMovements(TaskId(pending)));
    }

    [Fact]
    public async Task AC62_a_mixed_bucket_opens_no_task_until_the_held_serials_are_released_then_all_8_move()
    {
        var (s, available) = await BucketAsync(5);
        var held = Serials(3);
        await s.ReceiveAsync(3, status: "QUALITY_INSPECTION", serials: held);

        Assert.Equal("WMS_SERIAL_BUCKET_MIXED_STATUS", await ErrorCode(s.CreateTaskAsync(s.TaskBody(5)), 422));

        await s.PostLedgerAsync(s.StatusTransfer(3, "QUALITY_INSPECTION", "AVAILABLE", serials: held));
        // MOD-0175's intake hold of the 3 is still open: its serials never move (the reader leaves them out)
        var task = await s.TaskAsync(8);
        var changed = await Expect(s.ConfirmAsync(task, 8, s.A01), 422);
        Assert.Equal("WMS_SERIAL_BUCKET_CHANGED", Code(changed));
        Assert.Equal(5m, D(changed.GetProperty("error").GetProperty("details"), "found"));

        // the hold decided (the decision workflow is MOD-0175's, not this test's subject) → all 8 move
        s.Factory.Collection("quarantine_holds").UpdateMany(new BsonDocument("TenantId", s.Tenant.ToString("D")),
            new BsonDocument("$set", new BsonDocument("Status", "Decided")));
        var posted = await Expect(s.ConfirmAsync(task, 8, s.A01), 201);
        Assert.Equal(available.Concat(held).Order(), Strings(Confirmation(posted, 1).GetProperty("serialIds")).Order());
    }
}
