using System.Globalization;
using System.Text.Json;
using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Inventory;
using Diten.InventoryService.Application.Features.Inventory.Commands;
using Diten.InventoryService.Application.Features.PutawayTasks.Services;
using Diten.InventoryService.Application.Features.Warehouse.Services;
using Diten.InventoryService.Domain.Warehouse.Entities;
using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Recall.Entities;
using Diten.InventoryService.Domain.Rules;
using Diten.InventoryService.Persistence.Warehouse;
using Diten.InventoryService.Tests.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;
using static Diten.InventoryService.Tests.Warehouse.WarehouseScenario;

namespace Diten.InventoryService.Tests.Warehouse;

/// <summary>
/// One isolated putaway world (DL-2) on top of <see cref="WarehouseScenario"/>: a Receiving zone with bin <see cref="R01"/> under the first
/// storage location (the role comes from the REAL location-role API), the targets <see cref="A01"/> / <see cref="B01"/> (bins A and B of
/// <see cref="InventoryScenario"/>), the company code <c>GMG</c> in the MDM stub, the SKU's v1.1 <c>storageConditions</c> (default: stated, none),
/// stock put in by the REAL ledger API, and calls to the REAL putaway API with an operator token (read / create / confirm) and a supervisor
/// token (every <c>warehouse.*</c> key).
/// </summary>
public sealed class PutawayScenario
{
    public const string TaskRead = "warehouse.putaway-tasks.read";
    public const string TaskCreate = "warehouse.putaway-tasks.create";
    public const string TaskUpdate = "warehouse.putaway-tasks.update";
    public const string TaskConfirm = "warehouse.putaway-tasks.confirm";
    public const string TaskCancel = "warehouse.putaway-tasks.cancel";
    public const string TaskResolve = "warehouse.putaway-tasks.resolve";
    public const string Reason = "Raf hasarli - DEV-2026-031";
    public const string Lot = "lot-1";

    public PutawayScenario(WarehouseApiFactory factory, bool lotControlled = true, bool serialControlled = false, SkuLevel level = SkuLevel.Lsku,
        string companyCode = "GMG", IReadOnlyList<string>? productConditions = null)
    {
        W = new WarehouseScenario(factory, lotControlled, serialControlled, level);
        factory.LegalEntities.RegisterName(LegalEntity, companyCode, "Grand Medical " + companyCode);
        factory.StorageConditions[Sku] = productConditions ?? [];
        Zone = W.Node(LocationType.Zone);
        R01Node = W.Node(LocationType.Bin, Zone);
        R01 = R01Node.Id;
        A01 = Guid.Parse(W.Inventory.LocationA);
        B01 = Guid.Parse(W.Inventory.LocationB);
        Operator = W.TokenFor(User, TaskRead, TaskCreate, TaskConfirm);
        Supervisor = W.TokenFor(User, TaskRead, TaskCreate, TaskUpdate, TaskConfirm, TaskCancel, TaskResolve, Read, Manage);
    }

    public WarehouseScenario W { get; }

    public WarehouseApiFactory Factory => W.Factory;

    public InventoryScenario Inventory => W.Inventory;

    public Guid Tenant => W.Tenant;

    public Guid LegalEntity => W.LegalEntity;

    public Guid User => W.User;

    public Guid Item => Inventory.Item;

    public Guid Sku => Inventory.Sku;

    public Guid Warehouse => Inventory.WarehouseNode.Id;

    public Location Zone { get; }

    public Location R01Node { get; }

    public Guid R01 { get; }

    public Guid A01 { get; }

    public Guid B01 { get; }

    public string Operator { get; }

    public string Supervisor { get; }

    /// <summary>The Receiving role on the zone (bin R-01 inherits it).</summary>
    public async Task<PutawayScenario> ReadyAsync()
    {
        await W.CreatedAsync(Zone.Id, ["Receiving"]);
        return this;
    }

    public string TokenOf(Guid user, params string[] permissions) => W.TokenFor(user, permissions);

    // ── stock (REAL ledger API) ─────────────────────────────────────────────────────────────────────────

    public Dictionary<string, object?> Receipt(decimal quantity, Guid? location = null, string? status = null, string[]? serials = null)
    {
        var body = Inventory.Receipt(Q(quantity), toStatus: status, location: (location ?? R01).ToString("D"));
        if (serials is not null) body["serialIds"] = serials;
        return body;
    }

    public async Task<JsonElement> ReceiveAsync(decimal quantity, Guid? location = null, string? status = null, string[]? serials = null)
        => await Inventory.PostExpectingAsync(Receipt(quantity, location, status, serials), 201);

    /// <summary>A movement straight on the REAL ledger API (the "MOD-0173 owner" of the fixtures: reversal, issue, status transfer).</summary>
    public Task<JsonElement> PostLedgerAsync(Dictionary<string, object?> body, int status = 201) => Inventory.PostExpectingAsync(body, status);

    /// <summary>STATUS_TRANSFER at <paramref name="location"/> (default R-01) — the held-stock fixtures (AC-7 / AC-49 / AC-56 / AC-67).</summary>
    public Dictionary<string, object?> StatusTransfer(decimal quantity, string from, string to, Guid? location = null, string[]? serials = null)
    {
        var at = (location ?? R01).ToString("D");
        var body = Inventory.Movement("STATUS_TRANSFER", Q(quantity));
        body["fromWarehouseId"] = Inventory.Warehouse;
        body["fromLocationId"] = at;
        body["fromStockStatus"] = from;
        body["toWarehouseId"] = Inventory.Warehouse;
        body["toLocationId"] = at;
        body["toStockStatus"] = to;
        if (serials is not null) body["serialIds"] = serials;
        return body;
    }

    public Dictionary<string, object?> Issue(decimal quantity, Guid? location = null, string[]? serials = null)
    {
        var body = Inventory.Issue(Q(quantity), location: (location ?? R01).ToString("D"));
        if (serials is not null) body["serialIds"] = serials;
        return body;
    }

    /// <summary>Another module's movement under <paramref name="key"/> (the ledger then holds a different payload under the confirmation's key).</summary>
    public async Task<JsonElement> ForeignUnderKeyAsync(string key)
    {
        var body = Receipt(1, B01);
        body["idempotencyKey"] = key;
        return await PostLedgerAsync(body);
    }

    public static string PostingKey(string taskId, int seq) => $"wms:put:{taskId}:{seq}";

    /// <summary>
    /// A LATE commit: the confirmation's STORED ledger request (<see cref="PutawayPostingEngine.Request"/>, optionally changed) posted through the
    /// REAL stock door in this tenant's scope — the movement a lost / slow answer committed after the task moved on.
    /// </summary>
    public async Task<Response<MovementResponseDto>> LateCommitAsync(string taskId, int seq, Func<PostMovementCommand, PostMovementCommand>? change = null)
    {
        using var scope = Factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().SetScope(Tenant, LegalEntity, User);
        scope.ServiceProvider.GetRequiredService<ICorrelationContext>().Set(Guid.NewGuid());
        var task = await scope.ServiceProvider.GetRequiredService<WarehouseTaskRepository>().FindAsync(Guid.Parse(taskId))
                   ?? throw new InvalidOperationException("no task");
        var request = PutawayPostingEngine.Request(task, task.Confirmations.Single(c => c.Seq == seq));
        var answer = await scope.ServiceProvider.GetRequiredService<WarehouseMovementPoster>()
            .PostAsync(change is null ? request : change(request), LegalEntity, User, CancellationToken.None);
        Assert.True(answer.IsSuccessful, $"late commit refused: {answer.ErrorCode} {string.Join(" ", answer.Errors)}");
        return answer;
    }

    /// <summary>Changes the STORED task (a concurrent writer of the fixture) — version + 1 unless told otherwise.</summary>
    public async Task MutateStoredTaskAsync(string taskId, Action<WarehouseTask> change, bool bumpVersion = true)
    {
        var tasks = Factory.Database.GetCollection<WarehouseTask>(WarehouseCollections.Tasks);
        var id = Guid.Parse(taskId);
        var task = await tasks.Find(t => t.Id == id).SingleAsync();
        change(task);
        if (bumpVersion) task.Version++;
        await tasks.ReplaceOneAsync(t => t.Id == id, task);
    }

    public async Task<decimal> OnHandAsync(Guid location, string? status = null) => await Inventory.OnHandAsync(location.ToString("D"), status);

    public List<BsonDocument> TaskMovements(string taskId)
        => Inventory.Persisted("inventory_transactions").Where(t => t.Contains("SourceDocumentId") && t["SourceDocumentId"].ToString() == taskId).ToList();

    /// <summary>A recall case's lot block (the REAL recall reader reads <c>recall_lot_blocks</c> tenant-wide).</summary>
    public RecallLotBlock Recall(string lotId = Lot)
    {
        var block = new RecallLotBlock
        {
            Id = Guid.NewGuid(), TenantId = Tenant, LegalEntityId = LegalEntity, LotId = lotId, CaseId = Guid.NewGuid(), CaseNumber = "GMG-RCL-000001",
            Active = true, ActivatedAtUtc = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow
        };
        Factory.Database.GetCollection<RecallLotBlock>("recall_lot_blocks").InsertOne(block);
        return block;
    }

    // ── putaway API ─────────────────────────────────────────────────────────────────────────────────────

    public Dictionary<string, object?> TaskBody(decimal planned, Guid? to = null, string status = "AVAILABLE", Guid? from = null, string? lotId = Lot)
    {
        var body = new Dictionary<string, object?>
        {
            ["idempotencyKey"] = NewKey(),
            ["warehouseId"] = Warehouse.ToString("D"),
            ["fromLocationId"] = (from ?? R01).ToString("D"),
            ["item"] = new Dictionary<string, object?>
                { ["itemId"] = Item.ToString("D"), ["skuId"] = Sku.ToString("D"), ["skuLevel"] = WireNames.ToWire(Inventory.Level) },
            ["stockStatus"] = status,
            ["plannedQuantity"] = Q(planned)
        };
        if (Inventory.LotControlled && lotId is not null) body["lotId"] = lotId;
        if (to is { } t) body["toLocationId"] = t.ToString("D");
        return body;
    }

    public Task<HttpResponseMessage> CreateTaskAsync(Dictionary<string, object?> body, string? token = null)
        => W.Send(HttpMethod.Post, "/api/warehouse/putaway-tasks", body, (string)body["idempotencyKey"]!, null, token ?? Supervisor);

    public async Task<JsonElement> TaskAsync(decimal planned, Guid? to = null, string? token = null)
        => await Expect(CreateTaskAsync(TaskBody(planned, to), token), 201);

    public Dictionary<string, object?> ConfirmBody(decimal quantity, Guid to, string? deviation = null, string? key = null)
    {
        var body = new Dictionary<string, object?> { ["idempotencyKey"] = key ?? NewKey(), ["quantity"] = Q(quantity), ["toLocationId"] = to.ToString("D") };
        if (deviation is not null) body["deviationReason"] = deviation;
        return body;
    }

    public Task<HttpResponseMessage> ConfirmAsync(JsonElement task, decimal quantity, Guid to, string? deviation = null, string? key = null, string? token = null,
        string? ifMatch = null)
        => ConfirmRawAsync(TaskId(task), ConfirmBody(quantity, to, deviation, key), ifMatch ?? ETagOf(task), token);

    public Task<HttpResponseMessage> ConfirmRawAsync(string taskId, Dictionary<string, object?> body, string? ifMatch, string? token = null)
        => W.Send(HttpMethod.Post, $"/api/warehouse/putaway-tasks/{taskId}/confirmations", body, (string)body["idempotencyKey"]!, ifMatch, token ?? Supervisor);

    public Task<HttpResponseMessage> RetryAsync(string taskId, int seq, string? key = null, string? token = null)
    {
        var k = key ?? NewKey();
        return W.Send(HttpMethod.Post, $"/api/warehouse/putaway-tasks/{taskId}/confirmations/{seq}/retry", new Dictionary<string, object?> { ["idempotencyKey"] = k },
            k, null, token ?? Supervisor);
    }

    public Task<HttpResponseMessage> ReasonedAsync(string path, string? reason, string ifMatch, string? key = null, string? token = null)
    {
        var k = key ?? NewKey();
        var body = new Dictionary<string, object?> { ["idempotencyKey"] = k };
        if (reason is not null) body["reason"] = reason;
        return W.Send(HttpMethod.Post, path, body, k, ifMatch, token ?? Supervisor);
    }

    public Task<HttpResponseMessage> ReturnedAsync(string taskId, int seq, string ifMatch, string? reason = Reason, string? key = null, string? token = null)
        => ReasonedAsync($"/api/warehouse/putaway-tasks/{taskId}/confirmations/{seq}/returned", reason, ifMatch, key, token);

    public Task<HttpResponseMessage> ResolveAsync(string taskId, int seq, string ifMatch, string? reason = Reason, string? key = null, string? token = null)
        => ReasonedAsync($"/api/warehouse/putaway-tasks/{taskId}/confirmations/{seq}/resolve-integrity", reason, ifMatch, key, token);

    public Task<HttpResponseMessage> CancelAsync(string taskId, string ifMatch, string? reason = Reason, string? key = null, string? token = null)
        => ReasonedAsync($"/api/warehouse/putaway-tasks/{taskId}/cancel", reason, ifMatch, key, token);

    public Task<HttpResponseMessage> CloseShortAsync(string taskId, string ifMatch, string? reason = Reason, string? key = null, string? token = null)
        => ReasonedAsync($"/api/warehouse/putaway-tasks/{taskId}/close-short", reason, ifMatch, key, token);

    /// <summary>An active user of this tenant in the fake AUTH (Q-13 A).</summary>
    public Guid ActiveUser()
    {
        var user = Guid.NewGuid();
        Factory.ActiveUsers[(Tenant, user)] = true;
        return user;
    }

    public Task<HttpResponseMessage> BulkAssignAsync(Guid assignee, IEnumerable<(string TaskId, int Version)> items, string? key = null, string? token = null)
    {
        var body = new Dictionary<string, object?>
        {
            ["idempotencyKey"] = key ?? NewKey(),
            ["assigneeUserId"] = assignee.ToString("D"),
            ["items"] = items.Select(i => new Dictionary<string, object?> { ["taskId"] = i.TaskId, ["version"] = i.Version }).ToArray()
        };
        return W.Send(HttpMethod.Post, "/api/warehouse/putaway-tasks/bulk-assign", body, (string)body["idempotencyKey"]!, null, token ?? Supervisor);
    }

    public Task<HttpResponseMessage> ClaimAsync(string taskId, string ifMatch, string? key = null, string? token = null)
    {
        var k = key ?? NewKey();
        return W.Send(HttpMethod.Post, $"/api/warehouse/putaway-tasks/{taskId}/claim", new Dictionary<string, object?> { ["idempotencyKey"] = k }, k, ifMatch,
            token ?? Operator);
    }

    public Task<HttpResponseMessage> UpdateTaskAsync(string taskId, Dictionary<string, object?> change, string ifMatch, string? key = null, string? token = null)
    {
        var body = new Dictionary<string, object?>(change) { ["idempotencyKey"] = key ?? NewKey() };
        return W.Send(HttpMethod.Put, $"/api/warehouse/putaway-tasks/{taskId}", body, (string)body["idempotencyKey"]!, ifMatch, token ?? Supervisor);
    }

    public async Task<JsonElement> GetTaskAsync(string taskId, string? token = null)
        => await Expect(W.GetAsync($"/api/warehouse/putaway-tasks/{taskId}", token ?? Supervisor), 200);

    public async Task<JsonElement> CandidatesAsync(string query = "", string? token = null)
        => await Expect(W.GetAsync($"/api/warehouse/putaway-candidates?warehouseId={Warehouse:D}{query}", token ?? Supervisor), 200);

    public async Task<JsonElement> EventsApiAsync(string taskId, string? token = null)
        => await Expect(W.GetAsync($"/api/warehouse/putaway-tasks/{taskId}/events?pageSize=200", token ?? Supervisor), 200);

    /// <summary>One reconciler tick for THIS tenant at the host's clock.</summary>
    public async Task<ReconcileTick> ReconcileAsync()
        => await Factory.Services.GetRequiredService<PutawayReconciler>().RunOnceAsync(Factory.Clock.GetUtcNow(), CancellationToken.None, [Tenant]);

    // ── stored rows / readers ───────────────────────────────────────────────────────────────────────────

    public BsonDocument StoredTask(string taskId) => Assert.Single(W.Stored(WarehouseCollections.Tasks), t => t["_id"].ToString() == taskId);

    public List<BsonDocument> TaskEvents(string taskId)
        => W.Stored(WarehouseCollections.TaskEvents).Where(e => e["TaskId"].ToString() == taskId).OrderBy(e => e["Seq"].AsInt32).ToList();

    public string[] EventKinds(string taskId) => TaskEvents(taskId).Select(e => e["Kind"].AsString).ToArray();

    /// <summary>Forwarded (Pending / Delivered) and Abandoned central rows of one command class.</summary>
    public (int Forwarded, int Abandoned) Central(string requestType)
    {
        var rows = W.CentralRows(requestType);
        return (rows.Count(r => r["Status"].AsString is "Pending" or "Delivered"), rows.Count(r => r["Status"].AsString == "Abandoned"));
    }

    public static string Q(decimal value) => value.ToString("0.######", CultureInfo.InvariantCulture);

    public static decimal D(JsonElement e, string name) => decimal.Parse(e.GetProperty(name).GetString()!, CultureInfo.InvariantCulture);

    public static string TaskId(JsonElement task) => task.GetProperty("taskId").GetString()!;

    public static string ETagOf(JsonElement task) => $"W/\"{task.GetProperty("version").GetInt32()}\"";

    public static string Status(JsonElement task) => task.GetProperty("status").GetString()!;

    public static string Flag(JsonElement task) => task.GetProperty("integrationFlag").GetString()!;

    public static JsonElement Confirmation(JsonElement task, int seq)
        => task.GetProperty("confirmations").EnumerateArray().Single(c => c.GetProperty("seq").GetInt32() == seq);

    public static string ConfirmationStatus(JsonElement task, int seq) => Confirmation(task, seq).GetProperty("status").GetString()!;

    /// <summary>The task inside a refusal (<c>error.details.task</c>).</summary>
    public static JsonElement DetailsTask(JsonElement error) => error.GetProperty("error").GetProperty("details").GetProperty("task");

    public static string Code(JsonElement error) => error.GetProperty("error").GetProperty("code").GetString()!;

    // ── fake ledger behaviours (the REAL path runs unless the fake answers) ─────────────────────────────

    /// <summary>The ledger answers <paramref name="status"/> WITHOUT writing.</summary>
    public void LedgerAnswersBefore(int status, string code = "INVENTORY_UNAVAILABLE")
        => Factory.AroundPosting[Tenant] = (_, _) => Task.FromResult(Response<MovementResponseDto>.Fail(code, "test: ledger answer", status));

    /// <summary>The ledger COMMITS, then the answer is lost (500 / exception).</summary>
    public void LedgerCommitsThenFails(int status = 500, bool throwInstead = false)
        => Factory.AroundPosting[Tenant] = async (_, real) =>
        {
            await real();
            if (throwInstead) throw new InvalidOperationException("test: answer lost");
            return Response<MovementResponseDto>.Fail("INTERNAL", "test: answer lost", status);
        };

    public void LedgerThrows(Exception exception) => Factory.AroundPosting[Tenant] = (_, _) => throw exception;

    public void LedgerHealthy() => Factory.AroundPosting.TryRemove(Tenant, out _);

    /// <summary>A tick hook: the REAL posting runs once the gate opens (a slow ledger that commits late).</summary>
    public TaskCompletionSource LedgerWaitsFor()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Factory.AroundPosting[Tenant] = async (_, real) =>
        {
            await gate.Task.WaitAsync(TimeSpan.FromSeconds(30)); // a test that never opens the gate fails instead of hanging the host
            return await real();
        };
        return gate;
    }

    public int Lookups => Factory.KeyLookups.GetValueOrDefault(Tenant);
}
