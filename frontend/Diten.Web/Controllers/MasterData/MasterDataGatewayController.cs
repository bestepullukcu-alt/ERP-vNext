using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Diten.Web.Models.MasterData;
using Diten.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Diten.Web.Controllers.MasterData;

/// <summary>
/// MOD-0290-FU02 — shared Gateway plumbing for the Master Data surfaces so the Brands and Products controllers
/// cannot drift apart on auth, tenancy or error handling.
///
/// Every business call leaves from the SERVER through Gateway 5000. The browser never sees the MdmService port
/// (5059), a bearer token, or a raw service URL: the HttpOnly access-token cookie is read server-side here and
/// forwarded as an Authorization header.
/// </summary>
public abstract class MasterDataGatewayController : Controller
{
    protected const string ContractPath = "/api/mdm/brand-products/contract";

    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly ILogger _logger;

    protected readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    protected MasterDataGatewayController(HttpClient httpClient, IConfiguration configuration, ILogger logger)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"]
            ?? throw new InvalidOperationException("GatewayUrl configuration is required.");
        _logger = logger;
    }

    protected async Task<BrandProductContractViewModel?> LoadContractAsync(CancellationToken cancellationToken)
    {
        var response = await SendGatewayAsync(HttpMethod.Get, ContractPath, null, cancellationToken);
        if (response is null || !response.IsSuccessStatusCode)
        {
            return null;
        }

        return (await response.Content
            .ReadFromJsonAsync<BrandProductGatewayResponse<BrandProductContractViewModel>>(JsonOptions, cancellationToken))?.Data;
    }

    protected async Task<HttpResponseMessage?> SendGatewayAsync(
        HttpMethod method, string path, object? body, CancellationToken cancellationToken,
        Action<HttpRequestMessage>? configure = null)
    {
        try
        {
            using var request = new HttpRequestMessage(method, $"{_gatewayUrl}{path}");
            // WP-MDM-BRAND-ACCEPT-01 FIX2 (M2) — one correlation id per browser request, sent on every MDM call it makes, so
            // the brand history row and the central audit event carry the same id the caller sent (or one made here).
            request.Headers.TryAddWithoutValidation(CorrelationHeader, CorrelationId);
            configure?.Invoke(request);

            var token = Diten.Web.Services.Auth.AuthTokenCookies.GetAccessToken(Request);
            if (!string.IsNullOrWhiteSpace(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            // MdmService fails closed without a tenant, so an unresolved tenant is short-circuited here rather
            // than sent as an anonymous-looking request.
            var tenantId = GetTenantId();
            if (string.IsNullOrWhiteSpace(tenantId))
            {
                return null;
            }

            request.Headers.TryAddWithoutValidation("X-Tenant-Id", tenantId);

            if (body is not null)
            {
                var json = body is JsonElement element ? element.GetRawText() : JsonSerializer.Serialize(body, JsonOptions);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            }

            return await _httpClient.SendAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            // The Authorization header is deliberately never logged.
            _logger.LogError(ex, "Master Data Gateway request failed: {Method} {Path}", method, path);
            return null;
        }
    }

    internal const string CorrelationHeader = "X-Correlation-Id";
    private static readonly object CorrelationItemKey = new();

    /// <summary>
    /// FIX2 (M2) — the caller's X-Correlation-Id when it is a Guid, otherwise a fresh Guid; Guid "D", kept for the rest of
    /// this request. MDM normalises the same way, so a value that is not a Guid never reaches a record.
    /// </summary>
    protected string CorrelationId
    {
        get
        {
            if (HttpContext.Items.TryGetValue(CorrelationItemKey, out var stored) && stored is string known)
            {
                return known;
            }

            var resolved = Guid.TryParse(Request.Headers[CorrelationHeader].ToString().Trim(), out var incoming) && incoming != Guid.Empty
                ? incoming.ToString("D")
                : Guid.NewGuid().ToString("D");
            HttpContext.Items[CorrelationItemKey] = resolved;
            return resolved;
        }
    }

    protected async Task<IActionResult> ProxyGetAsync(string path, string permission, CancellationToken cancellationToken, params string[] alternatives)
    {
        if (RequireJson(permission, alternatives) is { } denied)
        {
            return denied;
        }

        return await RelayAsync(await SendGatewayAsync(HttpMethod.Get, path, null, cancellationToken), cancellationToken);
    }

    protected async Task<IActionResult> ProxyJsonAsync(
        HttpMethod method, string path, JsonElement? body, string permission, CancellationToken cancellationToken, params string[] alternatives)
    {
        if (RequireJson(permission, alternatives) is { } denied)
        {
            return denied;
        }

        // Belt-and-braces: even though no view model carries TenantId, a hand-crafted payload is refused here
        // rather than forwarded for the backend to ignore.
        if (body.HasValue && ContainsTenantId(body.Value))
        {
            return RefusalLocalizer is null
                ? BadRequest(new { errors = new[] { "TenantId is server-resolved and must not be supplied." } })
                : Refusal(StatusCodes.Status400BadRequest, null);
        }

        return await RelayAsync(await SendGatewayAsync(method, path, body, cancellationToken), cancellationToken);
    }

    // ── WP-MDM-BRAND-ACCEPT-01 FIX1 (item 7) — a refusal reaches the reader as a sentence in their language ─────────────
    // MDM writes "brand_code_duplicate: A brand with this code …" (BrandProductFailures). A screen that names a localizer
    // here (Brands) answers every refusal with the sentence for that code — `Reason_{code}` — and, for a code without
    // one, the sentence for the HTTP status (`RefusedStatus_{status}`) marked `unmapped` so the page warns in the
    // console. Never the code, never MDM's English. Null (Products today) relays MDM's answer exactly as before.

    protected virtual IStringLocalizer? RefusalLocalizer => null;

    private static readonly Regex ReasonCodePrefix = new("^(?<code>[a-z][a-z0-9_]*):", RegexOptions.CultureInvariant);

    private static readonly Regex FieldRefusal = new(
        "^field_invalid:\\s*(?<field>[A-Za-z][A-Za-z0-9]*)", RegexOptions.CultureInvariant);

    /// <summary>
    /// WP-MDM-BRAND-ACCEPT-01 FIX2 (M5) — MDM's field-shape refusals (<c>field_invalid: Description — …</c>), one sentence
    /// per field in the reader's language, the field named by the form's own label. Empty when there are none.
    /// </summary>
    internal static IReadOnlyList<string> FieldSentences(IStringLocalizer localizer, IEnumerable<string> errors)
    {
        var template = localizer[$"Reason_{FieldInvalidCode}"];
        if (template.ResourceNotFound)
        {
            return [];
        }

        return errors
            .Select(error => FieldRefusal.Match(error ?? string.Empty))
            .Where(match => match.Success)
            .Select(match => match.Groups["field"].Value)
            .Distinct(StringComparer.Ordinal)
            .Select(field =>
            {
                var label = localizer[field];
                return string.Format(template.Value, label.ResourceNotFound ? field : label.Value);
            })
            .ToList();
    }

    internal const string FieldInvalidCode = "field_invalid";

    /// <summary>The sentence for a refusal: the first error whose reason code has one, else the status sentence.</summary>
    internal static (string Sentence, string? ReasonCode, bool Unmapped) TranslateRefusal(
        IStringLocalizer localizer, IEnumerable<string> errors, int statusCode)
    {
        var list = errors as IReadOnlyCollection<string> ?? errors.ToList();
        if (FieldSentences(localizer, list) is { Count: > 0 } fields)
        {
            return (string.Join(" ", fields), FieldInvalidCode, false);
        }

        string? firstCode = null;
        foreach (var error in list)
        {
            var match = ReasonCodePrefix.Match(error ?? string.Empty);
            if (!match.Success)
            {
                continue;
            }

            var code = match.Groups["code"].Value;
            firstCode ??= code;
            var sentence = localizer[$"Reason_{code}"];
            if (!sentence.ResourceNotFound)
            {
                return (sentence.Value, code, false);
            }
        }

        // `Unmapped` means a CODE arrived that has no sentence (a missing mapping); a refusal without any code is simply
        // answered by its status.
        return (StatusSentence(localizer, statusCode), firstCode, firstCode is not null);
    }

    internal static string StatusSentence(IStringLocalizer localizer, int statusCode)
    {
        var sentence = localizer[$"RefusedStatus_{statusCode}"];
        return sentence.ResourceNotFound ? localizer["RefusedStatus_Other"].Value : sentence.Value;
    }

    private IActionResult Refusal(int statusCode, IEnumerable<string>? errors)
    {
        var (sentence, code, unmapped) = TranslateRefusal(RefusalLocalizer!, errors ?? [], statusCode);
        if (unmapped && code is not null)
        {
            _logger.LogWarning("Master data refusal {ReasonCode} ({StatusCode}) has no sentence; the status sentence was shown.", code, statusCode);
        }

        return new ObjectResult(new { success = false, errors = new[] { sentence }, reasonCode = code, unmapped })
        {
            StatusCode = statusCode
        };
    }

    protected async Task<IActionResult> RelayAsync(HttpResponseMessage? response, CancellationToken cancellationToken)
    {
        if (RefusalLocalizer is null || (response is not null && response.IsSuccessStatusCode))
        {
            return await ToProxyResultAsync(response, cancellationToken);
        }

        if (response is null)
        {
            return Refusal(StatusCodes.Status502BadGateway, null);
        }

        return Refusal((int)response.StatusCode, await ReadErrorsAsync(response, cancellationToken));
    }

    private async Task<IReadOnlyList<string>> ReadErrorsAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var envelope = await response.Content
                .ReadFromJsonAsync<BrandProductGatewayResponse<object>>(JsonOptions, cancellationToken);
            return envelope?.Errors ?? [];
        }
        catch
        {
            return [];
        }
    }

    protected static async Task<IActionResult> ToProxyResultAsync(HttpResponseMessage? response, CancellationToken cancellationToken)
    {
        if (response is null)
        {
            return new ObjectResult(new { errors = new[] { "Gateway unavailable." } }) { StatusCode = 502 };
        }

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        return new ContentResult
        {
            StatusCode = (int)response.StatusCode,
            ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/json",
            Content = content
        };
    }

    /// <summary>Surfaces backend reason codes verbatim so the operator sees why a write was refused.</summary>
    protected async Task<List<string>> ExtractErrorsAsync(HttpResponseMessage? response, string fallbackMessage, CancellationToken cancellationToken)
    {
        // FIX1 (item 7) — a screen with a refusal localizer shows the sentence, not the code and MDM's English.
        if (RefusalLocalizer is { } localizer)
        {
            var status = response is null ? StatusCodes.Status502BadGateway : (int)response.StatusCode;
            IReadOnlyList<string> errors = response is null ? [] : await ReadErrorsAsync(response, cancellationToken);
            // FIX2 (M5) — each field MDM refused reaches the form as its own sentence.
            if (FieldSentences(localizer, errors) is { Count: > 0 } fieldSentences)
            {
                return fieldSentences.ToList();
            }

            var (sentence, code, unmapped) = TranslateRefusal(localizer, errors, status);
            if (unmapped && code is not null)
            {
                _logger.LogWarning("Master data refusal {ReasonCode} ({StatusCode}) has no sentence; the status sentence was shown.", code, status);
            }

            return [sentence];
        }

        if (response is null)
        {
            return [fallbackMessage];
        }

        try
        {
            var envelope = await response.Content
                .ReadFromJsonAsync<BrandProductGatewayResponse<object>>(JsonOptions, cancellationToken);
            if (envelope?.Errors.Count > 0)
            {
                return envelope.Errors;
            }
        }
        catch
        {
            // Non-envelope payload (proxy error page, plain text) — fall through to the raw body.
        }

        var raw = await response.Content.ReadAsStringAsync(cancellationToken);
        return [string.IsNullOrWhiteSpace(raw) ? fallbackMessage : raw];
    }

    protected void AddGatewayErrors(IEnumerable<string> errors)
    {
        foreach (var error in errors)
        {
            ModelState.AddModelError(string.Empty, error);
        }
    }

    protected static void NormalizeExternalReferences(List<BrandProductExternalReferenceViewModel> references)
        => references.RemoveAll(x => string.IsNullOrWhiteSpace(x.SourceSystem) && string.IsNullOrWhiteSpace(x.ExternalId));

    protected static object? ToExternalReferencePayload(List<BrandProductExternalReferenceViewModel> references)
        => references.Count == 0
            ? null
            : references.Select(x => new
            {
                x.SourceSystem,
                x.ExternalId,
                x.ExternalCode,
                x.ExternalName,
                x.ImportedAt,
                x.IsPrimary
            }).ToList();

    protected bool HasAnyPermission(params string[] permissions)
        => permissions.Any(x => PermissionClaims.HasPermission(User, x));

    protected IActionResult? RequirePage(string permission, params string[] alternatives)
        => HasAnyPermission([permission, .. alternatives]) ? null : StatusCode(StatusCodes.Status403Forbidden);

    protected IActionResult? RequireJson(string permission, params string[] alternatives)
        => HasAnyPermission([permission, .. alternatives])
            ? null
            : RefusalLocalizer is null
                ? StatusCode(StatusCodes.Status403Forbidden, new { message = "Permission denied." })
                : Refusal(StatusCodes.Status403Forbidden, null);

    private static bool ContainsTenantId(JsonElement element)
        => element.ValueKind == JsonValueKind.Object
           && element.EnumerateObject().Any(x => string.Equals(x.Name, "tenantId", StringComparison.OrdinalIgnoreCase));

    private string? GetTenantId() => User.Claims.FirstOrDefault(x =>
        x.Type == "tenantId" || x.Type == "tenant_id" ||
        x.Type.EndsWith("/tenantId", StringComparison.OrdinalIgnoreCase))?.Value;
}
