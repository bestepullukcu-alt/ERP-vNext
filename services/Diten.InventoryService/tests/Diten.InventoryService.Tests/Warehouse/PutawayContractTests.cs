using System.Text.Json.Nodes;
using Xunit;
using static Diten.InventoryService.Tests.Warehouse.PutawayScenario;
using static Diten.InventoryService.Tests.Warehouse.WarehouseScenario;

namespace Diten.InventoryService.Tests.Warehouse;

/// <summary>
/// MOD-0178 AC-19 re-run as AC-60 (pack §19.7) — the S1 answers against the FROZEN <c>wms-bundle.openapi.yaml</c> (<see cref="WmsContract"/>):
/// one scenario drives EVERY S1 operation through <see cref="WmsContractHandler"/> (a violation throws), then the coverage table must hold each
/// operation's success; and the validator itself is shown to refuse what the contract forbids (an unnamed member, a JSON null, an unknown enum
/// value, a non-decimal quantity) — a validator that cannot say "no" is no guard.
/// </summary>
public sealed class PutawayContractTests : IClassFixture<WarehouseApiFactory>
{
    private readonly WarehouseApiFactory _factory;

    public PutawayContractTests(WarehouseApiFactory factory) => _factory = factory;

    [Fact]
    public async Task AC60_every_S1_operation_answers_the_frozen_contract()
    {
        var s = await new PutawayScenario(_factory).ReadyAsync();
        await s.ReceiveAsync(100);
        var alice = s.ActiveUser();

        await s.CandidatesAsync();
        var task = await s.TaskAsync(60);
        var id = TaskId(task);
        await Expect(s.W.GetAsync("/api/warehouse/putaway-tasks", s.Supervisor), 200);
        await s.GetTaskAsync(id);
        var updated = await Expect(s.UpdateTaskAsync(id, new() { ["note"] = "raf 2" }, ETagOf(task)), 200);
        var assigned = await Expect(s.BulkAssignAsync(alice, [(id, updated.GetProperty("version").GetInt32())]), 200);
        var current = await s.GetTaskAsync(id);
        var posted = await Expect(s.ConfirmAsync(current, 10, s.A01), 201);
        await s.EventsApiAsync(id);

        // retry, returned, resolve and close short on their own confirmations
        s.LedgerAnswersBefore(503);
        await Expect(s.ConfirmAsync(posted, 10, s.A01), 503);
        s.LedgerHealthy();
        _factory.Clock.Advance(TimeSpan.FromMinutes(3));
        var retried = await Expect(s.RetryAsync(id, 2), 200);
        s.LedgerAnswersBefore(422, "INSUFFICIENT_STOCK");
        var refused = DetailsTask(await Expect(s.ConfirmAsync(retried, 10, s.A01), 422));
        s.LedgerHealthy();
        var returned = await Expect(s.ReturnedAsync(id, 3, ETagOf(refused)), 200);
        await s.ForeignUnderKeyAsync(PostingKey(id, 4));
        var locked = DetailsTask(await Expect(s.ConfirmAsync(returned, 10, s.A01), 409));
        var resolved = await Expect(s.ResolveAsync(id, 4, ETagOf(locked)), 200);
        await Expect(s.CloseShortAsync(id, ETagOf(resolved)), 200);

        var other = await s.TaskAsync(5);
        await Expect(s.ClaimAsync(TaskId(other), ETagOf(other)), 200);
        await Expect(s.CancelAsync(TaskId(other), ETagOf(await s.GetTaskAsync(TaskId(other)))), 200);

        // the DL-1 location-role operations
        var bin = s.W.Node(Diten.InventoryService.Domain.Enums.LocationType.Bin);
        var role = await s.W.CreatedAsync(bin.Id, [], ["COOL_2_8"]);
        await Expect(s.W.GetAsync($"/api/warehouse/location-roles/{Id(role)}"), 200);
        var roleUpdated = await Expect(s.W.UpdateAsync(Id(role), new() { ["note"] = "soguk" }, ETag(role)), 200);
        await Expect(s.W.GetAsync("/api/warehouse/location-roles"), 200);
        await Expect(s.W.DeleteAsync(Id(role), ETag(roleUpdated)), 200);
        await Expect(s.W.BulkDeleteAsync([(Guid.NewGuid().ToString("D"), 1)]), 200);
        Assert.Equal(1, assigned.GetProperty("results").GetArrayLength());

        string[] expected =
        [
            "listPutawayCandidates 200", "listPutawayTasks 200", "createPutawayTask 201", "bulkAssignPutawayTasks 200",
            "getPutawayTask 200", "updatePutawayTask 200", "getPutawayTaskEvents 200", "claimPutawayTask 200",
            "confirmPutawayTask 201", "confirmPutawayTask 503", "confirmPutawayTask 422",
            "confirmPutawayTask 409", "retryPutawayConfirmation 200",
            "markPutawayConfirmationReturned 200", "resolvePutawayIntegrity 200",
            "closePutawayTaskShort 200", "cancelPutawayTask 200",
            "listLocationRoles 200", "createLocationRole 201", "getLocationRole 200", "updateLocationRole 200",
            "deleteLocationRole 200", "bulkDeleteLocationRoles 200"
        ];
        Assert.All(expected, key => Assert.True(WmsContract.Validated.ContainsKey(key), $"not validated: {key}"));
    }

    private static JsonObject ValidTask()
    {
        var user = Guid.NewGuid().ToString("D");
        return new JsonObject
        {
            ["taskId"] = Guid.NewGuid().ToString("D"), ["number"] = "GMG-PUT-000001", ["companyCodePrefix"] = "GMG", ["kind"] = "Putaway",
            ["status"] = "Open", ["origin"] = "Manual", ["legalEntityId"] = Guid.NewGuid().ToString("D"), ["warehouseId"] = "w",
            ["item"] = new JsonObject { ["itemId"] = Guid.NewGuid().ToString("D"), ["skuId"] = Guid.NewGuid().ToString("D"), ["skuLevel"] = "Lsku" },
            ["stockStatus"] = "AVAILABLE", ["fromLocationId"] = "r", ["targetSource"] = "Manual", ["plannedQuantity"] = "60.000",
            ["confirmedQuantity"] = "0.000", ["pendingQuantity"] = "0.000", ["remainingQuantity"] = "60.000", ["baseUomId"] = "EA",
            ["priority"] = "Normal", ["serialControlled"] = false, ["confirmations"] = new JsonArray(), ["integrationFlag"] = "None",
            ["allowedActions"] = new JsonArray("Confirm"), ["disabledActions"] = new JsonArray(),
            ["created"] = new JsonObject { ["userId"] = user, ["atUtc"] = "2026-10-30T10:00:00Z" }, ["version"] = 1, ["contractVersion"] = "v1"
        };
    }

    [Fact]
    public void AC60_the_frozen_schema_accepts_a_task_and_refuses_what_the_contract_forbids()
    {
        Assert.Empty(WmsContract.Validate(WmsContract.Task, ValidTask().ToJsonString()));

        var extra = ValidTask();
        extra["debug"] = "x";
        Assert.NotEmpty(WmsContract.Validate(WmsContract.Task, extra.ToJsonString()));

        var nullMember = ValidTask();
        nullMember["note"] = null;
        Assert.NotEmpty(WmsContract.Validate(WmsContract.Task, nullMember.ToJsonString()));

        var badEnum = ValidTask();
        badEnum["integrationFlag"] = "Stuck";
        Assert.NotEmpty(WmsContract.Validate(WmsContract.Task, badEnum.ToJsonString()));

        var badQuantity = ValidTask();
        badQuantity["plannedQuantity"] = "60,5";
        Assert.NotEmpty(WmsContract.Validate(WmsContract.Task, badQuantity.ToJsonString()));

        var sevenDigits = ValidTask();
        sevenDigits["plannedQuantity"] = "60.0000001";
        Assert.NotEmpty(WmsContract.Validate(WmsContract.Task, sevenDigits.ToJsonString()));

        var missing = ValidTask();
        missing.Remove("integrationFlag");
        Assert.NotEmpty(WmsContract.Validate(WmsContract.Task, missing.ToJsonString()));

        var badUuid = ValidTask();
        badUuid["taskId"] = "task-1";
        Assert.NotEmpty(WmsContract.Validate(WmsContract.Task, badUuid.ToJsonString()));

        var badTime = ValidTask();
        badTime["created"]!["atUtc"] = "yesterday";
        Assert.NotEmpty(WmsContract.Validate(WmsContract.Task, badTime.ToJsonString()));

        Assert.NotEmpty(WmsContract.Validate(WmsContract.ConfirmResult, ValidTask().ToJsonString())); // no confirmationSeq
        var result = ValidTask();
        result["confirmationSeq"] = 1;
        Assert.Empty(WmsContract.Validate(WmsContract.ConfirmResult, result.ToJsonString()));
        Assert.NotEmpty(WmsContract.Validate(WmsContract.Task, result.ToJsonString())); // a task never carries confirmationSeq
        result["debug"] = "x";
        Assert.NotEmpty(WmsContract.Validate(WmsContract.ConfirmResult, result.ToJsonString())); // the composed schema stays closed
        Assert.NotEmpty(WmsContract.Validate(WmsContract.Error, """{"error":{"message":"m","correlationId":"6f1c2c5e-0d7b-4b0e-9f6a-1c2d3e4f5a6b"},"contractVersion":"v1"}"""));
    }
}
