using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Diten.Web.Models.Procurement.Sourcing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace Diten.Web.Controllers;

// MOD-0145 Sourcing (RFQ/RFP) — tenant shell MVC controller.
// Route is single-segment /Sourcing (NO area prefix); views live under Views/Procurement/Sourcing/.
// Mirrors the shipped SuppliersController direct-gateway profile: server-side proxy for create + the RFx
// lifecycle sub-actions (publish/bid/award — Idempotency-Key stays server-side) and lookups; the DataTable
// reads and deletes straight through the Gateway (window.API.procurement + /api/sourcing/events).
// SOURCING contract exposes no RFx update endpoint (create + lifecycle only) → no update proxy (ASSUMPTION-SRC-FE-01).
[Authorize]
[Route("Sourcing")]
public sealed class SourcingController : Controller
{
    private const string ViewRoot = "~/Views/Procurement/Sourcing";

    // Contract RfxType / RfxStatus enum ordinals (sourcing.openapi.yaml). Enums serialize numerically
    // (no JsonStringEnumConverter on the service) → map ordinals to canonical contract names. ASSUMPTION-SRC-FE-02.
    private static readonly string[] RfxTypeNames = ["RFQ", "RFP", "RFI"];
    private static readonly string[] RfxStatusNames = ["Draft", "Published", "Evaluating", "Awarded", "Cancelled", "Closed"];

    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly IStringLocalizer<SharedResource> _sharedLocalizer;
    private readonly ILogger<SourcingController> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public SourcingController(
        HttpClient httpClient,
        IConfiguration configuration,
        IStringLocalizer<SharedResource> sharedLocalizer,
        ILogger<SourcingController> logger)
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
        View($"{ViewRoot}/Create.cshtml", new SourcingEditViewModel());

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([FromForm] SourcingEditViewModel model)
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
                $"{_gatewayUrl}/api/sourcing/events",
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
            _logger.LogError(ex, "RFx create failed.");
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

    // The SOURCING contract exposes no RFx update endpoint (create + lifecycle only). We do NOT fabricate a
    // gateway update call (K12 no-invention). The Edit page exists for compact file-contract parity; the POST
    // surfaces an honest, localized not-supported message rather than silently discarding input. ASSUMPTION-SRC-FE-01.
    [HttpPost("Edit/{id}")]
    [ValidateAntiForgeryToken]
    public IActionResult Edit(string id, [FromForm] SourcingEditViewModel model)
    {
        model.RfxId = id;
        NormalizeModel(model);
        ModelState.AddModelError(string.Empty, _sharedLocalizer["RfxUpdateNotSupported"].Value);
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
        var model = await LoadRfxAsync(id);
        if (model is null)
            return Json(new { success = false });

        return Json(new { success = true, data = model });
    }

    // Invited-supplier lookup — consumes the SUPPLIER surface (MOD-0140); consume-only, fail-closed if empty.
    [HttpGet("lookups")]
    public async Task<IActionResult> Lookups()
    {
        if (!AddAuthHeaders())
            return Json(EmptyLookups());

        try
        {
            var response = await _httpClient.GetAsync($"{_gatewayUrl}/api/suppliers");
            if (!response.IsSuccessStatusCode)
                return Json(EmptyLookups());

            var payload = await response.Content.ReadFromJsonAsync<GatewayResponse<SupplierLookupData>>(_jsonOptions);
            var items = payload?.Data?.Items ?? [];

            return Json(new
            {
                suppliers = items
                    .Where(x => !string.IsNullOrWhiteSpace(x.SupplierId))
                    .Select(x => new { value = x.SupplierId, text = string.IsNullOrWhiteSpace(x.Name) ? x.SupplierId : $"{x.Name} ({x.SupplierId})" })
                    .Cast<object>()
                    .ToList()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Sourcing supplier lookup load failed.");
            return Json(EmptyLookups());
        }
    }

    // ── RFx lifecycle sub-actions (Details page, premium-modal driven) — server proxy keeps Idempotency-Key server-side ──

    [HttpGet("bids/{id}")]
    public async Task<IActionResult> Bids(string id)
    {
        if (!AddAuthHeaders())
            return Json(new { success = false });

        try
        {
            var response = await _httpClient.GetAsync($"{_gatewayUrl}/api/sourcing/events/{Uri.EscapeDataString(id)}/bids");
            var raw = await response.Content.ReadAsStringAsync();
            return Content(raw, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RFx bids load failed for {RfxId}.", id);
            return Json(new { success = false });
        }
    }

    // JSON sub-action proxies (called by details.js via same-origin fetch). Like SuppliersController.Validate,
    // these carry no antiforgery token — auth is the JWT cookie + tenant claim; state-change idempotency is the
    // server-set Idempotency-Key header.
    [HttpPost("publish/{id}")]
    public Task<IActionResult> Publish(string id) =>
        ProxyStateActionAsync(HttpMethod.Post, $"/api/sourcing/events/{Uri.EscapeDataString(id)}/publish", content: null);

    [HttpPost("bids/{id}")]
    public Task<IActionResult> SubmitBid(string id, [FromBody] BidSubmitRequest body) =>
        ProxyStateActionAsync(HttpMethod.Post, $"/api/sourcing/events/{Uri.EscapeDataString(id)}/bids", body);

    [HttpPost("award/{id}")]
    public Task<IActionResult> Award(string id, [FromBody] AwardSubmitRequest body) =>
        ProxyStateActionAsync(HttpMethod.Post, $"/api/sourcing/events/{Uri.EscapeDataString(id)}/award", body);

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
            _logger.LogError(ex, "RFx lifecycle proxy failed for {Path}.", path);
            return StatusCode(StatusCodes.Status502BadGateway, new { success = false, errors = new[] { ex.GetBaseException().Message } });
        }
    }

    private async Task<SourcingEditViewModel?> LoadEditModelAsync(string id)
    {
        var rfx = await LoadRfxAsync(id);
        return rfx is null ? null : ToEditModel(rfx);
    }

    private async Task<RfxEventApiModel?> LoadRfxAsync(string id)
    {
        if (!AddAuthHeaders())
            return null;

        var response = await _httpClient.GetAsync($"{_gatewayUrl}/api/sourcing/events/{Uri.EscapeDataString(id)}");
        if (!response.IsSuccessStatusCode)
            return null;

        return await ReadDataAsync<RfxEventApiModel>(response);
    }

    private static SourcingEditViewModel ToEditModel(RfxEventApiModel model) => new()
    {
        RfxId = model.RfxId,
        Type = MapType(model.Type),
        Title = model.Title,
        ClosesAt = model.ClosesAt?.UtcDateTime,
        Status = MapStatus(model.Status),
        InvitedSupplierIds = model.InvitedSupplierIds ?? [],
        Lines = (model.Lines is { Count: > 0 })
            ? model.Lines.Select(l => new SourcingLineInput { ItemId = l.ItemId, Quantity = l.Quantity, UomId = l.UomId }).ToList()
            : [new SourcingLineInput()],
        SourceSystem = model.SourceSystem,
        ExternalRef = model.ExternalRef
    };

    private static RfxUpsertPayload ToPayload(SourcingEditViewModel model) => new()
    {
        Type = string.IsNullOrWhiteSpace(model.Type) ? "RFQ" : model.Type,
        Title = model.Title,
        ClosesAt = model.ClosesAt.HasValue ? new DateTimeOffset(DateTime.SpecifyKind(model.ClosesAt.Value, DateTimeKind.Utc)) : null,
        InvitedSupplierIds = model.InvitedSupplierIds?.Where(x => !string.IsNullOrWhiteSpace(x)).ToList(),
        Lines = (model.Lines ?? [])
            .Where(l => !string.IsNullOrWhiteSpace(l.ItemId) || !string.IsNullOrWhiteSpace(l.Quantity) || !string.IsNullOrWhiteSpace(l.UomId))
            .Select(l => new RfxLinePayload
            {
                ItemId = l.ItemId?.Trim() ?? string.Empty,
                Quantity = l.Quantity?.Trim() ?? string.Empty,
                UomId = l.UomId?.Trim() ?? string.Empty
            })
            .ToList()
    };

    private static void NormalizeModel(SourcingEditViewModel model)
    {
        model.InvitedSupplierIds = (model.InvitedSupplierIds ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        model.Lines = (model.Lines ?? [])
            .Where(l => l is not null && (!string.IsNullOrWhiteSpace(l.ItemId) || !string.IsNullOrWhiteSpace(l.Quantity) || !string.IsNullOrWhiteSpace(l.UomId)))
            .ToList();
        if (model.Lines.Count == 0)
            model.Lines.Add(new SourcingLineInput());
    }

    private static string MapType(int? ordinal) =>
        ordinal is >= 0 && ordinal < RfxTypeNames.Length ? RfxTypeNames[ordinal.Value] : "RFQ";

    private static string MapStatus(int? ordinal) =>
        ordinal is >= 0 && ordinal < RfxStatusNames.Length ? RfxStatusNames[ordinal.Value] : "Draft";

    private async Task<T?> ReadDataAsync<T>(HttpResponseMessage response)
    {
        var payload = await response.Content.ReadFromJsonAsync<GatewayResponse<T>>(_jsonOptions);
        return payload is not null ? payload.Data : default;
    }

    private object EmptyLookups() => new { suppliers = Array.Empty<object>() };

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

// ── Details sub-action request bodies (contract BidUpsert / award) ──
public sealed class BidSubmitRequest
{
    public string SupplierId { get; set; } = string.Empty;
    public List<BidLineSubmit> Lines { get; set; } = [];
}

public sealed class BidLineSubmit
{
    public string ItemId { get; set; } = string.Empty;
    public string UnitPrice { get; set; } = string.Empty;
    public int? LeadTimeDays { get; set; }
}

public sealed class AwardSubmitRequest
{
    public string AwardedBidId { get; set; } = string.Empty;
    public string? Rationale { get; set; }
}
