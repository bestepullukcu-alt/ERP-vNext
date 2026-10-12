using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Diten.Web.Models.ProductLegalEntityScopes;
using Diten.Web.Security;
using Diten.Web.Services.Auth;
using Diten.Web.Views.MasterDataManagement.ProductLegalEntityScopes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Diten.Web.Controllers;

[Authorize]
[Route("MasterDataManagement/ProductLegalEntityScopes")]
public sealed class ProductLegalEntityScopesController : Controller
{
    private const string ReadPermission = "mdm.product-legal-entity-scopes.read";
    private const string ConfigurePermission = "mdm.product-legal-entity-scopes.configure";
    private const string ReplacePermission = "mdm.product-legal-entity-scopes.replace";
    private const string EndPermission = "mdm.product-legal-entity-scopes.end";
    private readonly HttpClient _http;
    private readonly string _gateway;
    private readonly ITimeLimitedDataProtector _protector;
    private readonly IStringLocalizer<ProductLegalEntityScopesIndex> _localizer;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };

    public ProductLegalEntityScopesController(
        HttpClient http,
        IConfiguration configuration,
        IDataProtectionProvider protection,
        IStringLocalizer<ProductLegalEntityScopesIndex> localizer)
    {
        _http = http;
        _gateway = (configuration["GatewayUrl"] ?? throw new InvalidOperationException("GatewayUrl configuration is required.")).TrimEnd('/');
        _protector = protection.CreateProtector("Diten.Web", "MOD-0290-FU03", "ProductLegalEntityScopeAttempt", "v1").ToTimeLimitedDataProtector();
        _localizer = localizer;
    }

    /// <summary>
    /// UAS-001 / WP-MDM-SCOPE-ACCEPT-01 FIX1 B5 — a signed-in reader without the permission gets a 403 INSIDE the shell (the
    /// view draws only the _AccessDenied partial, no page skeleton). The former redirect went to an AccessDeniedPath this
    /// app does not configure (302 → 404).
    /// </summary>
    [HttpGet("")]
    public IActionResult Index()
    {
        ViewData["CanReadProductScope"] = Has(ReadPermission);
        if (!Has(ReadPermission))
        {
            Response.StatusCode = StatusCodes.Status403Forbidden;
            return View("~/Views/MasterDataManagement/ProductLegalEntityScopes/Index.cshtml");
        }
        ViewData["CanConfigureProductScope"] = Has(ConfigurePermission);
        ViewData["CanReplaceProductScope"] = Has(ReplacePermission);
        ViewData["CanEndProductScope"] = Has(EndPermission);
        if (Has(ConfigurePermission) || Has(ReplacePermission) || Has(EndPermission))
            ViewData["ProductScopeAttemptToken"] = NewAttemptToken();
        return View("~/Views/MasterDataManagement/ProductLegalEntityScopes/Index.cshtml");
    }

    /// <summary>
    /// WP-MDM-SCOPE-ACCEPT-01 FIX1 (BL-440 list standard) — the list component's server request (start, length, search,
    /// orderBy, orderDir) becomes MDM's Global Product page query (the Global Product page's own translation, one rule for
    /// both lists); MDM's page becomes the component's envelope { data: { items, total, filteredTotal } }. MDM is the judge
    /// of the order (unknown key = 400) and of which products the caller may see.
    /// </summary>
    [HttpGet("api")]
    public async Task<IActionResult> Products(CancellationToken cancellationToken)
    {
        if (!Has(ReadPermission)) return Denied();
        if (!TryRequest(HttpMethod.Get, $"{_gateway}/api/global-products?{GlobalProductsController.BuildListQuery(Request.Query)}", null, out var request))
            return Unauthorized();
        using (request)
        using (var response = await _http.SendAsync(request, cancellationToken))
        {
            if (!response.IsSuccessStatusCode) return Failure(response.StatusCode);
            try
            {
                using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
                var root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object
                    || !(root.TryGetProperty("data", out var page) || root.TryGetProperty("Data", out page))
                    || page.ValueKind != JsonValueKind.Object
                    || !(page.TryGetProperty("items", out var items) || page.TryGetProperty("Items", out items))
                    || items.ValueKind != JsonValueKind.Array
                    || !(page.TryGetProperty("totalCount", out var count) || page.TryGetProperty("TotalCount", out count))
                    || !count.TryGetInt64(out var total) || total < 0)
                    return Failure(HttpStatusCode.BadGateway);
                return new ContentResult
                {
                    StatusCode = StatusCodes.Status200OK,
                    ContentType = "application/json",
                    Content = JsonSerializer.Serialize(new { success = true, data = new { items = items.Clone(), total, filteredTotal = total } }, _json)
                };
            }
            catch (JsonException)
            {
                return Failure(HttpStatusCode.BadGateway);
            }
        }
    }

    [HttpGet("api/{globalProductId:guid}")]
    public Task<IActionResult> Policy(Guid globalProductId, CancellationToken cancellationToken) =>
        Has(ReadPermission)
            ? Proxy(HttpMethod.Get, PolicyUrl(globalProductId), null, cancellationToken)
            : Task.FromResult(Denied());

    [HttpGet("api/{globalProductId:guid}/history")]
    public Task<IActionResult> History(Guid globalProductId, CancellationToken cancellationToken) =>
        Has(ReadPermission)
            ? Proxy(HttpMethod.Get, $"{PolicyUrl(globalProductId)}/history", null, cancellationToken)
            : Task.FromResult(Denied());

    [HttpGet("api/{globalProductId:guid}/effective")]
    public Task<IActionResult> Effective(Guid globalProductId, CancellationToken cancellationToken) =>
        Has(ReadPermission)
            ? Proxy(HttpMethod.Get, $"{PolicyUrl(globalProductId)}/effective", null, cancellationToken)
            : Task.FromResult(Denied());

    [HttpGet("api/{globalProductId:guid}/create-options")]
    public Task<IActionResult> CreateOptions(Guid globalProductId, CancellationToken cancellationToken) =>
        Has(ConfigurePermission)
            ? Proxy(HttpMethod.Get, $"{PolicyUrl(globalProductId)}/create-options", null, cancellationToken)
            : Task.FromResult(Denied());

    [HttpPost("api")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Create(
        [FromForm] ProductLegalEntityScopeWriteViewModel model,
        [FromForm] string? formAttemptToken,
        CancellationToken cancellationToken) =>
        WritePolicy(ConfigurePermission, model, formAttemptToken, string.Empty, requireExpectedVersion: false, cancellationToken);

    [HttpPost("api/replace")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Replace(
        [FromForm] ProductLegalEntityScopeWriteViewModel model,
        [FromForm] string? formAttemptToken,
        CancellationToken cancellationToken) =>
        WritePolicy(ReplacePermission, model, formAttemptToken, "/replace", requireExpectedVersion: true, cancellationToken);

    [HttpPost("api/end")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> End(
        [FromForm] ProductLegalEntityScopeEndViewModel model,
        [FromForm] string? formAttemptToken,
        CancellationToken cancellationToken)
    {
        if (!Has(EndPermission)) return Denied();
        if (!TryAttempt(formAttemptToken, out var operationKey)) return InvalidAttempt();
        if (!ModelState.IsValid || model.GlobalProductId == Guid.Empty) return ValidationFailure();
        return await SendWrite(model.GlobalProductId, "/end", new { expectedVersion = model.ExpectedVersion }, operationKey, formAttemptToken!, cancellationToken);
    }

    private async Task<IActionResult> WritePolicy(
        string permission,
        ProductLegalEntityScopeWriteViewModel model,
        string? formAttemptToken,
        string suffix,
        bool requireExpectedVersion,
        CancellationToken cancellationToken)
    {
        if (!Has(permission)) return Denied();
        if (!TryAttempt(formAttemptToken, out var operationKey)) return InvalidAttempt();
        if (!ModelState.IsValid || model.GlobalProductId == Guid.Empty || model.Mode is < 1 or > 2 || requireExpectedVersion && !model.ExpectedVersion.HasValue)
            return ValidationFailure();
        object payload = requireExpectedVersion
            ? new { expectedVersion = model.ExpectedVersion!.Value, mode = model.Mode, legalEntityIds = model.LegalEntityIds }
            : new { mode = model.Mode, legalEntityIds = model.LegalEntityIds };
        return await SendWrite(model.GlobalProductId, suffix, payload, operationKey, formAttemptToken!, cancellationToken);
    }

    private async Task<IActionResult> SendWrite(Guid productId, string suffix, object payload, string operationKey, string currentToken, CancellationToken cancellationToken)
    {
        using var content = JsonContent.Create(payload, options: _json);
        if (!TryRequest(HttpMethod.Post, PolicyUrl(productId) + suffix, content, out var request)) return Unauthorized();
        request.Headers.TryAddWithoutValidation("Idempotency-Key", operationKey);
        using (request)
        using (var response = await _http.SendAsync(request, cancellationToken))
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if ((int)response.StatusCode is 200 or 201)
                return StatusCode((int)response.StatusCode, new { success = true, data = TryPayload(body), formAttemptToken = NewAttemptToken() });
            if (response.StatusCode == HttpStatusCode.Accepted)
                return StatusCode(202, new { success = false, errors = new[] { _localizer["ReconciliationPending"].Value }, formAttemptToken = currentToken });
            return Refusal(response.StatusCode, body);
        }
    }

    /// <summary>
    /// BL-598 FIX2 item 1 (the GskuRefusals bridge): the codes the scope write can be refused with, bound to the sentence
    /// the reader sees (ProductLegalEntityScopesIndex, seven languages). THE single list — the page shows the sentence
    /// this controller sends and keeps no code map of its own. Only a code listed here is turned into text; any other
    /// answer (another code, an unreadable body) is the status's general sentence — no other content of MDM's answer
    /// reaches the screen. The guard test holds every key here in all seven resx files.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> RefusalKeys = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["PRODUCT_SCOPE_WRITE_FENCE_ACTIVE"] = "ErrorFenceActive",
        ["PRODUCT_SCOPE_WRITER_LEASE_UNAVAILABLE"] = "ErrorWriterBusy",
        ["PRODUCT_SCOPE_WRITER_LEASE_CONTENDED"] = "ErrorWriterBusy",
        ["PRODUCT_SCOPE_FAIL_CLOSED_SUSPENDED"] = "ErrorSuspended",
        ["PRODUCT_LEGAL_ENTITY_SCOPE_ROLLOUT_INVALID"] = "ErrorRolloutUnavailable",
        ["PRODUCT_SCOPE_WRITER_LEASE_CAPACITY_EXCEEDED"] = "ErrorWriterCapacity",
        ["PRODUCT_SCOPE_ROLLOUT_STORE_UNAVAILABLE"] = "ErrorStoreUnavailable",
        ["PRODUCT_SCOPE_WRITER_LEASE_AMBIGUOUS"] = "ErrorWriterBusy",
        ["PRODUCT_SCOPE_REPLACE_CONTENDED"] = "ErrorWriterBusy",
        ["PRODUCT_SCOPE_RECONCILIATION_REQUIRED"] = "ReconciliationPending",
        ["PRODUCT_SCOPE_POLICY_STORE_NOT_READY"] = "ErrorStoreNotReady"
    };

    private IActionResult Refusal(HttpStatusCode statusCode, string body)
    {
        if (FirstCode(body) is { } code && RefusalKeys.TryGetValue(code, out var key))
            return StatusCode((int)statusCode, new { success = false, errors = new[] { _localizer[key].Value } });
        return Failure(statusCode);
    }

    // MDM's envelope: the first entry of errors / Errors, when it is a string. Anything else: no code.
    private string? FirstCode(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !(root.TryGetProperty("errors", out var errors) || root.TryGetProperty("Errors", out errors))
                || errors.ValueKind != JsonValueKind.Array || errors.GetArrayLength() == 0
                || errors[0].ValueKind != JsonValueKind.String)
                return null;
            return errors[0].GetString();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private async Task<IActionResult> Proxy(HttpMethod method, string url, HttpContent? content, CancellationToken cancellationToken)
    {
        if (!TryRequest(method, url, content, out var request)) return Unauthorized();
        using (request)
        using (var response = await _http.SendAsync(request, cancellationToken))
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            return new ContentResult { StatusCode = (int)response.StatusCode, ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/json", Content = body };
        }
    }

    private bool TryRequest(HttpMethod method, string url, HttpContent? content, out HttpRequestMessage request)
    {
        request = new HttpRequestMessage(method, url) { Content = content };
        var token = AuthTokenCookies.GetAccessToken(Request);
        if (!string.IsNullOrWhiteSpace(token)) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var canonicalTenantValues = User.Claims
            .Where(claim => claim.Type == "tenant_id")
            .Select(claim => claim.Value)
            .ToArray();
        var legacyTenantValues = User.Claims
            .Where(claim => claim.Type == "tenantId"
                || claim.Type.EndsWith("/tenantId", StringComparison.OrdinalIgnoreCase))
            .Select(claim => claim.Value)
            .ToArray();
        var supportedTenantValues = User.Claims
            .Where(claim => claim.Type is "tenantId" or "tenant_id"
                || claim.Type.EndsWith("/tenantId", StringComparison.OrdinalIgnoreCase))
            .Select(claim => claim.Value)
            .ToArray();
        var tenant = supportedTenantValues.FirstOrDefault();
        if (Guid.TryParse(tenant, out var tenantId)
            && canonicalTenantValues.Length <= 1
            && legacyTenantValues.Length <= 1
            && !supportedTenantValues.Any(value => Guid.TryParse(value, out var supportedTenantId)
                && supportedTenantId != tenantId)
            && (canonicalTenantValues.Length == 0
                || Guid.TryParse(canonicalTenantValues[0], out var canonicalTenantId)
                    && canonicalTenantId == tenantId
                    && User.Claims
                        .Where(claim => claim.Type == "tenantId"
                            || claim.Type.EndsWith("/tenantId", StringComparison.OrdinalIgnoreCase))
                        .Select(claim => claim.Value)
                        .All(value => Guid.TryParse(value, out var legacyTenantId)
                            && legacyTenantId == canonicalTenantId)))
        {
            request.Headers.Add("X-Tenant-Id", tenantId.ToString("D"));
            return true;
        }
        request.Dispose();
        request = null!;
        return false;
    }

    private IActionResult Failure(HttpStatusCode statusCode)
    {
        var key = statusCode switch
        {
            HttpStatusCode.Forbidden => "ErrorForbidden",
            HttpStatusCode.NotFound => "ErrorNotFound",
            HttpStatusCode.Conflict => "ErrorConflict",
            HttpStatusCode.ServiceUnavailable => "ErrorProviderUnavailable",
            HttpStatusCode.GatewayTimeout => "ErrorProviderTimeout",
            _ => "ErrorGateway"
        };
        return StatusCode((int)statusCode, new { success = false, errors = new[] { _localizer[key].Value } });
    }

    private object? TryPayload(string body)
    {
        try
        {
            var envelope = JsonSerializer.Deserialize<JsonElement>(body, _json);
            if (envelope.ValueKind != JsonValueKind.Object) return null;
            if (envelope.TryGetProperty("data", out var camelData)) return camelData.Clone();
            if (envelope.TryGetProperty("Data", out var pascalData)) return pascalData.Clone();
            return null;
        }
        catch (JsonException) { return null; }
    }

    private string PolicyUrl(Guid productId) => $"{_gateway}/api/global-products/{productId:D}/legal-entity-scope-policy";
    private bool Has(string permission) => PermissionClaims.HasPermission(User, permission);
    private string Subject() => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub") ?? User.Identity?.Name ?? string.Empty;
    private string NewAttemptToken() => _protector.Protect(JsonSerializer.Serialize(new Attempt(Subject(), Guid.NewGuid().ToString("D")), _json), TimeSpan.FromMinutes(30));
    private bool TryAttempt(string? token, out string key)
    {
        key = string.Empty;
        try
        {
            if (string.IsNullOrWhiteSpace(token)) return false;
            var attempt = JsonSerializer.Deserialize<Attempt>(_protector.Unprotect(token, out _), _json);
            if (attempt is null || attempt.Subject != Subject() || string.IsNullOrWhiteSpace(attempt.Key)) return false;
            key = attempt.Key;
            return true;
        }
        catch (CryptographicException) { return false; }
        catch (JsonException) { return false; }
    }

    /// <summary>FIX1 B5 — a coded 403 for the page's own calls; never a redirect.</summary>
    private IActionResult Denied() => StatusCode(StatusCodes.Status403Forbidden, new
    {
        success = false,
        errors = new[] { _localizer["ErrorForbidden"].Value },
        errorCode = "PRODUCT_SCOPE_PERMISSION_DENIED"
    });

    private IActionResult InvalidAttempt() => BadRequest(new { success = false, errors = new[] { _localizer["ErrorInvalidFormAttempt"].Value } });
    private IActionResult ValidationFailure() => BadRequest(new { success = false, errors = new[] { _localizer["ErrorValidation"].Value } });
    private sealed record Attempt(string Subject, string Key);
}
