using System.Reflection;
using System.Text.Json;
using Diten.InventoryService.Api.Controllers.Warehouse;
using Diten.InventoryService.Infrastructure.Authorization;
using Diten.InventoryService.Tests.TestInfrastructure;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;
using static Diten.InventoryService.Tests.Warehouse.PutawayScenario;
using static Diten.InventoryService.Tests.Warehouse.WarehouseScenario;

namespace Diten.InventoryService.Tests.Warehouse;

/// <summary>
/// MOD-0178 DL-2 — who sees and changes a task, and what is recorded (pack §8.1, §8.8, §14, §21; AC-13, AC-14, AC-15, AC-18, AC-48):
/// another tenant (also with the same company and user id) or another company of the tenant is 404 on every task endpoint and never listed;
/// a foreign company header is 403; no token 401, no key 403; every action carries its key; the central rows of pack §21 (Category
/// MasterData, Operation, EntityType WarehouseTask, the 0178 module code, EntityId = the task) next to the local timeline; the frozen outbound
/// GETs.
/// </summary>
public sealed class PutawayAccessAuditTests : IClassFixture<WarehouseApiFactory>
{
    private readonly WarehouseApiFactory _factory;

    public PutawayAccessAuditTests(WarehouseApiFactory factory) => _factory = factory;

    private static readonly string[] All = [TaskRead, TaskCreate, TaskUpdate, TaskConfirm, TaskCancel, TaskResolve];

    /// <summary>Every task endpoint with its method, path and key (the body is a valid one: scope decides first).</summary>
    private static IEnumerable<(HttpMethod Method, string Path, string Key)> Endpoints(string taskId) =>
    [
        (HttpMethod.Get, $"/api/warehouse/putaway-tasks/{taskId}", TaskRead),
        (HttpMethod.Get, $"/api/warehouse/putaway-tasks/{taskId}/events", TaskRead),
        (HttpMethod.Put, $"/api/warehouse/putaway-tasks/{taskId}", TaskUpdate),
        (HttpMethod.Post, $"/api/warehouse/putaway-tasks/{taskId}/claim", TaskConfirm),
        (HttpMethod.Post, $"/api/warehouse/putaway-tasks/{taskId}/confirmations", TaskConfirm),
        (HttpMethod.Post, $"/api/warehouse/putaway-tasks/{taskId}/confirmations/1/retry", TaskConfirm),
        (HttpMethod.Post, $"/api/warehouse/putaway-tasks/{taskId}/confirmations/1/returned", TaskConfirm),
        (HttpMethod.Post, $"/api/warehouse/putaway-tasks/{taskId}/confirmations/1/resolve-integrity", TaskResolve),
        (HttpMethod.Post, $"/api/warehouse/putaway-tasks/{taskId}/close-short", TaskConfirm),
        (HttpMethod.Post, $"/api/warehouse/putaway-tasks/{taskId}/cancel", TaskCancel)
    ];

    private static Dictionary<string, object?> BodyFor(string path, PutawayScenario s)
    {
        var body = new Dictionary<string, object?> { ["idempotencyKey"] = NewKey() };
        if (path.EndsWith("/confirmations", StringComparison.Ordinal))
        {
            body["quantity"] = "1";
            body["toLocationId"] = s.A01.ToString("D");
        }
        else if (path.EndsWith("/returned", StringComparison.Ordinal) || path.EndsWith("/resolve-integrity", StringComparison.Ordinal)
                 || path.EndsWith("/close-short", StringComparison.Ordinal) || path.EndsWith("/cancel", StringComparison.Ordinal))
        {
            body["reason"] = Reason;
        }
        else if (path.EndsWith("/claim", StringComparison.Ordinal) || path.EndsWith("/retry", StringComparison.Ordinal))
        {
        }
        else
        {
            body["note"] = "x";
        }

        return body;
    }

    private static async Task<int> CallAsync(PutawayScenario s, (HttpMethod Method, string Path, string Key) e, string token)
    {
        var body = e.Method == HttpMethod.Get ? null : BodyFor(e.Path, s);
        using var response = await s.W.Send(e.Method, e.Path, body, (string?)body?["idempotencyKey"], "W/\"1\"", token);
        return (int)response.StatusCode;
    }

    [Fact]
    public async Task AC13_another_tenant_even_with_the_same_company_and_user_gets_404_everywhere_and_an_empty_list()
    {
        var owner = await new PutawayScenario(_factory).ReadyAsync();
        await owner.ReceiveAsync(10);
        var task = await owner.TaskAsync(5);
        var stranger = TestTokens.Mint(Guid.NewGuid(), owner.LegalEntity, owner.User, All);

        foreach (var e in Endpoints(TaskId(task))) Assert.Equal(404, await CallAsync(owner, e, stranger));
        Assert.Empty((await Expect(owner.W.GetAsync("/api/warehouse/putaway-tasks", stranger), 200)).GetProperty("items").EnumerateArray());
        Assert.Empty((await Expect(owner.W.GetAsync($"/api/warehouse/putaway-candidates", stranger), 200)).GetProperty("items").EnumerateArray());
        Assert.Equal(1, (await owner.GetTaskAsync(TaskId(task))).GetProperty("version").GetInt32());
    }

    [Fact]
    public async Task AC13_another_company_of_the_tenant_gets_404_everywhere_and_never_sees_the_task_listed()
    {
        var owner = await new PutawayScenario(_factory).ReadyAsync();
        await owner.ReceiveAsync(10);
        var task = await owner.TaskAsync(5);
        var otherCompany = TestTokens.Mint(owner.Tenant, Guid.NewGuid(), owner.User, All);

        foreach (var e in Endpoints(TaskId(task))) Assert.Equal(404, await CallAsync(owner, e, otherCompany));
        Assert.Empty((await Expect(owner.W.GetAsync("/api/warehouse/putaway-tasks", otherCompany), 200)).GetProperty("items").EnumerateArray());
        Assert.Single((await Expect(owner.W.GetAsync("/api/warehouse/putaway-tasks", owner.Supervisor), 200)).GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task AC13_a_company_header_the_caller_may_not_use_is_403()
    {
        var s = await new PutawayScenario(_factory).ReadyAsync();
        Assert.Equal("CROSS_LE_FORBIDDEN", await ErrorCode(s.W.GetAsync("/api/warehouse/putaway-tasks", s.Supervisor,
            r => r.Headers.TryAddWithoutValidation("X-Legal-Entity-Id", Guid.NewGuid().ToString("D"))), 403));
    }

    [Fact]
    public async Task AC14_no_token_401_and_a_token_without_the_key_403_on_every_endpoint()
    {
        var s = await new PutawayScenario(_factory).ReadyAsync();
        await s.ReceiveAsync(10);
        var task = await s.TaskAsync(5);
        var endpoints = Endpoints(TaskId(task)).Concat(new (HttpMethod Method, string Path, string Key)[]
        {
            (HttpMethod.Get, "/api/warehouse/putaway-tasks", TaskRead),
            (HttpMethod.Get, "/api/warehouse/putaway-candidates", TaskRead),
            (HttpMethod.Post, "/api/warehouse/putaway-tasks", TaskCreate),
            (HttpMethod.Post, "/api/warehouse/putaway-tasks/bulk-assign", TaskUpdate),
            (HttpMethod.Get, "/api/warehouse/outbound-shipments", WarehousePermissions.OutboundShipmentsRead),
            (HttpMethod.Get, $"/api/warehouse/outbound-shipments/{Guid.NewGuid():D}", WarehousePermissions.OutboundShipmentsRead)
        }).ToList();

        foreach (var e in endpoints)
        {
            Assert.Equal(401, await CallAsync(s, e, ""));
            var allButOne = All.Append(WarehousePermissions.OutboundShipmentsRead).Where(k => k != e.Key).ToArray();
            Assert.Equal(403, await CallAsync(s, e, s.TokenOf(s.User, allButOne)));
        }
    }

    [Fact]
    public void AC14_every_putaway_and_outbound_action_carries_its_warehouse_key()
    {
        foreach (var controller in new[] { typeof(PutawayTasksController), typeof(OutboundShipmentsController) })
        {
            var actions = controller.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(m => m.GetCustomAttributes<HttpMethodAttribute>().Any()).ToList();
            Assert.NotEmpty(actions);
            Assert.All(actions, a => Assert.StartsWith("Permission:warehouse.", Assert.Single(a.GetCustomAttributes<HasPermissionAttribute>()).Policy));
        }
    }

    // ── AC-15 / AC-48 central rows next to the local timeline ───────────────────────────────────────────

    private static void AssertForwarded(PutawayScenario s, string requestType, string operation, string taskId)
    {
        var row = Assert.Single(s.W.CentralRows(requestType));
        Assert.Contains(row["Status"].AsString, new[] { "Pending", "Delivered" });
        Assert.True(row["ConfirmedByCommand"].AsBoolean);
        Assert.Equal("MasterData", row["Category"].AsString);
        Assert.Equal(operation, row["Operation"].AsString);
        Assert.Equal("WarehouseTask", row["EntityType"].AsString);
        Assert.Equal("PUTAWAY-PICKING-PACKING", row["SourceModule"].AsString);
        Assert.Equal(taskId, row["EntityId"].ToString());
    }

    [Fact]
    public async Task AC15_AC48_create_confirm_claim_update_cancel_each_one_central_row_and_the_what_is_local()
    {
        var s = await new PutawayScenario(_factory).ReadyAsync();
        await s.ReceiveAsync(100);
        var task = await s.TaskAsync(60);
        var id = TaskId(task);
        var claimed = await Expect(s.ClaimAsync(id, ETagOf(task)), 200);
        var updated = await Expect(s.UpdateTaskAsync(id, new() { ["note"] = "raf 3" }, ETagOf(claimed)), 200);
        var posted = await Expect(s.ConfirmAsync(updated, 20, s.A01), 201);

        AssertForwarded(s, "CreatePutawayTaskCommand", "Create", id);
        AssertForwarded(s, "ClaimPutawayTaskCommand", "Assign", id);
        AssertForwarded(s, "UpdatePutawayTaskCommand", "Update", id);
        AssertForwarded(s, "ConfirmPutawayTaskCommand", "Execute", id);
        var confirmRow = Assert.Single(s.W.CentralRows("ConfirmPutawayTaskCommand"));
        Assert.Equal(s.User.ToString("D"), confirmRow["ActorId"].ToString());

        var recorded = Assert.Single(s.TaskEvents(id), e => e["Kind"].AsString == "ConfirmationRecorded");
        Assert.Equal("1", recorded["Data"]["seq"].AsString);
        Assert.Equal(s.R01.ToString("D"), recorded["Data"]["fromLocationId"].AsString);
        var postedEvent = Assert.Single(s.TaskEvents(id), e => e["Kind"].AsString == "ConfirmationPosted");
        Assert.Equal(Confirmation(posted, 1).GetProperty("transactionNumber").GetString(), postedEvent["Data"]["transactionNumber"].AsString);

        var closed = await Expect(s.CloseShortAsync(id, ETagOf(posted)), 200);
        AssertForwarded(s, "ClosePutawayTaskShortCommand", "LifecycleTransition", id);
        Assert.Equal(["Created", "Claimed", "Updated", "ConfirmationRecorded", "ConfirmationPosted", "ClosedShort"], s.EventKinds(id));
        Assert.Equal("Completed", Status(closed));
    }

    [Fact]
    public async Task AC15_refusals_before_the_confirmation_record_leave_no_trace_at_all()
    {
        var s = await new PutawayScenario(_factory).ReadyAsync();
        await s.ReceiveAsync(10);
        var task = await s.TaskAsync(10);
        var id = TaskId(task);
        await Expect(s.ConfirmAsync(task, 11, s.A01), 422);                   // over the remaining quantity
        await Expect(s.ConfirmAsync(task, 1, s.R01), 422);                    // the source as target
        await Expect(s.ConfirmAsync(task, 1, s.A01, ifMatch: "W/\"9\""), 409); // stale If-Match
        await Expect(s.ConfirmAsync(task, 1, s.A01, token: s.TokenOf(Guid.NewGuid(), TaskRead, TaskConfirm)), 403);

        Assert.Equal((0, 0), s.Central("ConfirmPutawayTaskCommand"));
        Assert.Equal(["Created"], s.EventKinds(id));
    }

    // ── AC-18 the frozen outbound GETs ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AC18_outbound_list_is_empty_without_a_null_member_and_the_detail_is_unknown_outbound()
    {
        var s = await new PutawayScenario(_factory).ReadyAsync();
        var token = s.TokenOf(s.User, WarehousePermissions.OutboundShipmentsRead);
        var page = await Expect(s.W.GetAsync("/api/warehouse/outbound-shipments", token), 200);
        Assert.Empty(page.GetProperty("items").EnumerateArray());
        Assert.Equal("v1", page.GetProperty("contractVersion").GetString());
        Assert.False(page.TryGetProperty("nextCursor", out _));
        Assert.DoesNotContain(page.EnumerateObject(), p => p.Value.ValueKind == JsonValueKind.Null);

        var error = await Expect(s.W.GetAsync($"/api/warehouse/outbound-shipments/{Guid.NewGuid():D}", token), 404);
        Assert.Equal("UNKNOWN_OUTBOUND", Code(error));
        Assert.Equal("INVALID_REQUEST", await ErrorCode(s.W.GetAsync("/api/warehouse/outbound-shipments?status=Nope", token), 400));
    }
}
