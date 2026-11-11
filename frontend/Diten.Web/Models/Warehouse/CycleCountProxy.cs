using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using Diten.Web.Models.Inventory;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Models.Warehouse;

/// <summary>MOD-0181 permission keys the three DL-4 screens gate on (pack §14) — defined by InventoryService (<c>CycleCountPermissions</c>).</summary>
public static class CycleCountPermissionKeys
{
    public const string CountsRead = "cycle-count.counts.read";
    public const string CountsCreate = "cycle-count.counts.create";
    public const string CountsUpdate = "cycle-count.counts.update";
    public const string CountsBulkDelete = "cycle-count.counts.bulk-delete";
    public const string CountsCancel = "cycle-count.counts.cancel";
    public const string CountsRecord = "cycle-count.counts.record";
    public const string VariancesRead = "cycle-count.variances.read";
    public const string VariancesApprove = "cycle-count.variances.approve";
    public const string VariancesReject = "cycle-count.variances.reject";
    public const string VariancesRecount = "cycle-count.variances.recount";
    public const string VariancesPost = "cycle-count.variances.post";
    public const string VariancesResolve = "cycle-count.variances.resolve";
}

/// <summary>
/// The one door from the two cycle-count MVC proxies to Gateway 5000 (pack §11, §15): cookie bearer + <c>X-Tenant-Id</c>, the selected company as
/// <c>X-Legal-Entity-Id</c> for a token without one (W-0173-02), <c>Idempotency-Key</c> / <c>If-Match</c> on the writes and — pack §11 (r6, SB2/F-3) —
/// the page's <c>Accept-Language</c> on EVERY request (the service's error sentences and DL-5's evidence language rest on it; emsal
/// <c>CrmGatewayProxy</c>). The browser never calls 5067 or the Gateway.
/// </summary>
public sealed class CycleCountGatewayClient
{
    private readonly HttpClient _http;
    private readonly string _gatewayUrl;
    private readonly string? _accessToken;
    private readonly string _tenantId;
    private readonly Guid? _company;
    private readonly bool _companyRequired;
    private readonly string? _language;

    public CycleCountGatewayClient(HttpClient http, string gatewayUrl, string? accessToken, string tenantId, Guid? company, bool companyRequired, string? language)
    {
        _http = http;
        _gatewayUrl = gatewayUrl.TrimEnd('/');
        _accessToken = accessToken;
        _tenantId = tenantId;
        _company = company;
        _companyRequired = companyRequired;
        _language = language;
    }

    public Task<GatewayReply> GetAsync(string pathAndQuery, CancellationToken ct) => SendAsync(HttpMethod.Get, pathAndQuery, null, null, null, ct);

    public async Task<GatewayReply> SendAsync(HttpMethod method, string pathAndQuery, object? body, string? idempotencyKey, string? ifMatch, CancellationToken ct)
    {
        var scoped = pathAndQuery.StartsWith("/api/warehouse", StringComparison.Ordinal) || pathAndQuery.StartsWith("/api/locations", StringComparison.Ordinal);
        if (scoped && _companyRequired && _company is null)
            return new GatewayReply(403, JsonSerializer.Serialize(new { error = new { code = InventoryGatewayClient.CompanyNotSelected } }), null);

        using var request = new HttpRequestMessage(method, _gatewayUrl + pathAndQuery);
        if (scoped && _company is not null) request.Headers.Add(CompanySelection.LegalEntityHeader, _company.Value.ToString("D"));
        if (!string.IsNullOrWhiteSpace(_accessToken)) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        request.Headers.Add("X-Tenant-Id", _tenantId);
        if (!string.IsNullOrWhiteSpace(_language)) request.Headers.TryAddWithoutValidation("Accept-Language", _language);
        if (idempotencyKey is not null) request.Headers.Add("Idempotency-Key", idempotencyKey);
        if (ifMatch is not null) request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        if (body is not null) request.Content = JsonContent.Create(body, options: InventoryGatewayClient.Json);

        try
        {
            using var response = await _http.SendAsync(request, ct);
            var text = await response.Content.ReadAsStringAsync(ct);
            response.Headers.TryGetValues("X-Correlation-Id", out var correlation);
            return new GatewayReply((int)response.StatusCode, text, correlation?.FirstOrDefault()) { ETag = response.Headers.ETag?.ToString() };
        }
        catch (HttpRequestException)
        {
            return GatewayReply.Unreachable;
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return GatewayReply.Unreachable;
        }
    }
}

/// <summary>What the two cycle-count controllers share: the Gateway door (with the page language), the body reads and the contract-shaped refusal.</summary>
public static class CycleCountProxy
{
    /// <summary>The door for this request, or null when the principal carries no tenant. The page's <c>Accept-Language</c> travels on.</summary>
    public static CycleCountGatewayClient? Gateway(HttpClient http, string gatewayUrl, HttpRequest request, ClaimsPrincipal user)
    {
        var tenantId = user.Claims.FirstOrDefault(x => x.Type == "tenantId" || x.Type == "tenant_id" || x.Type.EndsWith("/tenantId", StringComparison.OrdinalIgnoreCase))?.Value;
        if (string.IsNullOrWhiteSpace(tenantId)) return null;
        var tokenHasCompany = CompanySelection.TokenLegalEntity(user) is not null;
        return new CycleCountGatewayClient(http, gatewayUrl, Diten.Web.Services.Auth.AuthTokenCookies.GetAccessToken(request), tenantId,
            tokenHasCompany ? null : CompanySelection.SelectedCompany(request, user), companyRequired: !tokenHasCompany, Language(request));
    }

    /// <summary>The page language: the request's culture (the shell's language choice), else its own Accept-Language header.</summary>
    public static string? Language(HttpRequest request)
    {
        var culture = request.HttpContext.Features.Get<Microsoft.AspNetCore.Localization.IRequestCultureFeature>()?.RequestCulture.UICulture.Name;
        if (!string.IsNullOrWhiteSpace(culture)) return culture;
        var header = request.Headers.AcceptLanguage.ToString();
        return string.IsNullOrWhiteSpace(header) ? null : header;
    }

    public static JsonNode? Node(GatewayReply reply)
    {
        if (string.IsNullOrWhiteSpace(reply.Body)) return null;
        try { return JsonNode.Parse(reply.Body); }
        catch (JsonException) { return null; }
    }

    public static string? Text(JsonNode? node, string name) => node?[name] is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

    /// <summary>A refusal in the contract's <c>Error</c> shape, with the message already in the reader's language.</summary>
    public static IActionResult Error(int status, string? code, string message, string? correlationId, object? details = null) =>
        new JsonResult(new { error = new { code = code ?? "ERROR", message, details, correlationId }, contractVersion = "v1" })
        { StatusCode = status == 0 ? StatusCodes.Status503ServiceUnavailable : status };
}

/// <summary>
/// WMS-COUNT-BUNDLE / INVENTORY error code → resx key (pack §8.14 "hata kodlarının UI metinleri 7 dilde"; AC-37). The code is never the message and
/// "403" / "Forbidden" never reach the reader (UAS-001 §5). Unknown codes fall back by HTTP status. Both screen families carry every key.
/// </summary>
public static class CycleCountErrorMessages
{
    public static readonly IReadOnlyList<string> Codes =
    [
        "COUNT_NOT_FOUND", "COUNT_LINE_NOT_FOUND", "COUNT_CONFLICT", "COUNT_INVALID_STATE", "COUNT_SCOPE_EMPTY", "COUNT_SCOPE_TOO_LARGE", "COUNT_SCOPE_OVERLAP",
        "COUNT_SCOPE_NODE_INVALID", "COUNT_ASSIGNEE_INVALID", "COUNT_ASSIGNEE_DIRECTORY_UNAVAILABLE", "COUNT_SNAPSHOT_DRIFT", "COUNT_LINES_UNCOUNTED",
        "COUNT_QUANTITY_INVALID", "COUNT_SELF_APPROVAL_FORBIDDEN", "COUNT_LOT_UNKNOWN", "COUNT_LOT_REQUIRED", "COUNT_SERIAL_NOT_POSTABLE", "COUNT_REASON_REQUIRED",
        "COUNT_RECOUNT_LIMIT", "COUNT_COMPANY_CODE_INVALID", "COUNT_RECORD_CODE_UNAVAILABLE", "COUNT_INVENTORY_UNAVAILABLE", "COUNT_ADJUSTMENT_INTEGRITY",
        "COUNT_LINE_OUT_OF_SCOPE", "COUNT_LINE_DUPLICATE",
        // passed through from INVENTORY / the platform (draft §5.2)
        "IDEMPOTENCY_KEY_REUSED", "INVALID_REQUEST", "UNKNOWN_LOCATION", "UNKNOWN_ITEM", "SKU_ITEM_MISMATCH", "SKU_LEVEL_NOT_STOCKABLE", "LOT_NOT_ALLOWED",
        "PRODUCT_MASTER_UNAVAILABLE", "PRODUCT_MASTER_DATA_INCOMPLETE", "LEGAL_ENTITY_MASTER_UNAVAILABLE", "INSUFFICIENT_STOCK", "LOT_BLOCKED", "LOT_EXPIRED",
        "SERIAL_COUNT_MISMATCH", "VALIDATION_ERROR", InventoryGatewayClient.CompanyNotSelected
    ];

    public const string NoPermission = "CycleCount.Error.NoPermission";
    public const string SessionExpired = "CycleCount.Error.SessionExpired";
    public const string ServiceUnavailable = "CycleCount.Error.ServiceUnavailable";
    public const string NotFound = "CycleCount.Error.NotFound";
    public const string Conflict = "CycleCount.Error.Conflict";
    public const string Invalid = "CycleCount.Error.Invalid";
    public const string Generic = "CycleCount.Error.Generic";

    /// <summary>Every key this class can return — the parity test asserts each is in all seven files of every cycle-count family.</summary>
    public static IEnumerable<string> AllKeys =>
        Codes.Select(c => "CycleCount.Error." + c).Concat([NoPermission, SessionExpired, ServiceUnavailable, NotFound, Conflict, Invalid, Generic]);

    public static string KeyFor(string? code, int status)
    {
        if (code is not null && Codes.Contains(code, StringComparer.Ordinal)) return "CycleCount.Error." + code;
        return status switch
        {
            401 => SessionExpired,
            403 => NoPermission,
            404 => NotFound,
            409 or 412 => Conflict,
            400 or 422 => Invalid,
            0 or 502 or 503 or 504 => ServiceUnavailable,
            _ => Generic
        };
    }
}

/// <summary>The body every cycle-count write of the two screens sends to its same-origin proxy (the form scripts fill it; no asp-for binding).</summary>
public sealed class CycleCountWriteRequest
{
    public string? IdempotencyKey { get; set; }
    public string? IfMatch { get; set; }

    // the Draft form (8 fields, pack §4.1)
    public string? WarehouseId { get; set; }
    public string? CountType { get; set; }
    public bool Blind { get; set; }
    public List<string>? ScopeLocationIds { get; set; }
    public List<string>? ScopeSkuIds { get; set; }
    public string? PlannedDate { get; set; }
    public string? AssignedTo { get; set; }
    public string? Note { get; set; }

    // the sheet (entry / found line)
    public string? CountedQuantity { get; set; }
    public string? ScanRef { get; set; }
    public string? LocationId { get; set; }
    public string? ItemId { get; set; }
    public string? SkuId { get; set; }
    public string? SkuLevel { get; set; }
    public string? LotId { get; set; }

    // decisions
    public string? Reason { get; set; }
    public string? Decision { get; set; }
    public List<string>? LineIds { get; set; }
    public List<CycleCountBulkItem>? Items { get; set; }
}

public sealed class CycleCountBulkItem
{
    public string? Id { get; set; }
    public int Version { get; set; }
}
