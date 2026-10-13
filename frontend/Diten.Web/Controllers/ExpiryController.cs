using System.Diagnostics;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Diten.Web.Models.Inventory;
using Diten.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Diten.Web.Controllers;

// MOD-0176 F4 — Son Kullanma / FEFO tenant pages (pack §9, §11.1–§11.3, §21.3) + their same-origin doors. Tenant shell only: every
// route is /Expiry/**, never /Platform. The controller holds no expiry rule — every figure, severity and order is InventoryService's
// (FEFO-BUNDLE 1.1.0): the MOD-0175 QuarantineController pattern, copied:
//   • api/{**everything} → Gateway /api/expiry/{everything} (the caller's own token from the auth cookie, TenantId from the token, the
//     W-0173-02 company rule, correlation id, Accept-Language, Idempotency-Key passed through). Writes ("İstisna aç") carry the
//     antiforgery token. Bodies are never logged.
//   • list/{approaching|expired} → the server-mode DataTable doors (start / length / search → page / pageSize / q).
//   • data/{dashboard|fefo-order} → one call per page + display names (MasterLabelResolver).
//   • export/{approaching|expired} → the list as CSV under expiry.dashboard.read, no evidence log (CT-F14).
//   • lookups/{warehouses|skus} → the pickers of the filters (FEFO Sırası: the SKUs with lot stock in the warehouse).
// UAS-001 is the views' (display only); InventoryService's [HasPermission] stays the authority.
[Authorize]
[Route("Expiry")]
public sealed class ExpiryController : Controller
{
    public const string ApiBase = "/api/expiry";
    private const string ViewRoot = "~/Views/Expiry";
    private const string CorrelationHeaderName = "X-Correlation-Id";
    private const string TenantHeaderName = "X-Tenant-Id";
    private const int MaxServicePageSize = 200;
    private const int MaxExportRows = 2000;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly IPermissionSnapshot _permissions;
    private readonly IStringLocalizer<Diten.Web.Views.Expiry.Expiry> _localizer;
    private readonly ILogger<ExpiryController> _logger;

    public ExpiryController(
        IHttpClientFactory httpClientFactory,
        HttpClient httpClient,
        IConfiguration configuration,
        IPermissionSnapshot permissions,
        IStringLocalizer<Diten.Web.Views.Expiry.Expiry> localizer,
        ILogger<ExpiryController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"]?.TrimEnd('/') ?? "http://localhost:5000";
        _permissions = permissions;
        _localizer = localizer;
        _logger = logger;
    }

    // ── pages (pack §11.1; UAS-001 is the view's: _AccessDenied and nothing else without the page key, no redirect) ──────

    [HttpGet("")]
    public IActionResult Index() => View($"{ViewRoot}/Index.cshtml");

    [HttpGet("Approaching")]
    public IActionResult Approaching() => View($"{ViewRoot}/Approaching.cshtml");

    [HttpGet("Expired")]
    public IActionResult Expired() => View($"{ViewRoot}/Expired.cshtml");

    [HttpGet("FefoOrder")]
    public IActionResult FefoOrder() => View($"{ViewRoot}/FefoOrder.cshtml");

    // ── FEFO-BUNDLE proxy ────────────────────────────────────────────────────────────────────────────────────────

    [HttpGet("api/{**everything}")]
    public Task<IActionResult> ProxyRead(string? everything) => ProxyAsync(everything);

    [HttpPost("api/{**everything}")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> ProxyWrite(string? everything) => ProxyAsync(everything);

    // ── server-mode lists ───────────────────────────────────────────────────────────────────────────────────────

    [HttpGet("list/approaching")]
    public async Task<IActionResult> ListApproaching(CancellationToken ct)
    {
        if (!_permissions.Has(ExpiryKeys.DashboardRead)) return Refuse(403, null);
        var (page, size) = Paging();
        var reply = await SendAsync(HttpMethod.Get, $"{ApiBase}/lots/approaching{ApproachingQuery(page, size)}", null, null, ct);
        return await ListAsync("listApproaching", reply, ct);
    }

    [HttpGet("list/expired")]
    public async Task<IActionResult> ListExpired(CancellationToken ct)
    {
        if (!_permissions.Has(ExpiryKeys.DashboardRead)) return Refuse(403, null);
        var (page, size) = Paging();
        var reply = await SendAsync(HttpMethod.Get, $"{ApiBase}/lots/expired{ExpiredQuery(page, size)}", null, null, ct);
        return await ListAsync("listExpired", reply, ct);
    }

    // ── one call per page ────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>FEFO Paneli: the panel + names; the company horizons when the reader may read the policy; the "min. kalan raf ömrü altı
    /// giriş · son 30 gün" count when the reader may read exceptions (pack §21.2 card 4 — not a DashboardView member).</summary>
    [HttpGet("data/dashboard")]
    public async Task<IActionResult> DashboardData(CancellationToken ct)
    {
        if (!_permissions.Has(ExpiryKeys.DashboardRead)) return Refuse(403, null);
        var query = new QueryBuilder().Add("warehouseId", Request.Query["warehouseId"]).Add("periodDays", Request.Query["periodDays"]);
        var reply = await SendAsync(HttpMethod.Get, $"{ApiBase}/dashboard{query}", null, null, ct);
        if (!reply.IsSuccess) return Failed("getDashboard", reply);
        var panel = JsonNode.Parse(reply.Body) as JsonObject ?? new JsonObject();
        var top = panel["topRisk"] as JsonArray ?? new JsonArray();
        var labels = await LabelsAsync(top, ct);
        Decorate(top, labels);

        JsonObject? policy = null;
        if (_permissions.Has(ExpiryKeys.PoliciesRead))
        {
            var p = await SendAsync(HttpMethod.Get, $"{ApiBase}/policy", null, null, ct);
            policy = p.IsSuccess ? JsonNode.Parse(p.Body) as JsonObject : null;
        }

        int? belowMin = null;
        if (_permissions.Has(ExpiryKeys.ExceptionsRead))
        {
            var cases = await SendAsync(HttpMethod.Get, $"{ApiBase}/exceptions?type=BELOW_MIN_REMAINING&sort=-openedAtUtc&page=1&pageSize=200", null, null, ct);
            if (cases.IsSuccess)
            {
                var since = DateTime.UtcNow.AddDays(-30);
                belowMin = ((JsonNode.Parse(cases.Body)?["items"] as JsonArray) ?? new JsonArray()).OfType<JsonObject>()
                    .Count(c => DateTime.TryParse((string?)c["openedAtUtc"], CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var at) && at >= since);
            }
        }

        return Json(new JsonObject
        {
            ["panel"] = panel,
            ["policy"] = policy is null ? null : new JsonObject { ["approachingDays"] = policy["approachingDays"]?.DeepClone(), ["criticalDays"] = policy["criticalDays"]?.DeepClone() },
            ["belowMinLast30"] = belowMin,
            ["labelsAvailable"] = labels.Available
        });
    }

    /// <summary>FEFO Sırası: getFefoOrder (read-only, KF-F) + names.</summary>
    [HttpGet("data/fefo-order")]
    public async Task<IActionResult> FefoOrderData(CancellationToken ct)
    {
        if (!_permissions.Has(ExpiryKeys.FefoOrderRead)) return Refuse(403, null);
        var query = new QueryBuilder()
            .Add("warehouseId", Request.Query["warehouseId"]).Add("itemId", Request.Query["itemId"]).Add("skuId", Request.Query["skuId"])
            .Add("skuLevel", Request.Query["skuLevel"]).Add("quantity", Request.Query["quantity"]);
        var reply = await SendAsync(HttpMethod.Get, $"{ApiBase}/fefo-order{query}", null, null, ct);
        if (!reply.IsSuccess) return Failed("getFefoOrder", reply);
        var order = JsonNode.Parse(reply.Body) as JsonObject ?? new JsonObject();
        return Json(new JsonObject { ["order"] = order });
    }

    // ── CSV (pack §11.3 EXPIRY_LIST_EXPORT, CT-F14: under expiry.dashboard.read, not an evidence export) ───────────

    [HttpGet("export/{list}")]
    public async Task<IActionResult> Export(string list, CancellationToken ct)
    {
        if (!_permissions.Has(ExpiryKeys.DashboardRead)) return Refuse(403, null);
        if (list is not ("approaching" or "expired")) return Refuse(404, null);
        var rows = new List<JsonObject>();
        for (var page = 1; rows.Count < MaxExportRows; page++)
        {
            var path = list == "approaching" ? $"{ApiBase}/lots/approaching{ApproachingQuery(page, MaxServicePageSize)}" : $"{ApiBase}/lots/expired{ExpiredQuery(page, MaxServicePageSize)}";
            var reply = await SendAsync(HttpMethod.Get, path, null, null, ct);
            if (!reply.IsSuccess) return Failed("export." + list, reply);
            var root = JsonNode.Parse(reply.Body) as JsonObject ?? new JsonObject();
            var items = (root["items"] as JsonArray ?? new JsonArray()).OfType<JsonObject>().ToList();
            rows.AddRange(items.Select(i => (JsonObject)i.DeepClone()));
            if (items.Count < MaxServicePageSize || rows.Count >= ((int?)root["total"] ?? 0)) break;
        }

        var array = new JsonArray(rows.Take(MaxExportRows).Select(r => (JsonNode)r).ToArray());
        var labels = await LabelsAsync(array, ct);
        Decorate(array, labels);
        var expired = list == "expired";
        var header = new[]
        {
            T("Expiry.Col.Product"), "SKU", T("Expiry.Col.LotNo"), T("Expiry.Col.Warehouse"), T("Expiry.Col.Expiry"),
            expired ? T("Expiry.Col.SinceExpiry") : T("Expiry.Col.DaysLeft"), T("Expiry.Col.Available"), T("Expiry.Col.Held"), T("Expiry.Col.Unit"), T("Expiry.Col.Status")
        };
        var csv = new StringBuilder();
        csv.AppendLine(string.Join(';', header.Select(Cell)));
        foreach (var r in array.OfType<JsonObject>())
        {
            csv.AppendLine(string.Join(';', new[]
            {
                (string?)r["itemLabel"], (string?)r["skuLabel"] ?? (string?)r["item"]?["skuId"], (string?)r["lotNumber"], (string?)r["warehouseLabel"],
                (string?)r["expiryDate"], (expired ? r["daysSinceExpiry"] : r["daysToExpiry"])?.ToString(), r["onHand"]?["available"]?.ToString(),
                r["onHand"]?["held"]?.ToString(), (string?)r["onHand"]?["baseUomId"], T("Expiry.Severity." + (string?)r["severity"])
            }.Select(Cell)));
        }

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
        return File(bytes, "text/csv; charset=utf-8", $"expiry-{list}-{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    // ── pickers ──────────────────────────────────────────────────────────────────────────────────────────────────

    [HttpGet("lookups/{kind}")]
    public async Task<IActionResult> Lookups(string kind, [FromQuery] string? warehouseId, CancellationToken ct)
    {
        if (!_permissions.Has(ExpiryKeys.DashboardRead) && !_permissions.Has(ExpiryKeys.FefoOrderRead)) return Refuse(403, null);
        var ledger = InventoryProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        if (ledger is null) return Refuse(401, null);
        switch (kind)
        {
            case "warehouses":
            {
                var reply = await ledger.GetAsync("/api/locations?type=Warehouse", ct);
                return reply.IsSuccess ? Json(new { items = Options(reply.Body) }) : Failed("lookups.warehouses", reply);
            }
            case "skus":
            {
                // the SKUs with lot stock in the warehouse (the FEFO order is per SKU + warehouse, pack §8.2)
                if (!Guid.TryParse(warehouseId, out var wh)) return Refuse(400, "INVALID_REQUEST");
                var reply = await ledger.GetAsync($"/api/inventory/balance?warehouseId={wh:D}&page=1&pageSize=200", ct);
                if (!reply.IsSuccess) return Failed("lookups.skus", reply);
                var rows = (JsonNode.Parse(reply.Body)?["rows"] as JsonArray ?? new JsonArray()).OfType<JsonObject>()
                    .Where(r => !string.IsNullOrEmpty((string?)r["lotId"])).ToList();
                var skus = rows.GroupBy(r => ((string?)r["itemId"], (string?)r["skuId"], (string?)r["skuLevel"])).Select(g => g.Key).ToList();
                var labels = await MasterLabelResolver.ResolveAsync(ledger, skus.Select(s => s.Item1), skus.Select(s => (s.Item2, s.Item3)), [], ct);
                return Json(new
                {
                    items = skus.Select(s => new
                    {
                        itemId = s.Item1, skuId = s.Item2, skuLevel = s.Item3,
                        text = string.Join(" · ", new[] { labels.Sku(s.Item2), labels.Product(s.Item1) ?? s.Item1 }.Where(x => !string.IsNullOrWhiteSpace(x)))
                    }),
                    labelsAvailable = labels.Available
                });
            }
            default:
                return Refuse(404, null);
        }
    }

    // ── plumbing ─────────────────────────────────────────────────────────────────────────────────────────────────

    private string ApproachingQuery(int page, int size)
        => new QueryBuilder().Add("page", page).Add("pageSize", size).Add("q", Search())
            .Add("warehouseId", Request.Query["warehouseId"]).Add("severity", Request.Query["severity"]).Add("horizonDays", Request.Query["horizonDays"])
            .Add("includeHeld", Request.Query["includeHeld"]).Add("sort", Request.Query["sort"]).ToString();

    private string ExpiredQuery(int page, int size)
        => new QueryBuilder().Add("page", page).Add("pageSize", size).Add("warehouseId", Request.Query["warehouseId"])
            .Add("minDaysSinceExpiry", Request.Query["minDaysSinceExpiry"]).ToString();

    private async Task<IActionResult> ListAsync(string operation, Reply reply, CancellationToken ct)
    {
        if (!reply.IsSuccess) return Failed(operation, reply);
        var root = JsonNode.Parse(reply.Body) as JsonObject ?? new JsonObject();
        var items = root["items"] as JsonArray ?? new JsonArray();
        var labels = await LabelsAsync(items, ct);
        Decorate(items, labels);
        var total = (int?)root["total"] ?? items.Count;
        var data = new JsonObject { ["items"] = items.DeepClone(), ["total"] = total, ["filteredTotal"] = total };
        return Content(new JsonObject { ["data"] = data, ["labelsAvailable"] = labels.Available }.ToJsonString(), "application/json");
    }

    private async Task<IActionResult> ProxyAsync(string? everything)
    {
        var path = string.IsNullOrWhiteSpace(everything) ? ApiBase : $"{ApiBase}/{everything.TrimStart('/')}";
        string? body = null;
        if (!HttpMethods.IsGet(Request.Method) && Request.ContentLength is > 0)
        {
            using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
            body = await reader.ReadToEndAsync(HttpContext.RequestAborted);
        }

        var reply = await SendAsync(new HttpMethod(Request.Method), path + Request.QueryString, body, Header("Idempotency-Key"), HttpContext.RequestAborted);
        if (reply.ETag is not null) Response.Headers.ETag = reply.ETag;
        return new ContentResult { StatusCode = reply.StatusCode == 0 ? 503 : reply.StatusCode, Content = reply.Body, ContentType = "application/json" };
    }

    private async Task<Reply> SendAsync(HttpMethod method, string pathAndQuery, string? body, string? idempotencyKey, CancellationToken ct)
    {
        var token = Diten.Web.Services.Auth.AuthTokenCookies.GetAccessToken(Request);
        if (string.IsNullOrWhiteSpace(token)) return Reply.Error(401, "UNAUTHORIZED");
        var tenantId = ResolveTenantId(token);
        if (tenantId is null) return Reply.Error(401, "UNAUTHORIZED");

        Guid? selected = null;
        if (CompanySelection.TokenLegalEntity(User) is null)
        {
            selected = CompanySelection.SelectedCompany(Request, User);
            if (selected is null) return Reply.Error(403, InventoryGatewayClient.CompanyNotSelected);
        }

        var correlationId = Request.Headers.TryGetValue(CorrelationHeaderName, out var c) && !string.IsNullOrWhiteSpace(c.ToString()) ? c.ToString() : HttpContext.TraceIdentifier;
        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var request = new HttpRequestMessage(method, _gatewayUrl + pathAndQuery);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.TryAddWithoutValidation(TenantHeaderName, tenantId);
            request.Headers.TryAddWithoutValidation(CorrelationHeaderName, correlationId);
            if (selected is not null) request.Headers.TryAddWithoutValidation(CompanySelection.LegalEntityHeader, selected.Value.ToString("D"));
            if (Header("Accept-Language") is { } language) request.Headers.TryAddWithoutValidation("Accept-Language", language);
            if (idempotencyKey is not null) request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
            if (body is not null) request.Content = new StringContent(body, Encoding.UTF8, "application/json");

            using var response = await _httpClientFactory.CreateClient().SendAsync(request, ct);
            var text = await response.Content.ReadAsStringAsync(ct);
            _logger.Log(response.IsSuccessStatusCode ? LogLevel.Information : LogLevel.Warning,
                "Expiry proxy completed. method={Method} path={Path} status={StatusCode} correlation_id={CorrelationId} elapsed_ms={ElapsedMs}",
                method.Method, pathAndQuery.Split('?')[0], (int)response.StatusCode, correlationId, stopwatch.ElapsedMilliseconds);
            return new Reply((int)response.StatusCode, text, response.Headers.ETag?.ToString());
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning("Expiry proxy unreachable. method={Method} path={Path} correlation_id={CorrelationId}", method.Method, pathAndQuery.Split('?')[0], correlationId);
            return Reply.Error(503, "SERVICE_UNAVAILABLE");
        }
    }

    /// <summary>Display names of the ids on this page (products, SKUs, warehouses) — MasterLabelResolver.</summary>
    private async Task<MasterLabels> LabelsAsync(JsonArray rows, CancellationToken ct)
    {
        var ledger = InventoryProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        if (ledger is null) return MasterLabels.None;
        var list = rows.OfType<JsonObject>().ToList();
        return await MasterLabelResolver.ResolveAsync(ledger, list.Select(r => (string?)r["item"]?["itemId"]),
            list.Select(r => ((string?)r["item"]?["skuId"], (string?)r["item"]?["skuLevel"])), list.Select(r => (string?)r["warehouse"]?["id"]), ct);
    }

    /// <summary>Adds itemLabel / skuLabel / warehouseLabel next to the ids (a missing name stays the raw id or the warehouse code).</summary>
    private static void Decorate(JsonArray rows, MasterLabels labels)
    {
        foreach (var row in rows.OfType<JsonObject>())
        {
            var item = row["item"];
            row["itemLabel"] = labels.Product((string?)item?["itemId"]) ?? (string?)item?["itemId"];
            row["skuLabel"] = labels.Sku((string?)item?["skuId"]);
            var warehouse = row["warehouse"];
            var codeName = string.Join(" · ", new[] { (string?)warehouse?["code"], (string?)warehouse?["name"] }.Where(x => !string.IsNullOrWhiteSpace(x)));
            row["warehouseLabel"] = labels.Location((string?)warehouse?["id"]) ?? (codeName.Length > 0 ? codeName : (string?)warehouse?["id"]);
        }
    }

    private static IEnumerable<object> Options(string body)
    {
        JsonNode? root;
        try { root = JsonNode.Parse(body); } catch (JsonException) { return []; }
        var list = root as JsonArray ?? root?["items"] as JsonArray ?? root?["data"] as JsonArray ?? root?["data"]?["items"] as JsonArray ?? new JsonArray();
        return list.OfType<JsonObject>().Select(o => (object)new
        {
            value = o["id"]?.ToString(),
            text = string.Join(" · ", new[] { o["code"]?.ToString(), o["name"]?.ToString() }.Where(x => !string.IsNullOrWhiteSpace(x)))
        });
    }

    private static string Cell(string? value)
    {
        var text = value ?? string.Empty;
        if (text.Length > 0 && "=+-@".Contains(text[0])) text = "'" + text; // CSV formula injection guard
        return text.Contains(';') || text.Contains('"') || text.Contains('\n') ? "\"" + text.Replace("\"", "\"\"") + "\"" : text;
    }

    private string T(string key) => _localizer[key].Value;

    private (int Page, int Size) Paging()
    {
        var start = int.TryParse(Request.Query["start"], NumberStyles.None, CultureInfo.InvariantCulture, out var s) ? s : 0;
        var length = int.TryParse(Request.Query["length"], NumberStyles.None, CultureInfo.InvariantCulture, out var l) && l > 0 ? l : 10;
        var size = Math.Clamp(length, 1, MaxServicePageSize);
        return (start / size + 1, size);
    }

    private string? Search()
    {
        var term = Request.Query["search"].ToString().Trim();
        return term.Length == 0 ? null : term;
    }

    private string? Header(string name) => Request.Headers.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value.ToString()) ? value.ToString() : null;

    private IActionResult Json(JsonObject body) => Content(body.ToJsonString(), "application/json");

    private IActionResult Failed(string operation, Reply reply)
    {
        _logger.LogWarning("Expiry {Operation} failed with {Status}.", operation, reply.StatusCode);
        return Refuse(reply.StatusCode == 0 ? 503 : reply.StatusCode, reply.Code);
    }

    private IActionResult Failed(string operation, GatewayReply reply)
    {
        _logger.LogWarning("Expiry {Operation} failed with {Status}.", operation, reply.StatusCode);
        return Refuse(reply.StatusCode == 0 ? 503 : reply.StatusCode, reply.Error?.Code);
    }

    /// <summary>A refusal in the reader's language (Expiry.Errors.{CODE}); the service's own code is kept.</summary>
    private IActionResult Refuse(int status, string? code)
    {
        var key = code is null ? (status is 401 or 403 ? "Expiry.Errors.FORBIDDEN" : status >= 500 ? "Expiry.Errors.SERVICE_UNAVAILABLE" : "Expiry.Errors.GENERIC")
            : "Expiry.Errors." + code;
        var message = _localizer[key];
        return new JsonResult(new { error = new { code = code ?? "ERROR", message = message.ResourceNotFound ? _localizer["Expiry.Errors.GENERIC"].Value : message.Value } })
        {
            StatusCode = status
        };
    }

    private string? ResolveTenantId(string token)
    {
        if (Guid.TryParse(FindTenantClaim(User.Claims), out var claimTenantId)) return claimTenantId.ToString();
        try
        {
            return Guid.TryParse(FindTenantClaim(new JwtSecurityTokenHandler().ReadJwtToken(token).Claims), out var jwtTenantId) ? jwtTenantId.ToString() : null;
        }
        catch
        {
            return null;
        }
    }

    private static string? FindTenantClaim(IEnumerable<Claim> claims)
        => claims.FirstOrDefault(claim =>
            string.Equals(claim.Type, "tenant_id", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(claim.Type, "tenantId", StringComparison.OrdinalIgnoreCase) ||
            claim.Type.EndsWith("/tenant_id", StringComparison.OrdinalIgnoreCase) ||
            claim.Type.EndsWith("/tenantId", StringComparison.OrdinalIgnoreCase))?.Value;

    private sealed record Reply(int StatusCode, string Body, string? ETag)
    {
        public bool IsSuccess => StatusCode is >= 200 and < 300;

        public string? Code
        {
            get
            {
                try { return JsonNode.Parse(Body)?["error"]?["code"]?.GetValue<string>(); }
                catch (Exception) { return null; }
            }
        }

        public static Reply Error(int status, string code) => new(status, JsonSerializer.Serialize(new { error = new { code } }), null);
    }

    private sealed class QueryBuilder
    {
        private readonly List<string> _parts = [];

        public QueryBuilder Add(string name, object? value)
        {
            var text = value switch
            {
                null => null,
                Microsoft.Extensions.Primitives.StringValues values => values.ToString(),
                IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
                _ => value.ToString()
            };
            if (!string.IsNullOrWhiteSpace(text)) _parts.Add($"{name}={Uri.EscapeDataString(text)}");
            return this;
        }

        public override string ToString() => _parts.Count == 0 ? string.Empty : "?" + string.Join('&', _parts);
    }
}

/// <summary>
/// MOD-0176 pack §11.3 / §14 — the keys this Web reads for DISPLAY (UAS-001 §4: absent, not disabled). InventoryService's
/// [HasPermission] stays the authority. Action code → key, verbatim from the pack table; "Karantinaya al" is MOD-0175's key (a link).
/// </summary>
public static class ExpiryKeys
{
    public const string DashboardRead = "expiry.dashboard.read";
    public const string FefoOrderRead = "expiry.fefo-order.read";
    public const string ExceptionsRead = "expiry.exceptions.read";
    public const string ExceptionsCreate = "expiry.exceptions.create";
    public const string PoliciesRead = "expiry.policies.read";
    public const string QuarantineHoldsCreate = "quarantine.holds.create";

    public static readonly IReadOnlyDictionary<string, string> Actions = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["EXPIRY_EXCEPTION_OPEN"] = ExceptionsCreate,
        ["EXPIRY_FEFO_ORDER_VIEW"] = FefoOrderRead,
        ["EXPIRY_LIST_EXPORT"] = DashboardRead,
        ["EXPIRY_FEFO_ORDER_CALCULATE"] = FefoOrderRead,
        ["QUARANTINE_LINK"] = QuarantineHoldsCreate
    };

    public static IReadOnlyDictionary<string, bool> For(IPermissionSnapshot permissions)
        => Actions.ToDictionary(a => a.Key, a => permissions.Has(a.Value), StringComparer.Ordinal);
}
