using System.Buffers.Binary;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Diten.Web.Models.FinishedGoods;
using Diten.Web.Security;
using Diten.Web.Views.MasterDataManagement.FinishedGoods;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Diten.Web.Controllers;

[Authorize]
[Route("MasterDataManagement/FinishedGoods")]
public sealed class FinishedGoodsController : Controller
{
    private const string SubmitPermission = "mdm.finished-goods.submit";
    private const string RetirePermission = "mdm.finished-goods.retire";
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

    [HttpPost("api/{id:guid}/submit")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> SubmitLifecycle(
        Guid id,
        [FromForm] int? expectedVersion,
        CancellationToken cancellationToken) =>
        ExecuteLifecycleAsync(id, expectedVersion, null, "submit", SubmitPermission, cancellationToken);

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
        if (!PermissionClaims.HasPermission(User, permission))
            return LifecycleFailure(HttpStatusCode.Forbidden);

        reasonCode = reasonCode?.Trim();
        var isRetire = string.Equals(action, "retire", StringComparison.Ordinal);
        if (id == Guid.Empty || expectedVersion is null or < 0
            || (isRetire && (string.IsNullOrWhiteSpace(reasonCode) || reasonCode.Length > 128))
            || (!isRetire && !string.IsNullOrEmpty(reasonCode))
            || !await HasOnlyFormFieldsAsync(isRetire ? ["ExpectedVersion", "ReasonCode"] : ["ExpectedVersion"]))
        {
            return LifecycleFailure(HttpStatusCode.BadRequest);
        }
        if (!TryResolveLifecycleIdentity(out var tenantId, out var actor))
            return LifecycleFailure(HttpStatusCode.Unauthorized);

        var operationId = CreateLifecycleOperationId(
            tenantId, actor, "finished-good", action, id, expectedVersion.Value, reasonCode ?? string.Empty);
        var content = isRetire
            ? JsonContent.Create(new { expectedVersion = expectedVersion.Value, reasonCode }, options: _jsonOptions)
            : JsonContent.Create(new { expectedVersion = expectedVersion.Value }, options: _jsonOptions);
        IReadOnlySet<int> allowedSuccessStatusCodes = isRetire
            ? new HashSet<int> { StatusCodes.Status200OK }
            : new HashSet<int> { StatusCodes.Status200OK, StatusCodes.Status202Accepted };
        if (!TryCreateRequest(
                HttpMethod.Post,
                $"{_gatewayUrl}/api/finished-goods/{id:D}/{action}",
                content,
                out var request))
        {
            return LifecycleFailure(HttpStatusCode.Unauthorized);
        }

        request.Headers.TryAddWithoutValidation("Idempotency-Key", operationId.ToString("D"));
        try
        {
            using (request)
            using (var response = await _httpClient.SendAsync(request, cancellationToken))
            {
                if (!response.IsSuccessStatusCode)
                    return LifecycleFailure(response.StatusCode);

                var responseStatus = (int)response.StatusCode;
                var mediaType = response.Content.Headers.ContentType?.MediaType;
                if (!allowedSuccessStatusCodes.Contains(responseStatus) || !IsJsonMediaType(mediaType))
                    return LifecycleFailure(HttpStatusCode.BadGateway);

                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                LifecycleGatewayEnvelope? envelope;
                try
                {
                    envelope = JsonSerializer.Deserialize<LifecycleGatewayEnvelope>(body, _jsonOptions);
                }
                catch (JsonException exception)
                {
                    _logger.LogWarning(
                        exception,
                        "Finished Good lifecycle proxy received malformed JSON for {Action}.",
                        action);
                    return LifecycleFailure(HttpStatusCode.BadGateway);
                }
                if (envelope?.IsSuccessful != true || envelope.StatusCode != responseStatus)
                    return LifecycleFailure(HttpStatusCode.BadGateway);

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
            _logger.LogWarning(exception, "Finished Good lifecycle proxy timed out for {Action}.", action);
            return LifecycleFailure(HttpStatusCode.GatewayTimeout);
        }
        catch (HttpRequestException exception)
        {
            _logger.LogError(exception, "Finished Good lifecycle proxy failed for {Action}.", action);
            return LifecycleFailure(HttpStatusCode.ServiceUnavailable);
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
        if (User.Identity?.IsAuthenticated != true || !IsHumanActorType(SingleClaim(User, "actor_type")))
            return false;

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

        if (!TryResolveCanonicalTenant(User, out tenantId))
            return false;
        actor = subjectId.ToString("D");
        return true;
    }

    private static string? SingleClaim(ClaimsPrincipal principal, string type)
    {
        var values = ClaimValues(principal, type);
        return values.Count == 1 ? values[0] : null;
    }

    private static bool IsHumanActorType(string? actorType) => actorType is
        "tenant_user" or "platform_admin" or "partner_admin";

    private static IReadOnlyList<string> ClaimValues(ClaimsPrincipal principal, string type) => principal.Claims
        .Where(claim => string.Equals(claim.Type, type, StringComparison.Ordinal))
        .Select(claim => claim.Value)
        .ToArray();

    private static bool TryResolveCanonicalTenant(ClaimsPrincipal principal, out Guid tenantId)
    {
        tenantId = Guid.Empty;
        var values = principal.Claims
            .Where(claim => claim.Type == "tenantId" || claim.Type == "tenant_id"
                || claim.Type.EndsWith("/tenantId", StringComparison.OrdinalIgnoreCase))
            .Select(claim => claim.Value)
            .ToArray();
        if (values.Length == 0)
            return false;

        Guid? canonical = null;
        foreach (var value in values)
        {
            if (!Guid.TryParseExact(value, "D", out var parsed) || parsed == Guid.Empty
                || canonical.HasValue && canonical.Value != parsed)
            {
                return false;
            }
            canonical ??= parsed;
        }

        tenantId = canonical ?? Guid.Empty;
        return tenantId != Guid.Empty;
    }

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

    private IActionResult LifecycleFailure(HttpStatusCode code)
    {
        var status = code switch
        {
            HttpStatusCode.BadRequest => 400,
            HttpStatusCode.Unauthorized => 401,
            HttpStatusCode.Forbidden => 403,
            HttpStatusCode.NotFound => 404,
            HttpStatusCode.Conflict => 409,
            HttpStatusCode.ServiceUnavailable => 503,
            HttpStatusCode.GatewayTimeout => 504,
            _ => 502
        };
        var key = status switch
        {
            400 => "ErrorValidation",
            401 => "ErrorUnauthorized",
            403 => "ErrorForbidden",
            404 => "ErrorNotFound",
            409 => "ErrorConflict",
            503 => "ErrorServiceUnavailable",
            504 => "ErrorTimeout",
            _ => "ErrorGateway"
        };
        return StatusCode(status, new { success = false, errors = new[] { _localizer[key].Value } });
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

    private sealed class LifecycleGatewayEnvelope
    {
        public bool IsSuccessful { get; init; }
        public int StatusCode { get; init; }
    }

    private sealed record GatewayCallResult<T>(bool Success, int StatusCode, T? Data, IReadOnlyList<string> Errors)
        where T : class
    {
        public static GatewayCallResult<T> Succeeded(int statusCode, T data) => new(true, statusCode, data, []);
        public static GatewayCallResult<T> Failure(int statusCode, IReadOnlyList<string> errors) =>
            new(false, statusCode, default, errors);
    }
}
