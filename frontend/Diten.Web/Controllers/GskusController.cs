using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Diten.Web.Models.Gskus;
using Diten.Web.Security;
using Diten.Web.Views.MasterDataManagement.Gskus;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Diten.Web.Controllers;

[Authorize]
[Route("MasterDataManagement/Gskus")]
public sealed class GskusController : Controller
{
    private const string ReadPermission = "mdm.gskus.read";
    private const string CreatePermission = "mdm.gskus.create";
    private static readonly TimeSpan FormAttemptLifetime = TimeSpan.FromMinutes(30);

    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly ITimeLimitedDataProtector _formAttemptProtector;
    private readonly IStringLocalizer<SharedResource> _sharedLocalizer;
    private readonly IStringLocalizer<GskusIndex> _localizer;
    private readonly ILogger<GskusController> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public GskusController(
        HttpClient httpClient,
        IConfiguration configuration,
        IDataProtectionProvider dataProtectionProvider,
        IStringLocalizer<SharedResource> sharedLocalizer,
        IStringLocalizer<GskusIndex> localizer,
        ILogger<GskusController> logger)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"]?.TrimEnd('/')
            ?? throw new InvalidOperationException("GatewayUrl configuration is required.");
        _formAttemptProtector = dataProtectionProvider
            .CreateProtector("Diten.Web", "MOD-0290", "GskuFormAttempt", "v1")
            .ToTimeLimitedDataProtector();
        _sharedLocalizer = sharedLocalizer;
        _localizer = localizer;
        _logger = logger;
    }

    [HttpGet("")]
    public IActionResult Index()
    {
        if (!HasPermission(ReadPermission))
            return Forbid();

        var canCreate = HasPermission(CreatePermission);
        ViewData["CanCreateGsku"] = canCreate;
        if (canCreate)
            ViewData["GskuFormAttemptToken"] = CreateFormAttemptToken();

        return View("~/Views/MasterDataManagement/Gskus/Index.cshtml");
    }

    [HttpGet("api")]
    public Task<IActionResult> List(CancellationToken cancellationToken)
    {
        if (!HasPermission(ReadPermission))
            return Task.FromResult<IActionResult>(ForbiddenResult());

        return ProxyGatewayAsync(
            HttpMethod.Get,
            $"{_gatewayUrl}/api/gskus{Request.QueryString}",
            cancellationToken);
    }

    [HttpGet("api/{id:guid}")]
    public Task<IActionResult> Detail(Guid id, CancellationToken cancellationToken)
    {
        if (!HasPermission(ReadPermission))
            return Task.FromResult<IActionResult>(ForbiddenResult());

        return ProxyGatewayAsync(HttpMethod.Get, $"{_gatewayUrl}/api/gskus/{id:D}", cancellationToken);
    }

    [HttpGet("api/create-options")]
    public Task<IActionResult> CreateOptions(CancellationToken cancellationToken)
    {
        if (!HasPermission(CreatePermission))
            return Task.FromResult<IActionResult>(ForbiddenResult());

        return ProxyGatewayAsync(HttpMethod.Get, $"{_gatewayUrl}/api/gskus/create-options", cancellationToken);
    }

    [HttpPost("api")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        [FromForm] CreateGskuViewModel model,
        [FromForm] string? formAttemptToken,
        CancellationToken cancellationToken)
    {
        if (!HasPermission(CreatePermission))
            return ForbiddenResult();

        if (!TryReadFormAttempt(formAttemptToken, out var operationKey))
            return BadRequest(new { success = false, errors = new[] { _localizer["ErrorInvalidFormAttempt"].Value } });

        model.PackUomCode = model.PackUomCode?.Trim().ToUpperInvariant() ?? string.Empty;
        if (!ModelState.IsValid
            || model.GlobalProductId == Guid.Empty
            || string.IsNullOrWhiteSpace(model.PackUomCode)
            || !decimal.TryParse(
                model.PackQuantity,
                NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out var packQuantity)
            || packQuantity <= 0)
        {
            return BadRequest(new { success = false, errors = new[] { _localizer["ErrorValidation"].Value } });
        }

        var payload = new
        {
            globalProductId = model.GlobalProductId,
            packQuantity,
            packUomCode = model.PackUomCode
        };

        if (!TryCreateGatewayRequest(
                HttpMethod.Post,
                $"{_gatewayUrl}/api/gskus/drafts",
                JsonContent.Create(payload, options: _jsonOptions),
                out var request))
        {
            return UnauthorizedResult();
        }

        request.Headers.TryAddWithoutValidation("Idempotency-Key", operationKey);

        try
        {
            using (request)
            using (var response = await _httpClient.SendAsync(request, cancellationToken))
            {
                var envelope = await ReadEnvelopeAsync<GskuDraftViewModel>(response, cancellationToken);

                if (response.StatusCode == HttpStatusCode.Created
                    && envelope?.IsSuccessful == true
                    && envelope.Data is not null)
                {
                    return StatusCode(StatusCodes.Status201Created, new
                    {
                        success = true,
                        data = envelope.Data,
                        formAttemptToken = CreateFormAttemptToken()
                    });
                }

                if (response.StatusCode == HttpStatusCode.Accepted)
                {
                    return StatusCode(StatusCodes.Status202Accepted, new
                    {
                        success = false,
                        data = envelope?.Data,
                        errors = new[] { _localizer["CreateReconciliationPending"].Value },
                        formAttemptToken
                    });
                }

                return SafeFailure(response.StatusCode);
            }
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(exception, "GSKU create proxy timed out.");
            return SafeFailure(HttpStatusCode.GatewayTimeout);
        }
        catch (HttpRequestException exception)
        {
            _logger.LogError(exception, "GSKU create proxy failed.");
            return SafeFailure(HttpStatusCode.ServiceUnavailable);
        }
    }

    private async Task<IActionResult> ProxyGatewayAsync(
        HttpMethod method,
        string targetUrl,
        CancellationToken cancellationToken)
    {
        if (!TryCreateGatewayRequest(method, targetUrl, content: null, out var request))
            return UnauthorizedResult();

        try
        {
            using (request)
            using (var response = await _httpClient.SendAsync(request, cancellationToken))
            {
                if (!response.IsSuccessStatusCode)
                    return SafeFailure(response.StatusCode);

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
            _logger.LogWarning(exception, "GSKU gateway proxy timed out for {TargetUrl}.", targetUrl);
            return SafeFailure(HttpStatusCode.GatewayTimeout);
        }
        catch (HttpRequestException exception)
        {
            _logger.LogError(exception, "GSKU gateway proxy failed for {TargetUrl}.", targetUrl);
            return SafeFailure(HttpStatusCode.ServiceUnavailable);
        }
    }

    private async Task<GskuGatewayResponse<T>?> ReadEnvelopeAsync<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<GskuGatewayResponse<T>>(_jsonOptions, cancellationToken);
        }
        catch (JsonException exception)
        {
            _logger.LogWarning(exception, "GSKU gateway returned an invalid response envelope.");
            return null;
        }
    }

    private IActionResult SafeFailure(HttpStatusCode statusCode)
    {
        var normalizedStatus = statusCode switch
        {
            HttpStatusCode.BadRequest => StatusCodes.Status400BadRequest,
            HttpStatusCode.Unauthorized => StatusCodes.Status401Unauthorized,
            HttpStatusCode.Forbidden => StatusCodes.Status403Forbidden,
            HttpStatusCode.NotFound => StatusCodes.Status404NotFound,
            HttpStatusCode.Conflict => StatusCodes.Status409Conflict,
            HttpStatusCode.ServiceUnavailable => StatusCodes.Status503ServiceUnavailable,
            HttpStatusCode.GatewayTimeout => StatusCodes.Status504GatewayTimeout,
            _ => StatusCodes.Status502BadGateway
        };

        return StatusCode(normalizedStatus, new
        {
            success = false,
            errors = new[] { MapStatusMessage(normalizedStatus) }
        });
    }

    private IActionResult ForbiddenResult() => StatusCode(StatusCodes.Status403Forbidden, new
    {
        success = false,
        errors = new[] { _localizer["ErrorForbidden"].Value }
    });

    private IActionResult UnauthorizedResult() => Unauthorized(new
    {
        success = false,
        errors = new[] { _sharedLocalizer["Unauthorized"].Value }
    });

    private string MapStatusMessage(int statusCode) => statusCode switch
    {
        StatusCodes.Status400BadRequest => _localizer["ErrorValidation"].Value,
        StatusCodes.Status401Unauthorized => _sharedLocalizer["Unauthorized"].Value,
        StatusCodes.Status403Forbidden => _localizer["ErrorForbidden"].Value,
        StatusCodes.Status404NotFound => _localizer["ErrorNotFound"].Value,
        StatusCodes.Status409Conflict => _localizer["ErrorConflict"].Value,
        StatusCodes.Status503ServiceUnavailable => _localizer["ErrorProviderUnavailable"].Value,
        StatusCodes.Status504GatewayTimeout => _localizer["ErrorProviderTimeout"].Value,
        _ => _sharedLocalizer["GatewayError"].Value
    };

    private bool TryCreateGatewayRequest(
        HttpMethod method,
        string url,
        HttpContent? content,
        out HttpRequestMessage request)
    {
        request = new HttpRequestMessage(method, url);
        var token = Diten.Web.Services.Auth.AuthTokenCookies.GetAccessToken(Request);
        if (!string.IsNullOrWhiteSpace(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

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
            if (payload is null
                || string.IsNullOrWhiteSpace(payload.OperationKey)
                || !CryptographicOperations.FixedTimeEquals(
                    System.Text.Encoding.UTF8.GetBytes(payload.UserSubject),
                    System.Text.Encoding.UTF8.GetBytes(ResolveUserSubject())))
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

    private bool HasPermission(string permission) => PermissionClaims.HasPermission(User, permission);

    private sealed record FormAttemptPayload(string UserSubject, string OperationKey);
}
