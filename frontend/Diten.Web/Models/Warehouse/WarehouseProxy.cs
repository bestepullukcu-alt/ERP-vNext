using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using Diten.Web.Models.Inventory;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;

namespace Diten.Web.Models.Warehouse;

/// <summary>MOD-0178 permission keys the two S1 screens gate on (pack §14.2) — defined by InventoryService (<c>WarehousePermissions</c>).</summary>
public static class WarehousePermissionKeys
{
    public const string PutawayTasksRead = "warehouse.putaway-tasks.read";
    public const string PutawayTasksCreate = "warehouse.putaway-tasks.create";
    public const string PutawayTasksUpdate = "warehouse.putaway-tasks.update";
    public const string PutawayTasksConfirm = "warehouse.putaway-tasks.confirm";
    public const string PutawayTasksCancel = "warehouse.putaway-tasks.cancel";
    public const string PutawayTasksResolve = "warehouse.putaway-tasks.resolve";
    public const string LocationRolesRead = "warehouse.location-roles.read";
    public const string LocationRolesManage = "warehouse.location-roles.manage";

    // DL-6 — the S2 screens (pack §11.1 / §11.3 / §14.2)
    public const string OutboundShipmentsRead = "warehouse.outbound-shipments.read";
    public const string OutboundShipmentsCreate = "warehouse.outbound-shipments.create";
    public const string OutboundShipmentsUpdate = "warehouse.outbound-shipments.update";
    public const string OutboundShipmentsCancel = "warehouse.outbound-shipments.cancel";
    public const string OutboundShipmentsRelease = "warehouse.outbound-shipments.release";
    public const string PickTasksRead = "warehouse.pick-tasks.read";
    public const string PickTasksUpdate = "warehouse.pick-tasks.update";
    public const string PickTasksConfirm = "warehouse.pick-tasks.confirm";
    public const string PickTasksResolve = "warehouse.pick-tasks.resolve";
    public const string PackagesRead = "warehouse.packages.read";
    public const string PackagesCreate = "warehouse.packages.create";
    public const string PackagesUpdate = "warehouse.packages.update";
}

/// <summary>
/// The one door from the two warehouse MVC proxies to Gateway 5000 (pack §11.2, §15): cookie bearer + <c>X-Tenant-Id</c> per REQUEST, and the
/// user's selected company as <c>X-Legal-Entity-Id</c> for a token without a company (W-0173-02 — <c>/api/warehouse</c> runs through the same
/// InventoryService scope middleware as the ledger and the locations). Unlike <see cref="InventoryGatewayClient"/> the WMS writes need PUT /
/// DELETE and BOTH <c>Idempotency-Key</c> and <c>If-Match</c> on one request (WMS draft §4). The browser never calls 5067 or the Gateway.
/// </summary>
public sealed class WarehouseGatewayClient
{
    private readonly HttpClient _http;
    private readonly string _gatewayUrl;
    private readonly string? _accessToken;
    private readonly string _tenantId;
    private readonly Guid? _company;
    private readonly bool _companyRequired;

    public WarehouseGatewayClient(HttpClient http, string gatewayUrl, string? accessToken, string tenantId, Guid? company, bool companyRequired)
    {
        _http = http;
        _gatewayUrl = gatewayUrl.TrimEnd('/');
        _accessToken = accessToken;
        _tenantId = tenantId;
        _company = company;
        _companyRequired = companyRequired;
    }

    public Task<GatewayReply> GetAsync(string pathAndQuery, CancellationToken ct) => SendAsync(HttpMethod.Get, pathAndQuery, null, null, null, ct);

    public Task<GatewayReply> SendAsync(HttpMethod method, string pathAndQuery, object? body, string? idempotencyKey, string? ifMatch, CancellationToken ct)
        => SendCoreAsync(method, pathAndQuery, body, idempotencyKey, ifMatch, ct);

    private static bool IsCompanyScoped(string pathAndQuery) =>
        pathAndQuery.StartsWith("/api/warehouse", StringComparison.Ordinal) || pathAndQuery.StartsWith("/api/locations", StringComparison.Ordinal);

    private async Task<GatewayReply> SendCoreAsync(HttpMethod method, string pathAndQuery, object? body, string? idempotencyKey, string? ifMatch, CancellationToken ct)
    {
        var scoped = IsCompanyScoped(pathAndQuery);
        if (scoped && _companyRequired && _company is null)
            return new GatewayReply(403, JsonSerializer.Serialize(new { error = new { code = InventoryGatewayClient.CompanyNotSelected } }), null);

        using var request = new HttpRequestMessage(method, _gatewayUrl + pathAndQuery);
        if (scoped && _company is not null) request.Headers.Add(CompanySelection.LegalEntityHeader, _company.Value.ToString("D"));
        if (!string.IsNullOrWhiteSpace(_accessToken)) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        request.Headers.Add("X-Tenant-Id", _tenantId);
        if (idempotencyKey is not null) request.Headers.Add("Idempotency-Key", idempotencyKey);
        if (ifMatch is not null) request.Headers.TryAddWithoutValidation("If-Match", ifMatch); // W/"<version>", verbatim
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

/// <summary>What the two warehouse controllers share: the Gateway doors, the request checks and the contract-shaped refusal.</summary>
public static partial class WarehouseProxy
{
    /// <summary>The WMS door for this request, or null when the principal carries no tenant.</summary>
    public static WarehouseGatewayClient? Gateway(HttpClient http, string gatewayUrl, HttpRequest request, ClaimsPrincipal user)
    {
        var tenantId = TenantId(user);
        if (tenantId is null) return null;
        var tokenHasCompany = CompanySelection.TokenLegalEntity(user) is not null;
        return new WarehouseGatewayClient(http, gatewayUrl, Diten.Web.Services.Auth.AuthTokenCookies.GetAccessToken(request), tenantId,
            tokenHasCompany ? null : CompanySelection.SelectedCompany(request, user), companyRequired: !tokenHasCompany);
    }

    private static string? TenantId(ClaimsPrincipal user)
    {
        var value = user.Claims.FirstOrDefault(x => x.Type == "tenantId" || x.Type == "tenant_id" || x.Type.EndsWith("/tenantId", StringComparison.OrdinalIgnoreCase))?.Value;
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    public static bool TryId(string? raw, out string id)
    {
        id = string.Empty;
        if (!Guid.TryParse(raw, out var parsed)) return false;
        id = parsed.ToString("D");
        return true;
    }

    public static bool IsETag(string? value) => value is not null && ETagPattern().IsMatch(value);

    public static bool IsKey(string? value) => Guid.TryParse(value, out _);

    /// <summary>A decimal as the WMS wire wants it: up to 6 decimals, no exponent, invariant culture (pack §12); null when not that.</summary>
    public static string? Quantity(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var text = raw.Trim().Replace(',', '.');
        return QuantityPattern().IsMatch(text) && decimal.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var q) && q > 0 ? text : null;
    }

    /// <summary>A refusal in the contract's <c>Error</c> shape, with the message already in the reader's language.</summary>
    public static IActionResult Error(int status, string? code, string message, string? correlationId) =>
        new JsonResult(new { error = new { code = code ?? "ERROR", message, correlationId }, contractVersion = InventoryProxy.ContractVersion })
        { StatusCode = status == 0 ? StatusCodes.Status503ServiceUnavailable : status };

    /// <summary>The repo server-mode list wire (<c>start, length, draw</c>) → WMS <c>page, pageSize ≤ 200</c>; null when not a page boundary.</summary>
    public static (int Page, int PageSize, int Draw)? Paging(IQueryCollection query)
    {
        var start = Int(query["start"], 0);
        var length = Int(query["length"], 25);
        if (start < 0 || length is < 1 or > 200 || start % length != 0) return null;
        return (start / length + 1, length, Int(query["draw"], 0));
    }

    public static string? Single(StringValues values)
    {
        var value = values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
        return value?.Trim();
    }

    private static int Int(StringValues values, int fallback) =>
        int.TryParse(values.FirstOrDefault(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : fallback;

    [GeneratedRegex(@"^W/""\d+""$")]
    private static partial Regex ETagPattern();

    [GeneratedRegex(@"^\d{1,12}(\.\d{1,6})?$")]
    private static partial Regex QuantityPattern();
}

/// <summary>
/// WMS / INVENTORY error code → resx key of the screen's family (pack §11.4: "hata kodları — INVENTORY'den geçen kodlar dahil", 7 languages). The
/// code is never the message and "403" / "Forbidden" never reach the reader (UAS-001 §5). Unknown codes fall back by HTTP status.
/// </summary>
public static class WarehouseErrorMessages
{
    public static readonly IReadOnlyList<string> Codes =
    [
        "WMS_ASSIGNEE_DIRECTORY_UNAVAILABLE", "WMS_ASSIGNEE_INVALID", "WMS_COMPANY_CODE_INVALID", "WMS_CONFIRMATION_CONFLICT_RETRY", "WMS_CONFIRMATION_IN_FLIGHT",
        "WMS_CONFIRMATION_NOT_RESOLVABLE", "WMS_CONFIRMATION_NOT_RETRYABLE", "WMS_CONFIRMATION_PENDING", "WMS_CONFLICT", "WMS_INVALID_TRANSITION",
        "WMS_LOCATION_HAS_OPEN_TASKS", "WMS_LOCATION_ROLE_CONFLICT", "WMS_LOCATION_ROLE_EXISTS", "WMS_LOCATION_ROLE_INHERITED_CONFLICT", "WMS_LOCATION_ROLE_NOT_FOUND",
        "WMS_LOCATION_TYPE_NOT_ALLOWED", "WMS_NOT_ASSIGNEE", "WMS_POSTING_INTEGRITY_ERROR", "WMS_QUANTITY_BELOW_CONFIRMED", "WMS_QUANTITY_EXCEEDS_OPEN",
        "WMS_QUANTITY_EXCEEDS_REMAINING", "WMS_RECORD_CODE_UNAVAILABLE", "WMS_SERIAL_BUCKET_CHANGED", "WMS_SERIAL_BUCKET_MIXED_STATUS", "WMS_SERIAL_PARTIAL_NOT_SUPPORTED",
        "WMS_SOURCE_NOT_RECEIVING", "WMS_SOURCE_STOCK_NOT_FOUND", "WMS_STOCK_NOT_PUTAWAY_ELIGIBLE", "WMS_STORAGE_CONDITIONS_UNKNOWN", "WMS_STORAGE_CONDITION_INVALID",
        "WMS_STORAGE_CONDITION_MISMATCH", "WMS_TARGET_NOT_ALLOWED", "WMS_TASK_ALREADY_ASSIGNED", "WMS_TASK_NOT_FOUND",
        // passed through from INVENTORY / the platform
        "IDEMPOTENCY_KEY_REUSED", "REASON_REQUIRED", "INSUFFICIENT_STOCK", "LOT_UNDER_RECALL", "INVENTORY_UNAVAILABLE", "VALIDATION_ERROR",
        InventoryGatewayClient.CompanyNotSelected
    ];

    public const string NoPermission = "Warehouse.Error.NoPermission";
    public const string SessionExpired = "Warehouse.Error.SessionExpired";
    public const string ServiceUnavailable = "Warehouse.Error.ServiceUnavailable";
    public const string NotFound = "Warehouse.Error.NotFound";
    public const string Conflict = "Warehouse.Error.Conflict";
    public const string Invalid = "Warehouse.Error.Invalid";
    public const string Generic = "Warehouse.Error.Generic";
    public const string TooLarge = "Warehouse.Error.TooLarge";

    /// <summary>Every key this class can return — the resx parity test asserts each is in all seven files of both families.</summary>
    public static IEnumerable<string> AllKeys =>
        Codes.Select(c => "Warehouse.Error." + c).Concat([NoPermission, SessionExpired, ServiceUnavailable, NotFound, Conflict, Invalid, Generic, TooLarge]);

    /// <summary>
    /// DL-6 — the codes only the S2 screens meet (pack §12 S2 rows; INVENTORY's RESERVATION_* / FEFO / lot codes passed through). The three S2
    /// families carry <see cref="S2Keys"/> on top of <see cref="AllKeys"/>; the S1 families are unchanged (their parity test reads AllKeys).
    /// </summary>
    public static readonly IReadOnlyList<string> S2Codes =
    [
        "UNKNOWN_OUTBOUND", "WMS_ALLOCATION_FAILED", "WMS_ALLOCATION_TOO_MANY_LINES", "WMS_DISPATCH_AMBIGUOUS", "WMS_DISPATCH_READ_FORBIDDEN",
        "WMS_DISPATCH_SOURCE_UNAVAILABLE", "WMS_ITEM_LEVEL_NOT_PUBLISHABLE", "WMS_ITEM_NOT_RESOLVABLE", "WMS_LOT_NOT_ALLOCATABLE",
        "WMS_OUTBOUND_ALLOCATION_IN_PROGRESS", "WMS_OUTBOUND_CANCEL_NOT_ALLOWED", "WMS_OUTBOUND_IMMUTABLE", "WMS_OUTBOUND_LEVEL_MIXED", "WMS_PACKAGE_EMPTY",
        "WMS_PACKAGE_EXCEEDS_PICKED", "WMS_PACKAGE_NOT_FOUND", "WMS_STAGING_NOT_CONFIGURED", "WMS_UOM_NOT_BASE",
        // passed through from INVENTORY (FU03 reservations, the FEFO hook, the lot gates, LOCATION)
        "FEFO_BYPASS_REASON_REQUIRED", "FEFO_ORDER_VIOLATION", "LOT_EXPIRED", "LOT_BLOCKED", "LOT_RETEST_DUE", "RESERVATION_NOT_ACTIVE", "RESERVATION_MISMATCH",
        "RESERVATION_CONFLICT", "UNKNOWN_LOCATION"
    ];

    /// <summary>The S2 families' extra error keys.</summary>
    public static IEnumerable<string> S2Keys => S2Codes.Select(c => "Warehouse.Error." + c);

    /// <summary>The S2 screens' lookup: the S1 codes, the S2 codes, then the status fallback.</summary>
    public static string KeyForS2(string? code, int status) =>
        code is not null && S2Codes.Contains(code, StringComparer.Ordinal) ? "Warehouse.Error." + code : KeyFor(code, status);

    public static string KeyFor(string? code, int status)
    {
        if (code is not null && Codes.Contains(code, StringComparer.Ordinal)) return "Warehouse.Error." + code;
        return status switch
        {
            401 => SessionExpired,
            403 => NoPermission,
            404 => NotFound,
            409 or 412 => Conflict,
            400 or 422 => Invalid,
            413 => TooLarge,
            0 or 502 or 503 or 504 => ServiceUnavailable,
            _ => Generic
        };
    }
}

/// <summary>The server export of the two lists (BL-452): RFC 4180 CSV with a BOM, a formula-looking cell neutralised (CSV injection).</summary>
public static class WarehouseCsv
{
    public const int MaxRows = 2000;

    public static byte[] Write(IEnumerable<string> header, IEnumerable<IEnumerable<string?>> rows)
    {
        var builder = new System.Text.StringBuilder();
        builder.AppendLine(string.Join(',', header.Select(Cell)));
        foreach (var row in rows) builder.AppendLine(string.Join(',', row.Select(Cell)));
        return [.. System.Text.Encoding.UTF8.GetPreamble(), .. System.Text.Encoding.UTF8.GetBytes(builder.ToString())];
    }

    private static string Cell(string? value)
    {
        var text = value ?? string.Empty;
        if (text.Length > 0 && "=+-@\t\r".Contains(text[0])) text = "'" + text;
        return text.IndexOfAny([',', '"', '\n', '\r']) >= 0 ? "\"" + text.Replace("\"", "\"\"") + "\"" : text;
    }
}
