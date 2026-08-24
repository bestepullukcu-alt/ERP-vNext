using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Diten.Web.Models.FinishedGoods;
using Diten.Web.Views.MasterDataManagement.FinishedGoods;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Diten.Web.Controllers;

[Authorize]
[Route("MasterDataManagement/FinishedGoods")]
public sealed class FinishedGoodsController : Controller
{
    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly IStringLocalizer<SharedResource> _sharedLocalizer;
    private readonly IStringLocalizer<FinishedGoodsIndex> _localizer;
    private readonly ILogger<FinishedGoodsController> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public FinishedGoodsController(
        HttpClient httpClient,
        IConfiguration configuration,
        IStringLocalizer<SharedResource> sharedLocalizer,
        IStringLocalizer<FinishedGoodsIndex> localizer,
        ILogger<FinishedGoodsController> logger)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"]?.TrimEnd('/')
            ?? throw new InvalidOperationException("GatewayUrl configuration is required.");
        _sharedLocalizer = sharedLocalizer;
        _localizer = localizer;
        _logger = logger;
    }

    [HttpGet("")]
    public IActionResult Index() => View("~/Views/MasterDataManagement/FinishedGoods/Index.cshtml");

    [HttpGet("api")]
    public Task<IActionResult> List(CancellationToken cancellationToken) =>
        ProxyGatewayAsync(
            HttpMethod.Get,
            $"{_gatewayUrl}/api/finished-goods{Request.QueryString}",
            cancellationToken);

    [HttpGet("api/{id:guid}")]
    public Task<IActionResult> Detail(Guid id, CancellationToken cancellationToken) =>
        ProxyGatewayAsync(
            HttpMethod.Get,
            $"{_gatewayUrl}/api/finished-goods/{id:D}",
            cancellationToken);

    [HttpGet("api/gsku-selector")]
    public Task<IActionResult> GskuSelector(CancellationToken cancellationToken) =>
        ProxyGatewayAsync(
            HttpMethod.Get,
            $"{_gatewayUrl}/api/finished-goods/gsku-selector{Request.QueryString}",
            cancellationToken);

    [HttpPost("api")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        [FromForm] CreateFinishedGoodViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid || model.GskuId == Guid.Empty)
        {
            return BadRequest(new
            {
                success = false,
                errors = new[] { _localizer["GskuRequired"].Value }
            });
        }

        var payload = new
        {
            gskuId = model.GskuId,
            idempotencyKey = Guid.NewGuid().ToString("N")
        };

        try
        {
            var draft = await SendGatewayAsync<FinishedGoodDraftViewModel>(
                HttpMethod.Post,
                $"{_gatewayUrl}/api/finished-goods/drafts",
                JsonContent.Create(payload, options: _jsonOptions),
                cancellationToken);
            if (!draft.Success || draft.Data is null)
            {
                return StatusCode(draft.StatusCode, new { success = false, errors = draft.Errors });
            }

            return StatusCode(draft.StatusCode, new { success = true, data = draft.Data });
        }
        catch (HttpRequestException exception)
        {
            _logger.LogError(exception, "Finished Good create proxy flow failed.");
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                success = false,
                errors = new[] { _sharedLocalizer["GatewayError"].Value }
            });
        }
    }

    private async Task<IActionResult> ProxyGatewayAsync(
        HttpMethod method,
        string targetUrl,
        CancellationToken cancellationToken)
    {
        if (!TryCreateRequest(method, targetUrl, content: null, out var request))
        {
            return Unauthorized(new { errors = new[] { _sharedLocalizer["Unauthorized"].Value } });
        }

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
        catch (HttpRequestException exception)
        {
            _logger.LogError(exception, "Finished Good gateway proxy failed for {TargetUrl}.", targetUrl);
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
            FinishedGoodGatewayResponse<T>? payload = null;
            try
            {
                payload = await response.Content.ReadFromJsonAsync<FinishedGoodGatewayResponse<T>>(
                    _jsonOptions,
                    cancellationToken);
            }
            catch (JsonException exception)
            {
                _logger.LogWarning(exception, "Finished Good gateway returned an invalid JSON envelope.");
            }

            if (response.IsSuccessStatusCode && payload?.Data is not null)
            {
                return GatewayCallResult<T>.Succeeded((int)response.StatusCode, payload.Data);
            }

            var errors = payload?.Errors.Where(error => !string.IsNullOrWhiteSpace(error)).Take(10).ToList() ?? [];
            if (errors.Count == 0)
            {
                errors.Add(MapStatusMessage(response.StatusCode));
            }

            return GatewayCallResult<T>.Failure((int)response.StatusCode, errors);
        }
    }

    private string MapStatusMessage(HttpStatusCode statusCode) => statusCode switch
    {
        HttpStatusCode.BadRequest => _localizer["ErrorValidation"].Value,
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
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        var tenantValue = User.Claims.FirstOrDefault(claim =>
            claim.Type == "tenantId"
            || claim.Type == "tenant_id"
            || claim.Type.EndsWith("/tenantId", StringComparison.OrdinalIgnoreCase))?.Value;
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
        public static GatewayCallResult<T> Failure(int statusCode, IReadOnlyList<string> errors) =>
            new(false, statusCode, default, errors);
    }
}
