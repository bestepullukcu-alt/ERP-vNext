using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Diten.Web.Models.GlobalProducts;
using Diten.Web.Views.MasterDataManagement.GlobalProducts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Diten.Web.Controllers;

[Authorize]
[Route("MasterDataManagement/GlobalProducts")]
public sealed class GlobalProductsController : Controller
{
    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly IStringLocalizer<SharedResource> _sharedLocalizer;
    private readonly IStringLocalizer<GlobalProductsIndex> _localizer;
    private readonly ILogger<GlobalProductsController> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public GlobalProductsController(
        HttpClient httpClient,
        IConfiguration configuration,
        IStringLocalizer<SharedResource> sharedLocalizer,
        IStringLocalizer<GlobalProductsIndex> localizer,
        ILogger<GlobalProductsController> logger)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"]?.TrimEnd('/')
            ?? throw new InvalidOperationException("GatewayUrl configuration is required.");
        _sharedLocalizer = sharedLocalizer;
        _localizer = localizer;
        _logger = logger;
    }

    [HttpGet("")]
    public IActionResult Index() => View("~/Views/MasterDataManagement/GlobalProducts/Index.cshtml");

    [HttpGet("api")]
    public Task<IActionResult> List(CancellationToken cancellationToken) =>
        ProxyGatewayAsync(HttpMethod.Get, $"{_gatewayUrl}/api/global-products{Request.QueryString}", null, cancellationToken);

    [HttpGet("api/{id:guid}")]
    public Task<IActionResult> Detail(Guid id, CancellationToken cancellationToken) =>
        ProxyGatewayAsync(HttpMethod.Get, $"{_gatewayUrl}/api/global-products/{id:D}", null, cancellationToken);

    [HttpGet("api/selector")]
    public Task<IActionResult> Selector(CancellationToken cancellationToken) =>
        ProxyGatewayAsync(HttpMethod.Get, $"{_gatewayUrl}/api/global-products/selector{Request.QueryString}", null, cancellationToken);

    [HttpPost("api")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        [FromForm] CreateGlobalProductViewModel model,
        CancellationToken cancellationToken)
    {
        model.GlobalProductName = model.GlobalProductName?.Trim() ?? string.Empty;
        if (!ModelState.IsValid || string.IsNullOrWhiteSpace(model.GlobalProductName))
        {
            return BadRequest(new
            {
                success = false,
                errors = new[] { _localizer["GlobalProductNameRequired"].Value }
            });
        }

        var operationKey = Guid.NewGuid().ToString("N");

        try
        {
            var reservation = await ReserveCodeAsync(model.GlobalProductName, operationKey, cancellationToken);
            if (!reservation.Success || reservation.Data is null)
                return StatusCode(reservation.StatusCode, new { success = false, errors = reservation.Errors });

            var draft = await CreateDraftAsync(
                model.GlobalProductName,
                reservation.Data.ReservationId,
                reservation.Data.Version,
                operationKey,
                cancellationToken);

            if (!draft.Success || draft.Data is null)
                return StatusCode(draft.StatusCode, new { success = false, errors = draft.Errors });

            return StatusCode(draft.StatusCode, new { success = true, data = draft.Data });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Global Product create proxy flow failed.");
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                success = false,
                errors = new[] { _sharedLocalizer["GatewayError"].Value }
            });
        }
    }

    private async Task<GatewayCallResult<CodeReservationViewModel>> ReserveCodeAsync(
        string globalProductName,
        string operationKey,
        CancellationToken cancellationToken)
    {
        var payload = new { globalProductName, idempotencyKey = operationKey + ":reserve" };
        return await SendGatewayAsync<CodeReservationViewModel>(
            HttpMethod.Post,
            $"{_gatewayUrl}/api/global-products/code-reservations",
            JsonContent.Create(payload, options: _jsonOptions),
            cancellationToken);
    }

    private async Task<GatewayCallResult<GlobalProductDraftViewModel>> CreateDraftAsync(
        string globalProductName,
        Guid reservationId,
        int expectedReservationVersion,
        string operationKey,
        CancellationToken cancellationToken)
    {
        var payload = new
        {
            globalProductName,
            reservationId,
            expectedReservationVersion,
            idempotencyKey = operationKey + ":draft"
        };

        return await SendGatewayAsync<GlobalProductDraftViewModel>(
            HttpMethod.Post,
            $"{_gatewayUrl}/api/global-products/drafts",
            JsonContent.Create(payload, options: _jsonOptions),
            cancellationToken);
    }

    private async Task<IActionResult> ProxyGatewayAsync(
        HttpMethod method,
        string targetUrl,
        HttpContent? content,
        CancellationToken cancellationToken)
    {
        if (!TryCreateRequest(method, targetUrl, content, out var request))
            return Unauthorized(new { errors = new[] { _sharedLocalizer["Unauthorized"].Value } });

        try
        {
            using (request)
            using (var response = await _httpClient.SendAsync(request, cancellationToken))
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                return new ContentResult
                {
                    StatusCode = (int)response.StatusCode,
                    ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/json",
                    Content = body
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Global Product gateway proxy failed for {TargetUrl}.", targetUrl);
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                errors = new[] { _sharedLocalizer["GatewayError"].Value }
            });
        }
    }

    private async Task<GatewayCallResult<T>> SendGatewayAsync<T>(
        HttpMethod method,
        string targetUrl,
        HttpContent content,
        CancellationToken cancellationToken)
        where T : class
    {
        if (!TryCreateRequest(method, targetUrl, content, out var request))
        {
            return GatewayCallResult<T>.Failure(
                StatusCodes.Status401Unauthorized,
                [_sharedLocalizer["Unauthorized"].Value]);
        }

        using (request)
        using (var response = await _httpClient.SendAsync(request, cancellationToken))
        {
            GatewayResponse<T>? payload = null;
            try
            {
                payload = await response.Content.ReadFromJsonAsync<GatewayResponse<T>>(_jsonOptions, cancellationToken);
            }
            catch (JsonException)
            {
                // The raw response is converted into a bounded gateway error below.
            }

            if (response.IsSuccessStatusCode && payload?.Data is not null)
                return GatewayCallResult<T>.Succeeded((int)response.StatusCode, payload.Data);

            var errors = payload?.Errors.Where(error => !string.IsNullOrWhiteSpace(error)).ToList() ?? [];
            if (errors.Count == 0)
                errors.Add(MapStatusMessage(response.StatusCode));

            return GatewayCallResult<T>.Failure((int)response.StatusCode, errors);
        }
    }

    private string MapStatusMessage(HttpStatusCode statusCode) => statusCode switch
    {
        HttpStatusCode.Unauthorized => _sharedLocalizer["Unauthorized"].Value,
        HttpStatusCode.Forbidden => _localizer["ErrorForbidden"].Value,
        HttpStatusCode.NotFound => _localizer["ErrorNotFound"].Value,
        HttpStatusCode.Conflict => _localizer["ErrorConflict"].Value,
        _ => _sharedLocalizer["GatewayError"].Value
    };

    private bool TryCreateRequest(HttpMethod method, string url, HttpContent? content, out HttpRequestMessage request)
    {
        request = new HttpRequestMessage(method, url);

        var token = Diten.Web.Services.Auth.AuthTokenCookies.GetAccessToken(Request);
        if (!string.IsNullOrWhiteSpace(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var tenantValue = User.Claims.FirstOrDefault(claim =>
            claim.Type == "tenantId" ||
            claim.Type == "tenant_id" ||
            claim.Type.EndsWith("/tenantId", StringComparison.OrdinalIgnoreCase))?.Value;

        if (!Guid.TryParse(tenantValue, out var tenantId))
        {
            request.Dispose();
            request = null!;
            return false;
        }

        request.Headers.Add("X-Tenant-Id", tenantId.ToString("D"));
        request.Content = content;
        return true;
    }

    private sealed record GatewayCallResult<T>(bool Success, int StatusCode, T? Data, IReadOnlyList<string> Errors)
        where T : class
    {
        public static GatewayCallResult<T> Succeeded(int statusCode, T data) => new(true, statusCode, data, []);
        public static GatewayCallResult<T> Failure(int statusCode, IReadOnlyList<string> errors) => new(false, statusCode, default, errors);
    }
}
