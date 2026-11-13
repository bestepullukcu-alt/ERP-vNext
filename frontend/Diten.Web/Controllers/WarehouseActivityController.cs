using System.Globalization;
using Diten.Web.Models.Inventory;
using Diten.Web.Models.Quarantine;
using Diten.Web.Models.Warehouse;
using Diten.Web.Services;
using Diten.Web.Views.Warehouse.WarehouseActivity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Diten.Web.Controllers;

// MOD-0178 DL-7 — Etkinlik İzi (WAREHOUSEACTIVITY, pack §8.23 / §11.1 / §11.5; Slim list, READ-ONLY): tenant shell MVC controller. The PAGE is
// /Warehouse/Activity (the manifest route); the same-origin data endpoints follow the view folder, /Warehouse/WarehouseActivity/… . Gateway 5000
// /api/warehouse/activity (the DL-8 {everything} route). The date range is mandatory and at most 31 days (§8.23) — checked here too so a wrong range
// never reaches the service; every action checks warehouse.activity.read first. No write. No answer carries a ship-to (Q-10a).
[Authorize]
[Route("Warehouse/WarehouseActivity")]
public sealed class WarehouseActivityController : Controller
{
    private const string ViewRoot = "~/Views/Warehouse/WarehouseActivity";
    private const string Base = "/api/warehouse/activity";

    /// <summary>§8.23: the Activity Trace range is at most 31 days.</summary>
    public const int MaxDays = 31;

    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly IPermissionSnapshot _permissions;
    private readonly IStringLocalizer<WarehouseActivityIndex> _localizer;
    private readonly ILogger<WarehouseActivityController> _logger;

    public WarehouseActivityController(HttpClient httpClient, IConfiguration configuration, IPermissionSnapshot permissions,
        IStringLocalizer<WarehouseActivityIndex> localizer, ILogger<WarehouseActivityController> logger)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"] ?? throw new InvalidOperationException("GatewayUrl configuration is required.");
        _permissions = permissions;
        _localizer = localizer;
        _logger = logger;
    }

    // UAS-001: the view draws _AccessDenied (and nothing else) without warehouse.activity.read — no redirect.
    [HttpGet("~/Warehouse/Activity")]
    public IActionResult Index() => View($"{ViewRoot}/Index.cshtml");

    [HttpGet("api")]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        if (!_permissions.Has(WarehousePermissionKeys.ActivityRead)) return Refuse(403, null, null);
        if (WarehouseProxy.Paging(Request.Query) is not { } paging) return Refuse(400, "VALIDATION_ERROR", null);
        if (RangeError(Request.Query) is { } rangeKey) return WarehouseProxy.Error(400, "VALIDATION_ERROR", _localizer[rangeKey].Value, null);
        if (ListQuery(Request.Query, DateTimeOffset.UtcNow) is not { } filters) return Refuse(400, "VALIDATION_ERROR", null);

        var gateway = WarehouseProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        if (gateway is null) return Refuse(401, null, null);
        var reply = await gateway.GetAsync($"{Base}?page={paging.Page}&pageSize={paging.PageSize}{filters}", ct);
        if (!reply.IsSuccess) return Failed("list", reply);

        var page = reply.Read<WmsActivityPageDto>() ?? new WmsActivityPageDto();
        var items = page.Items ?? [];
        var labels = await LabelsAsync(items, ct);
        var people = await PeopleAsync(gateway, items, ct);
        var total = page.Total ?? items.Count;
        return Json(new
        {
            data = new { items = items.Select(a => Row(a, labels, people)), total, filteredTotal = total },
            draw = paging.Draw,
            namesAvailable = labels.Available,
            contractVersion = InventoryProxy.ContractVersion
        });
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export(CancellationToken ct)
    {
        if (!_permissions.Has(WarehousePermissionKeys.ActivityRead)) return Refuse(403, null, null);
        if (RangeError(Request.Query) is { } rangeKey) return WarehouseProxy.Error(400, "VALIDATION_ERROR", _localizer[rangeKey].Value, null);
        if (ListQuery(Request.Query, DateTimeOffset.UtcNow) is not { } filters) return Refuse(400, "VALIDATION_ERROR", null);
        var gateway = WarehouseProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        if (gateway is null) return Refuse(401, null, null);

        var items = new List<WmsActivityDto>();
        for (var page = 1; ; page++)
        {
            var reply = await gateway.GetAsync($"{Base}?page={page}&pageSize=200{filters}", ct);
            if (!reply.IsSuccess) return Failed("export", reply);
            var dto = reply.Read<WmsActivityPageDto>() ?? new WmsActivityPageDto();
            if ((dto.Total ?? 0) > WarehouseCsv.MaxRows) return Refuse(413, null, reply.CorrelationId);
            items.AddRange(dto.Items ?? []);
            if ((dto.Items?.Count ?? 0) < 200 || items.Count >= (dto.Total ?? 0)) break;
        }

        var labels = await LabelsAsync(items, ct);
        var people = await PeopleAsync(gateway, items, ct);
        string W(string key) => _localizer[key].Value;
        var header = new[] { "ColTime", "ColSource", "ColEvent", "ColRecord", "ColWarehouse", "ColActor" }.Select(W);
        var rows = items.Select(a => new[]
        {
            a.AtUtc, W("Warehouse.Activity.Source." + a.Source), W(EventKey(a.Source, a.Kind)), a.RecordNumber, labels.Location(a.WarehouseId) ?? a.WarehouseId,
            a.System ? W("SystemActor") : a.Actor is null ? null : a.Actor.Name ?? people.GetValueOrDefault(a.Actor.UserId ?? string.Empty) ?? a.Actor.UserId
        });
        return File(WarehouseCsv.Write(header, rows), "text/csv; charset=utf-8", $"warehouse-activity-{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    [HttpGet("warehouses")]
    public async Task<IActionResult> Warehouses(CancellationToken ct)
    {
        if (!_permissions.Has(WarehousePermissionKeys.ActivityRead)) return Refuse(403, null, null);
        var inventory = InventoryProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        if (inventory is null) return Refuse(401, null, null);
        var result = await new LocationPickerSource(inventory).ActiveWarehousesAsync(ct);
        return Json(new { items = result.Items.Select(o => new { value = o.Value, text = o.Text }), available = result.Available });
    }

    /// <summary>The resx key of a row's event: the task / outbound timeline kind, or the movement type.</summary>
    public static string EventKey(string? source, string? kind) => source switch
    {
        "Outbound" => "Warehouse.Outbound.Event." + kind,
        "Movement" => "Warehouse.Activity.Movement." + kind,
        _ => "Warehouse.Event." + kind
    };

    /// <summary>
    /// The filters as the WMS query tail (<c>&amp;…</c>): <c>from</c> / <c>to</c> (a day <c>yyyy-MM-dd</c> or an RFC 3339 instant; no range = the last
    /// 7 days up to <paramref name="now"/>), <c>kind</c> (one of the four sources), <c>warehouseId</c>. Null when one is outside the contract — a
    /// reversed range or one longer than 31 days included (the service would answer 400 too, §8.23).
    /// </summary>
    public static string? ListQuery(IQueryCollection source, DateTimeOffset now)
    {
        var fromText = WarehouseProxy.Single(source["from"]);
        var toText = WarehouseProxy.Single(source["to"]);
        DateTimeOffset from, to;
        if (fromText is null && toText is null)
        {
            to = now;
            from = now.AddDays(-7);
        }
        else
        {
            if (Instant(fromText, false) is not { } f || Instant(toText, true) is not { } t) return null;
            from = f;
            to = t;
        }

        if (to < from || to - from > TimeSpan.FromDays(MaxDays)) return null;
        var query = new List<string> { "from=" + Uri.EscapeDataString(Wire(from)), "to=" + Uri.EscapeDataString(Wire(to)) };

        var kind = WarehouseProxy.Single(source["kind"]);
        if (kind is not null)
        {
            if (!WarehouseVocabulary.ActivitySources.Contains(kind, StringComparer.Ordinal)) return null;
            query.Add("kind=" + kind);
        }

        var warehouse = WarehouseProxy.Single(source["warehouseId"]);
        if (warehouse is not null)
        {
            if (!WarehouseProxy.TryId(warehouse, out var id)) return null;
            query.Add("warehouseId=" + id);
        }

        return "&" + string.Join('&', query);
    }

    /// <summary>
    /// The page word that says what is wrong with the range, or null: one end without the other (or unreadable) → <c>RangeRequired</c>, to before from →
    /// <c>RangeOrder</c>, longer than 31 days → <c>RangeTooLong</c> (§8.23). No range at all is fine (the last 7 days).
    /// </summary>
    public static string? RangeError(IQueryCollection source)
    {
        var fromText = WarehouseProxy.Single(source["from"]);
        var toText = WarehouseProxy.Single(source["to"]);
        if (fromText is null && toText is null) return null;
        if (Instant(fromText, false) is not { } from || Instant(toText, true) is not { } to) return "RangeRequired";
        if (to < from) return "RangeOrder";
        return to - from > TimeSpan.FromDays(MaxDays) ? "RangeTooLong" : null;
    }

    /// <summary>A day (<c>yyyy-MM-dd</c>: its start, or — for <paramref name="endOfDay"/> — its last second, UTC) or an RFC 3339 instant.</summary>
    private static DateTimeOffset? Instant(string? raw, bool endOfDay)
    {
        if (raw is null) return null;
        if (DateTime.TryParseExact(raw, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
        {
            var start = new DateTimeOffset(day, TimeSpan.Zero);
            return endOfDay ? start.AddDays(1).AddSeconds(-1) : start;
        }

        return raw.Contains('T', StringComparison.Ordinal)
               && DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var instant)
            ? instant
            : null;
    }

    private static string Wire(DateTimeOffset value) => value.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    private async Task<MasterLabels> LabelsAsync(IReadOnlyList<WmsActivityDto> items, CancellationToken ct)
    {
        if (items.Count == 0) return MasterLabels.None;
        var inventory = InventoryProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        if (inventory is null) return MasterLabels.None;
        return await MasterLabelResolver.ResolveAsync(inventory, [], [], items.Select(a => a.WarehouseId), ct);
    }

    private static async Task<IReadOnlyDictionary<string, string>> PeopleAsync(WarehouseGatewayClient gateway, IReadOnlyList<WmsActivityDto> items, CancellationToken ct)
    {
        var unnamed = items.Where(a => a.Actor?.UserId is not null && string.IsNullOrWhiteSpace(a.Actor.Name)).Select(a => a.Actor!.UserId!).Distinct().ToList();
        if (unnamed.Count == 0) return new Dictionary<string, string>();
        return await QuarantinePeople.ResolveAsync(unnamed, async (path, token) =>
        {
            var reply = await gateway.GetAsync(path, token);
            return (reply.StatusCode, reply.Body);
        }, ct);
    }

    /// <summary>One list / QuickView row: the time, the source and its event, the record (number + id for the link), the warehouse name, the actor and
    /// the row's own data (a timeline row's Data or a movement's type, quantity, lot and locations — never a ship-to).</summary>
    public static object Row(WmsActivityDto a, MasterLabels labels, IReadOnlyDictionary<string, string> people) => new
    {
        id = $"{a.Source}:{a.RecordId}:{a.Seq?.ToString(CultureInfo.InvariantCulture) ?? a.AtUtc}",
        at = a.AtUtc,
        a.Source,
        a.Kind,
        eventKey = EventKey(a.Source, a.Kind),
        a.RecordId,
        a.RecordNumber,
        a.Seq,
        warehouse = a.WarehouseId is null ? null : labels.Location(a.WarehouseId) ?? a.WarehouseId,
        actor = a.Actor?.UserId is null ? null : new { userId = a.Actor.UserId, name = !string.IsNullOrWhiteSpace(a.Actor.Name) ? a.Actor.Name : people.GetValueOrDefault(a.Actor.UserId) },
        a.System,
        data = a.Data ?? new Dictionary<string, string>()
    };

    private IActionResult Failed(string operation, GatewayReply reply)
    {
        _logger.LogWarning("Warehouse activity {Operation} refused with {Status} {Code} (correlation {CorrelationId}).",
            operation, reply.StatusCode, reply.Error?.Code, reply.Error?.CorrelationId ?? reply.CorrelationId);
        return Refuse(reply.StatusCode, reply.Error?.Code, reply.Error?.CorrelationId ?? reply.CorrelationId);
    }

    private IActionResult Refuse(int status, string? code, string? correlationId) =>
        WarehouseProxy.Error(status, code, _localizer[WarehouseErrorMessages.KeyFor(code, status)].Value, correlationId);
}
