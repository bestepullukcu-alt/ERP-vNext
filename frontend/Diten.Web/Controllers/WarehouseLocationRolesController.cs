using Diten.Web.Models.Inventory;
using Diten.Web.Models.Warehouse;
using Diten.Web.Services;
using Diten.Web.Views.Warehouse.WarehouseLocationRoles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Diten.Web.Controllers;

// MOD-0178 DL-3 — Depo Lokasyon Rolleri (WAREHOUSELOCATIONROLES, pack §11.1): tenant shell MVC controller. The PAGE is /Warehouse/LocationRoles
// (the manifest route, §11.1); the same-origin data and write endpoints follow the view folder, /Warehouse/WarehouseLocationRoles/… (the
// DataTable contract's proxy path /{Area}/{Module}/api, verify_datatable_page.py). Views under Views/Warehouse/WarehouseLocationRoles/. Same door as the putaway screen (WarehouseGatewayClient → Gateway 5000
// /api/warehouse/location-roles; DL-8 adds the route). Reads need warehouse.location-roles.read; every write needs .manage and is
// [ValidateAntiForgeryToken] (pack §11.3). A record without roles but with accepted storage conditions is valid (§11.3a); the inherited set is
// shown with its source node ("üst düğümden").
[Authorize]
[Route("Warehouse/WarehouseLocationRoles")]
public sealed class WarehouseLocationRolesController : Controller
{
    private const string ViewRoot = "~/Views/Warehouse/WarehouseLocationRoles";
    private const string Base = "/api/warehouse/location-roles";

    /// <summary>WMS draft §3.5 <c>WarehouseRole</c> — a contract enum, not a data list.</summary>
    public static readonly IReadOnlyList<string> Roles = ["Receiving", "Staging"];

    /// <summary>WMS draft §3.6 accepted storage-condition codes (InventoryService <c>LocationRoleRules.KnownStorageConditions</c>).</summary>
    public static readonly IReadOnlyList<string> StorageConditions = ["AMBIENT_15_25", "COOL_2_8", "FROZEN_BELOW_MINUS_20", "PROTECT_FROM_LIGHT", "PROTECT_FROM_MOISTURE"];

    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly IPermissionSnapshot _permissions;
    private readonly IStringLocalizer<WarehouseLocationRolesIndex> _localizer;
    private readonly ILogger<WarehouseLocationRolesController> _logger;

    public WarehouseLocationRolesController(HttpClient httpClient, IConfiguration configuration, IPermissionSnapshot permissions,
        IStringLocalizer<WarehouseLocationRolesIndex> localizer, ILogger<WarehouseLocationRolesController> logger)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"] ?? throw new InvalidOperationException("GatewayUrl configuration is required.");
        _permissions = permissions;
        _localizer = localizer;
        _logger = logger;
    }

    // UAS-001: the view draws _AccessDenied (and nothing else) without warehouse.location-roles.read — no redirect.
    [HttpGet("~/Warehouse/LocationRoles")]
    public IActionResult Index() => View($"{ViewRoot}/Index.cshtml");

    [HttpGet("api")]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        if (!_permissions.Has(WarehousePermissionKeys.LocationRolesRead)) return Refuse(403, null, null);
        if (WarehouseProxy.Paging(Request.Query) is not { } paging) return Refuse(400, "VALIDATION_ERROR", null);
        if (ListQuery(Request.Query) is not { } filters) return Refuse(400, "VALIDATION_ERROR", null);

        var gateway = WarehouseProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        if (gateway is null) return Refuse(401, null, null);
        var reply = await gateway.GetAsync($"{Base}?page={paging.Page}&pageSize={paging.PageSize}{filters}", ct);
        if (!reply.IsSuccess) return Failed("list", reply);

        var page = reply.Read<WmsLocationRolePageDto>() ?? new WmsLocationRolePageDto();
        var items = page.Items ?? [];
        var labels = await LabelsAsync(items, ct);
        var total = page.Total ?? items.Count;
        return Json(new
        {
            data = new { items = items.Select(i => Row(i, labels)), total, filteredTotal = total },
            draw = paging.Draw,
            contractVersion = InventoryProxy.ContractVersion
        });
    }

    // Server export (BL-452): every matching role as CSV, at most WarehouseCsv.MaxRows (413 beyond).
    [HttpGet("export")]
    public async Task<IActionResult> Export(CancellationToken ct)
    {
        if (!_permissions.Has(WarehousePermissionKeys.LocationRolesRead)) return Refuse(403, null, null);
        if (ListQuery(Request.Query) is not { } filters) return Refuse(400, "VALIDATION_ERROR", null);
        var gateway = WarehouseProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        if (gateway is null) return Refuse(401, null, null);

        var items = new List<WmsLocationRoleDto>();
        for (var page = 1; ; page++)
        {
            var reply = await gateway.GetAsync($"{Base}?page={page}&pageSize=200{filters}", ct);
            if (!reply.IsSuccess) return Failed("export", reply);
            var dto = reply.Read<WmsLocationRolePageDto>() ?? new WmsLocationRolePageDto();
            if ((dto.Total ?? 0) > WarehouseCsv.MaxRows) return Refuse(413, null, reply.CorrelationId);
            items.AddRange(dto.Items ?? []);
            if ((dto.Items?.Count ?? 0) < 200 || items.Count >= (dto.Total ?? 0)) break;
        }

        var labels = await LabelsAsync(items, ct);
        string W(string key) => _localizer[key].Value;
        var header = new[] { "ColLocation", "ColLocationType", "ColWarehouse", "ColRoles", "ColConditions", "ColNote" }.Select(W);
        var rows = items.Select(r => new[]
        {
            r.LocationCode ?? r.LocationId, r.LocationType is null ? null : W("Warehouse.LocationType." + r.LocationType), labels.Location(r.WarehouseId) ?? r.WarehouseId,
            string.Join(" | ", (r.Roles ?? []).Select(x => W("Warehouse.Role." + x))),
            string.Join(" | ", (r.EffectiveStorageConditions?.Codes ?? r.AcceptedStorageConditions ?? []).Select(x => W("Warehouse.StorageCondition." + x))),
            r.Note
        });
        return File(WarehouseCsv.Write(header, rows), "text/csv; charset=utf-8", $"warehouse-location-roles-{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    /// <summary>The list filters as the WMS query tail (<c>&amp;…</c>), or null when one is outside the contract.</summary>
    public static string? ListQuery(IQueryCollection source)
    {
        var query = string.Empty;
        var warehouse = WarehouseProxy.Single(source["warehouseId"]);
        if (warehouse is not null)
        {
            if (!WarehouseProxy.TryId(warehouse, out var wid)) return null;
            query += "&warehouseId=" + wid;
        }

        var role = WarehouseProxy.Single(source["role"]);
        if (role is not null)
        {
            if (!Roles.Contains(role, StringComparer.Ordinal)) return null;
            query += "&role=" + role;
        }

        return query;
    }

    // Warehouse filter: the company's active warehouses (LOCATION master).
    [HttpGet("warehouses")]
    public async Task<IActionResult> Warehouses(CancellationToken ct)
    {
        if (!_permissions.Has(WarehousePermissionKeys.LocationRolesRead)) return Refuse(403, null, null);
        var inventory = InventoryProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        if (inventory is null) return Refuse(401, null, null);
        var result = await new LocationPickerSource(inventory).ActiveWarehousesAsync(ct);
        return Json(new { items = result.Items.Select(o => new { value = o.Value, text = o.Text }), available = result.Available });
    }

    private async Task<MasterLabels> LabelsAsync(IReadOnlyList<WmsLocationRoleDto> items, CancellationToken ct)
    {
        var inventory = InventoryProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        return inventory is null ? MasterLabels.None : await MasterLabelResolver.ResolveAsync(inventory, [], [], items.Select(i => i.WarehouseId), ct);
    }

    [HttpGet("role/{id}")]
    public async Task<IActionResult> Role(string id, CancellationToken ct)
    {
        if (!_permissions.Has(WarehousePermissionKeys.LocationRolesRead)) return Refuse(403, null, null);
        if (!WarehouseProxy.TryId(id, out var roleId)) return Refuse(404, "WMS_LOCATION_ROLE_NOT_FOUND", null);
        var gateway = WarehouseProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        if (gateway is null) return Refuse(401, null, null);
        var reply = await gateway.GetAsync($"{Base}/{roleId}", ct);
        if (!reply.IsSuccess) return Failed("get", reply);
        var role = reply.Read<WmsLocationRoleDto>();
        if (role is null) return Refuse(503, null, reply.CorrelationId);
        return Json(new { role = Row(role, MasterLabels.None), etag = reply.ETag ?? $"W/\"{role.Version}\"" });
    }

    // Node picker (LOCATION searchLocations, Active only): the warehouse and every level under it.
    [HttpGet("nodes")]
    public async Task<IActionResult> Nodes([FromQuery] string? q, CancellationToken ct)
    {
        if (!_permissions.Has(WarehousePermissionKeys.LocationRolesManage)) return Refuse(403, null, null);
        var query = LocationListAdapter.ParentsQuery(q, ["Warehouse", "StorageLocation", "Zone", "Bin"], activeOnly: true);
        if (query is null) return Refuse(400, "VALIDATION_ERROR", null);
        var gateway = WarehouseProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        if (gateway is null) return Refuse(401, null, null);
        var reply = await gateway.GetAsync("/api/locations/search" + query, ct);
        if (!reply.IsSuccess) return Json(new { items = Array.Empty<object>(), available = false });
        var items = (reply.Read<LocationSearchPageDto>()?.Items ?? []).Where(i => !string.IsNullOrWhiteSpace(i.LocationId))
            .Select(i => new { value = i.LocationId, text = $"{i.Code} · {i.Name}", type = i.Type });
        return Json(new { items, available = true });
    }

    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([FromBody] LocationRoleWriteRequest? model, CancellationToken ct)
    {
        if (!_permissions.Has(WarehousePermissionKeys.LocationRolesManage)) return Refuse(403, null, null);
        if (model is null || !WarehouseProxy.IsKey(model.IdempotencyKey) || !WarehouseProxy.TryId(model.LocationId, out var location) || !ValidSets(model))
            return Refuse(400, "VALIDATION_ERROR", null);
        var key = Guid.Parse(model.IdempotencyKey!).ToString("D");
        var body = new Dictionary<string, object?>
        {
            ["idempotencyKey"] = key, ["locationId"] = location, ["roles"] = model.Roles ?? [], ["acceptedStorageConditions"] = model.AcceptedStorageConditions ?? []
        };
        if (!string.IsNullOrWhiteSpace(model.Note)) body["note"] = model.Note.Trim();
        return await WriteAsync("create", HttpMethod.Post, Base, body, key, null, "Warehouse.Saved.RoleCreated", ct);
    }

    // Edit (updateLocationRole, If-Match): the node never changes — it is not a member.
    [HttpPost("edit/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, [FromBody] LocationRoleWriteRequest? model, CancellationToken ct)
    {
        if (!_permissions.Has(WarehousePermissionKeys.LocationRolesManage)) return Refuse(403, null, null);
        if (!WarehouseProxy.TryId(id, out var roleId) || model is null || !WarehouseProxy.IsKey(model.IdempotencyKey) || !WarehouseProxy.IsETag(model.IfMatch)
            || model.LocationId is not null || !ValidSets(model))
            return Refuse(400, "VALIDATION_ERROR", null);
        var key = Guid.Parse(model.IdempotencyKey!).ToString("D");
        var body = new Dictionary<string, object?>
        {
            ["idempotencyKey"] = key, ["roles"] = model.Roles ?? [], ["acceptedStorageConditions"] = model.AcceptedStorageConditions ?? [],
            ["note"] = string.IsNullOrWhiteSpace(model.Note) ? null : model.Note.Trim()
        };
        return await WriteAsync("update", HttpMethod.Put, $"{Base}/{roleId}", body, key, model.IfMatch, "Warehouse.Saved.RoleUpdated", ct);
    }

    [HttpPost("delete/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id, [FromBody] LocationRoleWriteRequest? model, CancellationToken ct)
    {
        if (!_permissions.Has(WarehousePermissionKeys.LocationRolesManage)) return Refuse(403, null, null);
        if (!WarehouseProxy.TryId(id, out var roleId) || model is null || !WarehouseProxy.IsKey(model.IdempotencyKey) || !WarehouseProxy.IsETag(model.IfMatch))
            return Refuse(400, "VALIDATION_ERROR", null);
        var key = Guid.Parse(model.IdempotencyKey!).ToString("D");
        return await WriteAsync("delete", HttpMethod.Delete, $"{Base}/{roleId}", null, key, model.IfMatch, "Warehouse.Saved.RoleDeleted", ct);
    }

    [HttpPost("bulk")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkDelete([FromBody] LocationRoleWriteRequest? model, CancellationToken ct)
    {
        if (!_permissions.Has(WarehousePermissionKeys.LocationRolesManage)) return Refuse(403, null, null);
        if (model is null || !WarehouseProxy.IsKey(model.IdempotencyKey) || model.Items is not { Count: > 0 and <= 200 }
            || model.Items.Any(i => !WarehouseProxy.TryId(i.Id, out _) || i.Version < 1))
            return Refuse(400, "VALIDATION_ERROR", null);
        var gateway = WarehouseProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        if (gateway is null) return Refuse(401, null, null);
        var key = Guid.Parse(model.IdempotencyKey!).ToString("D");
        var body = new { idempotencyKey = key, items = model.Items.Select(i => new { roleId = Guid.Parse(i.Id!).ToString("D"), version = i.Version }) };
        var reply = await gateway.SendAsync(HttpMethod.Post, $"{Base}/bulk-delete", body, key, null, ct);
        if (!reply.IsSuccess) return Failed("bulk-delete", reply);
        var rows = reply.Read<WmsBulkDeleteResultDto>()?.Items ?? [];
        return Json(new { success = true, results = rows.Select(r => new { id = r.RoleId, outcome = r.Result }), deleted = rows.Count(r => r.Result == "Deleted") });
    }

    // ── helpers ──────────────────────────────────────────────────────────────────────────────────────────

    private static bool ValidSets(LocationRoleWriteRequest model) =>
        (model.Roles ?? []).All(r => Roles.Contains(r, StringComparer.Ordinal))
        && (model.AcceptedStorageConditions ?? []).All(c => StorageConditions.Contains(c, StringComparer.Ordinal));

    public static object Row(WmsLocationRoleDto r, MasterLabels labels) => new
    {
        id = r.RoleId,
        r.LocationId,
        location = r.LocationCode ?? r.LocationId,
        r.LocationType,
        warehouse = labels.Location(r.WarehouseId) ?? r.WarehouseId,
        roles = r.Roles ?? [],
        accepted = r.AcceptedStorageConditions ?? [],
        effective = r.EffectiveStorageConditions?.Codes ?? [],
        inheritedFrom = r.EffectiveStorageConditions?.InheritedFromLocationCode ?? r.EffectiveStorageConditions?.InheritedFromLocationId,
        r.Note,
        version = r.Version,
        etag = $"W/\"{r.Version}\""
    };

    private async Task<IActionResult> WriteAsync(string operation, HttpMethod method, string path, object? body, string key, string? ifMatch, string savedKey, CancellationToken ct)
    {
        var gateway = WarehouseProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        if (gateway is null) return Refuse(401, null, null);
        var reply = await gateway.SendAsync(method, path, body, key, ifMatch, ct);
        if (!reply.IsSuccess) return Failed(operation, reply);
        return Json(new { success = true, etag = reply.ETag, message = _localizer[savedKey].Value });
    }

    private IActionResult Failed(string operation, GatewayReply reply)
    {
        _logger.LogWarning("Location role {Operation} refused with {Status} {Code} (correlation {CorrelationId}).",
            operation, reply.StatusCode, reply.Error?.Code, reply.Error?.CorrelationId ?? reply.CorrelationId);
        return Refuse(reply.StatusCode, reply.Error?.Code, reply.Error?.CorrelationId ?? reply.CorrelationId);
    }

    private IActionResult Refuse(int status, string? code, string? correlationId) =>
        WarehouseProxy.Error(status, code, _localizer[WarehouseErrorMessages.KeyFor(code, status)].Value, correlationId);
}
