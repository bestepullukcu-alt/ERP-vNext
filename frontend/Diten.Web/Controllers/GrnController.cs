using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Diten.Web.Models.Procurement.Grn;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace Diten.Web.Controllers;

// MOD-0142 Receiving (GRN) — tenant shell MVC controller.
// Route is single-segment /Grn (NO area prefix); views live under Views/Procurement/Grn/.
// Mirrors the shipped SourcingController direct-gateway profile: server-side proxy for create + the
// GRN lifecycle sub-action (reverse — Idempotency-Key stays server-side); the DataTable reads and deletes
// straight through the Gateway (window.API.procurement + /api/grn).
// GRN-EVENT contract exposes no update endpoint (create + reverse only) → no update proxy (ASSUMPTION-GRN-FE-01).
[Authorize]
[Route("Grn")]
public sealed class GrnController : Controller
{
    private const string ViewRoot = "~/Views/Procurement/Grn";

    // GrnStatus enum ordinals (GoodsReceipt.cs). Enums serialize numerically (no JsonStringEnumConverter on
    // the service) → map ordinals to canonical contract names. ASSUMPTION-GRN-FE-02.
    private static readonly string[] GrnStatusNames = ["Draft", "Posted", "Reversed"];

    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly IStringLocalizer<SharedResource> _sharedLocalizer;
    private readonly ILogger<GrnController> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public GrnController(
        HttpClient httpClient,
        IConfiguration configuration,
        IStringLocalizer<SharedResource> sharedLocalizer,
        ILogger<GrnController> logger)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"]
            ?? throw new InvalidOperationException("GatewayUrl configuration is required.");
        _sharedLocalizer = sharedLocalizer;
        _logger = logger;
    }

    [HttpGet("")]
    public IActionResult Index() => View($"{ViewRoot}/Index.cshtml");

    [HttpGet("Create")]
    public IActionResult Create() =>
        View($"{ViewRoot}/Create.cshtml", new GrnEditViewModel());

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([FromForm] GrnEditViewModel model)
    {
        NormalizeModel(model);
        if (!ModelState.IsValid)
            return View($"{ViewRoot}/Create.cshtml", model);

        if (!AddAuthHeaders())
        {
            ModelState.AddModelError(string.Empty, _sharedLocalizer["Unauthorized"].Value);
            return View($"{ViewRoot}/Create.cshtml", model);
        }

        try
        {
            SetIdempotencyKey();
            var response = await _httpClient.PostAsJsonAsync(
                $"{_gatewayUrl}/api/grn",
                ToPayload(model),
                _jsonOptions);

            if (response.IsSuccessStatusCode)
            {
                TempData["SuccessMessage"] = _sharedLocalizer["RecordCreated"].Value;
                return RedirectToAction(nameof(Index));
            }

            AddGatewayErrorsToModelState(await ExtractGatewayErrorsAsync(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GRN create failed.");
            AddGatewayErrorsToModelState(BuildExceptionErrors(ex));
        }

        return View($"{ViewRoot}/Create.cshtml", model);
    }

    [HttpGet("Edit/{id}")]
    public async Task<IActionResult> Edit(string id)
    {
        var model = await LoadEditModelAsync(id);
        if (model is null)
        {
            TempData["ErrorMessage"] = _sharedLocalizer["GatewayError"].Value;
            return RedirectToAction(nameof(Index));
        }

        return View($"{ViewRoot}/Edit.cshtml", model);
    }

    // The GRN-EVENT contract exposes no update endpoint (create + reverse only; a receipt is corrected by a
    // reverse movement, not an edit — DEC-INV-07 append-only). We do NOT fabricate a gateway update call
    // (K12 no-invention). The Edit page exists for compact file-contract parity; the POST surfaces an honest,
    // localized not-supported message rather than silently discarding input. ASSUMPTION-GRN-FE-01.
    [HttpPost("Edit/{id}")]
    [ValidateAntiForgeryToken]
    public IActionResult Edit(string id, [FromForm] GrnEditViewModel model)
    {
        model.GrnId = id;
        NormalizeModel(model);
        ModelState.AddModelError(string.Empty, _sharedLocalizer["GrnUpdateNotSupported"].Value);
        return View($"{ViewRoot}/Edit.cshtml", model);
    }

    [HttpGet("Details/{id}")]
    public async Task<IActionResult> Details(string id)
    {
        var model = await LoadEditModelAsync(id);
        if (model is null)
        {
            TempData["ErrorMessage"] = _sharedLocalizer["GatewayError"].Value;
            return RedirectToAction(nameof(Index));
        }

        return View($"{ViewRoot}/Details.cshtml", model);
    }

    [HttpGet("get/{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var model = await LoadGrnAsync(id);
        if (model is null)
            return Json(new { success = false });

        return Json(new { success = true, data = model });
    }

    // ── GRN lifecycle sub-action (Details page, premium-modal driven) — server proxy keeps Idempotency-Key server-side ──

    // JSON sub-action proxy (called by details.js via same-origin fetch). Like SuppliersController.Validate,
    // this carries no antiforgery token — auth is the JWT cookie + tenant claim; state-change idempotency is the
    // server-set Idempotency-Key header. Reverse posts an INVENTORY REVERSAL/SUPPLIER_RETURN (ASSUMPTION-GRN-02).
    [HttpPost("reverse/{id}")]
    public Task<IActionResult> Reverse(string id) =>
        ProxyStateActionAsync(HttpMethod.Post, $"/api/grn/{Uri.EscapeDataString(id)}/reverse", content: null);

    private async Task<IActionResult> ProxyStateActionAsync(HttpMethod method, string path, object? content)
    {
        if (!AddAuthHeaders())
            return StatusCode(StatusCodes.Status401Unauthorized, new { success = false, errors = new[] { _sharedLocalizer["Unauthorized"].Value } });

        try
        {
            SetIdempotencyKey();
            using var request = new HttpRequestMessage(method, $"{_gatewayUrl}{path}");
            if (content is not null)
                request.Content = JsonContent.Create(content, options: _jsonOptions);

            var response = await _httpClient.SendAsync(request);
            var raw = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(raw))
                return StatusCode((int)response.StatusCode);

            Response.StatusCode = (int)response.StatusCode;
            return Content(raw, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GRN lifecycle proxy failed for {Path}.", path);
            return StatusCode(StatusCodes.Status502BadGateway, new { success = false, errors = new[] { ex.GetBaseException().Message } });
        }
    }

    private async Task<GrnEditViewModel?> LoadEditModelAsync(string id)
    {
        var grn = await LoadGrnAsync(id);
        return grn is null ? null : ToEditModel(grn);
    }

    private async Task<GrnResponseApiModel?> LoadGrnAsync(string id)
    {
        if (!AddAuthHeaders())
            return null;

        var response = await _httpClient.GetAsync($"{_gatewayUrl}/api/grn/{Uri.EscapeDataString(id)}");
        if (!response.IsSuccessStatusCode)
            return null;

        return await ReadDataAsync<GrnResponseApiModel>(response);
    }

    private static GrnEditViewModel ToEditModel(GrnResponseApiModel model) => new()
    {
        GrnId = model.GrnId,
        PoId = model.PoId,
        Status = MapStatus(model.Status),
        ReceivedAt = model.ReceivedAt?.UtcDateTime,
        Lines = (model.Lines is { Count: > 0 })
            ? model.Lines.Select(l => new GrnLineInput
            {
                PoLineId = l.PoLineId,
                InventoryTransactionId = l.InventoryTransactionId,
                LotId = l.LotId,
                Quantity = l.Quantity
            }).ToList()
            : [new GrnLineInput()]
    };

    private static GrnRequestPayload ToPayload(GrnEditViewModel model) => new()
    {
        PoId = string.IsNullOrWhiteSpace(model.PoId) ? null : model.PoId.Trim(),
        WarehouseId = model.WarehouseId?.Trim() ?? string.Empty,
        LocationId = string.IsNullOrWhiteSpace(model.LocationId) ? null : model.LocationId.Trim(),
        SourceSystem = string.IsNullOrWhiteSpace(model.SourceSystem) ? null : model.SourceSystem.Trim(),
        ExternalRef = string.IsNullOrWhiteSpace(model.ExternalRef) ? null : model.ExternalRef.Trim(),
        Lines = (model.Lines ?? [])
            .Where(l => l is not null && HasLineContent(l))
            .Select(l => new GrnLinePayload
            {
                PoLineId = string.IsNullOrWhiteSpace(l.PoLineId) ? null : l.PoLineId.Trim(),
                ItemId = l.ItemId?.Trim() ?? string.Empty,
                SkuId = l.SkuId?.Trim() ?? string.Empty,
                SkuLevel = l.SkuLevel?.Trim() ?? string.Empty,
                LotNumber = string.IsNullOrWhiteSpace(l.LotNumber) ? null : l.LotNumber.Trim(),
                SerialIds = SplitSerials(l.SerialIds),
                Quantity = l.Quantity?.Trim() ?? string.Empty,
                UomId = l.UomId?.Trim() ?? string.Empty,
                ToStockStatus = l.ToStockStatus?.Trim() ?? string.Empty
            })
            .ToList()
    };

    private static List<string>? SplitSerials(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var serials = raw
            .Split([',', ';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return serials.Count > 0 ? serials : null;
    }

    private static bool HasLineContent(GrnLineInput l) =>
        !string.IsNullOrWhiteSpace(l.ItemId) || !string.IsNullOrWhiteSpace(l.SkuId)
        || !string.IsNullOrWhiteSpace(l.Quantity) || !string.IsNullOrWhiteSpace(l.UomId)
        || !string.IsNullOrWhiteSpace(l.SkuLevel) || !string.IsNullOrWhiteSpace(l.ToStockStatus)
        || !string.IsNullOrWhiteSpace(l.LotNumber) || !string.IsNullOrWhiteSpace(l.SerialIds)
        || !string.IsNullOrWhiteSpace(l.PoLineId);

    private static void NormalizeModel(GrnEditViewModel model)
    {
        model.Lines = (model.Lines ?? [])
            .Where(l => l is not null && HasLineContent(l))
            .ToList();
        if (model.Lines.Count == 0)
            model.Lines.Add(new GrnLineInput());
    }

    private static string MapStatus(int? ordinal) =>
        ordinal is >= 0 && ordinal < GrnStatusNames.Length ? GrnStatusNames[ordinal.Value] : "Draft";

    private async Task<T?> ReadDataAsync<T>(HttpResponseMessage response)
    {
        var payload = await response.Content.ReadFromJsonAsync<GatewayResponse<T>>(_jsonOptions);
        return payload is not null ? payload.Data : default;
    }

    private void AddGatewayErrorsToModelState(IEnumerable<string> errors)
    {
        foreach (var error in errors)
            ModelState.AddModelError(string.Empty, error);
    }

    private List<string> BuildExceptionErrors(Exception ex)
    {
        var message = ex.GetBaseException().Message;
        if (string.IsNullOrWhiteSpace(message))
            message = _sharedLocalizer["GatewayError"].Value;

        return [message];
    }

    private async Task<List<string>> ExtractGatewayErrorsAsync(HttpResponseMessage response)
    {
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            return [_sharedLocalizer["Unauthorized"].Value];

        try
        {
            var payload = await response.Content.ReadFromJsonAsync<GatewayResponse<object>>(_jsonOptions);
            var errors = payload?.Errors?.Where(e => !string.IsNullOrWhiteSpace(e)).ToList();
            if (errors?.Count > 0)
                return errors;
        }
        catch { }

        var raw = await response.Content.ReadAsStringAsync();
        return [string.IsNullOrWhiteSpace(raw) ? _sharedLocalizer["GatewayError"].Value : raw];
    }

    private void SetIdempotencyKey()
    {
        if (_httpClient.DefaultRequestHeaders.Contains("Idempotency-Key"))
            _httpClient.DefaultRequestHeaders.Remove("Idempotency-Key");

        _httpClient.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
    }

    private bool AddAuthHeaders()
    {
        _httpClient.DefaultRequestHeaders.Authorization = null;
        var token = Diten.Web.Services.Auth.AuthTokenCookies.GetAccessToken(Request);
        if (!string.IsNullOrWhiteSpace(token))
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        if (_httpClient.DefaultRequestHeaders.Contains("X-Tenant-Id"))
            _httpClient.DefaultRequestHeaders.Remove("X-Tenant-Id");

        var tenantId = GetTenantId();
        if (string.IsNullOrWhiteSpace(tenantId))
            return false;

        _httpClient.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);
        return true;
    }

    private string? GetTenantId() =>
        User.Claims.FirstOrDefault(x =>
            x.Type == "tenantId" ||
            x.Type == "tenant_id" ||
            x.Type.EndsWith("/tenantId", StringComparison.OrdinalIgnoreCase))?.Value;
}
