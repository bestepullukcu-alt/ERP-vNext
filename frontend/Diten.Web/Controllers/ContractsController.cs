using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Diten.Web.Models.Procurement.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace Diten.Web.Controllers;

// MOD-0144 Contracting & Clause Library — tenant shell MVC controller.
// Route is single-segment /Contracts (NO area prefix); views live under Views/Procurement/Contracts/.
// Mirrors the shipped SourcingController/SuppliersController direct-gateway profile: server-side proxy for
// create + the lifecycle sub-actions (activate/terminate — Idempotency-Key stays server-side), the Draft-only
// update (PATCH), the supplier lookup, and the clause library (GET/POST). The DataTable reads and deletes
// straight through the Gateway (window.API.procurement + /api/contracts). Supplier (MOD-0140) and award/rfx
// (MOD-0145) are CONSUMED, never created (fail-closed 404 UNKNOWN_REFERENCE server-side).
[Authorize]
[Route("Contracts")]
public sealed class ContractsController : Controller
{
    private const string ViewRoot = "~/Views/Procurement/Contracts";

    // ContractStatus enum ordinals (Contract.cs). The service serializes enums numerically
    // (no JsonStringEnumConverter) → map ordinals to canonical contract names. Dates are contract date strings.
    private static readonly string[] StatusNames = ["Draft", "InReview", "Active", "Expired", "Terminated"];

    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly IStringLocalizer<SharedResource> _sharedLocalizer;
    private readonly ILogger<ContractsController> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public ContractsController(
        HttpClient httpClient,
        IConfiguration configuration,
        IStringLocalizer<SharedResource> sharedLocalizer,
        ILogger<ContractsController> logger)
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
        View($"{ViewRoot}/Create.cshtml", new ContractEditViewModel());

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([FromForm] ContractEditViewModel model)
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
                $"{_gatewayUrl}/api/contracts",
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
            _logger.LogError(ex, "Contract create failed.");
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

    // Backend exposes PATCH /api/contracts/{id} (Draft-only; non-Draft → 409 INVALID_STATE). The Edit page is
    // functional for Draft contracts; for non-Draft the form surfaces an honest, localized message and the
    // backend stays authoritative (K12 no-invention). ASSUMPTION-0144-01 additive update surface.
    [HttpPost("Edit/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, [FromForm] ContractEditViewModel model)
    {
        model.ContractId = id;
        NormalizeModel(model);

        if (!string.Equals(model.Status, "Draft", StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(string.Empty, _sharedLocalizer["ContractEditDraftOnly"].Value);
            return View($"{ViewRoot}/Edit.cshtml", model);
        }

        if (!ModelState.IsValid)
            return View($"{ViewRoot}/Edit.cshtml", model);

        if (!AddAuthHeaders())
        {
            ModelState.AddModelError(string.Empty, _sharedLocalizer["Unauthorized"].Value);
            return View($"{ViewRoot}/Edit.cshtml", model);
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Patch, $"{_gatewayUrl}/api/contracts/{Uri.EscapeDataString(id)}")
            {
                Content = JsonContent.Create(ToPayload(model), options: _jsonOptions)
            };
            var response = await _httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                TempData["SuccessMessage"] = _sharedLocalizer["RecordUpdated"].Value;
                return RedirectToAction(nameof(Index));
            }

            AddGatewayErrorsToModelState(await ExtractGatewayErrorsAsync(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Contract update failed for {ContractId}.", id);
            AddGatewayErrorsToModelState(BuildExceptionErrors(ex));
        }

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
        var model = await LoadContractAsync(id);
        if (model is null)
            return Json(new { success = false });

        return Json(new { success = true, data = model });
    }

    // Supplier lookup — consumes the SUPPLIER surface (MOD-0140); consume-only, fail-closed if empty.
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
            _logger.LogError(ex, "Contract supplier lookup load failed.");
            return Json(EmptyLookups());
        }
    }

    // ── Clause library (contract GET/POST /api/contracts/clauses) — consumed by index.js via same-origin fetch ──

    [HttpGet("clauses")]
    public async Task<IActionResult> Clauses([FromQuery] string? category, [FromQuery] string? cursor)
    {
        if (!AddAuthHeaders())
            return Json(new { success = false });

        try
        {
            var query = BuildClauseQuery(category, cursor);
            var response = await _httpClient.GetAsync($"{_gatewayUrl}/api/contracts/clauses{query}");
            var raw = await response.Content.ReadAsStringAsync();
            return Content(raw, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Clause library load failed.");
            return Json(new { success = false });
        }
    }

    // JSON sub-action proxy (called by index.js via same-origin fetch). Carries no antiforgery token — auth is
    // the JWT cookie + tenant claim; create idempotency is the server-set Idempotency-Key header.
    [HttpPost("clauses")]
    public Task<IActionResult> CreateClause([FromBody] ClauseUpsertPayload body) =>
        ProxyStateActionAsync(HttpMethod.Post, "/api/contracts/clauses", body);

    // ── Contract lifecycle sub-actions (Details page, premium-modal driven) — server proxy keeps Idempotency-Key server-side ──

    [HttpPost("activate/{id}")]
    public Task<IActionResult> Activate(string id) =>
        ProxyStateActionAsync(HttpMethod.Post, $"/api/contracts/{Uri.EscapeDataString(id)}/activate", content: null);

    [HttpPost("terminate/{id}")]
    public Task<IActionResult> Terminate(string id) =>
        ProxyStateActionAsync(HttpMethod.Post, $"/api/contracts/{Uri.EscapeDataString(id)}/terminate", content: null);

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
            _logger.LogError(ex, "Contract lifecycle proxy failed for {Path}.", path);
            return StatusCode(StatusCodes.Status502BadGateway, new { success = false, errors = new[] { ex.GetBaseException().Message } });
        }
    }

    private async Task<ContractEditViewModel?> LoadEditModelAsync(string id)
    {
        var contract = await LoadContractAsync(id);
        return contract is null ? null : ToEditModel(contract);
    }

    private async Task<ContractApiModel?> LoadContractAsync(string id)
    {
        if (!AddAuthHeaders())
            return null;

        var response = await _httpClient.GetAsync($"{_gatewayUrl}/api/contracts/{Uri.EscapeDataString(id)}");
        if (!response.IsSuccessStatusCode)
            return null;

        return await ReadDataAsync<ContractApiModel>(response);
    }

    private static ContractEditViewModel ToEditModel(ContractApiModel model) => new()
    {
        ContractId = model.ContractId,
        SupplierId = model.SupplierId,
        RfxId = model.RfxId,
        Title = model.Title,
        EffectiveFrom = ParseDate(model.EffectiveFrom),
        EffectiveTo = ParseDate(model.EffectiveTo),
        Currency = model.Currency,
        Status = MapStatus(model.Status),
        WorkflowInstanceId = model.WorkflowInstanceId,
        Clauses = (model.Clauses is { Count: > 0 })
            ? model.Clauses.Select(c => new ContractClauseInput
            {
                ClauseId = c.ClauseId,
                Deviation = c.Deviation ?? false,
                DeviationText = c.DeviationText
            }).ToList()
            : [new ContractClauseInput()],
        EvidenceRefs = model.EvidenceRefs ?? []
    };

    private static ContractUpsertPayload ToPayload(ContractEditViewModel model) => new()
    {
        SupplierId = model.SupplierId?.Trim() ?? string.Empty,
        RfxId = string.IsNullOrWhiteSpace(model.RfxId) ? null : model.RfxId.Trim(),
        Title = model.Title?.Trim() ?? string.Empty,
        EffectiveFrom = FormatDate(model.EffectiveFrom),
        EffectiveTo = FormatDate(model.EffectiveTo),
        Currency = string.IsNullOrWhiteSpace(model.Currency) ? null : model.Currency.Trim(),
        Clauses = (model.Clauses ?? [])
            .Where(c => !string.IsNullOrWhiteSpace(c.ClauseId))
            .Select(c => new ClauseRefPayload
            {
                ClauseId = c.ClauseId!.Trim(),
                Deviation = c.Deviation,
                DeviationText = string.IsNullOrWhiteSpace(c.DeviationText) ? null : c.DeviationText.Trim()
            })
            .ToList(),
        EvidenceRefs = (model.EvidenceRefs ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .ToList()
    };

    private static void NormalizeModel(ContractEditViewModel model)
    {
        model.EvidenceRefs = (model.EvidenceRefs ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        model.Clauses = (model.Clauses ?? [])
            .Where(c => c is not null && !string.IsNullOrWhiteSpace(c.ClauseId))
            .ToList();
        if (model.Clauses.Count == 0)
            model.Clauses.Add(new ContractClauseInput());

        model.Status = string.IsNullOrWhiteSpace(model.Status) ? "Draft" : model.Status;
    }

    private static string MapStatus(int? ordinal) =>
        ordinal is >= 0 && ordinal < StatusNames.Length ? StatusNames[ordinal.Value] : "Draft";

    private static DateTime? ParseDate(string? value) =>
        DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed) ? parsed : null;

    private static string? FormatDate(DateTime? value) =>
        value?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string BuildClauseQuery(string? category, string? cursor)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(category))
            parts.Add($"category={Uri.EscapeDataString(category)}");
        if (!string.IsNullOrWhiteSpace(cursor))
            parts.Add($"cursor={Uri.EscapeDataString(cursor)}");
        return parts.Count > 0 ? "?" + string.Join("&", parts) : string.Empty;
    }

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
