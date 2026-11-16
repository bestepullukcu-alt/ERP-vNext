using System.Net.Http.Headers;
using System.Text.Json;
using Xunit;
using static Diten.InventoryService.Tests.Warehouse.PutawayScenario;
using static Diten.InventoryService.Tests.Warehouse.WarehouseScenario;

namespace Diten.InventoryService.Tests.Warehouse;

/// <summary>
/// MOD-0178 DL-2 — the putaway golden flow on the REAL host and the REAL ledger (pack AC-2, AC-3, AC-16, AC-38): candidates → task → partial
/// confirmations to different bins (each its own <c>LOCATION_TRANSFER</c> with source MOD-0178 / PUTAWAY_TASK / task / seq) → Completed; a new
/// host on the same database reads the same task, confirmations and balances; the number comes from the company code (MDM <c>Code</c>).
/// </summary>
public sealed class PutawayFlowTests : IClassFixture<WarehouseApiFactory>
{
    private readonly WarehouseApiFactory _factory;

    public PutawayFlowTests(WarehouseApiFactory factory) => _factory = factory;

    private static JsonElement Candidate(JsonElement page, Guid location)
        => page.GetProperty("items").EnumerateArray().Single(c => c.GetProperty("locationId").GetString() == location.ToString("D"));

    [Fact]
    public async Task AC2_golden_flow_candidate_task_two_confirmations_completed_and_the_same_after_a_host_restart()
    {
        var s = await new PutawayScenario(_factory).ReadyAsync();
        await s.ReceiveAsync(100);

        var candidate = Candidate(await s.CandidatesAsync(), s.R01);
        Assert.Equal("100", Trim(candidate, "openQuantity"));
        Assert.True(candidate.GetProperty("eligible").GetBoolean());

        var task = await s.TaskAsync(60);
        Assert.Matches(@"^GMG-PUT-\d{6}$", task.GetProperty("number").GetString());
        Assert.Equal("Open", Status(task));
        Assert.Equal("40", Trim(Candidate(await s.CandidatesAsync(), s.R01), "openQuantity"));

        var first = await Expect(s.ConfirmAsync(task, 40, s.A01), 201);
        Assert.Equal(1, first.GetProperty("confirmationSeq").GetInt32());
        Assert.Equal("InProgress", Status(first));
        Assert.Equal("Posted", ConfirmationStatus(first, 1));

        var movement = Assert.Single(s.TaskMovements(TaskId(task)));
        Assert.Equal("LocationTransfer", movement["MovementType"].ToString());
        Assert.Equal("MOD-0178", movement["SourceModule"].AsString);
        Assert.Equal("PUTAWAY_TASK", movement["SourceType"].AsString);
        Assert.Equal("1", movement["SourceLineId"].AsString);
        Assert.Equal(60m, await s.OnHandAsync(s.R01));
        Assert.Equal(40m, await s.OnHandAsync(s.A01));

        var second = await Expect(s.ConfirmAsync(first, 20, s.A01), 201);
        Assert.Equal("Completed", Status(second));
        Assert.Equal("60", Trim(second, "confirmedQuantity"));
        Assert.Equal(2, s.TaskMovements(TaskId(task)).Count);

        // a brand-new host on the same database reads the same truth
        using var restarted = new WarehouseApiFactory();
        using var client = restarted.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", s.Supervisor);
        using var response = await client.GetAsync($"/api/warehouse/putaway-tasks/{TaskId(task)}");
        var reread = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal(200, (int)response.StatusCode);
        Assert.Equal("Completed", Status(reread));
        Assert.Equal(2, reread.GetProperty("confirmations").GetArrayLength());
        Assert.Equal(task.GetProperty("number").GetString(), reread.GetProperty("number").GetString());
        Assert.Equal(40m, await s.OnHandAsync(s.R01));
        Assert.Equal(60m, await s.OnHandAsync(s.A01));
    }

    [Fact]
    public async Task AC3_one_task_confirmed_to_two_bins_writes_two_movements()
    {
        var s = await new PutawayScenario(_factory).ReadyAsync();
        await s.ReceiveAsync(50);
        var task = await s.TaskAsync(50);

        var first = await Expect(s.ConfirmAsync(task, 30, s.A01), 201);
        var second = await Expect(s.ConfirmAsync(first, 20, s.B01), 201);

        Assert.Equal("Completed", Status(second));
        var movements = s.TaskMovements(TaskId(task));
        Assert.Equal(2, movements.Count);
        Assert.Equal([s.A01.ToString("D"), s.B01.ToString("D")], movements.OrderBy(m => m["SourceLineId"].AsString).Select(m => m["ToLocationId"].ToString()));
        Assert.Equal(30m, await s.OnHandAsync(s.A01));
        Assert.Equal(20m, await s.OnHandAsync(s.B01));
        Assert.Equal(0m, await s.OnHandAsync(s.R01));
    }

    [Fact]
    public async Task AC16_two_concurrent_tasks_get_different_numbers_and_the_number_is_not_a_body_member()
    {
        var s = await new PutawayScenario(_factory).ReadyAsync();
        await s.ReceiveAsync(100);

        var calls = await Task.WhenAll(Expect(s.CreateTaskAsync(s.TaskBody(10)), 201), Expect(s.CreateTaskAsync(s.TaskBody(10)), 201));
        var numbers = calls.Select(t => t.GetProperty("number").GetString()!).ToArray();
        Assert.NotEqual(numbers[0], numbers[1]);
        Assert.All(numbers, n => Assert.Matches(@"^GMG-PUT-\d{6}$", n));

        var body = s.TaskBody(10);
        body["number"] = "GMG-PUT-999999";
        Assert.Equal("INVALID_REQUEST", await ErrorCode(s.CreateTaskAsync(body), 400));
        var update = await s.UpdateTaskAsync(TaskId(calls[0]), new() { ["number"] = "X" }, ETagOf(calls[0]));
        Assert.Equal(400, (int)update.StatusCode);
    }

    [Theory]
    [InlineData("gmg1")]
    [InlineData("G")]
    [InlineData("GMGXYZ")]
    public async Task AC38_an_invalid_company_code_opens_no_task(string code)
    {
        var s = await new PutawayScenario(_factory, companyCode: code).ReadyAsync();
        await s.ReceiveAsync(10);

        var error = await Expect(s.CreateTaskAsync(s.TaskBody(5)), 422);
        Assert.Equal("WMS_COMPANY_CODE_INVALID", Code(error));
        Assert.Empty(s.W.Stored("wms_tasks"));
    }

    [Fact]
    public async Task AC38_a_changed_company_code_keeps_old_numbers_and_starts_its_own_counter()
    {
        var s = await new PutawayScenario(_factory, companyCode: "GMA").ReadyAsync();
        await s.ReceiveAsync(10);
        var old = await s.TaskAsync(2);
        Assert.StartsWith("GMA-PUT-", old.GetProperty("number").GetString());

        _factory.LegalEntities.RegisterName(s.LegalEntity, "GMB", "renamed");
        var renamed = await s.TaskAsync(2);
        Assert.StartsWith("GMB-PUT-", renamed.GetProperty("number").GetString());
        Assert.Equal(old.GetProperty("number").GetString(), (await s.GetTaskAsync(TaskId(old))).GetProperty("number").GetString());
        Assert.Equal(s.Inventory.WarehouseNode.Code, renamed.GetProperty("warehouseCode").GetString());
        Assert.DoesNotContain(s.Inventory.WarehouseNode.Code, renamed.GetProperty("number").GetString());
    }

    private static string Trim(JsonElement e, string name) => Q(D(e, name));
}
