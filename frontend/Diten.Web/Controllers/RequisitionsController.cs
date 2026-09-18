using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Diten.Web.Models.Procurement.Requisitions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace Diten.Web.Controllers;

// MOD-0141 Requisition (satın alma talebi) — tenant shell MVC controller.
// Route is single-segment /Requisitions (NO area prefix); views live under Views/Procurement/Requisitions/.
// Mirrors the shipped SourcingController direct-gateway profile: server-side proxy for create + the requisition
// submit lifecycle sub-action (Idempotency-Key stays server-side); the DataTable reads and deletes straight
// through the Gateway (window.API.procurement + /api/requisitions).
// REQUISITION-PO contract + shipped backend (3a) expose NO requisition update endpoint (create + submit + delete
// only) → no update proxy fabricated (K12 no-invention; ASSUMPTION-P2P-FE-02).
[Authorize]
[Route("Requisitions")]
public sealed class RequisitionsController : Controller
{
    private const string ViewRoot = "~/Views/Procurement/Requisitions";

    // Contract RequisitionStatus enum ordinals (requisition-po.openapi.yaml). Enums serialize numerically
    // (no JsonStringEnumConverter on the service) → map ordinals to canonical contract names. ASSUMPTION-P2P-FE-01.
    private static readonly string[] RequisitionStatusNames = ["Draft", "Submitted", "Approved", "Rejected", "Converted", "Cancelled"];

    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly IStringLocalizer<SharedResource> _sharedLocalizer;
    private readonly ILogger<RequisitionsController> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public RequisitionsController(
        HttpClient httpClient,
        IConfiguration configuration,
        IStringLocalizer<SharedResource> sharedLocalizer,
        ILogger<RequisitionsController> logger)
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
        View($"{ViewRoot}/Create.cshtml", new RequisitionsEditViewModel());

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([FromForm] RequisitionsEditViewModel model)
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
                $"{_gatewayUrl}/api/requisitions",
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
            _logger.LogError(ex, "Requisition create failed.");
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

    // The REQUISITION-PO contract + shipped backend expose no requisition update endpoint (create + submit only).
    // We do NOT fabricate a gateway update call (K12 no-invention). The Edit page exists for compact file-contract
    // parity; the POST surfaces an honest, localized not-supported message rather than silently discarding input.
    // ASSUMPTION-P2P-FE-02.
    [HttpPost("Edit/{id}")]
    [ValidateAntiForgeryToken]
    public IActionResult Edit(string id, [FromForm] RequisitionsEditViewModel model)
    {
        model.RequisitionId = id;
        NormalizeModel(model);
        ModelState.AddModelError(string.Empty, _sharedLocalizer["RequisitionUpdateNotSupported"].Value);
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
        var model = await LoadRequisitionAsync(id);
        if (model is null)
            return Json(new { success = false });

        return Json(new { success = true, data = model });
    }

    // ── Requisition lifecycle sub-action (Details page, premium-modal driven) — server proxy keeps Idempotency-Key server-side ──

    // JSON sub-action proxy (called by details.js via same-origin fetch). Carries no antiforgery token — auth is the
    // JWT cookie + tenant claim; state-change idempotency is the server-set Idempotency-Key header.
    [HttpPost("submit/{id}")]
    public Task<IActionResult> Submit(string id) =>
        ProxyStateActionAsync(HttpMethod.Post, $"/api/requisitions/{Uri.EscapeDataString(id)}/submit", content: null);

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
            _logger.LogError(ex, "Requisition lifecycle proxy failed for {Path}.", path);
            return StatusCode(StatusCodes.Status502BadGateway, new { success = false, errors = new[] { ex.GetBaseException().Message } });
        }
    }

    private async Task<RequisitionsEditViewModel?> LoadEditModelAsync(string id)
    {
        var requisition = await LoadRequisitionAsync(id);
        return requisition is null ? null : ToEditModel(requisition);
    }

    private async Task<RequisitionApiModel?> LoadRequisitionAsync(string id)
    {
        if (!AddAuthHeaders())
            return null;

        var response = await _httpClient.GetAsync($"{_gatewayUrl}/api/requisitions/{Uri.EscapeDataString(id)}");
        if (!response.IsSuccessStatusCode)
            return null;

        return await ReadDataAsync<RequisitionApiModel>(response);
    }

    private static RequisitionsEditViewModel ToEditModel(RequisitionApiModel model) => new()
    {
        RequisitionId = model.RequisitionId,
        Status = MapStatus(model.Status),
        Justification = model.Justification,
        Lines = (model.Lines is { Count: > 0 })
            ? model.Lines.Select(l => new RequisitionLineInput { ItemId = l.ItemId, SkuId = l.SkuId, Quantity = l.Quantity, UomId = l.UomId, NeedBy = l.NeedBy }).ToList()
            : [new RequisitionLineInput()]
    };

    private static RequisitionUpsertPayload ToPayload(RequisitionsEditViewModel model) => new()
    {
        Justification = string.IsNullOrWhiteSpace(model.Justification) ? null : model.Justification.Trim(),
        Lines = (model.Lines ?? [])
            .Where(l => !string.IsNullOrWhiteSpace(l.ItemId) || !string.IsNullOrWhiteSpace(l.Quantity) || !string.IsNullOrWhiteSpace(l.UomId))
            .Select(l => new RequisitionLinePayload
            {
                ItemId = l.ItemId?.Trim() ?? string.Empty,
                SkuId = string.IsNullOrWhiteSpace(l.SkuId) ? null : l.SkuId.Trim(),
                Quantity = l.Quantity?.Trim() ?? string.Empty,
                UomId = l.UomId?.Trim() ?? string.Empty,
                NeedBy = string.IsNullOrWhiteSpace(l.NeedBy) ? null : l.NeedBy.Trim()
            })
            .ToList()
    };

    private static void NormalizeModel(RequisitionsEditViewModel model)
    {
        model.Lines = (model.Lines ?? [])
            .Where(l => l is not null && (!string.IsNullOrWhiteSpace(l.ItemId) || !string.IsNullOrWhiteSpace(l.Quantity) || !string.IsNullOrWhiteSpace(l.UomId)))
            .ToList();
        if (model.Lines.Count == 0)
            model.Lines.Add(new RequisitionLineInput());
    }

    private static string MapStatus(int? ordinal) =>
        ordinal is >= 0 && ordinal < RequisitionStatusNames.Length ? RequisitionStatusNames[ordinal.Value] : "Draft";

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
