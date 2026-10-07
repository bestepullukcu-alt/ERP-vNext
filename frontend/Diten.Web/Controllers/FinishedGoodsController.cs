using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Diten.Web.Models.FinishedGoods;
using Diten.Web.Security;
using Diten.Web.Views.MasterDataManagement.FinishedGoods;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Diten.Web.Controllers;

[Authorize]
[Route("MasterDataManagement/FinishedGoods")]
public sealed class FinishedGoodsController : Controller
{
    private const string ReadPermission = "mdm.finished-goods.read";
    private const string CreatePermission = "mdm.finished-goods.create";
    private static readonly TimeSpan FormAttemptLifetime = TimeSpan.FromMinutes(30);

    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly ITimeLimitedDataProtector _formAttemptProtector;
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
        IDataProtectionProvider dataProtectionProvider,
        IStringLocalizer<SharedResource> sharedLocalizer,
        IStringLocalizer<FinishedGoodsIndex> localizer,
        ILogger<FinishedGoodsController> logger)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"]?.TrimEnd('/')
            ?? throw new InvalidOperationException("GatewayUrl configuration is required.");
        _formAttemptProtector = dataProtectionProvider
            .CreateProtector("Diten.Web", "MOD-0290", "FinishedGoodFormAttempt", "v1")
            .ToTimeLimitedDataProtector();
        _sharedLocalizer = sharedLocalizer;
        _localizer = localizer;
        _logger = logger;
    }

    [HttpGet("")]
    public IActionResult Index()
    {
        // FG FIX1 item 5 (UAS-001): no redirect and no register for a signed-in user without the read key — the status
        // says 403 and the view draws only the access-denied surface (Index.cshtml checks first, with the same reader).
        if (!HasPermission(ReadPermission))
        {
            Response.StatusCode = StatusCodes.Status403Forbidden;
            return View("~/Views/MasterDataManagement/FinishedGoods/Index.cshtml");
        }

        var canCreate = HasPermission(CreatePermission);
        ViewData["CanCreateFinishedGood"] = canCreate;
        if (canCreate)
            ViewData["FinishedGoodFormAttemptToken"] = CreateFormAttemptToken();
        return View("~/Views/MasterDataManagement/FinishedGoods/Index.cshtml");
    }

    [HttpGet("api")]
    public Task<IActionResult> List(CancellationToken cancellationToken) =>
        HasPermission(ReadPermission)
            ? ProxyGatewayAsync(HttpMethod.Get, $"{_gatewayUrl}/api/finished-goods{Request.QueryString}", cancellationToken)
            : Task.FromResult(ForbiddenResult());

    [HttpGet("api/{id:guid}")]
    public Task<IActionResult> Detail(Guid id, CancellationToken cancellationToken) =>
        HasPermission(ReadPermission)
            ? ProxyGatewayAsync(HttpMethod.Get, $"{_gatewayUrl}/api/finished-goods/{id:D}", cancellationToken)
            : Task.FromResult(ForbiddenResult());

    [HttpGet("api/gsku-selector")]
    public Task<IActionResult> GskuSelector(CancellationToken cancellationToken) =>
        HasPermission(CreatePermission)
            ? ProxyGatewayAsync(HttpMethod.Get, $"{_gatewayUrl}/api/finished-goods/gsku-selector{Request.QueryString}", cancellationToken)
            : Task.FromResult(ForbiddenResult());

    // FG FIX1 item 4: one form attempt = one MDM creation key. The key is drawn when the form is served (bound to the
    // signed-in subject, 30 minutes, Data Protection) — a repeat after a lost answer, a timeout or a 202 sends the SAME
    // key, so MDM answers it as a replay instead of creating a second Finished Good. A fresh token comes only with 201.
    [HttpPost("api")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        [FromForm] CreateFinishedGoodViewModel model,
        [FromForm] string? formAttemptToken,
        CancellationToken cancellationToken)
    {
        if (!HasPermission(CreatePermission))
            return ForbiddenResult();

        if (!TryReadFormAttempt(formAttemptToken, out var operationKey))
        {
            return BadRequest(new
            {
                success = false,
                errors = new[] { _localizer["ErrorInvalidFormAttempt"].Value }
            });
        }

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
            idempotencyKey = operationKey
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
                return StatusCode(draft.StatusCode, new { success = false, errors = draft.Errors, formAttemptToken });
            }

            return draft.StatusCode == StatusCodes.Status201Created
                ? StatusCode(draft.StatusCode, new { success = true, data = draft.Data, formAttemptToken = CreateFormAttemptToken() })
                : StatusCode(draft.StatusCode, new { success = true, data = draft.Data, formAttemptToken });
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(exception, "Finished Good create proxy timed out.");
            return StatusCode(StatusCodes.Status504GatewayTimeout, new
            {
                success = false,
                errors = new[] { _localizer["ErrorTimeout"].Value },
                formAttemptToken
            });
        }
        catch (HttpRequestException exception)
        {
            _logger.LogError(exception, "Finished Good create proxy flow failed.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                success = false,
                errors = new[] { _localizer["ErrorGateway"].Value },
                formAttemptToken
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
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(exception, "Finished Good gateway proxy timed out for {TargetUrl}.", targetUrl);
            return StatusCode(StatusCodes.Status504GatewayTimeout, new
            {
                errors = new[] { _localizer["ErrorTimeout"].Value }
            });
        }
        catch (HttpRequestException exception)
        {
            _logger.LogError(exception, "Finished Good gateway proxy failed for {TargetUrl}.", targetUrl);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                errors = new[] { _localizer["ErrorGateway"].Value }
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

    private IActionResult ForbiddenResult() => StatusCode(StatusCodes.Status403Forbidden, new
    {
        success = false,
        errors = new[] { _localizer["ErrorForbidden"].Value }
    });

    private bool HasPermission(string permission) => PermissionClaims.HasPermission(User, permission);

    private string CreateFormAttemptToken()
    {
        var payload = new FormAttemptPayload(
            ResolveUserSubject(),
            Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));
        return _formAttemptProtector.Protect(JsonSerializer.Serialize(payload, _jsonOptions), FormAttemptLifetime);
    }

    private bool TryReadFormAttempt(string? token, out string operationKey)
    {
        operationKey = string.Empty;
        if (string.IsNullOrWhiteSpace(token))
            return false;

        try
        {
            var json = _formAttemptProtector.Unprotect(token, out _);
            var payload = JsonSerializer.Deserialize<FormAttemptPayload>(json, _jsonOptions);
            var subject = ResolveUserSubject();
            if (payload is null
                || string.IsNullOrWhiteSpace(payload.OperationKey)
                || string.IsNullOrEmpty(subject)
                || !CryptographicOperations.FixedTimeEquals(
                    System.Text.Encoding.UTF8.GetBytes(payload.UserSubject),
                    System.Text.Encoding.UTF8.GetBytes(subject)))
            {
                return false;
            }

            operationKey = payload.OperationKey;
            return true;
        }
        catch (CryptographicException)
        {
            return false;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private string ResolveUserSubject() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue("sub")
        ?? User.Identity?.Name
        ?? string.Empty;

    private sealed record FormAttemptPayload(string UserSubject, string OperationKey);

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
