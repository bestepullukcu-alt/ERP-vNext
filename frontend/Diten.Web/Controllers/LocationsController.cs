using System.Text.Json;
using System.Text.RegularExpressions;
using Diten.Web.Models.Inventory;
using Diten.Web.Services;
using Diten.Web.Views.Inventory.Locations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Diten.Web.Controllers;

// MOD-0173-FU02 L-2 Locations list + the location proxies of BOTH location screens (LOC2) — tenant shell MVC controller.
// Route is single-segment /Locations; views live under Views/Inventory/Locations/ (pack §9 folder decision).
// The browser talks only to this same-origin controller; it calls Gateway 5000 /api/locations (INT-2) with the FU01
// InventoryGatewayClient — cookie bearer, X-Tenant-Id and the W-0173-02 company header (pack §8). The browser never
// calls 5066 or /api/locations itself. Every write is [ValidateAntiForgeryToken] and is only offered to a user holding
// inventory.locations.manage; InventoryService's [HasPermission] stays the authority (UAS-001 §4). No delete exists
// (K-04): a location ends Archived.
[Authorize]
[Route("Locations")]
public sealed partial class LocationsController : Controller
{
    private const string ViewRoot = "~/Views/Inventory/Locations";
    private const int MaxParentPaths = 50;

    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly IPermissionSnapshot _permissions;
    private readonly IStringLocalizer<LocationsIndex> _localizer;
    private readonly ILogger<LocationsController> _logger;

    public LocationsController(
        HttpClient httpClient,
        IConfiguration configuration,
        IPermissionSnapshot permissions,
        IStringLocalizer<LocationsIndex> localizer,
        ILogger<LocationsController> logger)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"]
            ?? throw new InvalidOperationException("GatewayUrl configuration is required.");
        _permissions = permissions;
        _localizer = localizer;
        _logger = logger;
    }

    // UAS-001: the view draws _AccessDenied (and nothing else) without inventory.locations.read — no redirect.
    [HttpGet("")]
    public IActionResult Index() => View($"{ViewRoot}/Index.cshtml");

    // List adapter: repo server-mode wire → LOCATION v1.1 searchLocations (pack §11). Errors keep the contract shape.
    [HttpGet("api")]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        if (!_permissions.Has(LocationPermissionKeys.Read))
            return Refuse(StatusCodes.Status403Forbidden, null, null);

        var translation = LocationListAdapter.SearchQuery(Request.Query);
        if (!translation.IsValid)
            return Refuse(StatusCodes.Status400BadRequest, translation.ErrorCode, null);

        var gateway = InventoryProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        if (gateway is null)
            return Refuse(StatusCodes.Status401Unauthorized, null, null);

        var reply = await gateway.GetAsync("/api/locations/search" + translation.Query, cancellationToken);
        if (!reply.IsSuccess)
            return Failed("search", reply);

        var page = reply.Read<LocationSearchPageDto>() ?? new LocationSearchPageDto();
        var items = page.Items ?? [];

        // "Parent path" column: one getLocationPath per distinct parent on this page (bins share a few parents).
        var paths = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var parentId in items.Select(i => i.ParentId).Where(p => !string.IsNullOrWhiteSpace(p)).Distinct(StringComparer.Ordinal).Take(MaxParentPaths))
        {
            var path = await gateway.GetAsync($"/api/locations/{Uri.EscapeDataString(parentId!)}/path", cancellationToken);
            paths[parentId!] = path.IsSuccess ? LocationListAdapter.PathText(path.Read<LocationPathDto>()) : null;
        }

        var total = page.Total ?? items.Count;
        return Json(new
        {
            data = new
            {
                items = items.Select(i => LocationListAdapter.Row(i, i.ParentId is not null && paths.TryGetValue(i.ParentId, out var p) ? p : null)),
                total,
                filteredTotal = total
            },
            contractVersion = page.ContractVersion ?? InventoryProxy.ContractVersion
        });
    }

    // One node for the detail panel (L-1) and the edit form (L-3): the v1 read (+ its ETag), the path (breadcrumb) and
    // the stock presence (K-10: yes / no, never a quantity). Path / presence are read paths: their failure does not
    // fail the node, the panel says what it could not read.
    [HttpGet("node/{id}")]
    public async Task<IActionResult> Node(string id, CancellationToken cancellationToken)
    {
        if (!_permissions.Has(LocationPermissionKeys.Read))
            return Refuse(StatusCodes.Status403Forbidden, null, null);
        if (!TryId(id, out var locationId))
            return Refuse(StatusCodes.Status400BadRequest, LocationListAdapter.InvalidQuery, null);

        var gateway = InventoryProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        if (gateway is null)
            return Refuse(StatusCodes.Status401Unauthorized, null, null);

        var reply = await gateway.GetAsync($"/api/locations/{locationId}", cancellationToken);
        if (!reply.IsSuccess)
            return Failed("get", reply);

        var path = await gateway.GetAsync($"/api/locations/{locationId}/path", cancellationToken);
        var presence = await gateway.GetAsync($"/api/locations/{locationId}/stock-presence", cancellationToken);
        var presenceDto = presence.IsSuccess ? presence.Read<LocationStockPresenceDto>() : null;

        return Json(new
        {
            location = reply.Read<LocationRefDto>(),
            etag = reply.ETag,
            path = path.IsSuccess ? path.Read<LocationPathDto>()?.Path : null,
            presence = presenceDto is null ? null : new { hasStock = presenceDto.HasStock == true, subtreeHasStock = presenceDto.SubtreeHasStock == true },
            contractVersion = InventoryProxy.ContractVersion
        });
    }

    // Change history (K-11), newest first, 10 per page.
    [HttpGet("history/{id}")]
    public async Task<IActionResult> History(string id, [FromQuery] int page = 1, CancellationToken cancellationToken = default)
    {
        if (!_permissions.Has(LocationPermissionKeys.Read))
            return Refuse(StatusCodes.Status403Forbidden, null, null);
        if (!TryId(id, out var locationId) || page < 1)
            return Refuse(StatusCodes.Status400BadRequest, LocationListAdapter.InvalidQuery, null);

        var gateway = InventoryProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        if (gateway is null)
            return Refuse(StatusCodes.Status401Unauthorized, null, null);

        var reply = await gateway.GetAsync($"/api/locations/{locationId}/history?page={page}&pageSize=10", cancellationToken);
        if (!reply.IsSuccess)
            return Failed("history", reply);

        var history = reply.Read<LocationHistoryPageDto>() ?? new LocationHistoryPageDto();
        return Json(new { items = history.Items ?? [], total = history.Total ?? 0, page = history.Page ?? page, pageSize = history.PageSize ?? 10 });
    }

    // Parent picker (form "Parent", list "Parent" filter): code / name search among the given types. A choice for a
    // write is Active only (K-06); the list filter may pick any status.
    [HttpGet("parents")]
    public async Task<IActionResult> Parents([FromQuery] string? q, [FromQuery(Name = "type")] string[]? types, [FromQuery] bool activeOnly, CancellationToken cancellationToken)
    {
        if (!_permissions.Has(LocationPermissionKeys.Read))
            return Refuse(StatusCodes.Status403Forbidden, null, null);

        var query = LocationListAdapter.ParentsQuery(q, types ?? [], activeOnly);
        if (query is null)
            return Refuse(StatusCodes.Status400BadRequest, LocationListAdapter.InvalidQuery, null);

        var gateway = InventoryProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        if (gateway is null)
            return Refuse(StatusCodes.Status401Unauthorized, null, null);

        var reply = await gateway.GetAsync("/api/locations/search" + query, cancellationToken);
        if (!reply.IsSuccess)
            return Json(new { items = Array.Empty<object>(), available = false, message = Sentence(reply.Error?.Code, reply.StatusCode) });

        var items = (reply.Read<LocationSearchPageDto>()?.Items ?? [])
            .Where(i => !string.IsNullOrWhiteSpace(i.LocationId))
            .Select(i => new { value = i.LocationId, text = $"{i.Code} · {i.Name}", type = i.Type, status = i.Status });
        return Json(new { items, available = true });
    }

    // Org-unit picker (L-3 field ⑦, pack §7 / LOC-11): Platform OrganizationUnits of the SAME company, Active, not
    // archived, through the Gateway. Without the Platform read permission (or Platform down) the list is
    // "unavailable" — the field can stay empty and the form still saves. Never a hardcoded list.
    [HttpGet("orgunits")]
    public async Task<IActionResult> OrgUnits(CancellationToken cancellationToken)
    {
        if (!_permissions.Has(LocationPermissionKeys.Manage))
            return Refuse(StatusCodes.Status403Forbidden, null, null);

        var company = CompanySelection.TokenLegalEntity(User) ?? CompanySelection.SelectedCompany(Request, User);
        var gateway = InventoryProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        if (gateway is null || company is null)
            return Json(new { items = Array.Empty<OrgUnitOption>(), available = false, message = _localizer["OrgUnitsUnavailable"].Value });

        var reply = await gateway.GetAsync("/api/platform/organization-units", cancellationToken);
        var options = reply.IsSuccess ? ParseOrgUnits(reply.Body, company.Value) : null;
        if (options is null)
        {
            _logger.LogInformation("Org-unit list unavailable for the location form ({Status}).", reply.StatusCode);
            return Json(new { items = Array.Empty<OrgUnitOption>(), available = false, message = _localizer["OrgUnitsUnavailable"].Value });
        }

        return Json(new { items = options.Select(o => new { value = o.Value, text = o.Text }), available = true });
    }

    // L-3 create (createLocation). The company is never sent (K-03): a Site takes the scope company, below inherits.
    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([FromBody] LocationCreateRequestViewModel? model, CancellationToken cancellationToken)
    {
        if (!_permissions.Has(LocationPermissionKeys.Manage))
            return Refuse(StatusCodes.Status403Forbidden, null, null);

        if (model is null
            || !Guid.TryParse(model.IdempotencyKey, out var key)
            || string.IsNullOrWhiteSpace(model.Code) || string.IsNullOrWhiteSpace(model.Name)
            || !LocationListAdapter.Types.Contains(model.Type ?? string.Empty, StringComparer.Ordinal)
            || (model.ParentId is not null && !Guid.TryParse(model.ParentId, out _))
            || (model.OrgUnitRef is not null && !Guid.TryParse(model.OrgUnitRef, out _))
            || (model.OperatingMode is not null && !LocationListAdapter.OperatingModes.Contains(model.OperatingMode, StringComparer.Ordinal)))
            return Refuse(StatusCodes.Status400BadRequest, "INVALID_REQUEST", null);

        var gateway = InventoryProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        if (gateway is null)
            return Refuse(StatusCodes.Status401Unauthorized, null, null);

        var idempotencyKey = key.ToString("D");
        var body = new Dictionary<string, object?>
        {
            ["idempotencyKey"] = idempotencyKey,
            ["code"] = model.Code.Trim(),
            ["name"] = model.Name.Trim(),
            ["type"] = model.Type
        };
        if (model.ParentId is not null) body["parentId"] = Guid.Parse(model.ParentId).ToString("D");
        if (model.IsStockable is not null) body["isStockable"] = model.IsStockable;
        if (model.OperatingMode is not null) body["operatingMode"] = model.OperatingMode;
        if (model.OrgUnitRef is not null) body["orgUnitRef"] = Guid.Parse(model.OrgUnitRef).ToString("D");
        if (!string.IsNullOrWhiteSpace(model.CostCenterCode)) body["costCenterCode"] = model.CostCenterCode.Trim();

        var reply = await gateway.PostAsync("/api/locations", body, idempotencyKey, cancellationToken);
        if (!reply.IsSuccess)
            return Failed("create", reply);

        var location = reply.Read<LocationRefDto>();
        return Json(new { success = true, location, etag = reply.ETag, message = _localizer["LocationCreated"].Value });
    }

    // L-3 edit (updateLocation, If-Match): only the members the reader changed are forwarded (absent = unchanged,
    // null = cleared); `type` is never accepted (LOC-04). A parent change is a move (K-07).
    [HttpPost("edit/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, [FromBody] LocationEditRequestViewModel? model, CancellationToken cancellationToken)
    {
        if (!_permissions.Has(LocationPermissionKeys.Manage))
            return Refuse(StatusCodes.Status403Forbidden, null, null);

        var changes = model is null ? null : EditBody(model.Changes);
        if (!TryId(id, out var locationId) || model is null || !IsETag(model.IfMatch) || changes is null || changes.Count == 0)
            return Refuse(StatusCodes.Status400BadRequest, "INVALID_REQUEST", null);

        var gateway = InventoryProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        if (gateway is null)
            return Refuse(StatusCodes.Status401Unauthorized, null, null);

        var reply = await gateway.PatchAsync($"/api/locations/{locationId}", changes, model.IfMatch!, cancellationToken);
        if (!reply.IsSuccess)
            return Failed("update", reply);

        return Json(new { success = true, location = reply.Read<LocationRefDto>(), etag = reply.ETag, message = _localizer["LocationSaved"].Value });
    }

    // Activate / Deactivate / Archive (changeLocationStatus, If-Match) — a confirmed row / detail action, never a form
    // field. Refusals (LOCATION_HAS_STOCK, LOCATION_HAS_ACTIVE_CHILDREN, LOCATION_PARENT_INACTIVE…) come back as
    // localized sentences for the dialog to show in place.
    [HttpPost("status/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Status(string id, [FromBody] LocationStatusRequestViewModel? model, CancellationToken cancellationToken)
    {
        if (!_permissions.Has(LocationPermissionKeys.Manage))
            return Refuse(StatusCodes.Status403Forbidden, null, null);

        if (!TryId(id, out var locationId) || model is null || !IsETag(model.IfMatch)
            || !LocationListAdapter.Statuses.Contains(model.Status ?? string.Empty, StringComparer.Ordinal))
            return Refuse(StatusCodes.Status400BadRequest, "INVALID_REQUEST", null);

        var gateway = InventoryProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        if (gateway is null)
            return Refuse(StatusCodes.Status401Unauthorized, null, null);

        var reply = await gateway.PostIfMatchAsync($"/api/locations/{locationId}/status", new { status = model.Status }, model.IfMatch!, cancellationToken);
        if (!reply.IsSuccess)
            return Failed("status", reply);

        return Json(new { success = true, location = reply.Read<LocationRefDto>(), etag = reply.ETag, message = _localizer["StatusChanged"].Value });
    }

    // L-4 bulk bins (bulkCreateBins): the preview (dryRun) writes nothing; the confirmed run is ONE request with the
    // dialog's key, so a double submit is a replay, never a second batch (K-12, AC-17).
    [HttpPost("bins/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Bins(string id, [FromBody] BulkBinsRequestViewModel? model, CancellationToken cancellationToken)
    {
        if (!_permissions.Has(LocationPermissionKeys.Manage))
            return Refuse(StatusCodes.Status403Forbidden, null, null);

        if (!TryId(id, out var locationId) || model is null || !Guid.TryParse(model.IdempotencyKey, out var key)
            || !AislePattern().IsMatch(model.AisleFrom ?? string.Empty) || !AislePattern().IsMatch(model.AisleTo ?? string.Empty)
            || !InRange(model.RackFrom) || !InRange(model.RackTo) || !InRange(model.LevelFrom) || !InRange(model.LevelTo)
            || !InRange(model.PositionsPerLevel))
            return Refuse(StatusCodes.Status400BadRequest, "INVALID_REQUEST", null);

        var gateway = InventoryProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        if (gateway is null)
            return Refuse(StatusCodes.Status401Unauthorized, null, null);

        var idempotencyKey = key.ToString("D");
        var body = new
        {
            idempotencyKey,
            aisleFrom = model.AisleFrom,
            aisleTo = model.AisleTo,
            rackFrom = model.RackFrom,
            rackTo = model.RackTo,
            levelFrom = model.LevelFrom,
            levelTo = model.LevelTo,
            positionsPerLevel = model.PositionsPerLevel,
            isStockable = model.IsStockable ?? true,
            dryRun = model.DryRun
        };

        var reply = await gateway.PostAsync($"/api/locations/{locationId}/bins/bulk", body, idempotencyKey, cancellationToken);
        if (!reply.IsSuccess)
            return Failed(model.DryRun ? "bins-preview" : "bins", reply);

        if (model.DryRun)
        {
            var preview = reply.Read<BulkBinsPreviewDto>() ?? new BulkBinsPreviewDto();
            return Json(new { success = true, dryRun = true, toCreate = preview.ToCreate ?? [], skipped = preview.Skipped ?? [], count = preview.Count ?? 0 });
        }

        var result = reply.Read<BulkBinsResultDto>() ?? new BulkBinsResultDto();
        var created = (result.Created ?? []).Select(c => c.Code).ToList();
        return Json(new
        {
            success = true,
            dryRun = false,
            created,
            skipped = result.Skipped ?? [],
            count = result.Count ?? created.Count,
            replay = reply.StatusCode == StatusCodes.Status200OK,
            message = string.Format(_localizer["BinsCreated"].Value, result.Count ?? created.Count)
        });
    }

    // ── helpers ──────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>The PATCH body: only LOCATION v1.1 <c>LocationUpdateRequest</c> members, each with its JSON kind checked.</summary>
    public static Dictionary<string, object?>? EditBody(JsonElement changes)
    {
        if (changes.ValueKind != JsonValueKind.Object) return null;
        var body = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var member in changes.EnumerateObject())
        {
            var value = member.Value;
            switch (member.Name)
            {
                case "code" or "name" when value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()):
                    body[member.Name] = value.GetString()!.Trim();
                    break;
                case "parentId" when value.ValueKind == JsonValueKind.String && Guid.TryParse(value.GetString(), out var parent):
                    body[member.Name] = parent.ToString("D");
                    break;
                case "isStockable" when value.ValueKind is JsonValueKind.True or JsonValueKind.False:
                    body[member.Name] = value.GetBoolean();
                    break;
                case "operatingMode" when value.ValueKind == JsonValueKind.Null
                                          || (value.ValueKind == JsonValueKind.String && LocationListAdapter.OperatingModes.Contains(value.GetString() ?? string.Empty, StringComparer.Ordinal)):
                    body[member.Name] = value.ValueKind == JsonValueKind.Null ? null : value.GetString();
                    break;
                case "orgUnitRef" when value.ValueKind == JsonValueKind.Null || (value.ValueKind == JsonValueKind.String && Guid.TryParse(value.GetString(), out _)):
                    body[member.Name] = value.ValueKind == JsonValueKind.Null ? null : Guid.Parse(value.GetString()!).ToString("D");
                    break;
                case "costCenterCode" when value.ValueKind == JsonValueKind.Null || value.ValueKind == JsonValueKind.String:
                    body[member.Name] = value.ValueKind == JsonValueKind.Null || string.IsNullOrWhiteSpace(value.GetString()) ? null : value.GetString()!.Trim();
                    break;
                default:
                    return null; // an unknown member or a wrong kind (incl. `type`): refused here, not forwarded
            }
        }

        return body;
    }

    /// <summary>Platform <c>Response&lt;List&lt;OrganizationUnitDto&gt;&gt;</c> → options of the given company only.</summary>
    public static List<OrgUnitOption>? ParseOrgUnits(string body, Guid company)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            var data = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("data", out var d) ? d : root;
            if (data.ValueKind != JsonValueKind.Array) return null;

            var options = new List<OrgUnitOption>();
            foreach (var unit in data.EnumerateArray())
            {
                if (unit.ValueKind != JsonValueKind.Object) continue;
                var id = Text(unit, "id");
                var legalEntity = Text(unit, "legalEntityId");
                var status = Text(unit, "status");
                var archived = unit.TryGetProperty("isArchived", out var a) && a.ValueKind == JsonValueKind.True;
                if (!Guid.TryParse(id, out var unitId) || !Guid.TryParse(legalEntity, out var unitCompany) || unitCompany != company) continue;
                if (archived || !string.Equals(status, "Active", StringComparison.OrdinalIgnoreCase)) continue;
                var code = Text(unit, "code");
                var name = Text(unit, "name");
                var label = string.IsNullOrWhiteSpace(code) ? name : string.IsNullOrWhiteSpace(name) ? code : $"{code} · {name}";
                options.Add(new OrgUnitOption(unitId.ToString("D"), string.IsNullOrWhiteSpace(label) ? unitId.ToString("D") : label));
            }

            return options.OrderBy(o => o.Text, StringComparer.CurrentCulture).ToList();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? Text(JsonElement item, string name) =>
        item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static bool TryId(string? raw, out string id)
    {
        id = string.Empty;
        if (!Guid.TryParse(raw, out var parsed)) return false;
        id = parsed.ToString("D");
        return true;
    }

    private static bool IsETag(string? value) => value is not null && ETagPattern().IsMatch(value);

    private static bool InRange(int? value) => value is >= 1 and <= 99;

    private string Sentence(string? code, int status) => _localizer[LocationErrorMessages.KeyFor(code, status)].Value;

    private IActionResult Failed(string operation, GatewayReply reply)
    {
        _logger.LogWarning("Location {Operation} refused with {Status} {Code} (correlation {CorrelationId}).",
            operation, reply.StatusCode, reply.Error?.Code, reply.Error?.CorrelationId ?? reply.CorrelationId);
        return Refuse(reply.StatusCode, reply.Error?.Code, reply.Error?.CorrelationId ?? reply.CorrelationId);
    }

    private IActionResult Refuse(int status, string? code, string? correlationId) =>
        InventoryProxy.Error(status, code, Sentence(code, status), correlationId);

    [GeneratedRegex(@"^([A-Za-z]|[0-9]{1,3})$")]
    private static partial Regex AislePattern();

    [GeneratedRegex(@"^W/""\d+""$")]
    private static partial Regex ETagPattern();
}
