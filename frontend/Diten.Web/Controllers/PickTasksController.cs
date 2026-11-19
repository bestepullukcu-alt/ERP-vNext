using Diten.Web.Models.Inventory;
using Diten.Web.Models.Quarantine;
using Diten.Web.Models.Warehouse;
using Diten.Web.Services;
using Diten.Web.Views.Warehouse.PickTasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Diten.Web.Controllers;

// MOD-0178 DL-6 — Toplama Görevleri (PICKTASKS, pack §11.1 / §11.5; Slim WITHOUT a create offcanvas — the allocation opens the tasks): tenant shell
// MVC controller, route /Warehouse/PickTasks, views under Views/Warehouse/PickTasks/. Same door as the putaway screen (WarehouseGatewayClient →
// Gateway 5000 /api/warehouse/pick-tasks). The confirmation body is the quantity only (the target is the outbound's Staging, the source the task's
// bin — §8.19, 400 otherwise). No answer carries the ship-to (Q-10a): an operator works with the seven keys of WarehouseOperator only (AC-34).
// Every data action checks ITS OWN key first (DL-3 CT F-1 lesson); every write is [ValidateAntiForgeryToken]; the service's allowedActions /
// disabledActions stay the menu's authority (§8.15, AC-39 / AC-43).
[Authorize]
[Route("Warehouse/PickTasks")]
public sealed class PickTasksController : Controller
{
    private const string ViewRoot = "~/Views/Warehouse/PickTasks";
    private const string Base = "/api/warehouse/pick-tasks";

    public static readonly IReadOnlyList<string> Statuses = ["Open", "InProgress", "Completed", "Cancelled"];

    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly IPermissionSnapshot _permissions;
    private readonly IStringLocalizer<PickTasksIndex> _localizer;
    private readonly ILogger<PickTasksController> _logger;

    public PickTasksController(HttpClient httpClient, IConfiguration configuration, IPermissionSnapshot permissions,
        IStringLocalizer<PickTasksIndex> localizer, ILogger<PickTasksController> logger)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"] ?? throw new InvalidOperationException("GatewayUrl configuration is required.");
        _permissions = permissions;
        _localizer = localizer;
        _logger = logger;
    }

    // UAS-001: the view draws _AccessDenied (and nothing else) without warehouse.pick-tasks.read — no redirect.
    [HttpGet("")]
    public IActionResult Index() => View($"{ViewRoot}/Index.cshtml");

    // ── reads ─────────────────────────────────────────────────────────────────────────────────────────────

    [HttpGet("api")]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        if (!_permissions.Has(WarehousePermissionKeys.PickTasksRead)) return Refuse(403, null, null);
        if (WarehouseProxy.Paging(Request.Query) is not { } paging) return Refuse(400, "VALIDATION_ERROR", null);
        if (ListQuery(Request.Query) is not { } filters) return Refuse(400, "VALIDATION_ERROR", null);

        var gateway = WarehouseProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        if (gateway is null) return Refuse(401, null, null);
        var reply = await gateway.GetAsync($"{Base}?page={paging.Page}&pageSize={paging.PageSize}{filters}", ct);
        if (!reply.IsSuccess) return Failed("list", reply);

        var page = reply.Read<WmsPickTaskPageDto>() ?? new WmsPickTaskPageDto();
        var tasks = page.Items ?? [];
        var labels = await LabelsAsync(tasks, ct);
        var people = await PeopleAsync(gateway, tasks.Select(t => t.Assignee).ToList(), ct);
        var total = page.Total ?? tasks.Count;
        return Json(new
        {
            data = new { items = tasks.Select(t => Row(t, labels, people)), total, filteredTotal = total },
            draw = paging.Draw,
            namesAvailable = labels.Available,
            contractVersion = InventoryProxy.ContractVersion
        });
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export(CancellationToken ct)
    {
        if (!_permissions.Has(WarehousePermissionKeys.PickTasksRead)) return Refuse(403, null, null);
        if (ListQuery(Request.Query) is not { } filters) return Refuse(400, "VALIDATION_ERROR", null);
        var gateway = WarehouseProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        if (gateway is null) return Refuse(401, null, null);

        var tasks = new List<WmsPickTaskDto>();
        for (var page = 1; ; page++)
        {
            var reply = await gateway.GetAsync($"{Base}?page={page}&pageSize=200{filters}", ct);
            if (!reply.IsSuccess) return Failed("export", reply);
            var dto = reply.Read<WmsPickTaskPageDto>() ?? new WmsPickTaskPageDto();
            if ((dto.Total ?? 0) > WarehouseCsv.MaxRows) return Refuse(413, null, reply.CorrelationId);
            tasks.AddRange(dto.Items ?? []);
            if ((dto.Items?.Count ?? 0) < 200 || tasks.Count >= (dto.Total ?? 0)) break;
        }

        var labels = await LabelsAsync(tasks, ct);
        var people = await PeopleAsync(gateway, tasks.Select(t => t.Assignee).ToList(), ct);
        string W(string key) => _localizer[key].Value;
        var header = new[] { "ColNumber", "ColStatus", "ColFlag", "ColOutbound", "ColProduct", "ColLot", "ColFrom", "FieldPlanned", "FieldConfirmed", "ColAssignee" }.Select(W);
        var rows = tasks.Select(t => new[]
        {
            t.Number, W("Warehouse.Status." + t.Status), t.IntegrationFlag is null or "None" ? string.Empty : W("Warehouse.Flag." + t.IntegrationFlag),
            t.Outbound?.OutboundNumber, labels.Sku(t.Item?.SkuId) ?? labels.Product(t.Item?.ItemId) ?? t.Item?.SkuId, t.LotNumber,
            labels.Location(t.FromLocationId) ?? t.FromLocationId, t.PlannedQuantity, t.ConfirmedQuantity,
            t.Assignee is null ? null : t.Assignee.Name ?? people.GetValueOrDefault(t.Assignee.UserId ?? string.Empty) ?? t.Assignee.UserId
        });
        return File(WarehouseCsv.Write(header, rows), "text/csv; charset=utf-8", $"pick-tasks-{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    /// <summary>The list filters as the WMS query tail (<c>&amp;…</c>, newest first), or null when one is outside the contract.</summary>
    public static string? ListQuery(IQueryCollection source)
    {
        var query = new List<string> { "orderBy=-createdAt" };
        foreach (var status in source["status"].Where(s => !string.IsNullOrWhiteSpace(s)))
        {
            if (!Statuses.Contains(status!, StringComparer.Ordinal)) return null;
            query.Add("status=" + status);
        }

        foreach (var name in new[] { "warehouseId", "assigneeUserId", "outboundId" })
        {
            var value = WarehouseProxy.Single(source[name]);
            if (value is null) continue;
            if (!WarehouseProxy.TryId(value, out var id)) return null;
            query.Add($"{name}={id}");
        }

        var search = WarehouseProxy.Single(source["search"]);
        if (search is not null) query.Add("q=" + Uri.EscapeDataString(search.Length > 100 ? search[..100] : search));
        return "&" + string.Join('&', query);
    }

    // QuickView: the task (+ its ETag), its timeline (newest first) and the names of its ids.
    [HttpGet("task/{id}")]
    public async Task<IActionResult> Detail(string id, CancellationToken ct)
    {
        if (!_permissions.Has(WarehousePermissionKeys.PickTasksRead)) return Refuse(403, null, null);
        if (!WarehouseProxy.TryId(id, out var taskId)) return Refuse(404, "WMS_TASK_NOT_FOUND", null);
        var gateway = WarehouseProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        if (gateway is null) return Refuse(401, null, null);

        var reply = await gateway.GetAsync($"{Base}/{taskId}", ct);
        if (!reply.IsSuccess) return Failed("get", reply);
        var task = reply.Read<WmsPickTaskDto>();
        if (task is null) return Refuse(503, null, reply.CorrelationId);

        var events = await gateway.GetAsync($"{Base}/{taskId}/events?page=1&pageSize=100", ct);
        var timeline = events.IsSuccess ? events.Read<WmsTaskEventPageDto>()?.Items ?? [] : null;
        var labels = await LabelsAsync([task], ct, (task.Confirmations ?? []).Select(c => c.ToLocationId));
        var people = await PeopleAsync(gateway, [task.Assignee, .. (task.Confirmations ?? []).Select(c => c.ConfirmedBy), .. (timeline ?? []).Select(e => e.Actor)], ct);

        return Json(new
        {
            task = Row(task, labels, people),
            etag = reply.ETag ?? $"W/\"{task.Version}\"",
            confirmations = (task.Confirmations ?? []).Select(c => new
            {
                c.Seq, c.Quantity, c.Status, c.PostingDate, c.Attempts, c.TransactionNumber, c.Resolution, c.IntegrityResolution, c.LastAttemptAtUtc,
                c.InFlightUntilUtc, c.ConfirmedAtUtc,
                toLocation = labels.Location(c.ToLocationId) ?? c.ToLocationId,
                confirmedBy = Person(c.ConfirmedBy, people),
                errorCode = c.Error?.Code
            }),
            timeline = timeline?.Select(e => new { e.Seq, e.Kind, e.AtUtc, e.System, actor = Person(e.Actor, people) }),
            timelineAvailable = timeline is not null,
            contractVersion = InventoryProxy.ContractVersion
        });
    }

    [HttpGet("warehouses")]
    public async Task<IActionResult> Warehouses(CancellationToken ct)
    {
        if (!_permissions.Has(WarehousePermissionKeys.PickTasksRead)) return Refuse(403, null, null);
        var inventory = InventoryProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        if (inventory is null) return Refuse(401, null, null);
        var result = await new LocationPickerSource(inventory).ActiveWarehousesAsync(ct);
        return Json(new { items = result.Items.Select(o => new { value = o.Value, text = o.Text }), available = result.Available });
    }

    // Assignee picker + filter (Q-13 A): AuthService's existing user lookup — active users of the caller's tenant, label only.
    [HttpGet("people")]
    public async Task<IActionResult> People([FromQuery] string? q, CancellationToken ct)
    {
        if (!_permissions.Has(WarehousePermissionKeys.PickTasksRead)) return Refuse(403, null, null);
        var gateway = WarehouseProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        if (gateway is null) return Refuse(401, null, null);
        var reply = await gateway.GetAsync(QuarantinePeople.SearchPath(q), ct);
        return reply.IsSuccess
            ? Content(new System.Text.Json.Nodes.JsonObject { ["items"] = QuarantinePeople.Options(reply.Body), ["available"] = true }.ToJsonString(), "application/json")
            : Json(new { items = Array.Empty<object>(), available = false });
    }

    // ── writes (pack §11.3) ───────────────────────────────────────────────────────────────────────────────

    // Confirm (confirmPickTask, If-Match): the quantity only — no target, no source (§8.19); channel Ui.
    [HttpPost("confirm/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Confirm(string id, [FromBody] PickWriteRequest? model, CancellationToken ct)
    {
        if (!_permissions.Has(WarehousePermissionKeys.PickTasksConfirm)) return Refuse(403, null, null);
        var quantity = WarehouseProxy.Quantity(model?.Quantity);
        if (!WarehouseProxy.TryId(id, out var taskId) || model is null || !WarehouseProxy.IsKey(model.IdempotencyKey) || !WarehouseProxy.IsETag(model.IfMatch) || quantity is null)
            return Refuse(400, "VALIDATION_ERROR", null);
        var key = Guid.Parse(model.IdempotencyKey!).ToString("D");
        var body = new Dictionary<string, object?> { ["idempotencyKey"] = key, ["quantity"] = quantity, ["channel"] = "Ui" };
        return await WriteAsync("confirm", $"{Base}/{taskId}/confirmations", body, key, model.IfMatch, "Warehouse.Saved.Confirmed", ct);
    }

    [HttpPost("retry/{id}/{seq:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Retry(string id, int seq, [FromBody] PickWriteRequest? model, CancellationToken ct)
    {
        if (!_permissions.Has(WarehousePermissionKeys.PickTasksConfirm)) return Refuse(403, null, null);
        if (!WarehouseProxy.TryId(id, out var taskId) || model is null || !WarehouseProxy.IsKey(model.IdempotencyKey)) return Refuse(400, "VALIDATION_ERROR", null);
        var key = Guid.Parse(model.IdempotencyKey!).ToString("D");
        return await WriteAsync("retry", $"{Base}/{taskId}/confirmations/{seq}/retry", new Dictionary<string, object?> { ["idempotencyKey"] = key }, key, null, "Warehouse.Saved.Retried", ct);
    }

    [HttpPost("claim/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Claim(string id, [FromBody] PickWriteRequest? model, CancellationToken ct)
    {
        if (!_permissions.Has(WarehousePermissionKeys.PickTasksConfirm)) return Refuse(403, null, null);
        if (!WarehouseProxy.TryId(id, out var taskId) || model is null || !WarehouseProxy.IsKey(model.IdempotencyKey) || !WarehouseProxy.IsETag(model.IfMatch))
            return Refuse(400, "VALIDATION_ERROR", null);
        var key = Guid.Parse(model.IdempotencyKey!).ToString("D");
        return await WriteAsync("claim", $"{Base}/{taskId}/claim", new Dictionary<string, object?> { ["idempotencyKey"] = key }, key, model.IfMatch, "Warehouse.Saved.Claimed", ct);
    }

    [HttpPost("returned/{id}/{seq:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Returned(string id, int seq, [FromBody] PickWriteRequest? model, CancellationToken ct)
    {
        if (!_permissions.Has(WarehousePermissionKeys.PickTasksConfirm)) return Refuse(403, null, null);
        return await ReasonedAsync("returned", id, model, t => $"{Base}/{t}/confirmations/{seq}/returned", "Warehouse.Saved.Returned", ct);
    }

    [HttpPost("resolve/{id}/{seq:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Resolve(string id, int seq, [FromBody] PickWriteRequest? model, CancellationToken ct)
    {
        if (!_permissions.Has(WarehousePermissionKeys.PickTasksResolve)) return Refuse(403, null, null);
        return await ReasonedAsync("resolve", id, model, t => $"{Base}/{t}/confirmations/{seq}/resolve-integrity", "Warehouse.Saved.Resolved", ct);
    }

    [HttpPost("close-short/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CloseShort(string id, [FromBody] PickWriteRequest? model, CancellationToken ct)
    {
        if (!_permissions.Has(WarehousePermissionKeys.PickTasksConfirm)) return Refuse(403, null, null);
        return await ReasonedAsync("close-short", id, model, t => $"{Base}/{t}/close-short", "Warehouse.Saved.ClosedShort", ct);
    }

    // Bulk "Ata" (Q-13 A): one request, per-row outcomes; the screen reports each row.
    [HttpPost("bulk")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkAssign([FromBody] PickWriteRequest? model, CancellationToken ct)
    {
        if (!_permissions.Has(WarehousePermissionKeys.PickTasksUpdate)) return Refuse(403, null, null);
        if (model is null || !WarehouseProxy.IsKey(model.IdempotencyKey) || !WarehouseProxy.TryId(model.AssigneeUserId, out var assignee)
            || model.Items is not { Count: > 0 and <= 200 } || model.Items.Any(i => !WarehouseProxy.TryId(i.Id, out _) || i.Version < 1))
            return Refuse(400, "VALIDATION_ERROR", null);

        var gateway = WarehouseProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        if (gateway is null) return Refuse(401, null, null);
        var key = Guid.Parse(model.IdempotencyKey!).ToString("D");
        var body = new { idempotencyKey = key, assigneeUserId = assignee, items = model.Items.Select(i => new { taskId = Guid.Parse(i.Id!).ToString("D"), version = i.Version }) };
        var reply = await gateway.SendAsync(HttpMethod.Post, $"{Base}/bulk-assign", body, key, null, ct);
        if (!reply.IsSuccess) return Failed("bulk-assign", reply);
        var rows = reply.Read<WmsBulkAssignResultDto>()?.Results ?? [];
        return Json(new { success = true, results = rows.Select(r => new { id = r.TaskId, outcome = r.Outcome }), assigned = rows.Count(r => r.Outcome == "Assigned") });
    }

    // ── helpers ──────────────────────────────────────────────────────────────────────────────────────────

    private async Task<IActionResult> ReasonedAsync(string operation, string id, PickWriteRequest? model, Func<string, string> path, string savedKey, CancellationToken ct)
    {
        if (!WarehouseProxy.TryId(id, out var taskId) || model is null || !WarehouseProxy.IsKey(model.IdempotencyKey) || !WarehouseProxy.IsETag(model.IfMatch))
            return Refuse(400, "VALIDATION_ERROR", null);
        var key = Guid.Parse(model.IdempotencyKey!).ToString("D");
        var body = new Dictionary<string, object?> { ["idempotencyKey"] = key };
        if (!string.IsNullOrWhiteSpace(model.Reason)) body["reason"] = model.Reason.Trim(); // the service owns REASON_REQUIRED (10-1000)
        return await WriteAsync(operation, path(taskId), body, key, model.IfMatch, savedKey, ct);
    }

    private async Task<IActionResult> WriteAsync(string operation, string path, object body, string key, string? ifMatch, string savedKey, CancellationToken ct)
    {
        var gateway = WarehouseProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        if (gateway is null) return Refuse(401, null, null);
        var reply = await gateway.SendAsync(HttpMethod.Post, path, body, key, ifMatch, ct);
        if (!reply.IsSuccess) return Failed(operation, reply);
        var task = reply.Read<WmsPickTaskDto>();
        return Json(new
        {
            success = true, id = task?.TaskId, number = task?.Number, outbound = Outbound(task?.Outbound),
            etag = reply.ETag ?? (task is null ? null : $"W/\"{task.Version}\""), message = _localizer[savedKey].Value
        });
    }

    private async Task<MasterLabels> LabelsAsync(IReadOnlyList<WmsPickTaskDto> tasks, CancellationToken ct, IEnumerable<string?>? extraLocations = null)
    {
        var inventory = InventoryProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        if (inventory is null) return MasterLabels.None;
        return await MasterLabelResolver.ResolveAsync(inventory,
            tasks.Select(t => t.Item?.ItemId),
            tasks.Select(t => (t.Item?.SkuId, t.Item?.SkuLevel)),
            tasks.SelectMany(t => new[] { t.FromLocationId, t.ToLocationId, t.WarehouseId }).Concat(extraLocations ?? []),
            ct);
    }

    private static async Task<IReadOnlyDictionary<string, string>> PeopleAsync(WarehouseGatewayClient gateway, IReadOnlyList<WmsPersonDto?> people, CancellationToken ct)
    {
        var unnamed = people.Where(p => p?.UserId is not null && string.IsNullOrWhiteSpace(p.Name)).Select(p => p!.UserId!).Distinct().ToList();
        if (unnamed.Count == 0) return new Dictionary<string, string>();
        return await QuarantinePeople.ResolveAsync(unnamed, async (path, token) =>
        {
            var reply = await gateway.GetAsync(path, token);
            return (reply.StatusCode, reply.Body);
        }, ct);
    }

    private static object? Person(WmsPersonDto? person, IReadOnlyDictionary<string, string> people) => person?.UserId is null ? null : new
    {
        userId = person.UserId,
        name = !string.IsNullOrWhiteSpace(person.Name) ? person.Name : people.GetValueOrDefault(person.UserId)
    };

    private static object? Outbound(WmsPickOutboundDto? o) => o is null ? null : new { id = o.OutboundId, number = o.OutboundNumber, status = o.Status };

    /// <summary>
    /// One list / QuickView row: the task's own fields, the names of its ids, its outbound (number + status — the "Paketle" link appears when it is
    /// <c>Picked</c>, §11.1 r21) and the service's menu rules. No ship-to (Q-10a).
    /// </summary>
    public static object Row(WmsPickTaskDto t, MasterLabels labels, IReadOnlyDictionary<string, string> people) => new
    {
        id = t.TaskId,
        t.Number,
        t.Status,
        t.Origin,
        integrationFlag = t.IntegrationFlag ?? "None",
        warehouse = t.WarehouseCode ?? labels.Location(t.WarehouseId) ?? t.WarehouseId,
        t.WarehouseId,
        outbound = Outbound(t.Outbound),
        sku = labels.Sku(t.Item?.SkuId) ?? labels.Product(t.Item?.ItemId) ?? t.Item?.SkuId,
        t.LotNumber,
        fromLocation = labels.Location(t.FromLocationId) ?? t.FromLocationId,
        toLocation = t.ToLocationId is null ? null : labels.Location(t.ToLocationId) ?? t.ToLocationId,
        planned = t.PlannedQuantity,
        confirmed = t.ConfirmedQuantity,
        pending = t.PendingQuantity,
        remaining = t.RemainingQuantity,
        uom = t.BaseUomId,
        assignee = Person(t.Assignee, people),
        t.Priority,
        t.SerialControlled,
        t.ShortCloseReason,
        pendingSeq = (t.Confirmations ?? []).Where(c => c.Status == "Pending").Select(c => (int?)c.Seq).LastOrDefault(),
        refusedSeq = (t.Confirmations ?? []).Where(c => c.Status == "Refused" && c.Resolution is null).Select(c => (int?)c.Seq).LastOrDefault(),
        integritySeq = (t.Confirmations ?? []).Where(c => c.Status == "IntegrityError").Select(c => (int?)c.Seq).LastOrDefault(),
        allowedActions = t.AllowedActions ?? [],
        disabledActions = (t.DisabledActions ?? []).Select(d => new { action = d.Action, reasonCode = d.ReasonCode }),
        version = t.Version,
        etag = $"W/\"{t.Version}\""
    };

    private IActionResult Failed(string operation, GatewayReply reply)
    {
        _logger.LogWarning("Pick {Operation} refused with {Status} {Code} (correlation {CorrelationId}).",
            operation, reply.StatusCode, reply.Error?.Code, reply.Error?.CorrelationId ?? reply.CorrelationId);
        return Refuse(reply.StatusCode, reply.Error?.Code, reply.Error?.CorrelationId ?? reply.CorrelationId);
    }

    private IActionResult Refuse(int status, string? code, string? correlationId) =>
        WarehouseProxy.Error(status, code, _localizer[WarehouseErrorMessages.KeyForS2(code, status)].Value, correlationId);
}
