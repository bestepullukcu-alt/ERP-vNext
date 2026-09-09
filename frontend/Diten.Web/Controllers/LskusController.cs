using System.Buffers.Binary;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Diten.Web.Models.Lskus;
using Diten.Web.Security;
using Diten.Web.Views.MasterDataManagement.Lskus;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Diten.Web.Controllers;

[Authorize]
[Route("MasterDataManagement/Lskus")]
public sealed class LskusController : Controller
{
    private const string ReadPermission = "mdm.lskus.read";
    private const string CreatePermission = "mdm.lskus.create";
    private const string SubmitPermission = "mdm.lskus.submit";
    private const string WithdrawPermission = "mdm.lskus.withdraw";
    private const string RequestRetirementPermission = "mdm.lskus.request-retirement";
    private readonly HttpClient _http;
    private readonly string _gateway;
    private readonly ITimeLimitedDataProtector _protector;
    private readonly IStringLocalizer<LskusIndex> _l10n;
    private readonly ILogger<LskusController> _logger;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public LskusController(
        HttpClient http,
        IConfiguration config,
        IDataProtectionProvider protection,
        IStringLocalizer<LskusIndex> l10n,
        ILogger<LskusController> logger)
    {
        _http = http;
        _gateway = (config["GatewayUrl"]
            ?? throw new InvalidOperationException("GatewayUrl configuration is required.")).TrimEnd('/');
        _protector = protection
            .CreateProtector("Diten.Web", "MOD-0290", "LskuFormAttempt", "v1")
            .ToTimeLimitedDataProtector();
        _l10n = l10n;
        _logger = logger;
    }

    [HttpGet("")]
    public IActionResult Index()
    {
        if (!Has(ReadPermission)) return Forbid();
        var canCreate = Has(CreatePermission);
        ViewData["CanCreateLsku"] = canCreate;
        ViewData["CanSubmitLsku"] = Has(SubmitPermission);
        ViewData["CanWithdrawLsku"] = Has(WithdrawPermission);
        ViewData["CanRequestRetirementLsku"] = Has(RequestRetirementPermission);
        if (canCreate) ViewData["LskuFormAttemptToken"] = NewToken();
        return View("~/Views/MasterDataManagement/Lskus/Index.cshtml");
    }

    [HttpGet("api")]
    public Task<IActionResult> List(CancellationToken cancellationToken) => Has(ReadPermission)
        ? Proxy(HttpMethod.Get, $"{_gateway}/api/lskus{Request.QueryString}", cancellationToken)
        : Task.FromResult<IActionResult>(Forbid());

    [HttpGet("api/{id:guid}")]
    public Task<IActionResult> Detail(Guid id, CancellationToken cancellationToken) => Has(ReadPermission)
        ? Proxy(HttpMethod.Get, $"{_gateway}/api/lskus/{id:D}", cancellationToken)
        : Task.FromResult<IActionResult>(Forbid());

    [HttpGet("api/create-options")]
    public Task<IActionResult> CreateOptions(CancellationToken cancellationToken) => Has(CreatePermission)
        ? Proxy(HttpMethod.Get, $"{_gateway}/api/lskus/create-options", cancellationToken)
        : Task.FromResult<IActionResult>(Forbid());

    [HttpPost("api")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        [FromForm] CreateLskuViewModel model,
        [FromForm] string? formAttemptToken,
        CancellationToken cancellationToken)
    {
        if (!Has(CreatePermission)) return Forbid();
        if (!TryToken(formAttemptToken, out var key))
            return BadRequest(new { success = false, errors = new[] { _l10n["ErrorInvalidFormAttempt"].Value } });
        if (!ModelState.IsValid || model.GskuId == Guid.Empty || string.IsNullOrWhiteSpace(model.MarketCode))
            return BadRequest(new { success = false, errors = new[] { _l10n["ErrorValidation"].Value } });
        if (!RequestMessage(
                HttpMethod.Post,
                $"{_gateway}/api/lskus/drafts",
                JsonContent.Create(new
                {
                    gskuId = model.GskuId,
                    marketCode = model.MarketCode.Trim()
                }, options: _json),
                out var request))
        {
            return Unauthorized();
        }

        request.Headers.TryAddWithoutValidation("Idempotency-Key", key);
        using (request)
        using (var response = await _http.SendAsync(request, cancellationToken))
        {
            if (response.StatusCode == HttpStatusCode.Accepted)
                return StatusCode(202, new { success = false, errors = new[] { _l10n["CreateReconciliationPending"].Value }, formAttemptToken });
            if (response.StatusCode == HttpStatusCode.Created)
            {
                var envelope = await response.Content.ReadFromJsonAsync<LskuGatewayResponse<LskuDraftViewModel>>(_json, cancellationToken);
                if (envelope?.IsSuccessful == true && envelope.Data is not null)
                    return StatusCode(201, new { success = true, data = envelope.Data, formAttemptToken = NewToken() });
            }
            return Failure(response.StatusCode);
        }
    }

    [HttpPost("api/{id:guid}/submit")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> SubmitLifecycle(
        Guid id,
        [FromForm] int? expectedVersion,
        CancellationToken cancellationToken) =>
        ExecuteLifecycleAsync(id, expectedVersion, null, "submit", SubmitPermission, cancellationToken);

    [HttpPost("api/{id:guid}/identity-approval/withdraw")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> WithdrawIdentityApproval(
        Guid id,
        [FromForm] int? expectedVersion,
        [FromForm] string? reasonCode,
        [FromForm] string? comment,
        CancellationToken cancellationToken)
    {
        if (!Has(WithdrawPermission)) return Failure(HttpStatusCode.Forbidden);
        if (id == Guid.Empty || expectedVersion is null or < 1
            || !HasExactText(reasonCode, 128)
            || comment is not null && !HasOptionalExactText(comment, 2000)
            || !await HasOnlyFormFieldsAsync("ExpectedVersion", "ReasonCode", "Comment"))
        {
            return Failure(HttpStatusCode.BadRequest);
        }
        if (!TryResolveLifecycleIdentity(out var tenantId, out var actor))
            return Failure(HttpStatusCode.Unauthorized);

        var operationId = CreateLifecycleOperationId(
            tenantId, actor, "lsku", "withdraw", id, expectedVersion.Value, $"{reasonCode}\n{comment}");
        if (!RequestMessage(
                HttpMethod.Post,
                $"{_gateway}/api/lskus/{id:D}/identity-approval/withdraw",
                JsonContent.Create(new { expectedVersion = expectedVersion.Value, reasonCode, comment }, options: _json),
                out var request))
        {
            return Failure(HttpStatusCode.Unauthorized);
        }

        request.Headers.TryAddWithoutValidation("Idempotency-Key", operationId.ToString("D"));
        return await SendLifecycleRequestAsync(request, "withdraw",
            new HashSet<int> { StatusCodes.Status200OK, StatusCodes.Status202Accepted }, cancellationToken);
    }

    [HttpPost("api/{id:guid}/retirement-requests")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestRetirement(
        Guid id,
        [FromForm] int? expectedVersion,
        [FromForm] string? requestReason,
        CancellationToken cancellationToken)
    {
        if (!Has(RequestRetirementPermission)) return Failure(HttpStatusCode.Forbidden);
        if (id == Guid.Empty || expectedVersion is null or < 1
            || !HasExactText(requestReason, 128)
            || !await HasOnlyFormFieldsAsync("ExpectedVersion", "RequestReason"))
        {
            return Failure(HttpStatusCode.BadRequest);
        }
        if (!TryResolveLifecycleIdentity(out var tenantId, out var actor))
            return Failure(HttpStatusCode.Unauthorized);

        var operationId = CreateLifecycleOperationId(
            tenantId, actor, "lsku", "request-retirement", id, expectedVersion.Value, requestReason!);
        if (!RequestMessage(
                HttpMethod.Post,
                $"{_gateway}/api/lskus/{id:D}/retirement-requests",
                JsonContent.Create(new { expectedVersion = expectedVersion.Value, requestReason }, options: _json),
                out var request))
        {
            return Failure(HttpStatusCode.Unauthorized);
        }

        request.Headers.TryAddWithoutValidation("Idempotency-Key", operationId.ToString("D"));
        return await SendLifecycleRequestAsync(request, "request-retirement",
            new HashSet<int> { StatusCodes.Status200OK, StatusCodes.Status202Accepted }, cancellationToken);
    }

    private async Task<IActionResult> ExecuteLifecycleAsync(
        Guid id,
        int? expectedVersion,
        string? reasonCode,
        string action,
        string permission,
        CancellationToken cancellationToken)
    {
        if (!Has(permission)) return Failure(HttpStatusCode.Forbidden);
        reasonCode = reasonCode?.Trim();
        if (id == Guid.Empty || expectedVersion is null or < 0
            || action != "submit" || !string.IsNullOrEmpty(reasonCode)
            || !await HasOnlyFormFieldsAsync("ExpectedVersion"))
        {
            return Failure(HttpStatusCode.BadRequest);
        }
        if (!TryResolveLifecycleIdentity(out var tenantId, out var actor))
            return Failure(HttpStatusCode.Unauthorized);

        var operationId = CreateLifecycleOperationId(
            tenantId, actor, "lsku", action, id, expectedVersion.Value, reasonCode ?? string.Empty);
        var content = JsonContent.Create(new { expectedVersion = expectedVersion.Value }, options: _json);
        IReadOnlySet<int> allowedSuccessStatusCodes = new HashSet<int> { StatusCodes.Status200OK, StatusCodes.Status202Accepted };
        if (!RequestMessage(HttpMethod.Post, $"{_gateway}/api/lskus/{id:D}/{action}", content, out var request))
            return Failure(HttpStatusCode.Unauthorized);

        request.Headers.TryAddWithoutValidation("Idempotency-Key", operationId.ToString("D"));
        return await SendLifecycleRequestAsync(request, action, allowedSuccessStatusCodes, cancellationToken);
    }

    private async Task<IActionResult> SendLifecycleRequestAsync(
        HttpRequestMessage request,
        string action,
        IReadOnlySet<int> allowedSuccessStatusCodes,
        CancellationToken cancellationToken)
    {
        try
        {
            using (request)
            using (var response = await _http.SendAsync(request, cancellationToken))
            {
                if (!response.IsSuccessStatusCode) return Failure(response.StatusCode);
                var responseStatus = (int)response.StatusCode;
                var mediaType = response.Content.Headers.ContentType?.MediaType;
                if (!allowedSuccessStatusCodes.Contains(responseStatus) || !IsJsonMediaType(mediaType))
                    return Failure(HttpStatusCode.BadGateway);

                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                LifecycleGatewayEnvelope? envelope;
                try
                {
                    envelope = JsonSerializer.Deserialize<LifecycleGatewayEnvelope>(body, _json);
                }
                catch (JsonException exception)
                {
                    _logger.LogWarning("LSKU lifecycle proxy received malformed JSON for {Action}: {FailureType}.", action, exception.GetType().Name);
                    return Failure(HttpStatusCode.BadGateway);
                }
                if (envelope?.IsSuccessful != true || envelope.StatusCode != responseStatus)
                    return Failure(HttpStatusCode.BadGateway);

                return new ContentResult { StatusCode = responseStatus, ContentType = "application/json", Content = body };
            }
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("LSKU lifecycle proxy timed out for {Action}: {FailureType}.", action, exception.GetType().Name);
            return Failure(HttpStatusCode.GatewayTimeout);
        }
        catch (HttpRequestException exception)
        {
            _logger.LogError("LSKU lifecycle proxy failed for {Action}: {FailureType}.", action, exception.GetType().Name);
            return Failure(HttpStatusCode.ServiceUnavailable);
        }
    }

    private async Task<IActionResult> Proxy(HttpMethod method, string url, CancellationToken cancellationToken)
    {
        if (!RequestMessage(method, url, null, out var request)) return Unauthorized();
        using (request)
        using (var response = await _http.SendAsync(request, cancellationToken))
        {
            if (!response.IsSuccessStatusCode) return Failure(response.StatusCode);
            return Content(
                await response.Content.ReadAsStringAsync(cancellationToken),
                response.Content.Headers.ContentType?.ToString() ?? "application/json");
        }
    }

    private bool RequestMessage(HttpMethod method, string url, HttpContent? content, out HttpRequestMessage request)
    {
        request = new HttpRequestMessage(method, url) { Content = content };
        var token = Diten.Web.Services.Auth.AuthTokenCookies.GetAccessToken(Request);
        if (!string.IsNullOrWhiteSpace(token)) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var tenant = User.Claims.FirstOrDefault(claim =>
            claim.Type is "tenantId" or "tenant_id"
            || claim.Type.EndsWith("/tenantId", StringComparison.OrdinalIgnoreCase))?.Value;
        if (!Guid.TryParse(tenant, out var tenantId))
        {
            request.Dispose();
            request = null!;
            return false;
        }
        request.Headers.Add("X-Tenant-Id", tenantId.ToString("D"));
        return true;
    }

    private IActionResult Failure(HttpStatusCode code)
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
        return StatusCode(status, new { success = false, errors = new[] { _l10n[key].Value } });
    }

    private async Task<bool> HasOnlyFormFieldsAsync(params string[] allowedFields)
    {
        if (!Request.HasFormContentType) return false;
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

    private static bool HasExactText(string? value, int maximumLength) =>
        !string.IsNullOrEmpty(value) && HasOptionalExactText(value, maximumLength);

    private static bool HasOptionalExactText(string value, int maximumLength) =>
        value.Length <= maximumLength
        && string.Equals(value, value.Trim(), StringComparison.Ordinal)
        && !value.Any(char.IsControl);

    private bool TryResolveLifecycleIdentity(out Guid tenantId, out string actor)
    {
        tenantId = Guid.Empty;
        actor = string.Empty;
        if (User.Identity?.IsAuthenticated != true || !IsHumanActorType(SingleClaim(User, "actor_type"))) return false;
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
        if (subjectId == Guid.Empty) return false;
        if (!TryResolveCanonicalTenant(User, out tenantId)) return false;
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
        if (values.Length == 0) return false;

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
        if (values.Count == 0) return true;
        if (!Guid.TryParseExact(values[0], "D", out var parsed) || parsed == Guid.Empty) return false;
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
        return Guid.ParseExact($"{hex[..8]}-{hex[8..12]}-{hex[12..16]}-{hex[16..20]}-{hex[20..32]}", "D");
    }

    private static void AppendLengthPrefixed(IncrementalHash hash, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        Span<byte> length = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(length, bytes.Length);
        hash.AppendData(length);
        hash.AppendData(bytes);
    }

    private string NewToken()
    {
        var payload = new Attempt(Subject(), Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));
        return _protector.Protect(JsonSerializer.Serialize(payload, _json), TimeSpan.FromMinutes(30));
    }

    private bool TryToken(string? token, out string key)
    {
        key = string.Empty;
        try
        {
            if (string.IsNullOrWhiteSpace(token)) return false;
            var payload = JsonSerializer.Deserialize<Attempt>(_protector.Unprotect(token, out _), _json);
            if (payload is null || payload.Subject != Subject() || string.IsNullOrWhiteSpace(payload.Key)) return false;
            key = payload.Key;
            return true;
        }
        catch (CryptographicException) { return false; }
        catch (JsonException) { return false; }
    }

    private bool Has(string key) => PermissionClaims.HasPermission(User, key);
    private string Subject() => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue("sub")
        ?? User.Identity?.Name
        ?? string.Empty;
    private sealed record Attempt(string Subject, string Key);
    private sealed class LifecycleGatewayEnvelope
    {
        public bool IsSuccessful { get; init; }
        public int StatusCode { get; init; }
    }
}
