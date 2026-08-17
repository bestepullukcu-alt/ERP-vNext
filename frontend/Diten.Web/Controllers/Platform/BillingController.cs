using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Diten.Web.Models.Billing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Diten.Web.Controllers.Platform;

[Route("Platform/Billing")]
public sealed class BillingController : Controller
{
    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly IStringLocalizer<SharedResource> _sharedLocalizer;
    private readonly ILogger<BillingController> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public BillingController(
        HttpClient httpClient,
        IConfiguration configuration,
        IStringLocalizer<SharedResource> sharedLocalizer,
        ILogger<BillingController> logger)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"] ?? "http://localhost:5000";
        _sharedLocalizer = sharedLocalizer;
        _logger = logger;
    }

    [HttpGet("")]
    public IActionResult Index() => View("~/Views/Platform/Billing/Index.cshtml");

    [AllowAnonymous]
    [HttpGet("/Billing")]
    public IActionResult LegacyIndexRedirect() => RedirectToAction(nameof(Index));

    [HttpGet("Create")]
    public IActionResult Create() => View("~/Views/Platform/Billing/Create.cshtml", new BillingInvoiceEditViewModel());

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([FromForm] BillingInvoiceEditViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View("~/Views/Platform/Billing/Create.cshtml", model);
        }

        AddAuthHeader();
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"{_gatewayUrl}/api/platform/billing/invoices", ToPayload(model), _jsonOptions);
            if (response.IsSuccessStatusCode)
            {
                TempData["SuccessMessage"] = _sharedLocalizer["RecordCreated"].Value;
                return RedirectToAction(nameof(Index));
            }

            AddGatewayErrorsToModelState(await ExtractGatewayErrorsAsync(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Billing invoice create failed.");
            ModelState.AddModelError(string.Empty, _sharedLocalizer["GatewayError"].Value);
        }

        return View("~/Views/Platform/Billing/Create.cshtml", model);
    }

    [HttpGet("Edit/{id:guid}")]
    public async Task<IActionResult> Edit(Guid id)
    {
        var detail = await LoadInvoiceAsync(id);
        if (detail is null)
        {
            TempData["ErrorMessage"] = _sharedLocalizer["GatewayError"].Value;
            return RedirectToAction(nameof(Index));
        }

        if (!string.Equals(detail.Status, "Draft", StringComparison.OrdinalIgnoreCase))
        {
            TempData["ErrorMessage"] = "Only draft invoices can be edited.";
            return RedirectToAction(nameof(Details), new { id });
        }

        return View("~/Views/Platform/Billing/Edit.cshtml", ToEditModel(detail));
    }

    [HttpPost("Edit/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, [FromForm] BillingInvoiceEditViewModel model)
    {
        model.Id = id;
        if (!ModelState.IsValid)
        {
            return View("~/Views/Platform/Billing/Edit.cshtml", model);
        }

        AddAuthHeader();
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"{_gatewayUrl}/api/platform/billing/invoices/{id}", ToPayload(model), _jsonOptions);
            if (response.IsSuccessStatusCode)
            {
                TempData["SuccessMessage"] = _sharedLocalizer["RecordUpdated"].Value;
                return RedirectToAction(nameof(Details), new { id });
            }

            AddGatewayErrorsToModelState(await ExtractGatewayErrorsAsync(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Billing invoice edit failed for {InvoiceId}.", id);
            ModelState.AddModelError(string.Empty, _sharedLocalizer["GatewayError"].Value);
        }

        return View("~/Views/Platform/Billing/Edit.cshtml", model);
    }

    [HttpGet("Details/{id:guid}")]
    public async Task<IActionResult> Details(Guid id)
    {
        var detail = await LoadInvoiceAsync(id);
        if (detail is null)
        {
            TempData["ErrorMessage"] = _sharedLocalizer["GatewayError"].Value;
            return RedirectToAction(nameof(Index));
        }

        return View("~/Views/Platform/Billing/Details.cshtml", detail);
    }

    [HttpGet("api")]
    public Task<IActionResult> ListProxy()
    {
        return ProxyGatewayAsync(HttpMethod.Get, $"{_gatewayUrl}/api/platform/billing/invoices{Request.QueryString}");
    }

    [HttpGet("api/{id:guid}")]
    public Task<IActionResult> DetailProxy(Guid id)
    {
        return ProxyGatewayAsync(HttpMethod.Get, $"{_gatewayUrl}/api/platform/billing/invoices/{id}");
    }

    [HttpGet("api/plans")]
    public Task<IActionResult> PlansProxy()
    {
        return ProxyGatewayAsync(HttpMethod.Get, $"{_gatewayUrl}/api/platform/billing/plans");
    }

    [HttpPost("api/{id:guid}/issue")]
    public Task<IActionResult> IssueProxy(Guid id)
    {
        return ProxyGatewayAsync(HttpMethod.Post, $"{_gatewayUrl}/api/platform/billing/invoices/{id}/issue");
    }

    [HttpPost("api/{id:guid}/cancel")]
    public Task<IActionResult> CancelProxy(Guid id)
    {
        return ProxyGatewayAsync(HttpMethod.Post, $"{_gatewayUrl}/api/platform/billing/invoices/{id}/cancel");
    }

    [HttpPost("api/{id:guid}/payments")]
    public async Task<IActionResult> PaymentProxy(Guid id)
    {
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var body = await reader.ReadToEndAsync();
        return await ProxyGatewayAsync(HttpMethod.Post, $"{_gatewayUrl}/api/platform/billing/invoices/{id}/payments", body);
    }

    [HttpPost("api/payments/{paymentRecordId:guid}/refunds")]
    public async Task<IActionResult> RefundProxy(Guid paymentRecordId)
    {
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var body = await reader.ReadToEndAsync();
        return await ProxyGatewayAsync(HttpMethod.Post, $"{_gatewayUrl}/api/platform/billing/payments/{paymentRecordId}/refunds", body);
    }

    private async Task<BillingInvoiceDetailViewModel?> LoadInvoiceAsync(Guid id)
    {
        AddAuthHeader();
        var response = await _httpClient.GetAsync($"{_gatewayUrl}/api/platform/billing/invoices/{id}");
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var payload = await response.Content.ReadFromJsonAsync<BillingGatewayResponse<BillingInvoiceDetailViewModel>>(_jsonOptions);
        return payload?.Data;
    }

    private async Task<IActionResult> ProxyGatewayAsync(HttpMethod method, string targetUrl, string? jsonBody = null)
    {
        AddAuthHeader();
        using var request = new HttpRequestMessage(method, targetUrl);
        if (jsonBody is not null)
        {
            request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
        }

        var response = await _httpClient.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();
        var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/json";
        return new ContentResult
        {
            Content = content,
            ContentType = contentType,
            StatusCode = (int)response.StatusCode
        };
    }

    private void AddAuthHeader()
    {
        _httpClient.DefaultRequestHeaders.Authorization = null;
        var token = Request.Cookies["access_token"];
        if (!string.IsNullOrWhiteSpace(token))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        if (_httpClient.DefaultRequestHeaders.Contains("X-Tenant-Id"))
        {
            _httpClient.DefaultRequestHeaders.Remove("X-Tenant-Id");
        }
    }

    private void AddGatewayErrorsToModelState(IEnumerable<string> errors)
    {
        foreach (var error in errors)
        {
            ModelState.AddModelError(string.Empty, error);
        }
    }

    private async Task<List<string>> ExtractGatewayErrorsAsync(HttpResponseMessage response)
    {
        try
        {
            var payload = await response.Content.ReadFromJsonAsync<BillingGatewayResponse<object>>(_jsonOptions);
            if (payload?.Errors.Count > 0)
            {
                return payload.Errors;
            }
        }
        catch { }

        var raw = await response.Content.ReadAsStringAsync();
        return [string.IsNullOrWhiteSpace(raw) ? _sharedLocalizer["GatewayError"].Value : raw];
    }

    private static BillingInvoiceEditViewModel ToEditModel(BillingInvoiceDetailViewModel model)
    {
        var line = model.Lines.FirstOrDefault();
        return new BillingInvoiceEditViewModel
        {
            Id = model.Id,
            BillingPlanId = model.BillingPlanId,
            CustomerReference = model.CustomerReference,
            DueDate = model.DueDate,
            Notes = model.Notes,
            LineDescription = line?.Description ?? string.Empty,
            Quantity = line?.Quantity,
            UnitPrice = line?.UnitPrice,
            DiscountAmount = line?.DiscountAmount,
            TaxAmount = line?.TaxAmount
        };
    }

    private static BillingInvoiceSavePayload ToPayload(BillingInvoiceEditViewModel model) => new()
    {
        BillingPlanId = model.BillingPlanId!.Value,
        CustomerReference = model.CustomerReference,
        DueDate = model.DueDate,
        Notes = model.Notes,
        Lines =
        [
            new BillingInvoiceLineSavePayload
            {
                Description = model.LineDescription,
                Quantity = model.Quantity ?? 0,
                UnitPrice = model.UnitPrice ?? 0,
                DiscountAmount = model.DiscountAmount ?? 0,
                TaxAmount = model.TaxAmount ?? 0
            }
        ]
    };
}
