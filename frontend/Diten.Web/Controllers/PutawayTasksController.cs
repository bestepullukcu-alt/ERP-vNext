using System.Text.Json;
using Diten.Web.Models.Inventory;
using Diten.Web.Models.Quarantine;
using Diten.Web.Models.Warehouse;
using Diten.Web.Services;
using Diten.Web.Views.Warehouse.PutawayTasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Diten.Web.Controllers;

// MOD-0178 DL-3 — Yerleştirme Görevleri (PUTAWAYTASKS, pack §11.1): tenant shell MVC controller, route /Warehouse/PutawayTasks, views under
// Views/Warehouse/PutawayTasks/. The browser talks only to this same-origin controller; it calls Gateway 5000 /api/warehouse (INT-WMS, DL-8 —
// integration-agent) with WarehouseGatewayClient: cookie bearer, X-Tenant-Id, the W-0173-02 company header. Master names (SKU, location) come
// from /api/product-master and /api/locations, people from AuthService's label read — read paths that never fail the page. Every write is
// [ValidateAntiForgeryToken] and offered only to a holder of its key (pack §11.3); InventoryService's [HasPermission] stays the authority
// (UAS-001 §4) and so do the state rules: the screen draws the service's allowedActions / disabledActions (§8.15, AC-39 / AC-43).
[Authorize]
[Route("Warehouse/PutawayTasks")]
public sealed class PutawayTasksController : Controller
{
    private const string ViewRoot = "~/Views/Warehouse/PutawayTasks";
    private const string Base = "/api/warehouse/putaway-tasks";

    public static readonly IReadOnlyList<string> Statuses = ["Open", "InProgress", "Completed", "Cancelled"];
    public static readonly IReadOnlyList<string> Priorities = ["Low", "Normal", "High"];

    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly IPermissionSnapshot _permissions;
    private readonly IStringLocalizer<PutawayTasksIndex> _localizer;
    private readonly ILogger<PutawayTasksController> _logger;

    public PutawayTasksController(HttpClient httpClient, IConfiguration configuration, IPermissionSnapshot permissions,
        IStringLocalizer<PutawayTasksIndex> localizer, ILogger<PutawayTasksController> logger)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"] ?? throw new InvalidOperationException("GatewayUrl configuration is required.");
        _permissions = permissions;
        _localizer = localizer;
        _logger = logger;
    }

    // UAS-001: the view draws _AccessDenied (and nothing else) without warehouse.putaway-tasks.read — no redirect.
    [HttpGet("")]
    public IActionResult Index() => View($"{ViewRoot}/Index.cshtml");

    // ── reads ─────────────────────────────────────────────────────────────────────────────────────────────

    // List adapter: repo server-mode wire → listPutawayTasks (status*, warehouseId, assigneeUserId, priority, q; newest first).
    [HttpGet("api")]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        if (!_permissions.Has(WarehousePermissionKeys.PutawayTasksRead)) return Refuse(403, null, null);
        if (WarehouseProxy.Paging(Request.Query) is not { } paging) return Refuse(400, "VALIDATION_ERROR", null);
        if (ListQuery(Request.Query) is not { } filters) return Refuse(400, "VALIDATION_ERROR", null);

        var gateway = WarehouseProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        if (gateway is null) return Refuse(401, null, null);
        var reply = await gateway.GetAsync($"{Base}?page={paging.Page}&pageSize={paging.PageSize}{filters}", ct);
        if (!reply.IsSuccess) return Failed("list", reply);

        var page = reply.Read<WmsTaskPageDto>() ?? new WmsTaskPageDto();
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

    // Server export (BL-452): every matching row as CSV — the same filters and search as the list, at most MaxExportRows (413 beyond).
    [HttpGet("export")]
    public async Task<IActionResult> Export(CancellationToken ct)
    {
        if (!_permissions.Has(WarehousePermissionKeys.PutawayTasksRead)) return Refuse(403, null, null);
        if (ListQuery(Request.Query) is not { } filters) return Refuse(400, "VALIDATION_ERROR", null);
        var gateway = WarehouseProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        if (gateway is null) return Refuse(401, null, null);

        var tasks = new List<WmsTaskDto>();
        for (var page = 1; ; page++)
        {
            var reply = await gateway.GetAsync($"{Base}?page={page}&pageSize=200{filters}", ct);
            if (!reply.IsSuccess) return Failed("export", reply);
            var dto = reply.Read<WmsTaskPageDto>() ?? new WmsTaskPageDto();
            if ((dto.Total ?? 0) > WarehouseCsv.MaxRows) return Refuse(413, null, reply.CorrelationId);
            tasks.AddRange(dto.Items ?? []);
            if ((dto.Items?.Count ?? 0) < 200 || tasks.Count >= (dto.Total ?? 0)) break;
        }

        var labels = await LabelsAsync(tasks, ct);
        var people = await PeopleAsync(gateway, tasks.Select(t => t.Assignee).ToList(), ct);
        string W(string key) => _localizer[key].Value;
        var header = new[] { "ColNumber", "ColStatus", "ColFlag", "ColProduct", "ColLot", "ColFrom", "ColTo", "FieldPlanned", "FieldConfirmed", "FieldRemaining", "ColAssignee", "ColPriority" }.Select(W);
        var rows = tasks.Select(t => new[]
        {
            t.Number, W("Warehouse.Status." + t.Status), t.IntegrationFlag is null or "None" ? string.Empty : W("Warehouse.Flag." + t.IntegrationFlag),
            labels.Sku(t.Item?.SkuId) ?? labels.Product(t.Item?.ItemId) ?? t.Item?.SkuId, t.LotNumber, labels.Location(t.FromLocationId) ?? t.FromLocationId,
            t.ToLocationId is null ? null : labels.Location(t.ToLocationId) ?? t.ToLocationId, t.PlannedQuantity, t.ConfirmedQuantity, t.RemainingQuantity,
            t.Assignee is null ? null : t.Assignee.Name ?? people.GetValueOrDefault(t.Assignee.UserId ?? string.Empty) ?? t.Assignee.UserId,
            t.Priority is null ? null : W("Warehouse.Priority." + t.Priority)
        });
        return File(WarehouseCsv.Write(header, rows), "text/csv; charset=utf-8", $"putaway-tasks-{DateTime.UtcNow:yyyyMMdd}.csv");
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

        var priority = WarehouseProxy.Single(source["priority"]);
        if (priority is not null)
        {
            if (!Priorities.Contains(priority, StringComparer.Ordinal)) return null;
            query.Add("priority=" + priority);
        }

        foreach (var name in new[] { "warehouseId", "assigneeUserId" })
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
        if (!_permissions.Has(WarehousePermissionKeys.PutawayTasksRead)) return Refuse(403, null, null);
        if (!WarehouseProxy.TryId(id, out var taskId)) return Refuse(404, "WMS_TASK_NOT_FOUND", null);
        var gateway = WarehouseProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        if (gateway is null) return Refuse(401, null, null);

        var reply = await gateway.GetAsync($"{Base}/{taskId}", ct);
        if (!reply.IsSuccess) return Failed("get", reply);
        var task = reply.Read<WmsTaskDto>();
        if (task is null) return Refuse(503, null, reply.CorrelationId);

        var events = await gateway.GetAsync($"{Base}/{taskId}/events?page=1&pageSize=100", ct);
        var timeline = events.IsSuccess ? events.Read<WmsTaskEventPageDto>()?.Items ?? [] : null;
        var labels = await LabelsAsync([task], ct, (task.Confirmations ?? []).Select(c => c.ToLocationId).Append(task.SuggestedToLocationId));
        var people = await PeopleAsync(gateway, [task.Assignee, .. (task.Confirmations ?? []).Select(c => c.ConfirmedBy), .. (timeline ?? []).Select(e => e.Actor)], ct);

        return Json(new
        {
            task = Row(task, labels, people),
            etag = reply.ETag ?? $"W/\"{task.Version}\"",
            confirmations = (task.Confirmations ?? []).Select(c => new
            {
                c.Seq, c.Quantity, c.Status, c.PostingDate, c.Attempts, c.DeviationReason, c.TransactionNumber, c.Resolution, c.IntegrityResolution,
                c.LastAttemptAtUtc, c.InFlightUntilUtc, c.ConfirmedAtUtc,
                toLocation = labels.Location(c.ToLocationId) ?? c.ToLocationId,
                confirmedBy = Person(c.ConfirmedBy, people),
                errorCode = c.Error?.Code
            }),
            timeline = timeline?.Select(e => new { e.Seq, e.Kind, e.AtUtc, e.System, actor = Person(e.Actor, people) }),
            timelineAvailable = timeline is not null,
            contractVersion = InventoryProxy.ContractVersion
        });
    }

    // "Yerleştirilecek stok" picker (§11.2): eligible AND locked rows — a locked row is never hidden, it carries its reason and the blocking
    // document number (§8.15, AC-41).
    [HttpGet("candidates")]
    public async Task<IActionResult> Candidates([FromQuery] string? q, [FromQuery] string? warehouseId, CancellationToken ct)
    {
        if (!_permissions.Has(WarehousePermissionKeys.PutawayTasksCreate)) return Refuse(403, null, null);
        var query = "?page=1&pageSize=200";
        if (warehouseId is not null)
        {
            if (!WarehouseProxy.TryId(warehouseId, out var wid)) return Refuse(400, "VALIDATION_ERROR", null);
            query += "&warehouseId=" + wid;
        }

        if (!string.IsNullOrWhiteSpace(q)) query += "&q=" + Uri.EscapeDataString(q.Trim().Length > 100 ? q.Trim()[..100] : q.Trim());
        var gateway = WarehouseProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        if (gateway is null) return Refuse(401, null, null);
        var reply = await gateway.GetAsync("/api/warehouse/putaway-candidates" + query, ct);
        if (!reply.IsSuccess) return Failed("candidates", reply);

        var items = reply.Read<WmsCandidatePageDto>()?.Items ?? [];
        var inventory = InventoryProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        var labels = inventory is null ? MasterLabels.None
            : await MasterLabelResolver.ResolveAsync(inventory, items.Select(i => i.Item?.ItemId), items.Select(i => (i.Item?.SkuId, i.Item?.SkuLevel)), [], ct);
        return Json(new
        {
            items = items.Select(c => new
            {
                key = c.CandidateKey,
                c.WarehouseId,
                c.LocationCode,
                c.LotNumber,
                c.StockStatus,
                openQuantity = c.OpenQuantity,
                uom = c.BaseUomId,
                sku = labels.Sku(c.Item?.SkuId) ?? labels.Product(c.Item?.ItemId) ?? c.Item?.SkuId,
                eligible = c.Eligible,
                reason = c.NotEligibleReason,
                blockingKind = c.BlockingDocument?.Kind,
                blockingNumber = c.BlockingDocument?.Number
            }),
            available = true
        });
    }

    // Warehouse filter: the tenant company's active warehouses (LOCATION master; never a hardcoded list).
    [HttpGet("warehouses")]
    public async Task<IActionResult> Warehouses(CancellationToken ct)
    {
        if (!_permissions.Has(WarehousePermissionKeys.PutawayTasksRead)) return Refuse(403, null, null);
        var inventory = InventoryProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        if (inventory is null) return Refuse(401, null, null);
        var result = await new LocationPickerSource(inventory).ActiveWarehousesAsync(ct);
        return Json(new { items = result.Items.Select(o => new { value = o.Value, text = o.Text }), available = result.Available });
    }

    // Target bin (§8.6, §8.15): the active stockable nodes under the task's warehouse; ?code= is the keyboard / barcode path — an EXACT code
    // match selects the bin, anything else is a field error and nothing is sent (AC-44).
    [HttpGet("bins")]
    public async Task<IActionResult> Bins([FromQuery] string? warehouseId, [FromQuery] string? q, [FromQuery] string? code, CancellationToken ct)
    {
        if (!_permissions.Has(WarehousePermissionKeys.PutawayTasksRead)) return Refuse(403, null, null);
        if (!WarehouseProxy.TryId(warehouseId, out var wid)) return Refuse(400, "VALIDATION_ERROR", null);
        var inventory = InventoryProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        if (inventory is null) return Refuse(401, null, null);

        var result = await new LocationPickerSource(inventory).StockableLocationsUnderAsync(wid, code ?? q, ct);
        if (code is null)
            return Json(new { items = result.Items.Take(50).Select(o => new { value = o.Value, text = o.Text }), available = result.Available });

        var match = BinMatch(result.Items, code);
        return Json(new { match = match is null ? null : new { value = match.Value, text = match.Text }, available = result.Available });
    }

    /// <summary>The bin whose code is exactly <paramref name="code"/> (case-insensitive, trimmed) — never a "contains" hit (AC-44).</summary>
    public static PickerOption? BinMatch(IEnumerable<PickerOption> options, string? code)
    {
        var wanted = code?.Trim();
        if (string.IsNullOrEmpty(wanted)) return null;
        var hits = options.Where(o => string.Equals(CodeOf(o.Text), wanted, StringComparison.OrdinalIgnoreCase)).ToList();
        return hits.Count == 1 ? hits[0] : null;
    }

    private static string CodeOf(string label)
    {
        var separator = label.IndexOf(" · ", StringComparison.Ordinal);
        return separator < 0 ? label : label[..separator];
    }

    // Assignee picker + filter (Q-13 A): AuthService's existing user lookup — active users of the caller's tenant, label only.
    [HttpGet("people")]
    public async Task<IActionResult> People([FromQuery] string? q, CancellationToken ct)
    {
        if (!_permissions.Has(WarehousePermissionKeys.PutawayTasksRead)) return Refuse(403, null, null); // the "Atanan" filter reads it too
        var gateway = WarehouseProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        if (gateway is null) return Refuse(401, null, null);
        var reply = await gateway.GetAsync(QuarantinePeople.SearchPath(q), ct);
        return reply.IsSuccess
            ? Content(new System.Text.Json.Nodes.JsonObject { ["items"] = QuarantinePeople.Options(reply.Body), ["available"] = true }.ToJsonString(), "application/json")
            : Json(new { items = Array.Empty<object>(), available = false });
    }

    // ── writes (pack §11.3) ───────────────────────────────────────────────────────────────────────────────

    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([FromBody] PutawayWriteRequest? model, CancellationToken ct)
    {
        if (!_permissions.Has(WarehousePermissionKeys.PutawayTasksCreate)) return Refuse(403, null, null);
        var planned = WarehouseProxy.Quantity(model?.PlannedQuantity);
        if (model is null || !WarehouseProxy.IsKey(model.IdempotencyKey) || string.IsNullOrWhiteSpace(model.CandidateKey) || planned is null
            || !OptionalId(model.ToLocationId) || !OptionalId(model.AssigneeUserId) || !Target(model.TargetSource)
            || (model.Priority is not null && !Priorities.Contains(model.Priority, StringComparer.Ordinal))
            || (model.TargetSource == "SuggestionApplied" && string.IsNullOrWhiteSpace(model.ToLocationId)))
            return Refuse(400, "VALIDATION_ERROR", null);

        var key = Guid.Parse(model.IdempotencyKey!).ToString("D");
        var body = new Dictionary<string, object?> { ["idempotencyKey"] = key, ["candidateKey"] = model.CandidateKey.Trim(), ["plannedQuantity"] = planned };
        if (!string.IsNullOrWhiteSpace(model.ToLocationId)) body["toLocationId"] = Guid.Parse(model.ToLocationId).ToString("D");
        if (model.TargetSource is not null) body["targetSource"] = model.TargetSource;
        if (!string.IsNullOrWhiteSpace(model.AssigneeUserId)) body["assigneeUserId"] = Guid.Parse(model.AssigneeUserId).ToString("D");
        if (model.Priority is not null) body["priority"] = model.Priority;
        if (!string.IsNullOrWhiteSpace(model.Note)) body["note"] = model.Note.Trim();
        return await WriteAsync("create", HttpMethod.Post, Base, body, key, null, "Warehouse.Saved.Created", ct);
    }

    // Edit (updatePutawayTask, If-Match): only the members the reader changed; the source bucket is never a member (§8.15, 400).
    [HttpPost("edit/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, [FromBody] PutawayWriteRequest? model, CancellationToken ct)
    {
        if (!_permissions.Has(WarehousePermissionKeys.PutawayTasksUpdate)) return Refuse(403, null, null);
        var changes = model is null ? null : EditBody(model.Changes);
        if (!WarehouseProxy.TryId(id, out var taskId) || model is null || !WarehouseProxy.IsKey(model.IdempotencyKey) || !WarehouseProxy.IsETag(model.IfMatch)
            || changes is null || changes.Count == 0)
            return Refuse(400, "VALIDATION_ERROR", null);

        var key = Guid.Parse(model.IdempotencyKey!).ToString("D");
        changes["idempotencyKey"] = key;
        return await WriteAsync("update", HttpMethod.Put, $"{Base}/{taskId}", changes, key, model.IfMatch, "Warehouse.Saved.Updated", ct);
    }

    // Confirm (confirmPutawayTask, If-Match): quantity, actual target bin, deviation reason; channel Ui (§8.3).
    [HttpPost("confirm/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Confirm(string id, [FromBody] PutawayWriteRequest? model, CancellationToken ct)
    {
        if (!_permissions.Has(WarehousePermissionKeys.PutawayTasksConfirm)) return Refuse(403, null, null);
        var quantity = WarehouseProxy.Quantity(model?.Quantity);
        if (!WarehouseProxy.TryId(id, out var taskId) || model is null || !WarehouseProxy.IsKey(model.IdempotencyKey) || !WarehouseProxy.IsETag(model.IfMatch)
            || quantity is null || !WarehouseProxy.TryId(model.ToLocationId, out var to))
            return Refuse(400, "VALIDATION_ERROR", null);

        var key = Guid.Parse(model.IdempotencyKey!).ToString("D");
        var body = new Dictionary<string, object?> { ["idempotencyKey"] = key, ["quantity"] = quantity, ["toLocationId"] = to, ["channel"] = "Ui" };
        if (!string.IsNullOrWhiteSpace(model.DeviationReason)) body["deviationReason"] = model.DeviationReason.Trim();
        return await WriteAsync("confirm", HttpMethod.Post, $"{Base}/{taskId}/confirmations", body, key, model.IfMatch, "Warehouse.Saved.Confirmed", ct);
    }

    [HttpPost("retry/{id}/{seq:int}")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Retry(string id, int seq, [FromBody] PutawayWriteRequest? model, CancellationToken ct)
        => KeyOnlyAsync("retry", WarehousePermissionKeys.PutawayTasksConfirm, id, model, false, t => $"{Base}/{t}/confirmations/{seq}/retry", "Warehouse.Saved.Retried", ct);

    [HttpPost("claim/{id}")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Claim(string id, [FromBody] PutawayWriteRequest? model, CancellationToken ct)
        => KeyOnlyAsync("claim", WarehousePermissionKeys.PutawayTasksConfirm, id, model, true, t => $"{Base}/{t}/claim", "Warehouse.Saved.Claimed", ct);

    [HttpPost("returned/{id}/{seq:int}")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Returned(string id, int seq, [FromBody] PutawayWriteRequest? model, CancellationToken ct)
        => ReasonedAsync("returned", WarehousePermissionKeys.PutawayTasksConfirm, id, model, t => $"{Base}/{t}/confirmations/{seq}/returned", "Warehouse.Saved.Returned", ct);

    [HttpPost("resolve/{id}/{seq:int}")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Resolve(string id, int seq, [FromBody] PutawayWriteRequest? model, CancellationToken ct)
        => ReasonedAsync("resolve", WarehousePermissionKeys.PutawayTasksResolve, id, model, t => $"{Base}/{t}/confirmations/{seq}/resolve-integrity", "Warehouse.Saved.Resolved", ct);

    [HttpPost("close-short/{id}")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> CloseShort(string id, [FromBody] PutawayWriteRequest? model, CancellationToken ct)
        => ReasonedAsync("close-short", WarehousePermissionKeys.PutawayTasksConfirm, id, model, t => $"{Base}/{t}/close-short", "Warehouse.Saved.ClosedShort", ct);

    [HttpPost("cancel/{id}")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Cancel(string id, [FromBody] PutawayWriteRequest? model, CancellationToken ct)
        => ReasonedAsync("cancel", WarehousePermissionKeys.PutawayTasksCancel, id, model, t => $"{Base}/{t}/cancel", "Warehouse.Saved.Cancelled", ct);

    // Bulk "Ata" (Q-13 A): one request, per-row outcomes; the screen reports each row.
    [HttpPost("bulk")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkAssign([FromBody] PutawayWriteRequest? model, CancellationToken ct)
    {
        if (!_permissions.Has(WarehousePermissionKeys.PutawayTasksUpdate)) return Refuse(403, null, null);
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

    /// <summary>The PUT body: only <c>UpdatePutawayTaskRequest</c> members, each with its JSON kind checked; anything else (the source) refused.</summary>
    public static Dictionary<string, object?>? EditBody(JsonElement changes)
    {
        if (changes.ValueKind != JsonValueKind.Object) return null;
        var body = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var member in changes.EnumerateObject())
        {
            var value = member.Value;
            switch (member.Name)
            {
                case "plannedQuantity" when value.ValueKind == JsonValueKind.String && WarehouseProxy.Quantity(value.GetString()) is { } q:
                    body[member.Name] = q;
                    break;
                case "toLocationId" or "assigneeUserId" when value.ValueKind == JsonValueKind.Null:
                    body[member.Name] = null;
                    break;
                case "toLocationId" or "assigneeUserId" when value.ValueKind == JsonValueKind.String && WarehouseProxy.TryId(value.GetString(), out var id):
                    body[member.Name] = id;
                    break;
                case "targetSource" when value.ValueKind == JsonValueKind.String && Target(value.GetString()):
                    body[member.Name] = value.GetString();
                    break;
                case "priority" when value.ValueKind == JsonValueKind.String && Priorities.Contains(value.GetString() ?? string.Empty, StringComparer.Ordinal):
                    body[member.Name] = value.GetString();
                    break;
                case "note" when value.ValueKind == JsonValueKind.Null || value.ValueKind == JsonValueKind.String:
                    body[member.Name] = value.ValueKind == JsonValueKind.Null || string.IsNullOrWhiteSpace(value.GetString()) ? null : value.GetString()!.Trim();
                    break;
                default:
                    return null;
            }
        }

        if (body.TryGetValue("targetSource", out var source) && (string?)source == "SuggestionApplied" && body.GetValueOrDefault("toLocationId") is null) return null;
        return body;
    }

    private static bool Target(string? value) => value is null or "Manual" or "SuggestionApplied";

    private static bool OptionalId(string? value) => string.IsNullOrWhiteSpace(value) || Guid.TryParse(value, out _);

    private async Task<IActionResult> KeyOnlyAsync(string operation, string permission, string id, PutawayWriteRequest? model, bool needsIfMatch,
        Func<string, string> path, string savedKey, CancellationToken ct)
    {
        if (!_permissions.Has(permission)) return Refuse(403, null, null);
        if (!WarehouseProxy.TryId(id, out var taskId) || model is null || !WarehouseProxy.IsKey(model.IdempotencyKey)
            || (needsIfMatch ? !WarehouseProxy.IsETag(model.IfMatch) : model.IfMatch is not null && !WarehouseProxy.IsETag(model.IfMatch)))
            return Refuse(400, "VALIDATION_ERROR", null);
        var key = Guid.Parse(model.IdempotencyKey!).ToString("D");
        return await WriteAsync(operation, HttpMethod.Post, path(taskId), new { idempotencyKey = key }, key, needsIfMatch ? model.IfMatch : null, savedKey, ct);
    }

    private async Task<IActionResult> ReasonedAsync(string operation, string permission, string id, PutawayWriteRequest? model, Func<string, string> path,
        string savedKey, CancellationToken ct)
    {
        if (!_permissions.Has(permission)) return Refuse(403, null, null);
        if (!WarehouseProxy.TryId(id, out var taskId) || model is null || !WarehouseProxy.IsKey(model.IdempotencyKey) || !WarehouseProxy.IsETag(model.IfMatch))
            return Refuse(400, "VALIDATION_ERROR", null);
        var key = Guid.Parse(model.IdempotencyKey!).ToString("D");
        var body = new Dictionary<string, object?> { ["idempotencyKey"] = key };
        if (!string.IsNullOrWhiteSpace(model.Reason)) body["reason"] = model.Reason.Trim(); // the service owns REASON_REQUIRED (10–1000)
        return await WriteAsync(operation, HttpMethod.Post, path(taskId), body, key, model.IfMatch, savedKey, ct);
    }

    private async Task<IActionResult> WriteAsync(string operation, HttpMethod method, string path, object body, string key, string? ifMatch, string savedKey, CancellationToken ct)
    {
        var gateway = WarehouseProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        if (gateway is null) return Refuse(401, null, null);
        var reply = await gateway.SendAsync(method, path, body, key, ifMatch, ct);
        if (!reply.IsSuccess) return Failed(operation, reply);
        var task = reply.Read<WmsTaskDto>();
        return Json(new { success = true, id = task?.TaskId, number = task?.Number, etag = reply.ETag ?? (task is null ? null : $"W/\"{task.Version}\""), message = _localizer[savedKey].Value });
    }

    private async Task<MasterLabels> LabelsAsync(IReadOnlyList<WmsTaskDto> tasks, CancellationToken ct, IEnumerable<string?>? extraLocations = null)
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

    /// <summary>One list / QuickView row: the task's own fields, the names of its ids (raw id when a master could not say), its menu rules.</summary>
    public static object Row(WmsTaskDto t, MasterLabels labels, IReadOnlyDictionary<string, string> people) => new
    {
        id = t.TaskId,
        t.Number,
        t.Status,
        integrationFlag = t.IntegrationFlag ?? "None",
        warehouse = t.WarehouseCode ?? labels.Location(t.WarehouseId) ?? t.WarehouseId,
        t.WarehouseId,
        sku = labels.Sku(t.Item?.SkuId) ?? labels.Product(t.Item?.ItemId) ?? t.Item?.SkuId,
        t.LotNumber,
        t.StockStatus,
        fromLocation = labels.Location(t.FromLocationId) ?? t.FromLocationId,
        toLocation = t.ToLocationId is null ? null : labels.Location(t.ToLocationId) ?? t.ToLocationId,
        t.ToLocationId,
        suggestedToLocation = t.SuggestedToLocationId is null ? null : labels.Location(t.SuggestedToLocationId) ?? t.SuggestedToLocationId,
        t.SuggestedToLocationId,
        t.TargetSource,
        planned = t.PlannedQuantity,
        confirmed = t.ConfirmedQuantity,
        pending = t.PendingQuantity,
        remaining = t.RemainingQuantity,
        uom = t.BaseUomId,
        assignee = Person(t.Assignee, people),
        t.Priority,
        t.Note,
        t.SerialControlled,
        t.Origin,
        t.ShortCloseReason,
        t.CancelReason,
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
        _logger.LogWarning("Putaway {Operation} refused with {Status} {Code} (correlation {CorrelationId}).",
            operation, reply.StatusCode, reply.Error?.Code, reply.Error?.CorrelationId ?? reply.CorrelationId);
        return Refuse(reply.StatusCode, reply.Error?.Code, reply.Error?.CorrelationId ?? reply.CorrelationId);
    }

    private IActionResult Refuse(int status, string? code, string? correlationId) =>
        WarehouseProxy.Error(status, code, _localizer[WarehouseErrorMessages.KeyFor(code, status)].Value, correlationId);
}
