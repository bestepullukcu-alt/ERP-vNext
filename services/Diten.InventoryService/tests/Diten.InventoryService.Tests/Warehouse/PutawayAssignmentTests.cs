using System.Net;
using System.Text;
using System.Text.Json;
using Diten.InventoryService.Application.Features.Warehouse.Ports;
using Diten.InventoryService.Infrastructure.Warehouse;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static Diten.InventoryService.Tests.Warehouse.PutawayScenario;
using static Diten.InventoryService.Tests.Warehouse.WarehouseScenario;

namespace Diten.InventoryService.Tests.Warehouse;

/// <summary>
/// MOD-0178 DL-2 — the assignee (pack Q-13 decision A, Beste 2026-10-30; §8.13, §12 "assigneeUserId" / "toplu atama"; AC-4, AC-15, AC-47):
/// a task is assigned on create, on update or in bulk to an active user of the tenant, checked with AUTH (a fake behind
/// <see cref="IWarehouseAssigneeDirectory"/>); an unknown / inactive / other tenant's user is 422 <c>WMS_ASSIGNEE_INVALID</c>; AUTH down is 503
/// and nothing is written. The bulk command writes each row at its own version, answers per row and replays by its key.
/// </summary>
public sealed class PutawayAssignmentTests : IClassFixture<WarehouseApiFactory>
{
    private const string BulkAssign = "BulkAssignPutawayTaskCommand";

    private readonly WarehouseApiFactory _factory;

    public PutawayAssignmentTests(WarehouseApiFactory factory) => _factory = factory;

    private async Task<PutawayScenario> ReadyAsync(decimal stock = 100)
    {
        var s = await new PutawayScenario(_factory).ReadyAsync();
        await s.ReceiveAsync(stock);
        return s;
    }

    private static Dictionary<string, object?> WithAssignee(Dictionary<string, object?> body, Guid user)
    {
        body["assigneeUserId"] = user.ToString("D");
        return body;
    }

    private static JsonElement Row(JsonElement result, string taskId)
        => result.GetProperty("results").EnumerateArray().Single(r => r.GetProperty("taskId").GetString() == taskId);

    [Fact]
    public async Task Q13_create_with_an_active_user_assigns_and_only_that_operator_or_a_supervisor_confirms()
    {
        var s = await ReadyAsync();
        var alice = s.ActiveUser();
        var task = await Expect(s.CreateTaskAsync(WithAssignee(s.TaskBody(10), alice)), 201);
        Assert.Equal(alice.ToString("D"), task.GetProperty("assignee").GetProperty("userId").GetString());
        Assert.Equal("WMS_TASK_ALREADY_ASSIGNED", await ErrorCode(s.ClaimAsync(TaskId(task), ETagOf(task)), 409));
        Assert.Equal("WMS_NOT_ASSIGNEE", await ErrorCode(s.ConfirmAsync(task, 5, s.A01, token: s.TokenOf(Guid.NewGuid(), TaskRead, TaskConfirm)), 403));
        await Expect(s.ConfirmAsync(task, 5, s.A01, token: s.TokenOf(alice, TaskRead, TaskConfirm)), 201);
    }

    [Fact]
    public async Task Q13_an_unknown_inactive_or_other_tenants_user_is_422_and_nothing_is_written()
    {
        var s = await ReadyAsync();
        var foreign = Guid.NewGuid();
        _factory.ActiveUsers[(Guid.NewGuid(), foreign)] = true; // an active user — of another tenant

        foreach (var user in new[] { Guid.NewGuid(), foreign })
        {
            var error = await Expect(s.CreateTaskAsync(WithAssignee(s.TaskBody(10), user)), 422);
            Assert.Equal("WMS_ASSIGNEE_INVALID", Code(error));
        }

        Assert.Empty(s.W.Stored("wms_tasks"));
        Assert.Equal((0, 0), s.Central("CreatePutawayTaskCommand"));
    }

    [Fact]
    public async Task Q13_auth_down_is_503_on_create_update_and_bulk_and_nothing_is_written()
    {
        var s = await ReadyAsync();
        var task = await s.TaskAsync(10);
        var alice = s.ActiveUser();
        _factory.AuthDown[s.Tenant] = true;
        try
        {
            Assert.Equal("WMS_ASSIGNEE_DIRECTORY_UNAVAILABLE", await ErrorCode(s.CreateTaskAsync(WithAssignee(s.TaskBody(10), alice)), 503));
            Assert.Equal("WMS_ASSIGNEE_DIRECTORY_UNAVAILABLE",
                await ErrorCode(s.UpdateTaskAsync(TaskId(task), new() { ["assigneeUserId"] = alice.ToString("D") }, ETagOf(task)), 503));
            Assert.Equal("WMS_ASSIGNEE_DIRECTORY_UNAVAILABLE", await ErrorCode(s.BulkAssignAsync(alice, [(TaskId(task), 1)]), 503));
        }
        finally
        {
            _factory.AuthDown.TryRemove(s.Tenant, out _);
        }

        Assert.Single(s.W.Stored("wms_tasks"));
        Assert.Equal(1, s.StoredTask(TaskId(task))["Version"].AsInt32);
        Assert.Equal(["Created"], s.EventKinds(TaskId(task)));
        Assert.Equal((0, 0), s.Central(BulkAssign));
        Assert.Equal((0, 0), s.Central("UpdatePutawayTaskCommand"));
    }

    [Fact]
    public async Task Q13_update_assigns_with_a_check_and_clears_without_one()
    {
        var s = await ReadyAsync();
        var task = await s.TaskAsync(10);
        Assert.Equal("WMS_ASSIGNEE_INVALID",
            await ErrorCode(s.UpdateTaskAsync(TaskId(task), new() { ["assigneeUserId"] = Guid.NewGuid().ToString("D") }, ETagOf(task)), 422));

        var alice = s.ActiveUser();
        var assigned = await Expect(s.UpdateTaskAsync(TaskId(task), new() { ["assigneeUserId"] = alice.ToString("D") }, ETagOf(task)), 200);
        Assert.Equal(alice.ToString("D"), assigned.GetProperty("assignee").GetProperty("userId").GetString());

        _factory.AuthDown[s.Tenant] = true;
        try
        {
            var cleared = await Expect(s.UpdateTaskAsync(TaskId(task), new() { ["assigneeUserId"] = null }, ETagOf(assigned)), 200);
            Assert.False(cleared.TryGetProperty("assignee", out var a) && a.ValueKind == JsonValueKind.Object);
        }
        finally
        {
            _factory.AuthDown.TryRemove(s.Tenant, out _);
        }
    }

    [Fact]
    public async Task AC4_AC15_bulk_assign_answers_per_row_one_central_row_without_entity_and_replays_by_key()
    {
        var s = await ReadyAsync();
        var ok = await s.TaskAsync(10);
        var stale = await s.TaskAsync(10);
        var pending = await s.TaskAsync(10);
        s.LedgerAnswersBefore(503);
        var pendingNow = DetailsTask(await Expect(s.ConfirmAsync(pending, 5, s.A01), 503));
        s.LedgerHealthy();
        var unknown = Guid.NewGuid().ToString("D");
        var alice = s.ActiveUser();
        var key = NewKey();
        var items = new[] { (TaskId(ok), 1), (TaskId(stale), 9), (TaskId(pending), pendingNow.GetProperty("version").GetInt32()), (unknown, 1) };

        var result = await Expect(s.BulkAssignAsync(alice, items, key), 200);
        Assert.Equal("Assigned", Row(result, TaskId(ok)).GetProperty("outcome").GetString());
        Assert.Equal(2, Row(result, TaskId(ok)).GetProperty("version").GetInt32());
        Assert.Equal("Conflict", Row(result, TaskId(stale)).GetProperty("outcome").GetString());
        Assert.Equal("PendingConfirmation", Row(result, TaskId(pending)).GetProperty("outcome").GetString());
        Assert.Equal("NotFound", Row(result, unknown).GetProperty("outcome").GetString());

        var row = Assert.Single(s.W.CentralRows(BulkAssign));
        Assert.Contains(row["Status"].AsString, new[] { "Pending", "Delivered" });
        Assert.Equal("Assign", row["Operation"].AsString);
        Assert.True(!row.Contains("EntityId") || row["EntityId"].IsBsonNull);
        Assert.Equal(["Created", "Assigned"], s.EventKinds(TaskId(ok)));
        Assert.Equal(alice.ToString("D"), (await s.GetTaskAsync(TaskId(ok))).GetProperty("assignee").GetProperty("userId").GetString());

        // the same key + body → replay (written row Assigned, the others Conflict / NotFound), nothing new; another body → 400
        var checks = _factory.AssigneeChecks.GetValueOrDefault(s.Tenant);
        var replay = await Expect(s.BulkAssignAsync(alice, items, key), 200);
        Assert.Equal("Assigned", Row(replay, TaskId(ok)).GetProperty("outcome").GetString());
        Assert.Equal("Conflict", Row(replay, TaskId(pending)).GetProperty("outcome").GetString());
        Assert.Equal("NotFound", Row(replay, unknown).GetProperty("outcome").GetString());
        Assert.Equal(checks, _factory.AssigneeChecks.GetValueOrDefault(s.Tenant));
        Assert.Equal("IDEMPOTENCY_KEY_REUSED", await ErrorCode(s.BulkAssignAsync(alice, items.Take(1), key), 400));
        Assert.Single(s.W.CentralRows(BulkAssign));
        Assert.Equal(["Created", "Assigned"], s.EventKinds(TaskId(ok)));
    }

    [Fact]
    public async Task AC15_a_bulk_assign_that_writes_no_row_leaves_no_trace_and_no_key()
    {
        var s = await ReadyAsync();
        var task = await s.TaskAsync(10);
        var alice = s.ActiveUser();
        var key = NewKey();

        var result = await Expect(s.BulkAssignAsync(alice, [(TaskId(task), 7)], key), 200);
        Assert.Equal("Conflict", Row(result, TaskId(task)).GetProperty("outcome").GetString());
        Assert.Empty(s.W.CentralRows(BulkAssign));
        Assert.Equal(["Created"], s.EventKinds(TaskId(task)));

        // no key was stored: the same key with the right version is a new request and writes
        Assert.Equal("Assigned", Row(await Expect(s.BulkAssignAsync(alice, [(TaskId(task), 1)], key), 200), TaskId(task)).GetProperty("outcome").GetString());
    }

    [Fact]
    public async Task AC4_F3_the_same_key_on_bulk_assign_then_update_of_the_same_task_is_two_requests()
    {
        var s = await ReadyAsync();
        var task = await s.TaskAsync(10);
        var alice = s.ActiveUser();
        var key = NewKey();
        await Expect(s.BulkAssignAsync(alice, [(TaskId(task), 1)], key), 200);

        var updated = await Expect(s.UpdateTaskAsync(TaskId(task), new() { ["note"] = "raf 4" }, "W/\"2\"", key), 200);
        Assert.Equal(3, updated.GetProperty("version").GetInt32());
        Assert.Equal(["Created", "Assigned", "Updated"], s.EventKinds(TaskId(task)));
    }

    [Fact]
    public async Task Q13_bulk_assign_needs_the_update_key_and_checks_the_assignee()
    {
        var s = await ReadyAsync();
        var task = await s.TaskAsync(10);
        Assert.Equal(403, (int)(await s.BulkAssignAsync(s.ActiveUser(), [(TaskId(task), 1)], token: s.Operator)).StatusCode);
        Assert.Equal("WMS_ASSIGNEE_INVALID", await ErrorCode(s.BulkAssignAsync(Guid.NewGuid(), [(TaskId(task), 1)]), 422));
        Assert.Equal("INVALID_REQUEST", await ErrorCode(s.BulkAssignAsync(s.ActiveUser(), []), 400));
        Assert.Equal(["Created"], s.EventKinds(TaskId(task)));
    }
}

/// <summary>
/// The AUTH client of Q-13 A measured on its own (no host): the path of <c>lookup-validation</c>, 200 + <c>referenceable</c> → Valid, 404 →
/// Invalid, and every other answer (401, 403, 400, 5xx, no answer) → Unavailable — never Valid.
/// </summary>
public sealed class AuthAssigneeDirectoryTests
{
    private sealed class Stub(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
    {
        public List<string> Paths { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Paths.Add(request.RequestUri!.PathAndQuery);
            return Task.FromResult(answer(request));
        }
    }

    private static async Task<(AssigneeCheck Check, List<string> Paths)> CheckAsync(Guid user, Func<HttpRequestMessage, HttpResponseMessage> answer)
    {
        var stub = new Stub(answer);
        var directory = new AuthAssigneeDirectory(new HttpClient(stub) { BaseAddress = new Uri("http://gateway.test/") }, NullLogger<AuthAssigneeDirectory>.Instance);
        return (await directory.CheckAsync(user), stub.Paths);
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body) => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    [Fact]
    public async Task A_referenceable_user_is_valid_on_the_lookup_validation_path()
    {
        var user = Guid.NewGuid();
        var (check, paths) = await CheckAsync(user, _ => Json(HttpStatusCode.OK, $"{{\"userId\":\"{user:D}\",\"referenceable\":true}}"));
        Assert.Equal(AssigneeCheck.Valid, check);
        Assert.Equal([$"/api/users/{user:D}/lookup-validation"], paths);
        Assert.Equal(AssigneeCheck.Valid, (await CheckAsync(user, _ => Json(HttpStatusCode.OK, $"{{\"data\":{{\"userId\":\"{user:D}\",\"referenceable\":true}}}}"))).Check);
    }

    [Fact]
    public async Task Not_found_is_invalid_and_another_users_answer_is_not_valid()
    {
        var user = Guid.NewGuid();
        Assert.Equal(AssigneeCheck.Invalid, (await CheckAsync(user, _ => Json(HttpStatusCode.NotFound, "{}"))).Check);
        Assert.Equal(AssigneeCheck.Invalid, (await CheckAsync(user, _ => Json(HttpStatusCode.OK, $"{{\"userId\":\"{Guid.NewGuid():D}\",\"referenceable\":true}}"))).Check);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task Any_other_answer_is_unavailable(HttpStatusCode status)
        => Assert.Equal(AssigneeCheck.Unavailable, (await CheckAsync(Guid.NewGuid(), _ => Json(status, "{}"))).Check);

    [Fact]
    public async Task No_answer_is_unavailable()
        => Assert.Equal(AssigneeCheck.Unavailable, (await CheckAsync(Guid.NewGuid(), _ => throw new HttpRequestException("no route"))).Check);
}
