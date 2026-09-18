using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Diten.Web.Models.Procurement.InvoiceMatch;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace Diten.Web.Controllers;

// MOD-0143 Invoice Capture & 3-Way Match — tenant shell MVC controller.
// Route is single-segment /InvoiceMatch (NO area prefix); views live under Views/Procurement/InvoiceMatch/.
// Mirrors the shipped Sourcing/GRN direct-gateway profile: server-side proxy for capture + the lifecycle
// sub-actions (runThreeWayMatch, resolveMatchException — Idempotency-Key stays server-side); the DataTable
// reads the exception queue and deletes straight through the Gateway (window.API.procurement + /api/invoice-match).
// The MATCH contract exposes NO invoice update endpoint (capture + match + resolve only) → no update proxy;
// the Edit page exists only for compact file-contract parity (ASSUMPTION-P2P-FE-01).
// ASSUMPTION-P2P-FE-02: the contract exposes no GetInvoiceList endpoint — the only list surface is GET /exceptions.
// The Index DataTable is therefore backed by the exception queue (each row is an invoice with an open/resolved
// exception); individual invoices are reached via Details (GET /invoices/{id}) from the queue, a create redirect,
// or a direct URL. No list endpoint is fabricated (K12 no-invention).
[Authorize]
[Route("InvoiceMatch")]
public sealed class InvoiceMatchController : Controller
{
    private const string ViewRoot = "~/Views/Procurement/InvoiceMatch";

    // InvoiceStatus enum ordinals (Invoice.cs). Enums serialize numerically (no JsonStringEnumConverter on
    // the service) → map ordinals to canonical contract names. ASSUMPTION-P2P-FE-03.
    private static readonly string[] InvoiceStatusNames =
        ["Captured", "Matched", "MatchedWithinTolerance", "Exception", "Rejected", "ClearedForPayment"];

    // MatchExceptionReason / MatchExceptionStatus enum ordinals (MatchException.cs).
    private static readonly string[] ExceptionReasonNames =
        ["QtyMismatch", "PriceMismatch", "AmountMismatch", "NoReceipt", "NoPo", "DuplicateInvoice", "CurrencyMismatch"];
    private static readonly string[] ExceptionStatusNames = ["Open", "Resolved"];

    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly IStringLocalizer<SharedResource> _sharedLocalizer;
    private readonly ILogger<InvoiceMatchController> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public InvoiceMatchController(
        HttpClient httpClient,
        IConfiguration configuration,
        IStringLocalizer<SharedResource> sharedLocalizer,
        ILogger<InvoiceMatchController> logger)
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
        View($"{ViewRoot}/Create.cshtml", new InvoiceMatchEditViewModel());

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([FromForm] InvoiceMatchEditViewModel model)
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
                $"{_gatewayUrl}/api/invoice-match/invoices",
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
            _logger.LogError(ex, "Invoice capture failed.");
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

    // The MATCH contract exposes no update endpoint (capture + match + resolve only; an invoice is corrected by
    // re-matching or resolving an exception, not by an edit). We do NOT fabricate a gateway update call
    // (K12 no-invention). The Edit page exists for compact file-contract parity; the POST surfaces an honest,
    // localized not-supported message rather than silently discarding input. ASSUMPTION-P2P-FE-01.
    [HttpPost("Edit/{id}")]
    [ValidateAntiForgeryToken]
    public IActionResult Edit(string id, [FromForm] InvoiceMatchEditViewModel model)
    {
        model.InvoiceId = id;
        NormalizeModel(model);
        ModelState.AddModelError(string.Empty, _sharedLocalizer["InvoiceUpdateNotSupported"].Value);
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

        // Best-effort: surface the open exception (if any) for this invoice so Details can offer Resolve.
        // Uses the real GET /exceptions endpoint (no invention); failure leaves the Resolve action hidden.
        await PopulateOpenExceptionAsync(model);
        return View($"{ViewRoot}/Details.cshtml", model);
    }

    [HttpGet("get/{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var model = await LoadInvoiceAsync(id);
        if (model is null)
            return Json(new { success = false });

        return Json(new { success = true, data = model });
    }

    // ── Invoice lifecycle sub-actions (premium-modal driven) — server proxy keeps Idempotency-Key server-side ──

    // runThreeWayMatch — PO ↔ GRN ↔ Invoice policy-driven match. Returns the MatchOutcome (result + variances).
    [HttpPost("match/{id}")]
    public Task<IActionResult> Match(string id) =>
        ProxyStateActionAsync(HttpMethod.Post, $"/api/invoice-match/invoices/{Uri.EscapeDataString(id)}/match", content: null);

    // resolveMatchException — approve/reject/tolerance-override under approval trail (MOD-0023) + audit.
    [HttpPost("resolve/{id}")]
    public Task<IActionResult> Resolve(string id, [FromBody] ResolveRequest? request) =>
        ProxyStateActionAsync(
            HttpMethod.Post,
            $"/api/invoice-match/exceptions/{Uri.EscapeDataString(id)}/resolve",
            content: new { decision = request?.Decision ?? string.Empty, note = request?.Note });

    // listMatchExceptions passthrough (JSON) — used by the exception-queue DataTable filter when a reasonCode
    // is applied server-side. Mirrors SuppliersController.Validate: no antiforgery token; auth is the JWT cookie
    // + tenant claim. Read-only, so no Idempotency-Key.
    [HttpGet("exceptions")]
    public async Task<IActionResult> Exceptions([FromQuery] string? reasonCode, [FromQuery] string? cursor)
    {
        if (!AddAuthHeaders())
            return StatusCode(StatusCodes.Status401Unauthorized, new { success = false });

        try
        {
            var query = new List<string>();
            if (!string.IsNullOrWhiteSpace(reasonCode)) query.Add($"reasonCode={Uri.EscapeDataString(reasonCode)}");
            if (!string.IsNullOrWhiteSpace(cursor)) query.Add($"cursor={Uri.EscapeDataString(cursor)}");
            var suffix = query.Count > 0 ? "?" + string.Join("&", query) : string.Empty;

            var response = await _httpClient.GetAsync($"{_gatewayUrl}/api/invoice-match/exceptions{suffix}");
            var raw = await response.Content.ReadAsStringAsync();
            Response.StatusCode = (int)response.StatusCode;
            return string.IsNullOrWhiteSpace(raw) ? StatusCode((int)response.StatusCode) : Content(raw, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Invoice-match exception list proxy failed.");
            return StatusCode(StatusCodes.Status502BadGateway, new { success = false });
        }
    }

    public sealed record ResolveRequest(string? Decision, string? Note);

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
            _logger.LogError(ex, "Invoice-match lifecycle proxy failed for {Path}.", path);
            return StatusCode(StatusCodes.Status502BadGateway, new { success = false, errors = new[] { ex.GetBaseException().Message } });
        }
    }

    private async Task<InvoiceMatchEditViewModel?> LoadEditModelAsync(string id)
    {
        var invoice = await LoadInvoiceAsync(id);
        return invoice is null ? null : ToEditModel(invoice);
    }

    private async Task<InvoiceResponseApiModel?> LoadInvoiceAsync(string id)
    {
        if (!AddAuthHeaders())
            return null;

        var response = await _httpClient.GetAsync($"{_gatewayUrl}/api/invoice-match/invoices/{Uri.EscapeDataString(id)}");
        if (!response.IsSuccessStatusCode)
            return null;

        return await ReadDataAsync<InvoiceResponseApiModel>(response);
    }

    private async Task PopulateOpenExceptionAsync(InvoiceMatchEditViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.InvoiceId) || !AddAuthHeaders())
            return;

        try
        {
            var response = await _httpClient.GetAsync($"{_gatewayUrl}/api/invoice-match/exceptions");
            if (!response.IsSuccessStatusCode)
                return;

            var page = await ReadDataAsync<MatchExceptionListData>(response);
            var open = page?.Items?.FirstOrDefault(e =>
                string.Equals(e.InvoiceId, model.InvoiceId, StringComparison.OrdinalIgnoreCase)
                && MapExceptionStatus(e.Status) == "Open");
            if (open is null)
                return;

            model.ExceptionId = open.ExceptionId;
            model.ExceptionReason = MapExceptionReason(open.ReasonCode);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Invoice-match open-exception lookup failed for {InvoiceId}.", model.InvoiceId);
        }
    }

    private static InvoiceMatchEditViewModel ToEditModel(InvoiceResponseApiModel model) => new()
    {
        InvoiceId = model.InvoiceId,
        SupplierId = model.SupplierId ?? string.Empty,
        PoId = model.PoId ?? string.Empty,
        InvoiceNumber = model.InvoiceNumber ?? string.Empty,
        Currency = model.Currency ?? string.Empty,
        Status = MapStatus(model.Status),
        TotalAmount = model.TotalAmount,
        Lines = (model.Lines is { Count: > 0 })
            ? model.Lines.Select(l => new InvoiceLineInput
            {
                PoLineId = l.PoLineId,
                ItemId = l.ItemId,
                Quantity = l.Quantity,
                UnitPrice = l.UnitPrice,
                LineAmount = l.LineAmount
            }).ToList()
            : [new InvoiceLineInput()]
    };

    private static InvoiceRequestPayload ToPayload(InvoiceMatchEditViewModel model) => new()
    {
        SupplierId = model.SupplierId?.Trim() ?? string.Empty,
        PoId = model.PoId?.Trim() ?? string.Empty,
        InvoiceNumber = model.InvoiceNumber?.Trim() ?? string.Empty,
        Currency = model.Currency?.Trim() ?? string.Empty,
        SourceSystem = string.IsNullOrWhiteSpace(model.SourceSystem) ? null : model.SourceSystem.Trim(),
        ExternalRef = string.IsNullOrWhiteSpace(model.ExternalRef) ? null : model.ExternalRef.Trim(),
        Lines = (model.Lines ?? [])
            .Where(l => l is not null && HasLineContent(l))
            .Select(l => new InvoiceLinePayload
            {
                PoLineId = string.IsNullOrWhiteSpace(l.PoLineId) ? null : l.PoLineId.Trim(),
                ItemId = l.ItemId?.Trim() ?? string.Empty,
                Quantity = l.Quantity?.Trim() ?? string.Empty,
                UnitPrice = l.UnitPrice?.Trim() ?? string.Empty,
                LineAmount = string.IsNullOrWhiteSpace(l.LineAmount) ? null : l.LineAmount.Trim()
            })
            .ToList()
    };

    private static bool HasLineContent(InvoiceLineInput l) =>
        !string.IsNullOrWhiteSpace(l.ItemId) || !string.IsNullOrWhiteSpace(l.Quantity)
        || !string.IsNullOrWhiteSpace(l.UnitPrice) || !string.IsNullOrWhiteSpace(l.LineAmount)
        || !string.IsNullOrWhiteSpace(l.PoLineId);

    private static void NormalizeModel(InvoiceMatchEditViewModel model)
    {
        model.Lines = (model.Lines ?? [])
            .Where(l => l is not null && HasLineContent(l))
            .ToList();
        if (model.Lines.Count == 0)
            model.Lines.Add(new InvoiceLineInput());
    }

    private static string MapStatus(int? ordinal) =>
        ordinal is >= 0 && ordinal < InvoiceStatusNames.Length ? InvoiceStatusNames[ordinal.Value] : "Captured";

    private static string MapExceptionReason(int? ordinal) =>
        ordinal is >= 0 && ordinal < ExceptionReasonNames.Length ? ExceptionReasonNames[ordinal.Value] : string.Empty;

    private static string MapExceptionStatus(int? ordinal) =>
        ordinal is >= 0 && ordinal < ExceptionStatusNames.Length ? ExceptionStatusNames[ordinal.Value] : "Open";

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
