using System.Net;
using System.Text.Json;
using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.ReferenceValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Diten.CrmService.Infrastructure.ReferenceValidation;

/// <summary>
/// Consumes MOD-0048 / PSS-012 published reference values through the Gateway (never CRM-local).
/// Controlled dependency: if the consumer endpoint is not available or the set is not published, this
/// returns <see cref="ReferenceValidationStatus.SetMissing"/> — it never fabricates or falls back to a local list.
/// </summary>
public sealed class GatewayReferenceDataValidator : IReferenceDataValidator, IReferenceMetadataReader, IReferenceDataCatalogReader
{
    private const string TenantHeaderName = "X-Tenant-Id";
    private const string AuthorizationHeaderName = "Authorization";

    /// <summary>The consumer service's own error signal for "this set is global; do not send a scope key".</summary>
    private const string GlobalScopeKeyRefusal = "scope_key_not_allowed_for_global";
    private readonly HttpClient _httpClient;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<GatewayReferenceDataValidator> _logger;
    private readonly string _pathTemplate;

    public GatewayReferenceDataValidator(
        HttpClient httpClient,
        IConfiguration configuration,
        IHttpContextAccessor httpContextAccessor,
        ITenantContext tenantContext,
        ILogger<GatewayReferenceDataValidator> logger)
    {
        _httpClient = httpClient;
        _httpContextAccessor = httpContextAccessor;
        _tenantContext = tenantContext;
        _logger = logger;
        _httpClient.BaseAddress = new Uri(configuration["Gateway:BaseUrl"] ?? "http://localhost:5000");
        _pathTemplate = configuration["ReferenceData:PublishedValuesPathTemplate"]
            ?? "/api/v1/reference-data/sets/{setCode}/published-values";
    }

    /// <summary>
    /// Reads a set's published values, sending <c>scope_key</c> only when the set actually wants one.
    /// <para><b>Why this is not a plain GET.</b> PSS-012 sets come in two shapes. A TENANT-scoped set requires an
    /// explicit <c>scope_key</c> and refuses the read without one (<c>scope_key_required</c>); a GLOBAL set refuses the
    /// read WITH one (<c>scope_key_not_allowed_for_global</c>). Nothing in the consumer response tells us which shape a
    /// set has before we ask, so we ask the tenant-scoped way first — the common case, and the one that must never
    /// leak — and retry without the key only when the service says that key was the problem. A hardcoded list of
    /// "global sets" here would be a second source of truth that drifts the day someone re-scopes a set.</para>
    /// <para>The retry is triggered by the service's own signal, so no other failure is silently retried and no
    /// existing consumer changes behaviour.</para>
    /// </summary>
    private async Task<HttpResponseMessage> SendPublishedValuesAsync(
        string setCode, Guid tenantId, CancellationToken cancellationToken)
    {
        var basePath = _pathTemplate.Replace("{setCode}", Uri.EscapeDataString(setCode));

        using var scopedRequest = new HttpRequestMessage(
            HttpMethod.Get, basePath + $"?scope_key={Uri.EscapeDataString(tenantId.ToString())}");
        ForwardContextHeaders(scopedRequest);
        var scopedResponse = await _httpClient.SendAsync(scopedRequest, cancellationToken);

        if (scopedResponse.IsSuccessStatusCode
            || !await RefusedScopeKeyAsync(scopedResponse, cancellationToken))
        {
            return scopedResponse;
        }

        scopedResponse.Dispose();
        _logger.LogDebug(
            "Reference set '{SetCode}' is global-scoped; retrying the consumer read without scope_key.", setCode);

        using var globalRequest = new HttpRequestMessage(HttpMethod.Get, basePath);
        ForwardContextHeaders(globalRequest);
        return await _httpClient.SendAsync(globalRequest, cancellationToken);
    }

    /// <summary>Did the consumer service refuse specifically because a global set was given a scope key?</summary>
    private static async Task<bool> RefusedScopeKeyAsync(
        HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            return body.Contains(GlobalScopeKeyRefusal, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            return false;
        }
    }

    public async Task<ReferenceValidationResult> ValidateAsync(string setCode, string value, CancellationToken cancellationToken)
    {
        // PSS-012 tenant-scoped sets require an explicit scope_key; without it the consumer read fails
        // (`scope_key_required`). The key is the server-resolved tenant — never a hardcoded/default tenant.
        if (_tenantContext.TenantId is not { } tenantId)
        {
            _logger.LogWarning(
                "Reference set '{SetCode}' lookup skipped: no tenant context (scope_key unavailable); treating as SetMissing.", setCode);
            return new ReferenceValidationResult(ReferenceValidationStatus.SetMissing, setCode, value);
        }

        try
        {
            using var response = await SendPublishedValuesAsync(setCode, tenantId, cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return new ReferenceValidationResult(ReferenceValidationStatus.SetMissing, setCode, value);
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Reference set '{SetCode}' lookup returned {Status}; treating as SetMissing.", setCode, response.StatusCode);
                return new ReferenceValidationResult(ReferenceValidationStatus.SetMissing, setCode, value);
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var codes = ExtractValueCodes(await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken));

            var exists = codes.Any(code => string.Equals(code, value, StringComparison.OrdinalIgnoreCase));
            return new ReferenceValidationResult(
                exists ? ReferenceValidationStatus.Valid : ReferenceValidationStatus.InvalidValue, setCode, value);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Reference set '{SetCode}' lookup failed; treating as SetMissing (controlled dependency).", setCode);
            return new ReferenceValidationResult(ReferenceValidationStatus.SetMissing, setCode, value);
        }
    }

    public async Task<IReadOnlyDictionary<string, string>?> GetValueAttributesAsync(string setCode, string value, CancellationToken cancellationToken)
    {
        if (_tenantContext.TenantId is not { } tenantId)
        {
            return null;
        }

        try
        {
            using var response = await SendPublishedValuesAsync(setCode, tenantId, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (!TryFindValueArray(document.RootElement, out var array))
            {
                return null;
            }

            foreach (var item in array.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var code = ReadCode(item);
                if (code is null || !string.Equals(code, value, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!item.TryGetProperty("attributes", out var attrs) || attrs.ValueKind != JsonValueKind.Object)
                {
                    return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                }

                var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var prop in attrs.EnumerateObject())
                {
                    map[prop.Name] = prop.Value.ValueKind == JsonValueKind.String
                        ? prop.Value.GetString() ?? string.Empty
                        : prop.Value.ToString();
                }

                return map;
            }

            return null;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Reference set '{SetCode}' metadata read failed; treating as no-metadata.", setCode);
            return null;
        }
    }

    /// <summary>
    /// MOD-0150 Import/Export Task 1 — reads the whole published value list of a set for the workbook ReferenceData
    /// helper sheet and its in-cell dropdowns. Unlike <see cref="ValidateAsync"/> this KEEPS deprecated/inactive values
    /// (flagged, so the sheet can mark them) — the sheet documents the set, the validator still refuses them on write.
    /// An unpublished/unavailable set returns <see cref="ReferenceSetSnapshot.NotPublished"/>; never a local fallback.
    /// </summary>
    public async Task<ReferenceSetSnapshot> GetPublishedValuesAsync(string setCode, CancellationToken cancellationToken)
    {
        if (_tenantContext.TenantId is not { } tenantId)
        {
            _logger.LogWarning(
                "Reference set '{SetCode}' catalog read skipped: no tenant context (scope_key unavailable).", setCode);
            return ReferenceSetSnapshot.NotPublished(setCode);
        }

        try
        {
            using var response = await SendPublishedValuesAsync(setCode, tenantId, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return ReferenceSetSnapshot.NotPublished(setCode);
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (!TryFindValueArray(document.RootElement, out var array))
            {
                return ReferenceSetSnapshot.NotPublished(setCode);
            }

            var values = new List<ReferenceValueSnapshot>();
            foreach (var item in array.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    values.Add(new ReferenceValueSnapshot(item.GetString()!, null, null, true, false, null));
                    continue;
                }

                if (item.ValueKind != JsonValueKind.Object || ReadCode(item) is not { } code)
                {
                    continue;
                }

                values.Add(new ReferenceValueSnapshot(
                    code,
                    ReadText(item, "displayName", "display_name", "text", "label"),
                    ReadText(item, "description"),
                    !IsDeprecated(item),
                    IsDeprecated(item),
                    ReadAttributes(item)));
            }

            // A published-but-empty set is still "published" — the sheet then shows the set with no selectable value.
            return new ReferenceSetSnapshot(setCode, true, values);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Reference set '{SetCode}' catalog read failed; treating as not published.", setCode);
            return ReferenceSetSnapshot.NotPublished(setCode);
        }
    }

    private static string? ReadText(JsonElement item, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (item.TryGetProperty(key, out var prop) && prop.ValueKind == JsonValueKind.String)
            {
                return prop.GetString();
            }
        }

        return null;
    }

    private static IReadOnlyDictionary<string, string>? ReadAttributes(JsonElement item)
    {
        if (!item.TryGetProperty("attributes", out var attrs) || attrs.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in attrs.EnumerateObject())
        {
            map[prop.Name] = prop.Value.ValueKind == JsonValueKind.String
                ? prop.Value.GetString() ?? string.Empty
                : prop.Value.ToString();
        }

        return map;
    }

    private static string? ReadCode(JsonElement item)
    {
        foreach (var key in new[] { "valueCode", "value_code", "code", "value" })
        {
            if (item.TryGetProperty(key, out var prop) && prop.ValueKind == JsonValueKind.String)
            {
                return prop.GetString();
            }
        }

        return null;
    }

    private static IReadOnlyList<string> ExtractValueCodes(JsonDocument document)
    {
        // MOD-0048/PSS-012 returns Response<BusinessReferenceDataPublishedValuesModel>:
        //   { data: { setCode, versionNumber, publishedAt, items: [ { valueCode, displayName, isActive, ... } ] } }
        // Older/alternate shapes (bare array, { data: [...] }, { items: [...] }) stay supported.
        if (!TryFindValueArray(document.RootElement, out var array))
        {
            return Array.Empty<string>();
        }

        var codes = new List<string>();
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                codes.Add(item.GetString()!);
            }
            else if (item.ValueKind == JsonValueKind.Object)
            {
                // Deprecated values are never selectable for new/updated records — skip them so the
                // caller reports InvalidValue (400), matching the reference-data contract.
                if (IsDeprecated(item))
                {
                    continue;
                }

                foreach (var key in new[] { "valueCode", "value_code", "code", "value" })
                {
                    if (item.TryGetProperty(key, out var prop) && prop.ValueKind == JsonValueKind.String)
                    {
                        codes.Add(prop.GetString()!);
                        break;
                    }
                }
            }
        }

        return codes;
    }

    private static bool IsDeprecated(JsonElement item)
    {
        foreach (var key in new[] { "isDeprecated", "is_deprecated" })
        {
            if (item.TryGetProperty(key, out var d) && d.ValueKind == JsonValueKind.True)
            {
                return true;
            }
        }

        // The published-values model exposes the inverse flag (isActive/is_active).
        foreach (var key in new[] { "isActive", "is_active" })
        {
            if (item.TryGetProperty(key, out var a) && a.ValueKind == JsonValueKind.False)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Locates the value array in any supported response shape: bare array, <c>{items:[…]}</c>,
    /// <c>{data:[…]}</c> or the canonical <c>{data:{items:[…]}}</c> envelope.</summary>
    private static bool TryFindValueArray(JsonElement root, out JsonElement array)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            array = root;
            return true;
        }

        if (root.ValueKind == JsonValueKind.Object)
        {
            if (root.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
            {
                array = items;
                return true;
            }

            if (root.TryGetProperty("data", out var data))
            {
                return TryFindValueArray(data, out array);
            }
        }

        array = default;
        return false;
    }

    private void ForwardContextHeaders(HttpRequestMessage request)
    {
        var context = _httpContextAccessor.HttpContext;
        if (context is null)
        {
            return;
        }

        if (context.Request.Headers.TryGetValue(TenantHeaderName, out var tenant) && !string.IsNullOrWhiteSpace(tenant))
        {
            request.Headers.TryAddWithoutValidation(TenantHeaderName, tenant.ToString());
        }

        if (context.Request.Headers.TryGetValue(AuthorizationHeaderName, out var auth) && !string.IsNullOrWhiteSpace(auth))
        {
            request.Headers.TryAddWithoutValidation(AuthorizationHeaderName, auth.ToString());
        }
    }
}
