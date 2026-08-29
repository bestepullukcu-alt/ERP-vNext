using System.Globalization;
using System.Buffers.Binary;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
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
    private const string SubmitPermission = "mdm.gskus.submit";
    private const string RetirePermission = "mdm.gskus.retire";
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
        ViewData["CanSubmitGsku"] = HasPermission(SubmitPermission);
        ViewData["CanRetireGsku"] = HasPermission(RetirePermission);
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

    [HttpPost("api/{id:guid}/submit")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> SubmitLifecycle(
        Guid id,
        [FromForm] int? expectedVersion,
        CancellationToken cancellationToken) =>
        ExecuteLifecycleAsync(id, expectedVersion, reasonCode: null, "submit", SubmitPermission, cancellationToken);

    [HttpPost("api/{id:guid}/retire")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> RetireLifecycle(
        Guid id,
        [FromForm] int? expectedVersion,
        [FromForm] string? reasonCode,
        CancellationToken cancellationToken) =>
        ExecuteLifecycleAsync(id, expectedVersion, reasonCode, "retire", RetirePermission, cancellationToken);

    private async Task<IActionResult> ExecuteLifecycleAsync(
        Guid id,
        int? expectedVersion,
        string? reasonCode,
        string action,
        string permission,
        CancellationToken cancellationToken)
    {
        if (!HasPermission(permission))
            return ForbiddenResult();

        reasonCode = reasonCode?.Trim();
        var isRetire = string.Equals(action, "retire", StringComparison.Ordinal);
        if (id == Guid.Empty || expectedVersion is null or < 0
            || (isRetire && (string.IsNullOrWhiteSpace(reasonCode) || reasonCode.Length > 128))
            || (!isRetire && !string.IsNullOrEmpty(reasonCode))
            || !await HasOnlyFormFieldsAsync(isRetire
                ? ["ExpectedVersion", "ReasonCode"]
                : ["ExpectedVersion"]))
        {
            return SafeFailure(HttpStatusCode.BadRequest);
        }
        if (!TryResolveLifecycleIdentity(out var tenantId, out var actor))
            return UnauthorizedResult();

        var operationId = CreateLifecycleOperationId(
            tenantId, actor, "gsku", action, id, expectedVersion.Value, reasonCode ?? string.Empty);
        var payload = isRetire
            ? JsonContent.Create(new { expectedVersion = expectedVersion.Value, reasonCode }, options: _jsonOptions)
            : JsonContent.Create(new { expectedVersion = expectedVersion.Value }, options: _jsonOptions);
        IReadOnlySet<int> allowedSuccessStatusCodes = isRetire
            ? new HashSet<int> { StatusCodes.Status200OK }
            : new HashSet<int> { StatusCodes.Status200OK, StatusCodes.Status202Accepted };
        if (!TryCreateGatewayRequest(
                HttpMethod.Post,
                $"{_gatewayUrl}/api/gskus/{id:D}/{action}",
                payload,
                out var request))
        {
            return UnauthorizedResult();
        }
        request.Headers.TryAddWithoutValidation("Idempotency-Key", operationId.ToString("D"));

        try
        {
            using (request)
            using (var response = await _httpClient.SendAsync(request, cancellationToken))
            {
                var responseStatus = (int)response.StatusCode;
                if (!response.IsSuccessStatusCode)
                    return SafeFailure(response.StatusCode);

                var mediaType = response.Content.Headers.ContentType?.MediaType;
                if (!allowedSuccessStatusCodes.Contains(responseStatus)
                    || !IsJsonMediaType(mediaType))
                {
                    return SafeFailure(HttpStatusCode.BadGateway);
                }

                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                LifecycleGatewayEnvelope? envelope;
                try
                {
                    envelope = JsonSerializer.Deserialize<LifecycleGatewayEnvelope>(body, _jsonOptions);
                }
                catch (JsonException exception)
                {
                    _logger.LogWarning(exception, "GSKU lifecycle proxy received malformed JSON for {Action}.", action);
                    return SafeFailure(HttpStatusCode.BadGateway);
                }

                if (envelope?.IsSuccessful != true || envelope.StatusCode != responseStatus)
                    return SafeFailure(HttpStatusCode.BadGateway);

                return new ContentResult
                {
                    StatusCode = responseStatus,
                    ContentType = "application/json",
                    Content = body
                };
            }
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(exception, "GSKU lifecycle proxy timed out for {Action}.", action);
            return SafeFailure(HttpStatusCode.GatewayTimeout);
        }
        catch (HttpRequestException exception)
        {
            _logger.LogError(exception, "GSKU lifecycle proxy failed for {Action}.", action);
            return SafeFailure(HttpStatusCode.ServiceUnavailable);
        }
    }

    private async Task<bool> HasOnlyFormFieldsAsync(params string[] allowedFields)
    {
        if (!Request.HasFormContentType)
            return false;
        var form = await Request.ReadFormAsync(HttpContext.RequestAborted);
        var required = new HashSet<string>(allowedFields, StringComparer.Ordinal);
        var allowed = new HashSet<string>(required, StringComparer.Ordinal) { "__RequestVerificationToken" };
        return form.Count == allowed.Count
            && form.Keys.All(allowed.Contains)
            && form.TryGetValue("__RequestVerificationToken", out var antiforgery)
            && antiforgery.Count == 1
            && required.All(field => form.TryGetValue(field, out var values) && values.Count == 1);
    }

    private static bool IsJsonMediaType(string? mediaType) =>
        string.Equals(mediaType, "application/json", StringComparison.OrdinalIgnoreCase)
        || mediaType?.EndsWith("+json", StringComparison.OrdinalIgnoreCase) == true;

    private bool TryResolveLifecycleIdentity(out Guid tenantId, out string actor)
    {
        tenantId = Guid.Empty;
        actor = string.Empty;
        if (User.Identity?.IsAuthenticated != true
            || !IsHumanActorType(SingleClaim(User, "actor_type")))
        {
            return false;
        }

        var subjects = ClaimValues(User, "sub");
        var nameIdentifiers = ClaimValues(User, ClaimTypes.NameIdentifier);
        if (subjects.Count > 1 || nameIdentifiers.Count > 1
            || subjects.Count == 0 && nameIdentifiers.Count == 0
            || !TryCanonicalGuid(subjects, out var subject)
            || !TryCanonicalGuid(nameIdentifiers, out var nameIdentifier)
            || subject.HasValue && nameIdentifier.HasValue && subject != nameIdentifier)
        {
            return false;
        }

        var subjectId = subject ?? nameIdentifier ?? Guid.Empty;
        if (subjectId == Guid.Empty)
            return false;

        var tenantValue = User.Claims.FirstOrDefault(claim =>
            claim.Type == "tenantId" || claim.Type == "tenant_id"
            || claim.Type.EndsWith("/tenantId", StringComparison.OrdinalIgnoreCase))?.Value;
        actor = subjectId.ToString("D");
        return Guid.TryParse(tenantValue, out tenantId) && tenantId != Guid.Empty;
    }

    private static string? SingleClaim(ClaimsPrincipal principal, string type)
    {
        var values = ClaimValues(principal, type);
        return values.Count == 1 ? values[0] : null;
    }

    private static bool IsHumanActorType(string? actorType) => actorType is
        "tenant_user" or "platform_admin" or "partner_admin";

    private static IReadOnlyList<string> ClaimValues(ClaimsPrincipal principal, string type) =>
        principal.Claims
            .Where(claim => string.Equals(claim.Type, type, StringComparison.Ordinal))
            .Select(claim => claim.Value)
            .ToArray();

    private static bool TryCanonicalGuid(IReadOnlyList<string> values, out Guid? result)
    {
        result = null;
        if (values.Count == 0)
            return true;

        if (!Guid.TryParseExact(values[0], "D", out var parsed) || parsed == Guid.Empty)
            return false;

        result = parsed;
        return true;
    }

    private static Guid CreateLifecycleOperationId(
        Guid tenantId,
        string actor,
        string aggregateType,
        string action,
        Guid aggregateId,
        int expectedVersion,
        string reasonCode)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendLengthPrefixed(hash, tenantId.ToString("D"));
        AppendLengthPrefixed(hash, actor);
        AppendLengthPrefixed(hash, aggregateType);
        AppendLengthPrefixed(hash, action);
        AppendLengthPrefixed(hash, aggregateId.ToString("D"));
        AppendLengthPrefixed(hash, expectedVersion.ToString(CultureInfo.InvariantCulture));
        AppendLengthPrefixed(hash, reasonCode);
        var digest = hash.GetHashAndReset();
        var hex = Convert.ToHexString(digest.AsSpan(0, 16));
        return Guid.ParseExact(
            $"{hex[..8]}-{hex[8..12]}-{hex[12..16]}-{hex[16..20]}-{hex[20..32]}",
            "D");
    }

    private static void AppendLengthPrefixed(IncrementalHash hash, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        Span<byte> length = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(length, bytes.Length);
        hash.AppendData(length);
        hash.AppendData(bytes);
    }

    private sealed class LifecycleGatewayEnvelope
    {
        public bool IsSuccessful { get; init; }
        public int StatusCode { get; init; }
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
