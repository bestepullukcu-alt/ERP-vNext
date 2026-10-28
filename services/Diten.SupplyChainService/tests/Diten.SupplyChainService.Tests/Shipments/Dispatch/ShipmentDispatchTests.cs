using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Diten.SupplyChainService.Application.Common;
using Diten.SupplyChainService.Application.Features.Shipments.Dispatch;
using Diten.SupplyChainService.Domain.Features.Shipments;
using Diten.SupplyChainService.Persistence.Features.Shipments;
using Diten.SupplyChainService.Tests.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;
using Xunit.Abstractions;

namespace Diten.SupplyChainService.Tests.Shipments.Dispatch;

/// <summary>
/// MOD-0183 §23.5, DL-1 slice (S23-1…S23-6, S23-8…S23-15, S23-17…S23-20): the real host, the real coordinator and
/// repository on the owned replica set, the real INVENTORY client over <see cref="FakeInventory"/>, and a test clock for
/// the lease. Net stock is INVENTORY's own sum of what it stored (issue −q, reversal +q).
/// </summary>
public sealed class ShipmentDispatchTests(ITestOutputHelper output)
{
    private const string TestSecret = "mod0183-dl1-test-signing-secret-not-for-production-123456";
    private static string Connection => Environment.GetEnvironmentVariable("MOD0183_TEST_MONGO")
        ?? throw new InvalidOperationException("MOD0183_TEST_MONGO must point to an isolated replica set.");
    private static readonly TimeSpan PastLease = TimeSpan.FromSeconds(41);   // request timeout 10 s + 30 s, plus one
    private readonly Guid _tenant = Guid.NewGuid();
    private readonly Guid _le = Guid.NewGuid();
    private readonly Guid _actor = Guid.NewGuid();
    private readonly Guid _correlation = Guid.NewGuid();

    private sealed class Probe : IShipmentCommitProbe
    {
        public int Fail;
        public Task BeforeCommitAsync(CancellationToken ct) =>
            Interlocked.Exchange(ref Fail, 0) == 1 ? throw new IOException("Injected failure before commit.") : Task.CompletedTask;
    }
    private sealed class Host : WebApplicationFactory<Program>
    {
        public readonly FakeInventory Inventory = new();
        public readonly ManualClock Clock = new();
        public readonly Probe Probe = new();
        private (string Method, Func<Task> Action)? _hook;
        /// <summary>Runs <paramref name="action"/> once, just before the next call of repository method <paramref name="method"/>.</summary>
        public void Before(string method, Func<Task> action) => _hook = (method, action);
        private Func<Task>? TakeHook(string method)
        {
            lock (this)
            {
                if (_hook is not { } h || h.Method != method) return null;
                _hook = null; return h.Action;
            }
        }
        protected override IHost CreateHost(IHostBuilder builder)
        {
            builder.ConfigureHostConfiguration(c => c.AddInMemoryCollection(Settings()));
            return base.CreateHost(builder);
        }
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(Settings()));
            builder.ConfigureServices(s => s.Replace(ServiceDescriptor.Singleton<IShipmentCommitProbe>(Probe)).StubLegalEntityValidation().UseFakeInventory(Inventory, Clock)
                .Replace(ServiceDescriptor.Scoped<IShipmentRepository>(sp => HookedShipmentRepository.Wrap(ActivatorUtilities.CreateInstance<ShipmentRepository>(sp),
                    TakeHook))));
        }
        private static Dictionary<string, string?> Settings() => new(AuthServiceToken.JwtSettings(TestSecret))
        {
            ["Mongo:ConnectionString"] = Connection,
            ["Mongo:DatabaseName"] = "diten_mod0183_dispatch_tests",
            ["Inventory:ReferenceBaseUrl"] = DispatchTestHost.InventoryBaseUrl
        };
    }

    // ---- helpers -------------------------------------------------------------------------------------------------
    private static readonly (string Line, string Quantity, string? SkuLevel)[] ThreeLines = [("1", "10", "Lsku"), ("2", "5", "Lsku"), ("3", "2", "Lsku")];
    private static JsonObject CreateBody(IEnumerable<(string Line, string Quantity, string? SkuLevel)> lines)
    {
        var array = new JsonArray();
        foreach (var (line, quantity, level) in lines)
        {
            var l = new JsonObject
            {
                ["lineNumber"] = line, ["itemId"] = "b1f2c3d4-0000-0000-0000-00000000000" + line, ["skuId"] = "c3d4e5f6-0000-0000-0000-00000000000" + line,
                ["quantity"] = quantity, ["uomId"] = "EA", ["inventoryReferenceId"] = null, ["lotId"] = "lot-" + line, ["fromLocationId"] = "loc-" + line
            };
            if (level is not null) l["skuLevel"] = level;
            array.Add(l);
        }
        return new JsonObject
        {
            ["sourceModule"] = "MOD-0141", ["sourceType"] = "SALES_ORDER", ["sourceDocumentId"] = "SO-DL1", ["warehouseReferenceId"] = "wh-01",
            ["shipToReference"] = "CUST-1", ["plannedShipAt"] = "2026-10-28T08:00:00Z", ["lines"] = array
        };
    }
    private static JsonObject Transition(string status, string at = "2026-10-28T08:15:00Z") => new() { ["targetStatus"] = status, ["occurredAt"] = at };
    private string Token(Guid tenant) => AuthServiceToken.Mint(TestSecret, tenant, _actor,
        new[] { "read", "create", "dispatch", "pod.capture", "cancel" }.Select(p => "supplychain.shipments." + p));
    private async Task<(int Status, JsonObject Body)> Send(Host app, string method, string path, JsonObject? body = null, string? key = null, Guid? tenant = null, Guid? le = null)
    {
        using var client = app.CreateClient();
        var t = tenant ?? _tenant;
        using var request = new HttpRequestMessage(new HttpMethod(method), "/api/shipment-bundle/shipments" + path);
        request.Headers.Add("Authorization", "Bearer " + Token(t));
        request.Headers.Add("X-Tenant-Id", t.ToString()); request.Headers.Add("X-Legal-Entity-Id", (le ?? _le).ToString());
        request.Headers.Add("X-Correlation-Id", _correlation.ToString());
        if (key is not null) request.Headers.Add("Idempotency-Key", key);
        if (body is not null) request.Content = JsonContent.Create(body);
        using var response = await client.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();
        output.WriteLine($"{method} {path} => {(int)response.StatusCode} {text}");
        return ((int)response.StatusCode, string.IsNullOrEmpty(text) ? new JsonObject() : JsonNode.Parse(text)!.AsObject());
    }
    private async Task<Guid> Planned(Host app, IEnumerable<(string, string, string?)>? lines = null)
    {
        var created = await Send(app, "POST", "", CreateBody(lines ?? ThreeLines), "create-" + Guid.NewGuid());
        Assert.Equal(201, created.Status);
        var id = Guid.Parse(created.Body["shipmentId"]!.GetValue<string>());
        Assert.Equal(200, (await Send(app, "POST", $"/{id}/transition", Transition("Planned"), "plan-" + id)).Status);
        return id;
    }
    private Task<(int Status, JsonObject Body)> Dispatch(Host app, Guid id, string key, string at = "2026-10-28T08:15:00Z") =>
        Send(app, "POST", $"/{id}/transition", Transition("Dispatched", at), key);
    private async Task<JsonObject> Detail(Host app, Guid id) => (await Send(app, "GET", "/" + id)).Body;
    private async Task<Shipment> Stored(Host app, Guid id) =>
        await app.Services.GetRequiredService<IMongoDatabase>().GetCollection<Shipment>("sce_shipments").Find(x => x.Id == id).SingleAsync();
    private async Task<long> Events(Host app, Guid id, string type) =>
        await app.Services.GetRequiredService<IMongoDatabase>().GetCollection<BsonDocument>("sce_shipment_outbox")
            .CountDocumentsAsync(new BsonDocument { { "ShipmentId", id.ToString() }, { "EventType", type } });
    private async Task<List<BsonDocument>> AttemptAudit(Host app, Guid id) =>
        await app.Services.GetRequiredService<IMongoDatabase>().GetCollection<BsonDocument>("sce_shipment_audit")
            .Find(new BsonDocument { { "ShipmentId", id.ToString() }, { "Operation", "dispatch-attempt" } }).ToListAsync();
    private static string Code(JsonObject body) => body["error"]!["code"]!.GetValue<string>();
    private static JsonObject Details(JsonObject body) => body["error"]!["details"]!.AsObject();
    private static async Task Until(Func<bool> condition)
    {
        for (var i = 0; i < 400 && !condition(); i++) await Task.Delay(25);
        Assert.True(condition(), "condition not reached");
    }
    private static Task<HttpResponseMessage?> Answer(HttpResponseMessage response) => Task.FromResult<HttpResponseMessage?>(response);
    private static Task<HttpResponseMessage?> Default => Task.FromResult<HttpResponseMessage?>(null);
    private static bool IsIssue(FakeInventory.Call c, string line) => c.MovementType == "GOODS_ISSUE_SALES" && c.Line == line;

    // ---- S23-1 / S23-3 / reader --------------------------------------------------------------------------------------
    [Fact]
    public async Task S23_1_and_3_DispatchIssuesEachLineOnceAndAReplayPostsNothing()
    {
        using var app = new Host(); var id = await Planned(app, [("1", "30.000", "Lsku")]);
        var first = await Dispatch(app, id, "dispatch-1");
        Assert.Equal(200, first.Status); Assert.Equal("Dispatched", first.Body["status"]!.GetValue<string>());
        var call = Assert.Single(app.Inventory.Calls);
        Assert.Equal(("GOODS_ISSUE_SALES", $"{id}:1:dispatch:a1", _le.ToString(), _correlation.ToString()), (call.MovementType, call.Key, call.LegalEntity, call.Correlation));
        Assert.StartsWith("Bearer ", call.Authorization);
        Assert.Equal(("lot-1", "loc-1", "wh-01", "AVAILABLE", "MOD-0183", "SHIPMENT", id.ToString(), "1", id.ToString()),
            (call.Body["lotId"]!.GetValue<string>(), call.Body["fromLocationId"]!.GetValue<string>(), call.Body["fromWarehouseId"]!.GetValue<string>(),
             call.Body["fromStockStatus"]!.GetValue<string>(), call.Body["sourceModule"]!.GetValue<string>(), call.Body["sourceType"]!.GetValue<string>(),
             call.Body["sourceDocumentId"]!.GetValue<string>(), call.Body["sourceLineId"]!.GetValue<string>(), call.Body["documentGroupId"]!.GetValue<string>()));
        Assert.Equal("2026-10-28T08:15:00.0000000Z", call.Body["transactionDate"]!.GetValue<string>());
        Assert.Equal(-30m, app.Inventory.Net);
        var detail = await Detail(app, id);
        Assert.True(detail["stockIssued"]!.GetValue<bool>()); Assert.Equal("None", detail["dispatchState"]!.GetValue<string>());
        Assert.Equal(app.Inventory.IssueIds.Single(), detail["lines"]![0]!["issueTransactionId"]!.GetValue<string>());
        Assert.Equal(1, await Events(app, id, "ShipmentDispatched"));
        // S23-3: same command again → the stored answer; INVENTORY is not called, no second event.
        var replay = await Dispatch(app, id, "dispatch-1");
        Assert.Equal(200, replay.Status); Assert.True(replay.Body["idempotentReplay"]!.GetValue<bool>());
        Assert.Single(app.Inventory.Calls); Assert.Equal(1, await Events(app, id, "ShipmentDispatched"));
        // Plan §3 "reader": the live 3.4.0 detail is still a valid return source.
        var snapshot = Diten.SupplyChainService.Infrastructure.Features.Returns.ReturnReferenceReader.ParseShipment(
            JsonDocument.Parse(detail.ToJsonString()).RootElement, id, DateTimeOffset.UtcNow);
        Assert.Single(snapshot.Lines);
        Assert.Empty(Diten.SupplyChainService.Tests.Common.ShipmentBundleContract.ValidateResponse("/shipments/{shipmentId}", "GET", 200, detail));
    }

    // ---- S23-2 -------------------------------------------------------------------------------------------------------
    [Fact]
    public async Task S23_2_LotUnderRecallIsRefusedAndNothingIsDispatched()
    {
        using var app = new Host(); var id = await Planned(app, [("1", "4", "Lsku")]);
        app.Inventory.Script = c => IsIssue(c, "1") ? Answer(FakeInventory.Error(422, "LOT_UNDER_RECALL",
            new JsonObject { ["lotId"] = "lot-1", ["recallCaseNumber"] = "RC-2026-0004" })) : Default;
        var refused = await Dispatch(app, id, "dispatch-recall");
        Assert.Equal(422, refused.Status); Assert.Equal("INVENTORY_REFUSED", Code(refused.Body));
        Assert.Equal(("1", "LOT_UNDER_RECALL", "RC-2026-0004"), (Details(refused.Body)["lineNumber"]!.GetValue<string>(),
            Details(refused.Body)["inventoryCode"]!.GetValue<string>(), Details(refused.Body)["inventory"]!["recallCaseNumber"]!.GetValue<string>()));
        var detail = await Detail(app, id);
        Assert.Equal(("Planned", "None", false), (detail["status"]!.GetValue<string>(), detail["dispatchState"]!.GetValue<string>(), detail["stockIssued"]!.GetValue<bool>()));
        Assert.Equal(0, await Events(app, id, "ShipmentDispatched")); Assert.Equal(0m, app.Inventory.Net);
        // The compensated outcome is the command's receipt: the same command answers the same without calling INVENTORY.
        var calls = app.Inventory.Calls.Count;
        var again = await Dispatch(app, id, "dispatch-recall");
        Assert.Equal((422, "INVENTORY_REFUSED"), (again.Status, Code(again.Body))); Assert.Equal(calls, app.Inventory.Calls.Count);
    }

    // ---- S23-4 -------------------------------------------------------------------------------------------------------
    [Theory]
    [InlineData(503, "PRODUCT_MASTER_UNAVAILABLE", 503, "DEPENDENCY_UNAVAILABLE")]
    [InlineData(403, "CROSS_LE_FORBIDDEN", 403, "INVALID_REQUEST")]
    [InlineData(403, "FORBIDDEN", 403, "INVALID_REQUEST")]
    public async Task S23_4_FailureBeforeTheFirstPostChangesNothing(int inventoryStatus, string inventoryCode, int expected, string code)
    {
        using var app = new Host(); var id = await Planned(app);
        var before = await Stored(app, id);
        app.Inventory.Script = _ => Answer(FakeInventory.Error(inventoryStatus, inventoryCode));
        var r = await Dispatch(app, id, "dispatch-" + inventoryStatus);
        Assert.Equal((expected, code), (r.Status, Code(r.Body)));
        if (expected == 403) Assert.Equal(inventoryCode, Details(r.Body)["inventoryCode"]!.GetValue<string>());
        var after = await Stored(app, id);
        Assert.Equal((before.Status, before.Version), (after.Status, after.Version)); Assert.Null(after.DispatchAttempt); Assert.Empty(after.DispatchHistory);
        var detail = await Detail(app, id);
        Assert.Equal("None", detail["dispatchState"]!.GetValue<string>()); Assert.Null(detail["stockIssued"]);
        Assert.Single(app.Inventory.Calls); Assert.Equal(0, app.Inventory.Stored);
        // Once INVENTORY answers, the same command dispatches.
        app.Inventory.Script = null;
        Assert.Equal(200, (await Dispatch(app, id, "dispatch-" + inventoryStatus)).Status);
    }

    // ---- S23-5 -------------------------------------------------------------------------------------------------------
    [Fact]
    public async Task S23_5_NoSkuLevelIsRefusedBeforeInventoryAndLotRequiredCompensates()
    {
        using var app = new Host();
        var incomplete = await Planned(app, [("1", "1", "Lsku"), ("2", "1", null)]);
        var r = await Dispatch(app, incomplete, "dispatch-incomplete");
        Assert.Equal((422, "SHIPMENT_LINE_STOCK_INCOMPLETE", "2"), (r.Status, Code(r.Body), Details(r.Body)["lineNumber"]!.GetValue<string>()));
        Assert.Empty(app.Inventory.Calls); Assert.Null((await Stored(app, incomplete)).DispatchAttempt);

        var id = await Planned(app, [("1", "3", "Lsku"), ("2", "7", "Lsku")]);
        app.Inventory.Script = c => IsIssue(c, "2") ? Answer(FakeInventory.Error(422, "LOT_REQUIRED")) : Default;
        r = await Dispatch(app, id, "dispatch-lot");
        Assert.Equal((422, "INVENTORY_REFUSED", "LOT_REQUIRED"), (r.Status, Code(r.Body), Details(r.Body)["inventoryCode"]!.GetValue<string>()));
        Assert.Single(app.Inventory.Calls, c => c.MovementType == "REVERSAL"); Assert.Equal(0m, app.Inventory.Net);
    }

    // ---- S23-6 -------------------------------------------------------------------------------------------------------
    [Fact]
    public async Task S23_6_CrossTenantAndCrossLegalEntityAre404AndPostNothing()
    {
        using var app = new Host(); var id = await Planned(app);
        Assert.Equal(404, (await Send(app, "POST", $"/{id}/transition", Transition("Dispatched"), "x-tenant", tenant: Guid.NewGuid())).Status);
        Assert.Equal(404, (await Send(app, "POST", $"/{id}/transition", Transition("Dispatched"), "x-le", le: Guid.NewGuid())).Status);
        Assert.Empty(app.Inventory.Calls); Assert.Null((await Stored(app, id)).DispatchAttempt);
    }

    // ---- S23-8 / S23-14 / S23-17 -------------------------------------------------------------------------------------
    [Fact]
    public async Task S23_8_14_17_RefusalReversesInStoredOrderAndARedispatchUsesNewKeys()
    {
        using var app = new Host(); var id = await Planned(app);
        var refuse = true;
        app.Inventory.Script = c => refuse && IsIssue(c, "2") ? Answer(FakeInventory.Error(422, "INSUFFICIENT_AVAILABLE")) : Default;
        var r = await Dispatch(app, id, "dispatch-a1");
        Assert.Equal((422, "INVENTORY_REFUSED", "2"), (r.Status, Code(r.Body), Details(r.Body)["lineNumber"]!.GetValue<string>()));
        var calls = app.Inventory.Calls;
        Assert.Equal(new[] { "GOODS_ISSUE_SALES:1", "GOODS_ISSUE_SALES:2", "REVERSAL:" }, calls.Select(c => c.MovementType + ":" + c.Line));
        var reversal = calls[2]; var issue1 = (await Stored(app, id)).DispatchHistory.Single().Lines[0].IssueTransactionId;
        Assert.Equal($"{id}:1:reversal:a1", reversal.Key);
        Assert.Equal(issue1, reversal.Body["sourceDocumentId"]!.GetValue<string>());
        // S23-17: only the §23.4 fields.
        Assert.Equal(new[] { "documentGroupId", "idempotencyKey", "itemId", "movementType", "postingDate", "quantity", "skuId", "skuLevel",
            "sourceDocumentId", "sourceModule", "sourceType", "transactionDate", "uomId" }, reversal.Body.Select(p => p.Key).Order(StringComparer.Ordinal));
        var stored = await Stored(app, id);
        Assert.Equal((ShipmentStatus.Planned, (DispatchAttempt?)null, 2), (stored.Status, stored.DispatchAttempt, stored.NextDispatchAttemptNumber()));
        Assert.Equal(DispatchState.Compensated, stored.DispatchHistory.Single().State);
        Assert.Equal(0, await Events(app, id, "ShipmentDispatched")); Assert.Equal(0m, app.Inventory.Net);
        Assert.Contains(issue1, (await AttemptAudit(app, id)).Single()["IssueTransactionIds"].AsBsonArray.Select(x => x.AsString));

        // S23-14: the cause is removed; the next dispatch is attempt 2 with new keys and new movements.
        refuse = false;
        var again = await Dispatch(app, id, "dispatch-a2");
        Assert.Equal(200, again.Status);
        var a2 = app.Inventory.Calls.Skip(3).ToArray();
        Assert.Equal(new[] { $"{id}:1:dispatch:a2", $"{id}:2:dispatch:a2", $"{id}:3:dispatch:a2" }, a2.Select(c => c.Key));
        var completed = (await Stored(app, id)).DispatchAttempt!;
        Assert.Equal(2, completed.N);
        Assert.DoesNotContain(issue1, completed.Lines.Select(l => l.IssueTransactionId));
        Assert.Equal(3, completed.Lines.Select(l => l.IssueTransactionId).Distinct().Count());
        Assert.Equal(-17m, app.Inventory.Net);
        Assert.Equal(1, await Events(app, id, "ShipmentDispatched"));
    }

    // ---- S23-9 / S23-18(e) -------------------------------------------------------------------------------------------
    [Fact]
    public async Task S23_9_CrashBetweenLinesResumesAfterTheLeaseWithTheStoredRequest()
    {
        using var app = new Host(); var id = await Planned(app, [("1", "1", "Lsku"), ("2", "2", "Lsku")]);
        // Line 2 is posted by INVENTORY, then the writer "crashes" before it can record it.
        var crash = true;
        app.Inventory.Script = c =>
        {
            if (!(crash && IsIssue(c, "2"))) return Default;
            crash = false; app.Inventory.Answer(c);
            throw new InvalidOperationException("writer crashed after INVENTORY posted line 2");
        };
        Assert.Equal(500, (await Dispatch(app, id, "dispatch-crash")).Status);
        Assert.Equal("InProgress", (await Detail(app, id))["dispatchState"]!.GetValue<string>());
        // Within the lease another dispatch is refused; after it, the retry (other occurredAt) resumes attempt 1.
        Assert.Equal((409, "DISPATCH_IN_PROGRESS"), await StatusCode(Dispatch(app, id, "dispatch-retry", "2026-10-28T09:00:00Z")));
        app.Clock.Advance(PastLease);
        var resumed = await Dispatch(app, id, "dispatch-retry", "2026-10-28T09:00:00Z");
        Assert.Equal(200, resumed.Status);
        var line2 = app.Inventory.Calls.Where(c => IsIssue(c, "2")).ToArray();
        Assert.Equal(2, line2.Length); Assert.Equal(line2[0].BodyJson, line2[1].BodyJson); Assert.Equal($"{id}:2:dispatch:a1", line2[1].Key);
        Assert.Equal(2, app.Inventory.Stored); Assert.Equal(-3m, app.Inventory.Net);
        Assert.Equal(1, await Events(app, id, "ShipmentDispatched"));
        Assert.Equal(new DateTimeOffset(2026, 10, 28, 8, 15, 0, TimeSpan.Zero), (await Stored(app, id)).DispatchedAt);
    }
    private static async Task<(int, string)> StatusCode(Task<(int Status, JsonObject Body)> call) { var r = await call; return (r.Status, Code(r.Body)); }

    // ---- S23-10 / S23-20 ---------------------------------------------------------------------------------------------
    [Theory]
    [InlineData("Cancelled", 503)]
    [InlineData("Dispatched", 503)]
    [InlineData("Cancelled", 403)]
    public async Task S23_10_20_FailedReversalBlocksUntilTheExitResendsIt(string exit, int reversalFailure)
    {
        using var app = new Host(); var id = await Planned(app, [("1", "6", "Lsku"), ("2", "1", "Lsku")]);
        var refuse = true; var failReversal = true;
        app.Inventory.Script = c =>
            refuse && IsIssue(c, "2") ? Answer(FakeInventory.Error(422, "LOT_BLOCKED"))
            : failReversal && c.MovementType == "REVERSAL" ? Answer(FakeInventory.Error(reversalFailure, reversalFailure == 403 ? "FORBIDDEN" : "INVENTORY_UNAVAILABLE"))
            : Default;
        var r = await Dispatch(app, id, "dispatch-cf");
        // S23-20: a 403 on a REVERSAL is CompensationFailed / 409, never 403.
        Assert.Equal((409, "DISPATCH_COMPENSATION_FAILED"), (r.Status, Code(r.Body)));
        Assert.Equal("1", Details(r.Body)["lines"]![0]!.GetValue<string>());
        var detail = await Detail(app, id);
        Assert.Equal(("Planned", "CompensationFailed"), (detail["status"]!.GetValue<string>(), detail["dispatchState"]!.GetValue<string>()));
        Assert.Equal((409, "DISPATCH_COMPENSATION_FAILED"), await StatusCode(Send(app, "POST", $"/{id}/transition", Transition("InTransit"), "blocked-transit")));
        Assert.Equal((409, "DISPATCH_COMPENSATION_FAILED"), await StatusCode(Send(app, "POST", $"/{id}/pod",
            new JsonObject { ["recipientName"] = "R", ["receivedAt"] = "2026-10-29T08:00:00Z", ["evidenceReferenceIds"] = new JsonArray("e") }, "blocked-pod")));
        Assert.Equal(-6m, app.Inventory.Net);

        // The exit resends the open reversal first: same key, same stored dates.
        failReversal = false; refuse = false;
        var exited = await Send(app, "POST", $"/{id}/transition", Transition(exit, "2026-10-28T10:00:00Z"), "exit-" + exit);
        Assert.Equal(200, exited.Status); Assert.Equal(exit, exited.Body["status"]!.GetValue<string>());
        var reversals = app.Inventory.Calls.Where(c => c.MovementType == "REVERSAL").ToArray();
        Assert.Equal(2, reversals.Length); Assert.Equal(reversals[0].BodyJson, reversals[1].BodyJson); Assert.Equal($"{id}:1:reversal:a1", reversals[1].Key);
        var stored = await Stored(app, id);
        Assert.Equal(DispatchState.Compensated, stored.DispatchHistory.Single().State);
        if (exit == "Dispatched")
        {
            Assert.All(app.Inventory.Calls.Where(c => c.MovementType == "GOODS_ISSUE_SALES").Skip(2), c => Assert.EndsWith(":dispatch:a2", c.Key));
            Assert.Equal((2, DispatchState.Completed), (stored.DispatchAttempt!.N, stored.DispatchAttempt.State));
            Assert.Equal(new DateTimeOffset(2026, 10, 28, 10, 0, 0, TimeSpan.Zero), stored.DispatchAttempt.OccurredAt);
            Assert.Equal(-7m, app.Inventory.Net);
        }
        else Assert.Equal(0m, app.Inventory.Net);
        Assert.Contains(await AttemptAudit(app, id), a => a["Outcome"] == "CompensationFailed");
    }

    // ---- S23-11 ------------------------------------------------------------------------------------------------------
    [Fact]
    public async Task S23_11_KeyReusedIsAnIntegrityFaultThatBlocksAndIsNeverARefusal()
    {
        using var app = new Host(); var id = await Planned(app, [("1", "2", "Lsku"), ("2", "3", "Lsku")]);
        // Line 2's key already holds other bytes in INVENTORY (a forced date change).
        app.Inventory.Script = c =>
        {
            if (!IsIssue(c, "2")) return Default;
            var other = c.Body.DeepClone().AsObject(); other["transactionDate"] = "2026-10-27T08:15:00.0000000Z";
            app.Inventory.Script = null;
            app.Inventory.Answer(c with { Body = other, BodyJson = other.ToJsonString() });
            return Answer(FakeInventory.Error(400, "IDEMPOTENCY_KEY_REUSED"));
        };
        var r = await Dispatch(app, id, "dispatch-reuse");
        Assert.Equal((500, "INTERNAL_ERROR"), (r.Status, Code(r.Body)));
        Assert.Equal("CompensationFailed", (await Detail(app, id))["dispatchState"]!.GetValue<string>());
        Assert.DoesNotContain(app.Inventory.Calls, c => c.MovementType == "REVERSAL");
        Assert.Contains(await AttemptAudit(app, id), a => a["IntegrityFaults"].AsBsonArray.Any(f => f.AsString.StartsWith("IDEMPOTENCY_KEY_REUSED", StringComparison.Ordinal)));
        // Re-review R-2: the block stores the reversal date, so a late writer and the exit send the same REVERSAL bytes.
        Assert.NotNull((await Stored(app, id)).DispatchAttempt!.ReversalDate);
    }

    // ---- S23-12 ------------------------------------------------------------------------------------------------------
    [Fact]
    public void S23_12_OnlyTheCoordinatorAndTheLateWriterPolicyReachInventory()
    {
        var client = typeof(Diten.SupplyChainService.Application.Features.Shipments.Dispatch.IInventoryMovementClient);
        var assemblies = new[] { client.Assembly, typeof(Program).Assembly, typeof(ShipmentRepository).Assembly,
            typeof(Diten.SupplyChainService.Infrastructure.Features.Shipments.InventoryMovementClient).Assembly };
        var users = assemblies.Distinct().SelectMany(a => a.GetTypes())
            .Where(t => t.GetConstructors(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Any(c => c.GetParameters().Any(p => p.ParameterType == client)))
            .Select(t => t.Name).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(new[] { "LateWriterPolicy", "ShipmentDispatchCoordinator" }, users);
        var hosted = assemblies.Distinct().SelectMany(a => a.GetTypes()).Where(t => typeof(IHostedService).IsAssignableFrom(t));
        Assert.All(hosted, h => Assert.DoesNotContain(h.GetConstructors().SelectMany(c => c.GetParameters()), p =>
            p.ParameterType == client || p.ParameterType.Name is "ShipmentDispatchCoordinator" or "LateWriterPolicy"));
    }

    // ---- S23-13 ------------------------------------------------------------------------------------------------------
    [Fact]
    public async Task S23_13_CrashDuringCompensationResendsTheSameReversal()
    {
        using var app = new Host(); var id = await Planned(app, [("1", "4", "Lsku"), ("2", "1", "Lsku")]);
        var crash = true;
        app.Inventory.Script = c =>
        {
            if (IsIssue(c, "2")) return Answer(FakeInventory.Error(422, "INSUFFICIENT_STOCK"));
            if (crash && c.MovementType == "REVERSAL") { crash = false; app.Inventory.Answer(c); throw new InvalidOperationException("crash after REVERSAL posted"); }
            return Default;
        };
        Assert.Equal(500, (await Dispatch(app, id, "dispatch-c1")).Status);
        app.Clock.Advance(PastLease);
        var retry = await Dispatch(app, id, "dispatch-c2", "2026-10-28T11:00:00Z");
        Assert.Equal((422, "INVENTORY_REFUSED"), (retry.Status, Code(retry.Body)));
        var reversals = app.Inventory.Calls.Where(c => c.MovementType == "REVERSAL").ToArray();
        Assert.Equal(2, reversals.Length); Assert.Equal(reversals[0].Key, reversals[1].Key); Assert.Equal(reversals[0].BodyJson, reversals[1].BodyJson);
        Assert.Equal(0m, app.Inventory.Net);
        Assert.DoesNotContain(await AttemptAudit(app, id), a => a["IntegrityFaults"].AsBsonArray.Count > 0);
        Assert.Equal(DispatchState.Compensated, (await Stored(app, id)).DispatchHistory.Single().State);
    }

    // ---- S23-15 ------------------------------------------------------------------------------------------------------
    [Fact]
    public async Task S23_15_OneActiveAttemptPerShipment()
    {
        using var app = new Host(); var id = await Planned(app, [("1", "1", "Lsku")]);
        var gate = new TaskCompletionSource(); var posts = 0;
        // Only the first post waits: a second writer that wrongly takes the lease posts at once and the test fails, not hangs.
        app.Inventory.Script = async c => { if (IsIssue(c, "1") && Interlocked.Increment(ref posts) == 1) await gate.Task; return null; };
        var a = Dispatch(app, id, "dispatch-a");
        await Until(() => app.Inventory.Calls.Count == 1);
        var b = await Dispatch(app, id, "dispatch-b");
        Assert.Equal((409, "DISPATCH_IN_PROGRESS", 1), (b.Status, Code(b.Body), Details(b.Body)["attempt"]!.GetValue<int>()));
        Assert.Equal((409, "DISPATCH_IN_PROGRESS"), await StatusCode(Send(app, "POST", $"/{id}/transition", Transition("Cancelled"), "cancel-during")));
        Assert.Equal((409, "DISPATCH_IN_PROGRESS"), await StatusCode(Send(app, "POST", $"/{id}/pod",
            new JsonObject { ["recipientName"] = "R", ["receivedAt"] = "2026-10-29T08:00:00Z", ["evidenceReferenceIds"] = new JsonArray("e") }, "pod-during")));
        gate.SetResult();
        Assert.Equal(200, (await a).Status);
        Assert.Single(app.Inventory.Calls); Assert.Equal(1, (await Stored(app, id)).DispatchAttempt!.N);
    }

    // ---- S23-18 fencing ----------------------------------------------------------------------------------------------
    // (a) A gets 201 T2 for line 2 and pauses; B takes the lease, resends → replay T2, completes; A wakes → does not reverse.
    [Fact]
    public async Task S23_18a_PausedWriterOfACompletedAttemptOnlyLogs()
    {
        using var app = new Host(); var id = await Planned(app, [("1", "1", "Lsku"), ("2", "2", "Lsku")]);
        var gate = new TaskCompletionSource(); var paused = 0;
        app.Inventory.Script = async c =>
        {
            if (!IsIssue(c, "2") || Interlocked.Increment(ref paused) != 1) return null;
            var posted = app.Inventory.Answer(c); await gate.Task; return posted;
        };
        var a = Dispatch(app, id, "dispatch-a");
        await Until(() => app.Inventory.Stored == 2);
        app.Clock.Advance(PastLease);
        var b = await Dispatch(app, id, "dispatch-b");
        Assert.Equal(200, b.Status);
        gate.SetResult();
        Assert.Equal((409, "DISPATCH_IN_PROGRESS"), await StatusCode(a));
        Assert.DoesNotContain(app.Inventory.Calls, c => c.MovementType == "REVERSAL");
        Assert.Equal(-3m, app.Inventory.Net);
        var detail = await Detail(app, id);
        Assert.Equal(("Dispatched", "None", true), (detail["status"]!.GetValue<string>(), detail["dispatchState"]!.GetValue<string>(), detail["stockIssued"]!.GetValue<bool>()));
        Assert.Empty((await Stored(app, id)).DispatchLateWrites);
    }

    // (b) A's attempt was compensated while it paused before INVENTORY processed line 3 → A reverses its unrecorded issue.
    [Fact]
    public async Task S23_18b_PausedWriterOfACompensatedAttemptReversesItsUnrecordedIssue()
    {
        using var app = new Host(); var id = await Planned(app);
        var gate = new TaskCompletionSource(); var line3 = 0;
        app.Inventory.Script = async c =>
        {
            if (!IsIssue(c, "3")) return null;
            var n = Interlocked.Increment(ref line3);
            if (n == 1) { await gate.Task; return app.Inventory.Answer(c); }      // A: INVENTORY processes it only later
            return FakeInventory.Error(422, "INSUFFICIENT_AVAILABLE");             // B's resend: not there yet → refused
        };
        var a = Dispatch(app, id, "dispatch-a");
        await Until(() => line3 == 1);
        app.Clock.Advance(PastLease);
        var b = await Dispatch(app, id, "dispatch-b");
        Assert.Equal((422, "INVENTORY_REFUSED"), (b.Status, Code(b.Body)));
        gate.SetResult();
        Assert.Equal(409, (await a).Status);
        var late = app.Inventory.Calls.Last();
        Assert.Equal(("REVERSAL", $"{id}:3:reversal:a1"), (late.MovementType, late.Key));
        Assert.Equal(0m, app.Inventory.Net);
        Assert.Equal("Reversed", (await Stored(app, id)).DispatchLateWrites.Single().Outcome);
    }

    // (c) A crashes after its 201 for line 2 → B resends the sent line before compensating, then reverses it.
    [Fact]
    public async Task S23_18c_HolderResendsTheSentLineBeforeCompensating()
    {
        using var app = new Host(); var id = await Planned(app);
        var crashed = false;
        app.Inventory.Script = c =>
        {
            if (IsIssue(c, "2") && !crashed) { crashed = true; app.Inventory.Answer(c); throw new InvalidOperationException("crash after 201"); }
            return IsIssue(c, "3") ? Answer(FakeInventory.Error(422, "UNKNOWN_LOCATION")) : Default;
        };
        Assert.Equal(500, (await Dispatch(app, id, "dispatch-a")).Status);
        app.Clock.Advance(PastLease);
        var b = await Dispatch(app, id, "dispatch-b");
        Assert.Equal((422, "INVENTORY_REFUSED"), (b.Status, Code(b.Body)));
        Assert.Equal(2, app.Inventory.Calls.Count(c => c.MovementType == "REVERSAL")); Assert.Equal(0m, app.Inventory.Net);
    }

    // (d) a1 compensated, a2 Dispatched, then A wakes with an a1 201 for line 2 → stored n (2) is greater → A reverses it.
    [Fact]
    public async Task S23_18d_WriterOfAnOlderAttemptReversesAfterTheNextAttemptDispatched()
    {
        using var app = new Host(); var id = await Planned(app, [("1", "1", "Lsku"), ("2", "2", "Lsku")]);
        var gate = new TaskCompletionSource(); var a1Line2 = 0;
        app.Inventory.Script = async c =>
        {
            if (!(IsIssue(c, "2") && c.Key.EndsWith(":a1", StringComparison.Ordinal))) return null;
            if (Interlocked.Increment(ref a1Line2) == 1) { await gate.Task; return app.Inventory.Answer(c); }
            return FakeInventory.Error(422, "INSUFFICIENT_AVAILABLE");
        };
        var a = Dispatch(app, id, "dispatch-a");
        await Until(() => a1Line2 == 1);
        app.Clock.Advance(PastLease);
        Assert.Equal(422, (await Dispatch(app, id, "dispatch-b")).Status);
        Assert.Equal(200, (await Dispatch(app, id, "dispatch-c")).Status);
        gate.SetResult();
        Assert.Equal(409, (await a).Status);
        var late = app.Inventory.Calls.Last();
        Assert.Equal(("REVERSAL", $"{id}:2:reversal:a1"), (late.MovementType, late.Key));
        Assert.Equal(-3m, app.Inventory.Net);
        var stored = await Stored(app, id);
        Assert.Equal((ShipmentStatus.Dispatched, 2, DispatchState.Completed), (stored.Status, stored.DispatchAttempt!.N, stored.DispatchAttempt.State));
    }

    // ---- S23-19 ------------------------------------------------------------------------------------------------------
    [Fact]
    public async Task S23_19_ExternallyReversedIssueCountsAsReversed()
    {
        using var app = new Host(); var id = await Planned(app, [("1", "5", "Lsku"), ("2", "1", "Lsku")]);
        app.Inventory.Script = c =>
        {
            if (!IsIssue(c, "2")) return Default;
            app.Inventory.ReverseExternally(app.Inventory.IssueIds.Single());
            return Answer(FakeInventory.Error(422, "LOT_EXPIRED"));
        };
        var r = await Dispatch(app, id, "dispatch-ext");
        Assert.Equal((422, "INVENTORY_REFUSED"), (r.Status, Code(r.Body)));
        var stored = await Stored(app, id);
        Assert.Null(stored.DispatchAttempt);
        var closed = stored.DispatchHistory.Single();
        Assert.Equal((DispatchState.Compensated, true), (closed.State, closed.Lines[0].ExternallyReversed));
        Assert.Equal(0m, app.Inventory.Net);
        var audit = (await AttemptAudit(app, id)).Single();
        Assert.Contains(audit["IntegrityFaults"].AsBsonArray, f => f.AsString.StartsWith("ALREADY_REVERSED issue " + closed.Lines[0].IssueTransactionId, StringComparison.Ordinal));
    }

    // ---- S23-17 (400 on a REVERSAL is an integrity fault) and D atomicity ---------------------------------------------
    [Fact]
    public async Task S23_17_A400OnTheReversalIsAnIntegrityFault()
    {
        using var app = new Host(); var id = await Planned(app, [("1", "5", "Lsku"), ("2", "1", "Lsku")]);
        app.Inventory.Script = c => IsIssue(c, "2") ? Answer(FakeInventory.Error(422, "LOT_EXPIRED"))
            : c.MovementType == "REVERSAL" ? Answer(FakeInventory.Error(400, "INVALID_REQUEST")) : Default;
        var r = await Dispatch(app, id, "dispatch-400");
        Assert.Equal((409, "DISPATCH_COMPENSATION_FAILED"), (r.Status, Code(r.Body)));
        Assert.Contains(await AttemptAudit(app, id), a => a["IntegrityFaults"].AsBsonArray.Any(f => f.AsString.StartsWith("INVENTORY 400", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task CompletionFailureBeforeCommitIsRetriedWithoutPostingAgain()
    {
        using var app = new Host(); var id = await Planned(app, [("1", "1", "Lsku")]);
        app.Probe.Fail = 1;
        Assert.Equal(500, (await Dispatch(app, id, "dispatch-commit")).Status);
        Assert.Equal(0, await Events(app, id, "ShipmentDispatched"));
        app.Clock.Advance(PastLease);
        Assert.Equal(200, (await Dispatch(app, id, "dispatch-commit")).Status);
        Assert.Single(app.Inventory.Calls); Assert.Equal(1, await Events(app, id, "ShipmentDispatched"));
    }

    // ---- review fixes on 2cdeb44f9 (MOD-0183-DL1-CODE-REVIEW-STEPS1-4.md) ----------------------------------------------
    // F-1: a 500 / 502 / 504 may come after INVENTORY committed — the line stays sent, the attempt InProgress with the
    // lease, and the next holder's resend replays the issue: one movement, never an unrecorded one.
    [Theory]
    [InlineData(500)]
    [InlineData(502)]
    [InlineData(504)]
    public async Task F1_AGatewayErrorAfterInventoryCommittedKeepsTheLineSentAndTheRetryReplays(int status)
    {
        using var app = new Host(); var id = await Planned(app, [("1", "1", "Lsku")]);
        var first = true;
        app.Inventory.Script = c =>
        {
            if (!(first && IsIssue(c, "1"))) return Default;
            first = false; app.Inventory.Answer(c);
            return Answer(FakeInventory.Error(status, "UPSTREAM_ERROR"));
        };
        Assert.Equal((503, "DEPENDENCY_UNAVAILABLE"), await StatusCode(Dispatch(app, id, "dispatch-f1")));
        var stored = await Stored(app, id);
        Assert.Equal((DispatchState.InProgress, true), (stored.DispatchAttempt!.State, stored.DispatchAttempt.Lines[0].Sent));
        Assert.Equal((409, "DISPATCH_IN_PROGRESS"), await StatusCode(Dispatch(app, id, "dispatch-f1-again")));
        app.Clock.Advance(PastLease);
        Assert.Equal(200, (await Dispatch(app, id, "dispatch-f1-retry")).Status);
        var issues = app.Inventory.Calls.Where(c => IsIssue(c, "1")).ToArray();
        Assert.Equal(2, issues.Length); Assert.Equal(issues[0].BodyJson, issues[1].BodyJson);
        Assert.Equal((1, -1m), (app.Inventory.Stored, app.Inventory.Net));
    }

    // F-3: the compensation outcome (close, audit, receipt) is one transaction — a failure before its commit leaves the
    // attempt open, and the same key later answers the stored refusal without opening a2 or posting anything new.
    [Fact]
    public async Task F3_CompensationOutcomeCommitsWithItsReceipt()
    {
        using var app = new Host(); var id = await Planned(app, [("1", "4", "Lsku"), ("2", "1", "Lsku")]);
        app.Inventory.Script = c => IsIssue(c, "2") ? Answer(FakeInventory.Error(422, "LOT_BLOCKED")) : Default;
        app.Probe.Fail = 1;
        Assert.Equal(500, (await Dispatch(app, id, "dispatch-f3")).Status);
        var open = await Stored(app, id);
        Assert.Empty(open.DispatchHistory); Assert.Equal((1, DispatchState.InProgress), (open.DispatchAttempt!.N, open.DispatchAttempt.State));
        Assert.Empty(await AttemptAudit(app, id));
        app.Clock.Advance(PastLease);
        Assert.Equal((422, "INVENTORY_REFUSED"), await StatusCode(Dispatch(app, id, "dispatch-f3")));
        var calls = app.Inventory.Calls.Count;
        Assert.Equal((422, "INVENTORY_REFUSED"), await StatusCode(Dispatch(app, id, "dispatch-f3")));
        Assert.Equal(calls, app.Inventory.Calls.Count);
        Assert.DoesNotContain(app.Inventory.Calls, c => c.Key.EndsWith(":a2", StringComparison.Ordinal));
        Assert.Equal(0m, app.Inventory.Net);
        var stored = await Stored(app, id);
        Assert.Null(stored.DispatchAttempt); Assert.Equal(DispatchState.Compensated, stored.DispatchHistory.Single().State);
        Assert.Single(await AttemptAudit(app, id), a => a["Outcome"] == "Compensated");
    }

    // F-5: a late writer of a CompensationFailed attempt reverses with the attempt's stored reversal date (same key, same
    // bytes as the holder's), and the exit takes that reversal's id instead of posting it again — no KEY_REUSED loop.
    [Fact]
    public async Task F5_LateWriterUsesTheStoredReversalDateAndTheExitTakesItsReversal()
    {
        using var app = new Host(); var id = await Planned(app, [("1", "3", "Lsku")]);
        var crashed = false;
        app.Inventory.Script = c =>
        {
            if (crashed || !IsIssue(c, "1")) return Default;
            crashed = true; app.Inventory.Answer(c);
            throw new InvalidOperationException("writer crashed after INVENTORY posted line 1");
        };
        Assert.Equal(500, (await Dispatch(app, id, "dispatch-f5")).Status);
        // A holder's compensation then stopped: CompensationFailed with its reversal date, line 1 sent without an id.
        var t0 = new DateTimeOffset(2026, 10, 28, 9, 0, 0, TimeSpan.Zero);
        await app.Services.GetRequiredService<IMongoDatabase>().GetCollection<Shipment>("sce_shipments").UpdateOneAsync(x => x.Id == id,
            Builders<Shipment>.Update.Set(x => x.DispatchAttempt!.State, DispatchState.CompensationFailed).Set(x => x.DispatchAttempt!.ReversalDate, t0));
        app.Clock.Advance(TimeSpan.FromHours(1));
        var blocked = await Stored(app, id);
        using (var scope = app.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<RequestContext>();
            context.Scope = new ShipmentScope(_tenant, _le, _actor); context.CorrelationId = _correlation; context.Authorization = "Bearer " + Token(_tenant);
            await scope.ServiceProvider.GetRequiredService<LateWriterPolicy>().HandleAsync(id, 1, "1", blocked.DispatchAttempt!.Lines[0].RequestJson!,
                app.Inventory.IssueIds.Single(), default);
        }
        var late = Assert.Single(app.Inventory.Calls, c => c.MovementType == "REVERSAL");
        Assert.Equal(($"{id}:1:reversal:a1", "2026-10-28T09:00:00.0000000Z"), (late.Key, late.Body["transactionDate"]!.GetValue<string>()));
        Assert.Equal(0m, app.Inventory.Net);
        var exited = await Send(app, "POST", $"/{id}/transition", Transition("Cancelled", "2026-10-28T10:00:00Z"), "exit-f5");
        Assert.Equal((200, "Cancelled"), (exited.Status, exited.Body["status"]!.GetValue<string>()));
        Assert.Single(app.Inventory.Calls, c => c.MovementType == "REVERSAL");
        var stored = await Stored(app, id);
        var reversal = stored.DispatchLateWrites.Single().ReversalTransactionId;
        Assert.NotNull(reversal); Assert.Equal(reversal, stored.DispatchHistory.Single().Lines.Single().ReversalTransactionId);
        Assert.Equal(0m, app.Inventory.Net);
    }

    // F-6: a 403 on a resent line keeps its sent marker but releases the lease — the next command needs no lease wait.
    [Fact]
    public async Task F6_A403OnAResendReleasesTheLease()
    {
        using var app = new Host(); var id = await Planned(app, [("1", "1", "Lsku")]);
        var call = 0;
        app.Inventory.Script = c => Interlocked.Increment(ref call) switch
        {
            1 => throw new HttpRequestException("connection reset"),
            2 => Answer(FakeInventory.Error(403, "FORBIDDEN")),
            _ => Default
        };
        Assert.Equal((503, "DEPENDENCY_UNAVAILABLE"), await StatusCode(Dispatch(app, id, "dispatch-f6a")));
        Assert.Equal((409, "DISPATCH_IN_PROGRESS"), await StatusCode(Dispatch(app, id, "dispatch-f6b")));   // ambiguous: lease kept
        app.Clock.Advance(PastLease);
        Assert.Equal((403, "INVALID_REQUEST"), await StatusCode(Dispatch(app, id, "dispatch-f6c")));
        Assert.True((await Stored(app, id)).DispatchAttempt!.Lines[0].Sent);
        Assert.Equal(200, (await Dispatch(app, id, "dispatch-f6d")).Status);
        Assert.Equal(-1m, app.Inventory.Net);
    }

    // F-7: a dispatch whose conditional begin loses to a Cancelled answers the transition refusal, not "in progress".
    [Fact]
    public async Task F7_ADispatchThatLosesTheRaceToCancelAnswersInvalidTransition()
    {
        using var app = new Host(); var id = await Planned(app, [("1", "1", "Lsku")]);
        app.Before(nameof(IShipmentRepository.TryBeginOrResumeDispatchAsync), () => Shipments(app)
            .UpdateOneAsync(x => x.Id == id, Builders<Shipment>.Update.Set(x => x.Status, ShipmentStatus.Cancelled)));
        Assert.Equal((422, "INVALID_SHIPMENT_TRANSITION"), await StatusCode(Dispatch(app, id, "dispatch-f7")));
        Assert.Empty(app.Inventory.Calls);
    }

    // ---- re-review fixes on 1bda485ee (MOD-0183-DL1-CODE-REVIEW-FIXES.md) --------------------------------------------
    private static IMongoCollection<Shipment> Shipments(Host app) => app.Services.GetRequiredService<IMongoDatabase>().GetCollection<Shipment>("sce_shipments");

    // N-1: a crash in the middle of compensation (line 1's REVERSAL posted, not recorded). The stock is then freed; the
    // resume continues the compensation — it never resends the refused line or completes with a reversed issue.
    [Fact]
    public async Task N1_AResumeMidCompensationContinuesItAndNeverCompletes()
    {
        using var app = new Host(); var id = await Planned(app, [("1", "4", "Lsku"), ("2", "1", "Lsku")]);
        var refuse = true; var crash = true;
        app.Inventory.Script = c =>
        {
            if (refuse && IsIssue(c, "2")) return Answer(FakeInventory.Error(422, "INSUFFICIENT_STOCK"));
            if (crash && c.MovementType == "REVERSAL") { crash = false; app.Inventory.Answer(c); throw new InvalidOperationException("crash after REVERSAL posted"); }
            return Default;
        };
        Assert.Equal(500, (await Dispatch(app, id, "dispatch-n1a")).Status);
        refuse = false;
        app.Clock.Advance(PastLease);
        var retry = await Dispatch(app, id, "dispatch-n1b", "2026-10-28T11:00:00Z");
        Assert.Equal((422, "INVENTORY_REFUSED", "2"), (retry.Status, Code(retry.Body), Details(retry.Body)["lineNumber"]!.GetValue<string>()));
        Assert.Single(app.Inventory.Calls, c => IsIssue(c, "2"));
        Assert.Equal((0m, 0L), (app.Inventory.Net, await Events(app, id, "ShipmentDispatched")));
        var stored = await Stored(app, id);
        Assert.Equal((ShipmentStatus.Planned, DispatchState.Compensated), (stored.Status, stored.DispatchHistory.Single().State));
        Assert.NotNull(stored.DispatchHistory.Single().Lines[0].ReversalTransactionId);
        // The same command replays the stored refusal.
        Assert.Equal((422, "INVENTORY_REFUSED"), await StatusCode(Dispatch(app, id, "dispatch-n1b", "2026-10-28T11:00:00Z")));
    }

    // N-1, second layer: D refuses to complete an attempt that has started compensating, even when a writer got there.
    [Fact]
    public async Task N1_CompletionRefusesACompensatingAttempt()
    {
        using var app = new Host(); var id = await Planned(app, [("1", "1", "Lsku")]);
        var crashed = false;
        app.Inventory.Script = c =>
        {
            if (crashed || !IsIssue(c, "1")) return Default;
            crashed = true; app.Inventory.Answer(c);
            throw new InvalidOperationException("writer crashed after INVENTORY posted line 1");
        };
        Assert.Equal(500, (await Dispatch(app, id, "dispatch-d1")).Status);
        app.Clock.Advance(PastLease);
        // A compensation starts between this writer's last post and its completion.
        app.Before(nameof(IShipmentRepository.MutateAsync), () => Shipments(app).UpdateOneAsync(x => x.Id == id,
            Builders<Shipment>.Update.Set(x => x.DispatchAttempt!.ReversalDate, new DateTimeOffset(2026, 10, 28, 9, 0, 0, TimeSpan.Zero))));
        Assert.Equal((409, "DISPATCH_IN_PROGRESS"), await StatusCode(Dispatch(app, id, "dispatch-d2")));
        var stored = await Stored(app, id);
        Assert.Equal((ShipmentStatus.Planned, DispatchState.InProgress), (stored.Status, stored.DispatchAttempt!.State));
        Assert.Equal(0, await Events(app, id, "ShipmentDispatched"));
    }

    // N-2: KEY_REUSED on the B′ resend of the exit blocks the attempt (§23.4) — it is never skipped and closed.
    [Fact]
    public async Task N2_KeyReusedOnTheExitResendBlocksTheAttempt()
    {
        using var app = new Host(); var id = await Planned(app, [("1", "3", "Lsku")]);
        var crashed = false;
        app.Inventory.Script = c =>
        {
            if (crashed && IsIssue(c, "1")) return Answer(FakeInventory.Error(400, "IDEMPOTENCY_KEY_REUSED"));
            if (crashed || !IsIssue(c, "1")) return Default;
            crashed = true; app.Inventory.Answer(c);
            throw new InvalidOperationException("writer crashed after INVENTORY posted line 1");
        };
        Assert.Equal(500, (await Dispatch(app, id, "dispatch-n2")).Status);
        await Shipments(app).UpdateOneAsync(x => x.Id == id, Builders<Shipment>.Update.Set(x => x.DispatchAttempt!.State, DispatchState.CompensationFailed)
            .Set(x => x.DispatchAttempt!.ReversalDate, new DateTimeOffset(2026, 10, 28, 9, 0, 0, TimeSpan.Zero)));
        app.Clock.Advance(TimeSpan.FromHours(1));
        var exited = await Send(app, "POST", $"/{id}/transition", Transition("Cancelled", "2026-10-28T10:00:00Z"), "exit-n2");
        Assert.Equal((409, "DISPATCH_COMPENSATION_FAILED"), (exited.Status, Code(exited.Body)));
        var stored = await Stored(app, id);
        Assert.Equal((ShipmentStatus.Planned, DispatchState.CompensationFailed), (stored.Status, stored.DispatchAttempt!.State));
        Assert.Empty(stored.DispatchHistory); Assert.Equal(-3m, app.Inventory.Net);
        Assert.Contains(await AttemptAudit(app, id), a => a["IntegrityFaults"].AsBsonArray.Any(f => f.AsString == "IDEMPOTENCY_KEY_REUSED resend line 1"));
    }

    // R-3: a begin lost to an attempt that became CompensationFailed answers DISPATCH_COMPENSATION_FAILED.
    [Fact]
    public async Task R3_ABeginLostToACompensationFailedAttemptSaysSo()
    {
        using var app = new Host(); var id = await Planned(app, [("1", "1", "Lsku")]);
        var crashed = false;
        app.Inventory.Script = c =>
        {
            if (crashed || !IsIssue(c, "1")) return Default;
            crashed = true; app.Inventory.Answer(c);
            throw new InvalidOperationException("writer crashed after INVENTORY posted line 1");
        };
        Assert.Equal(500, (await Dispatch(app, id, "dispatch-r3a")).Status);
        app.Clock.Advance(PastLease);
        app.Before(nameof(IShipmentRepository.TryBeginOrResumeDispatchAsync), () => Shipments(app).UpdateOneAsync(x => x.Id == id,
            Builders<Shipment>.Update.Set(x => x.DispatchAttempt!.State, DispatchState.CompensationFailed)));
        Assert.Equal((409, "DISPATCH_COMPENSATION_FAILED"), await StatusCode(Dispatch(app, id, "dispatch-r3b")));
    }

    // R-4: the late writer's record and its audit row commit together — a failure before the commit leaves neither.
    [Fact]
    public async Task R4_LateWriterRecordAndAuditCommitTogether()
    {
        using var app = new Host(); var id = await Planned(app);
        var gate = new TaskCompletionSource(); var line3 = 0;
        app.Inventory.Script = async c =>
        {
            if (!IsIssue(c, "3")) return null;
            if (Interlocked.Increment(ref line3) == 1) { await gate.Task; return app.Inventory.Answer(c); }
            return FakeInventory.Error(422, "INSUFFICIENT_AVAILABLE");
        };
        var a = Dispatch(app, id, "dispatch-a");
        await Until(() => line3 == 1);
        app.Clock.Advance(PastLease);
        Assert.Equal(422, (await Dispatch(app, id, "dispatch-b")).Status);
        app.Probe.Fail = 1;
        gate.SetResult();
        Assert.Equal(500, (await a).Status);
        Assert.Equal(("REVERSAL", 0m), (app.Inventory.Calls.Last().MovementType, app.Inventory.Net));
        Assert.Empty((await Stored(app, id)).DispatchLateWrites);
        Assert.DoesNotContain(await AttemptAudit(app, id), r => r["Outcome"].AsString.StartsWith("LateWriter", StringComparison.Ordinal));
    }
}
