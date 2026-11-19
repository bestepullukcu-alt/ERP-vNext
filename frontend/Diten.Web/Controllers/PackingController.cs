using Diten.Web.Models.Inventory;
using Diten.Web.Models.Warehouse;
using Diten.Web.Services;
using Diten.Web.Views.Warehouse.Packing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Diten.Web.Controllers;

// MOD-0178 DL-6 — Paketleme (PACKINGSTATION, pack §11.1 / §8.20; detail + package form, NOT a DataTable): route /Warehouse/Packing/{outboundId}, view
// Views/Warehouse/Packing/Index.cshtml. Two ways in: the outbound's Details ("Paketle", outbound Picked) and a pick task (row / QuickView link, r21).
// The page key is warehouse.packages.read — NEVER warehouse.outbound-shipments.read (Q-10a, AC-34, sabotage (do)): the page reads only
// GET /outbound-shipments/{outboundId}/packages (outbound summary, allocation summary, packages; no ship-to), so an operator packs without seeing the
// recipient. Writes: open a package (packages.create), edit / close an Open one (packages.update). Packing writes no stock (§8.20).
[Authorize]
[Route("Warehouse/Packing")]
public sealed class PackingController : Controller
{
    private const string ViewRoot = "~/Views/Warehouse/Packing";
    private const string Base = "/api/warehouse";

    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly IPermissionSnapshot _permissions;
    private readonly IStringLocalizer<PackingIndex> _localizer;
    private readonly ILogger<PackingController> _logger;

    public PackingController(HttpClient httpClient, IConfiguration configuration, IPermissionSnapshot permissions,
        IStringLocalizer<PackingIndex> localizer, ILogger<PackingController> logger)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"] ?? throw new InvalidOperationException("GatewayUrl configuration is required.");
        _permissions = permissions;
        _localizer = localizer;
        _logger = logger;
    }

    // UAS-001: the view draws _AccessDenied (and nothing else) without warehouse.packages.read — no redirect.
    [HttpGet("{outboundId}")]
    public IActionResult Index(string outboundId)
    {
        ViewData["OutboundId"] = WarehouseProxy.TryId(outboundId, out var id) ? id : null;
        return View($"{ViewRoot}/Index.cshtml");
    }

    // The page's one read (listPackages): outbound number + status, the allocations (item, lot, picked, packed) and the packages.
    [HttpGet("api/{outboundId}")]
    public async Task<IActionResult> Packages(string outboundId, CancellationToken ct)
    {
        if (!_permissions.Has(WarehousePermissionKeys.PackagesRead)) return Refuse(403, null, null);
        if (!WarehouseProxy.TryId(outboundId, out var id)) return Refuse(404, "UNKNOWN_OUTBOUND", null);
        var gateway = WarehouseProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        if (gateway is null) return Refuse(401, null, null);

        var reply = await gateway.GetAsync($"{Base}/outbound-shipments/{id}/packages", ct);
        if (!reply.IsSuccess) return Failed("list", reply);
        var view = reply.Read<WmsPackingViewDto>();
        if (view?.Outbound is null) return Refuse(503, null, reply.CorrelationId);

        var allocations = view.Allocations ?? [];
        var inventory = InventoryProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        var labels = inventory is null ? MasterLabels.None
            : await MasterLabelResolver.ResolveAsync(inventory, allocations.Select(a => a.Item?.ItemId), allocations.Select(a => (a.Item?.SkuId, a.Item?.SkuLevel)), [], ct);
        return Json(new
        {
            outbound = new { id = view.Outbound.OutboundId, number = view.Outbound.OutboundNumber, view.Outbound.Status, version = view.Outbound.Version },
            allocations = allocations.Select(a => new
            {
                a.AllocSeq, sku = labels.Sku(a.Item?.SkuId) ?? labels.Product(a.Item?.ItemId) ?? a.Item?.SkuId, a.LotNumber, a.Picked, a.Packed
            }),
            packages = (view.Packages ?? []).Select(p => new
            {
                id = p.PackageId, number = p.PackageNumber, p.Status, p.WeightKg, p.DimensionsCm, contents = p.Contents ?? [], version = p.Version,
                etag = $"W/\"{p.Version}\""
            }),
            namesAvailable = labels.Available,
            contractVersion = InventoryProxy.ContractVersion
        });
    }

    // "Yeni koli" (createPackage): only on a Picked outbound (409 otherwise); Σ (open + closed) ≤ picked per allocation (422 WMS_PACKAGE_EXCEEDS_PICKED).
    [HttpPost("create/{outboundId}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string outboundId, [FromBody] PackageWriteRequest? model, CancellationToken ct)
    {
        if (!_permissions.Has(WarehousePermissionKeys.PackagesCreate)) return Refuse(403, null, null);
        if (!WarehouseProxy.TryId(outboundId, out var id) || model is null || !WarehouseProxy.IsKey(model.IdempotencyKey) || Body(model) is not { } body)
            return Refuse(400, "VALIDATION_ERROR", null);
        var key = Guid.Parse(model.IdempotencyKey!).ToString("D");
        body["idempotencyKey"] = key;
        return await WriteAsync("create", HttpMethod.Post, $"{Base}/outbound-shipments/{id}/packages", body, key, null, "Warehouse.Package.Saved.Created", ct);
    }

    // "Koliyi düzenle" (updatePackage, If-Match): an Open package's content / weight / dimensions.
    [HttpPost("edit/{packageId}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string packageId, [FromBody] PackageWriteRequest? model, CancellationToken ct)
    {
        if (!_permissions.Has(WarehousePermissionKeys.PackagesUpdate)) return Refuse(403, null, null);
        if (!WarehouseProxy.TryId(packageId, out var id) || model is null || !WarehouseProxy.IsKey(model.IdempotencyKey) || !WarehouseProxy.IsETag(model.IfMatch)
            || Body(model) is not { } body)
            return Refuse(400, "VALIDATION_ERROR", null);
        var key = Guid.Parse(model.IdempotencyKey!).ToString("D");
        body["idempotencyKey"] = key;
        return await WriteAsync("update", HttpMethod.Put, $"{Base}/packages/{id}", body, key, model.IfMatch, "Warehouse.Package.Saved.Updated", ct);
    }

    // "Koliyi kapat" (closePackage, If-Match): at least one content line (422 WMS_PACKAGE_EMPTY); the last close packs the outbound (§8.18).
    [HttpPost("close/{packageId}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Close(string packageId, [FromBody] PackageWriteRequest? model, CancellationToken ct)
    {
        if (!_permissions.Has(WarehousePermissionKeys.PackagesUpdate)) return Refuse(403, null, null);
        if (!WarehouseProxy.TryId(packageId, out var id) || model is null || !WarehouseProxy.IsKey(model.IdempotencyKey) || !WarehouseProxy.IsETag(model.IfMatch))
            return Refuse(400, "VALIDATION_ERROR", null);
        var key = Guid.Parse(model.IdempotencyKey!).ToString("D");
        return await WriteAsync("close", HttpMethod.Post, $"{Base}/packages/{id}/close", new Dictionary<string, object?> { ["idempotencyKey"] = key }, key, model.IfMatch,
            "Warehouse.Package.Saved.Closed", ct);
    }

    /// <summary>
    /// The package body (WMS draft §11): <c>weightKg?</c> (decimal), <c>dimensionsCm?</c> (<c>LxWxH</c>), <c>contents?: [{ allocSeq, quantity }]</c>;
    /// null when a member is outside that shape. The service owns Σ ≤ picked and the dimension pattern (400 / 422).
    /// </summary>
    public static Dictionary<string, object?>? Body(PackageWriteRequest model)
    {
        var body = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(model.WeightKg))
        {
            if (WarehouseProxy.Quantity(model.WeightKg) is not { } weight) return null;
            body["weightKg"] = weight;
        }

        if (!string.IsNullOrWhiteSpace(model.DimensionsCm)) body["dimensionsCm"] = model.DimensionsCm.Trim().Replace('X', 'x');
        if (model.Contents is { } contents)
        {
            if (contents.Count > 100 || contents.Any(c => c.AllocSeq < 1 || WarehouseProxy.Quantity(c.Quantity) is null)) return null;
            body["contents"] = contents.Select(c => new { allocSeq = c.AllocSeq, quantity = WarehouseProxy.Quantity(c.Quantity) }).ToList();
        }

        return body;
    }

    private async Task<IActionResult> WriteAsync(string operation, HttpMethod method, string path, object body, string key, string? ifMatch, string savedKey, CancellationToken ct)
    {
        var gateway = WarehouseProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        if (gateway is null) return Refuse(401, null, null);
        var reply = await gateway.SendAsync(method, path, body, key, ifMatch, ct);
        if (!reply.IsSuccess) return Failed(operation, reply);
        var package = reply.Read<WmsPackageDto>();
        return Json(new
        {
            success = true, id = package?.PackageId, number = package?.PackageNumber, status = package?.Status,
            etag = reply.ETag ?? (package is null ? null : $"W/\"{package.Version}\""), message = _localizer[savedKey].Value
        });
    }

    private IActionResult Failed(string operation, GatewayReply reply)
    {
        _logger.LogWarning("Packing {Operation} refused with {Status} {Code} (correlation {CorrelationId}).",
            operation, reply.StatusCode, reply.Error?.Code, reply.Error?.CorrelationId ?? reply.CorrelationId);
        return Refuse(reply.StatusCode, reply.Error?.Code, reply.Error?.CorrelationId ?? reply.CorrelationId);
    }

    private IActionResult Refuse(int status, string? code, string? correlationId) =>
        WarehouseProxy.Error(status, code, _localizer[WarehouseErrorMessages.KeyForS2(code, status)].Value, correlationId);
}
