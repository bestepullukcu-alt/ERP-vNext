using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Buffers.Binary;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Diten.Web.Models.GlobalProducts;
using Diten.Web.Security;
using Diten.Web.Views.MasterDataManagement.GlobalProducts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Diten.Web.Controllers;

[Authorize]
[Route("MasterDataManagement/GlobalProducts")]
public sealed class GlobalProductsController : Controller
{
    private const string SubmitPermission = "mdm.global-products.submit";
    private const string RetirePermission = "mdm.global-products.retire";
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

    [HttpPost("api/{id:guid}/submit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitLifecycle(
        Guid id,
        [FromForm] int? expectedVersion,
        CancellationToken cancellationToken)
    {
        if (!PermissionClaims.HasPermission(User, SubmitPermission))
            return LifecycleFailure(StatusCodes.Status403Forbidden);
        if (id == Guid.Empty || expectedVersion is null or < 0
            || !await HasOnlyFormFieldsAsync("ExpectedVersion"))
            return LifecycleFailure(StatusCodes.Status400BadRequest);
        if (!TryResolveLifecycleIdentity(out var tenantId, out var actor))
            return LifecycleFailure(StatusCodes.Status401Unauthorized);

        var operationId = CreateLifecycleOperationId(
            tenantId, actor, "GlobalProduct", "submit", id, expectedVersion.Value, string.Empty);
        return await ProxyLifecycleAsync(
            $"{_gatewayUrl}/api/global-products/{id:D}/submit",
            JsonContent.Create(new { expectedVersion = expectedVersion.Value }, options: _jsonOptions),
            operationId,
            new HashSet<int> { StatusCodes.Status200OK, StatusCodes.Status202Accepted },
            cancellationToken);
    }

    [HttpPost("api/{id:guid}/retire")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RetireLifecycle(
        Guid id,
        [FromForm] int? expectedVersion,
        [FromForm] string? reasonCode,
        CancellationToken cancellationToken)
    {
        if (!PermissionClaims.HasPermission(User, RetirePermission))
            return LifecycleFailure(StatusCodes.Status403Forbidden);

        reasonCode = reasonCode?.Trim();
        if (id == Guid.Empty || expectedVersion is null or < 0 || string.IsNullOrWhiteSpace(reasonCode)
            || reasonCode.Length > 128 || !await HasOnlyFormFieldsAsync("ExpectedVersion", "ReasonCode"))
        {
            return LifecycleFailure(StatusCodes.Status400BadRequest);
        }
        if (!TryResolveLifecycleIdentity(out var tenantId, out var actor))
            return LifecycleFailure(StatusCodes.Status401Unauthorized);

        var operationId = CreateLifecycleOperationId(
            tenantId, actor, "GlobalProduct", "retire", id, expectedVersion.Value, reasonCode);
        return await ProxyLifecycleAsync(
            $"{_gatewayUrl}/api/global-products/{id:D}/retire",
            JsonContent.Create(new { expectedVersion = expectedVersion.Value, reasonCode }, options: _jsonOptions),
            operationId,
            new HashSet<int> { StatusCodes.Status200OK },
            cancellationToken);
    }

    private async Task<IActionResult> ProxyLifecycleAsync(
        string targetUrl,
        HttpContent content,
        Guid operationId,
        IReadOnlySet<int> allowedSuccessStatusCodes,
        CancellationToken cancellationToken)
    {
        if (!TryCreateRequest(HttpMethod.Post, targetUrl, content, out var request))
            return LifecycleFailure(StatusCodes.Status401Unauthorized);

        request.Headers.TryAddWithoutValidation("Idempotency-Key", operationId.ToString("D"));
        try
        {
            using (request)
            using (var response = await _httpClient.SendAsync(request, cancellationToken))
            {
                var responseStatus = (int)response.StatusCode;
                if (!response.IsSuccessStatusCode)
                    return LifecycleFailure((int)response.StatusCode);

                var mediaType = response.Content.Headers.ContentType?.MediaType;
                if (!allowedSuccessStatusCodes.Contains(responseStatus)
                    || !IsJsonMediaType(mediaType))
                {
                    return LifecycleFailure(StatusCodes.Status502BadGateway);
                }

                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                LifecycleGatewayEnvelope? envelope;
                try
                {
                    envelope = JsonSerializer.Deserialize<LifecycleGatewayEnvelope>(body, _jsonOptions);
                }
                catch (JsonException exception)
                {
                    _logger.LogWarning(exception, "Global Product lifecycle proxy received malformed JSON.");
                    return LifecycleFailure(StatusCodes.Status502BadGateway);
                }

                if (envelope?.IsSuccessful != true || envelope.StatusCode != responseStatus)
                    return LifecycleFailure(StatusCodes.Status502BadGateway);

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
            _logger.LogWarning(exception, "Global Product lifecycle proxy timed out.");
            return LifecycleFailure(StatusCodes.Status504GatewayTimeout);
        }
        catch (HttpRequestException exception)
        {
            _logger.LogError(exception, "Global Product lifecycle proxy failed.");
            return LifecycleFailure(StatusCodes.Status503ServiceUnavailable);
        }
    }

    private IActionResult LifecycleFailure(int statusCode)
    {
        var normalizedStatus = statusCode is 400 or 401 or 403 or 404 or 409 or 503 or 504
            ? statusCode
            : StatusCodes.Status502BadGateway;
        return StatusCode(normalizedStatus, new
        {
            success = false,
            errors = new[] { MapLifecycleStatusMessage(normalizedStatus) }
        });
    }

    private string MapLifecycleStatusMessage(int statusCode) => statusCode switch
    {
        StatusCodes.Status400BadRequest => _localizer["ErrorValidation"].Value,
        StatusCodes.Status401Unauthorized => _localizer["ErrorUnauthorized"].Value,
        StatusCodes.Status403Forbidden => _localizer["ErrorForbidden"].Value,
        StatusCodes.Status404NotFound => _localizer["ErrorNotFound"].Value,
        StatusCodes.Status409Conflict => _localizer["ErrorConflict"].Value,
        StatusCodes.Status503ServiceUnavailable => _localizer["ErrorServiceUnavailable"].Value,
        StatusCodes.Status504GatewayTimeout => _localizer["ErrorTimeout"].Value,
        _ => _localizer["ErrorGateway"].Value
    };

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
        AppendLengthPrefixed(hash, expectedVersion.ToString(System.Globalization.CultureInfo.InvariantCulture));
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
