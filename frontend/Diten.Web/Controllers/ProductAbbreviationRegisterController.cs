using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Globalization;
using System.Text.Json;
using Diten.BuildingBlocks.ListExport;
using Diten.Web.Models.ProductAbbreviationRegister;
using Diten.Web.Views.MDM.ProductAbbreviationRegister;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Diten.Web.Controllers;

[Authorize]
[Route("MDM/ProductAbbreviationRegister")]
public sealed class ProductAbbreviationRegisterController : Controller
{
    private const string ServicePath = "/api/product-abbreviations";
    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly IStringLocalizer<SharedResource> _sharedLocalizer;
    private readonly IStringLocalizer<ProductAbbreviationRegisterIndex> _pageLocalizer;
    private readonly ILogger<ProductAbbreviationRegisterController> _logger;

    public ProductAbbreviationRegisterController(
        HttpClient httpClient,
        IConfiguration configuration,
        IStringLocalizer<SharedResource> sharedLocalizer,
        IStringLocalizer<ProductAbbreviationRegisterIndex> pageLocalizer,
        ILogger<ProductAbbreviationRegisterController> logger)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"]?.TrimEnd('/')
            ?? throw new InvalidOperationException("GatewayUrl configuration is required.");
        _sharedLocalizer = sharedLocalizer;
        _pageLocalizer = pageLocalizer;
        _logger = logger;
    }

    /// <summary>
    /// UAS-001 / FIX1 S5 — a signed-in reader without the permission gets a 403 INSIDE the shell (the view draws
    /// only the _AccessDenied partial). <c>Forbid()</c> redirected to an AccessDeniedPath this app does not configure
    /// (302 → 404); the trap is written down in ShellAccessFilter.
    /// </summary>
    [HttpGet("")]
    public IActionResult Index()
    {
        if (!Has("mdm.product-abbreviations.read"))
        {
            Response.StatusCode = StatusCodes.Status403Forbidden;
        }

        return View("~/Views/MDM/ProductAbbreviationRegister/Index.cshtml");
    }

    [HttpGet("api/global-products/selector")]
    public Task<IActionResult> GlobalProductSelector(CancellationToken cancellationToken)
        => Has("mdm.global-products.read") ? ProxyAsync(
            HttpMethod.Get,
            $"{_gatewayUrl}/api/global-products/selector{Request.QueryString}",
            content: null,
            mutation: false,
            cancellationToken) : Task.FromResult(Denied());

    [HttpGet("api/by-global-product/{globalProductId:guid}")]
    public Task<IActionResult> GetByGlobalProduct(Guid globalProductId, CancellationToken cancellationToken)
        => Has("mdm.product-abbreviations.read") ? ProxyAsync(
            HttpMethod.Get,
            $"{_gatewayUrl}{ServicePath}/by-global-product/{globalProductId:D}",
            content: null,
            mutation: false,
            cancellationToken) : Task.FromResult(Denied());

    /// <summary>
    /// WP-MDM-ABB-LIST-01 — the register list in the list component's SERVER protocol: start, length, search, orderBy,
    /// orderDir and the applied product filter in; <c>{ data: { items, total, filteredTotal, hasOpenEntry, truncated } }</c>
    /// out. A list shows ONE product's entries (MDM answers them in one bounded read, at most
    /// <see cref="MdmEntryReadLimit"/>); this proxy searches, sorts (whitelist below; anything else is 400, never a silent
    /// default) and pages them. MDM is not changed. No product chosen = an empty page, without a gateway call.
    /// <list type="bullet">
    /// <item>LIST-FIX1 item 4 — <c>hasOpenEntry</c> is read from EVERY entry of the product, not from the page or the
    /// search result: the request button must not reappear on page two or behind a search.</item>
    /// <item>LIST-FIX1 item 6 — MDM stops at <see cref="MdmEntryReadLimit"/> entries; a read that came back full says so
    /// (<c>truncated</c>), because the totals then count only what was read.</item>
    /// </list>
    /// </summary>
    [HttpGet("api/list")]
    public async Task<IActionResult> List(
        [FromQuery] int start = 0,
        [FromQuery] int length = 10,
        [FromQuery] string? search = null,
        [FromQuery] string? orderBy = null,
        [FromQuery] string? orderDir = null,
        [FromQuery] string? globalProductId = null,
        CancellationToken cancellationToken = default)
    {
        if (!Has("mdm.product-abbreviations.read")) return Denied();
        if (start < 0 || length is < 1 or > MaxListPageLength)
            return ListRefused("ABBREVIATION_LIST_PAGE_INVALID");
        if (!IsListOrder(orderBy, orderDir))
            return ListRefused("ABBREVIATION_LIST_ORDER_INVALID");
        if (!TryReadProduct(globalProductId, out var product))
            return ListRefused("ABBREVIATION_LIST_PRODUCT_INVALID");
        if (product is null)
            return Ok(new
            {
                isSuccessful = true,
                data = new
                {
                    items = Array.Empty<ProductAbbreviationListRow>(), total = 0, filteredTotal = 0,
                    hasOpenEntry = false, truncated = false
                }
            });

        var (refusal, rows) = await ReadEntriesAsync(product.Value, cancellationToken);
        if (refusal is not null) return refusal;

        var filtered = Search(rows, search);
        var ordered = ProductAbbreviationListRow.Order(filtered, orderBy ?? "abbreviation", orderDir == "desc");
        return Ok(new
        {
            isSuccessful = true,
            data = new
            {
                items = ordered.Skip(start).Take(length).ToList(),
                total = rows.Count,
                filteredTotal = filtered.Count,
                hasOpenEntry = rows.Any(row => row.IsOpen),
                truncated = rows.Count >= MdmEntryReadLimit
            }
        });
    }

    /// <summary>
    /// LIST-FIX1 item 5 — BL-452: THE FILE IS THE LIST. The same product, search and order as the list, every matching
    /// entry (no start/length), the visible columns in screen order (whitelist <see cref="ExportColumns"/>; anything else
    /// is 400), headers and values in the request culture. A read MDM cut off at <see cref="MdmEntryReadLimit"/> is
    /// refused with 413 EXPORT_TOO_LARGE: a GxP register is never exported silently in part.
    /// </summary>
    [HttpGet("api/list/export")]
    public async Task<IActionResult> Export(
        [FromQuery] string? format = null,
        [FromQuery] string[]? columns = null,
        [FromQuery] string? search = null,
        [FromQuery] string? orderBy = null,
        [FromQuery] string? orderDir = null,
        [FromQuery] string? globalProductId = null,
        CancellationToken cancellationToken = default)
    {
        if (!Has("mdm.product-abbreviations.read")) return Denied();
        if (!ListExportContract.TryParseFormat(format, out var exportFormat))
            return ListRefused(ListExportContract.FormatInvalidCode);
        if (!IsListOrder(orderBy, orderDir))
            return ListRefused("ABBREVIATION_LIST_ORDER_INVALID");
        if (!TryReadProduct(globalProductId, out var product))
            return ListRefused("ABBREVIATION_LIST_PRODUCT_INVALID");
        if (!ExportColumns.TryResolve(columns, out var exportColumns, out _))
            return ListRefused(ListExportContract.ColumnsInvalidCode);

        List<ProductAbbreviationListRow> rows = [];
        if (product is not null)
        {
            var (refusal, read) = await ReadEntriesAsync(product.Value, cancellationToken);
            if (refusal is not null) return refusal;
            if (read.Count >= MdmEntryReadLimit)
                return StatusCode(StatusCodes.Status413PayloadTooLarge, new
                {
                    success = false,
                    errors = new[] { ListExportContract.TooLargeCode },
                    errorCode = ListExportContract.TooLargeCode
                });
            rows = read;
        }

        var ordered = ProductAbbreviationListRow.Order(Search(rows, search), orderBy ?? "abbreviation", orderDir == "desc");
        var culture = ListExportContract.ResolveCulture(Request.Headers.AcceptLanguage.ToString());
        // The page's resources answer in the CURRENT UI culture: the file's language for the time it is written.
        var file = WithCulture(culture, () =>
            ListExportWriter.Write(exportFormat, exportColumns, ordered, culture, _pageLocalizer["ProductAbbreviationRegisterTitle"].Value));

        return File(file, ListExportContract.ContentType(exportFormat),
            ListExportContract.FileName("product-abbreviation-register", DateTimeOffset.UtcNow, exportFormat));
    }

    /// <summary>
    /// LIST-FIX2 item 6 — runs <paramref name="work"/> in <paramref name="culture"/> and gives the caller its own culture back,
    /// thrown or not. SYNCHRONOUS on purpose: .NET carries the culture with the async flow, so a switch inside an async
    /// action is dropped when the action returns and its restore could never be observed (CT sabotage: "not restored" was
    /// not red). Here the caller shares the culture, so the restore is real and is measured.
    /// </summary>
    public static T WithCulture<T>(CultureInfo culture, Func<T> work)
    {
        var previous = (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture);
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = culture;
            return work();
        }
        finally
        {
            (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture) = previous;
        }
    }

    /// <summary>MDM's one-product read stops here (<c>GetByGlobalProductIdAsync</c>, Limit 500).</summary>
    public const int MdmEntryReadLimit = 500;

    private const int MaxListPageLength = 100;

    /// <summary>The register list's sortable columns (the page's column <c>data</c> names); the rest is 400.</summary>
    internal static readonly IReadOnlySet<string> SortableListColumns =
        new HashSet<string>(StringComparer.Ordinal) { "abbreviation", "lifecycleStatus", "version", "retirementPending" };

    private static bool IsListOrder(string? orderBy, string? orderDir)
        => (orderBy is null || SortableListColumns.Contains(orderBy))
           && (orderDir is null || orderDir is "asc" or "desc");

    /// <summary>No value = no product (an empty list); a value that is not a product id is refused, never read as "none".</summary>
    private static bool TryReadProduct(string? value, out Guid? product)
    {
        product = null;
        if (string.IsNullOrEmpty(value)) return true;
        if (!Guid.TryParseExact(value, "D", out var id)) return false;
        if (id != Guid.Empty) product = id;
        return true;
    }

    /// <summary>
    /// The product's entries as MDM answers them: 401 without a usable token / tenant, 502 when the gateway cannot be
    /// reached or answers what is not an entry list, MDM's own refusal (403, 404, 503 …) passed on as it came.
    /// </summary>
    private async Task<(IActionResult? Refusal, List<ProductAbbreviationListRow> Rows)> ReadEntriesAsync(
        Guid product,
        CancellationToken cancellationToken)
    {
        var targetUrl = $"{_gatewayUrl}{ServicePath}/by-global-product/{product:D}/entries";
        if (!TryCreateGatewayRequest(HttpMethod.Get, targetUrl, null, mutation: false, out var request))
            return (Unauthorized(new { errors = new[] { _sharedLocalizer["Unauthorized"].Value } }), []);

        string body;
        int status;
        try
        {
            using (request)
            using (var response = await _httpClient.SendAsync(request, cancellationToken))
            {
                body = await response.Content.ReadAsStringAsync(cancellationToken);
                status = (int)response.StatusCode;
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(exception, "ABB Register list proxy failed for {TargetUrl}.", targetUrl);
            return (StatusCode(StatusCodes.Status502BadGateway, new { errors = new[] { _sharedLocalizer["GatewayError"].Value } }), []);
        }

        if (status is < 200 or > 299)
            return (new ContentResult { StatusCode = status, ContentType = "application/json", Content = body }, []);

        return ProductAbbreviationListRow.TryReadEntries(body, out var rows)
            ? (null, rows)
            : (StatusCode(StatusCodes.Status502BadGateway, new { errors = new[] { _sharedLocalizer["GatewayError"].Value } }), []);
    }

    /// <summary>
    /// LIST-FIX1 item 7 — what the reader can see in a row, as the page's former client-side search matched it: the code,
    /// the lifecycle as shown (the reader's language), the version and the retirement-pending yes/no. The product column
    /// is the filter itself (one product per list), so it does not narrow anything.
    /// </summary>
    private List<ProductAbbreviationListRow> Search(List<ProductAbbreviationListRow> rows, string? search)
    {
        var term = search?.Trim();
        if (string.IsNullOrEmpty(term)) return rows;
        // LIST-FIX2 item 1 — two kinds of text, two comparisons. The CODE (three ASCII letters, stored upper-case) and the
        // version are not words of any language: compared ordinally, so a Turkish reader's "abi" finds "ABI" (under Turkish
        // rules "i" pairs with "İ" and never would). The lifecycle and yes/no are the reader's WORDS: compared in the
        // reader's language, so "iptal" finds "İptal edildi" and "in use" finds "In use".
        var words = CultureInfo.CurrentUICulture.CompareInfo;
        return rows.Where(row =>
                row.Abbreviation.Contains(term, StringComparison.OrdinalIgnoreCase)
                || row.Version.ToString(CultureInfo.InvariantCulture).Contains(term, StringComparison.Ordinal)
                || ReaderWords(row).Any(text => words.IndexOf(text, term, CompareOptions.IgnoreCase) >= 0))
            .ToList();
    }

    private IEnumerable<string> ReaderWords(ProductAbbreviationListRow row)
    {
        yield return LifecycleText(row);
        yield return YesNo(row.RetirementPending);
    }

    private string LifecycleText(ProductAbbreviationListRow row)
        => row.LifecycleName is { } name
            ? _pageLocalizer["Lifecycle" + name[0] + name[1..].ToLowerInvariant()].Value
            : string.Empty;

    private string YesNo(bool value) => _pageLocalizer[value ? "Yes" : "No"].Value;

    /// <summary>
    /// The export whitelist: the list's data columns by their <c>data</c> key, headers from the page's resources.
    /// LIST-FIX2 item 2 — NO product column. The screen shows the product's code and name, which this proxy cannot read
    /// for every register reader (MDM's product read needs mdm.global-products.read, which an ABB reader or auditor need
    /// not hold), and a bare GUID in a GxP printout is not the screen. The list is one product's, chosen by the reader.
    /// </summary>
    private ListExportColumnSet<ProductAbbreviationListRow> ExportColumns => new(
    [
        new("abbreviation", _ => _pageLocalizer["Abbreviation"].Value, (row, _) => row.Abbreviation),
        new("lifecycleStatus", _ => _pageLocalizer["LifecycleStatus"].Value, (row, _) => LifecycleText(row)),
        new("version", _ => _pageLocalizer["Version"].Value, (row, culture) => row.Version.ToString(culture)),
        new("retirementPending", _ => _pageLocalizer["RetirementPending"].Value, (row, _) => YesNo(row.RetirementPending))
    ]);

    private IActionResult ListRefused(string code) => StatusCode(StatusCodes.Status400BadRequest, new
    {
        success = false,
        errors = new[] { code },
        errorCode = code
    });

    /// <summary>FIX1 B2 — every entry of the product (requested, active, closed), for the register page.</summary>
    [HttpGet("api/by-global-product/{globalProductId:guid}/entries")]
    public Task<IActionResult> ListByGlobalProduct(Guid globalProductId, CancellationToken cancellationToken)
        => Has("mdm.product-abbreviations.read") ? ProxyAsync(
            HttpMethod.Get,
            $"{_gatewayUrl}{ServicePath}/by-global-product/{globalProductId:D}/entries",
            content: null,
            mutation: false,
            cancellationToken) : Task.FromResult(Denied());

    [HttpGet("api/{registerEntryId:guid}/evidence")]
    public Task<IActionResult> GetEvidence(Guid registerEntryId, CancellationToken cancellationToken)
        => Has("mdm.product-abbreviations.audit") ? ProxyAsync(
            HttpMethod.Get,
            $"{_gatewayUrl}{ServicePath}/{registerEntryId:D}/evidence",
            content: null,
            mutation: false,
            cancellationToken) : Task.FromResult(Denied());

    [HttpPost("api/requests")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> RequestAllocation(
        [FromForm] RequestProductAbbreviationViewModel model,
        CancellationToken cancellationToken)
    {
        if (!Has("mdm.product-abbreviations.request") || !Has("mdm.global-products.read"))
            return Task.FromResult(Denied());
        model.Abbreviation = model.Abbreviation?.Trim() ?? string.Empty;
        if (!ModelState.IsValid)
        {
            return Task.FromResult<IActionResult>(BadRequest(new
            {
                errors = ModelState.Values.SelectMany(value => value.Errors)
                    .Select(error => error.ErrorMessage)
            }));
        }

        return ProxyAsync(
            HttpMethod.Post,
            $"{_gatewayUrl}{ServicePath}/requests",
            JsonContent.Create(new { model.GlobalProductId, model.Abbreviation }),
            mutation: true,
            cancellationToken);
    }

    [HttpPatch("api/{registerEntryId:guid}/cancel")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Cancel(
        Guid registerEntryId,
        [FromBody] ProductAbbreviationDecisionViewModel model,
        CancellationToken cancellationToken)
        => Has("mdm.product-abbreviations.cancel") ? ProxyMutationAsync(HttpMethod.Patch, $"{ServicePath}/{registerEntryId:D}/cancel", model, cancellationToken) : Task.FromResult(Denied());

    [HttpPatch("api/{registerEntryId:guid}/approve")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Approve(
        Guid registerEntryId,
        [FromBody] ProductAbbreviationDecisionViewModel model,
        CancellationToken cancellationToken)
        => Has("mdm.product-abbreviations.approve") ? ProxyMutationAsync(HttpMethod.Patch, $"{ServicePath}/{registerEntryId:D}/approve", model, cancellationToken) : Task.FromResult(Denied());

    [HttpPatch("api/{registerEntryId:guid}/reject")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Reject(
        Guid registerEntryId,
        [FromBody] ProductAbbreviationDecisionViewModel model,
        CancellationToken cancellationToken)
        => Has("mdm.product-abbreviations.reject") ? ProxyMutationAsync(HttpMethod.Patch, $"{ServicePath}/{registerEntryId:D}/reject", model, cancellationToken) : Task.FromResult(Denied());

    [HttpPost("api/{registerEntryId:guid}/corrections")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> InitiateCorrection(
        Guid registerEntryId,
        [FromBody] ProductAbbreviationCorrectionViewModel model,
        CancellationToken cancellationToken)
        => Has("mdm.product-abbreviations.correct") ? ProxyMutationAsync(HttpMethod.Post, $"{ServicePath}/{registerEntryId:D}/corrections", model, cancellationToken) : Task.FromResult(Denied());

    [HttpPost("api/{registerEntryId:guid}/retirement-requests")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> RequestRetirement(
        Guid registerEntryId,
        [FromBody] ProductAbbreviationRetirementViewModel model,
        CancellationToken cancellationToken)
        => Has("mdm.product-abbreviations.retire") ? ProxyMutationAsync(HttpMethod.Post, $"{ServicePath}/{registerEntryId:D}/retirement-requests", model, cancellationToken) : Task.FromResult(Denied());

    private Task<IActionResult> ProxyMutationAsync<T>(
        HttpMethod method,
        string relativePath,
        T model,
        CancellationToken cancellationToken)
        => ProxyAsync(
            method,
            $"{_gatewayUrl}{relativePath}",
            JsonContent.Create(model),
            mutation: true,
            cancellationToken);

    private async Task<IActionResult> ProxyAsync(
        HttpMethod method,
        string targetUrl,
        HttpContent? content,
        bool mutation,
        CancellationToken cancellationToken)
    {
        if (!TryCreateGatewayRequest(method, targetUrl, content, mutation, out var request))
            return Unauthorized(new { errors = new[] { _sharedLocalizer["Unauthorized"].Value } });

        try
        {
            if (mutation)
            {
                var subjects = User.Claims.Where(c => c.Type is "sub" or ClaimTypes.NameIdentifier)
                    .Select(c => c.Value).Distinct(StringComparer.Ordinal).ToArray();
                if (subjects.Length != 1 || !Guid.TryParseExact(subjects[0], "D", out var subject) || subject == Guid.Empty)
                {
                    request.Dispose();
                    return Unauthorized();
                }
                var payload = content is null ? string.Empty : await content.ReadAsStringAsync(cancellationToken);
                // Server-owned, exact-payload replay identity. No browser identity or credential is hashed.
                var facts = System.Text.Json.JsonSerializer.Serialize(new[] {
                    request.Headers.GetValues("X-Tenant-Id").Single(), subject.ToString("D"), method.Method,
                    new Uri(targetUrl).AbsolutePath, payload });
                request.Headers.Add("Idempotency-Key", Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(facts))));
            }
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
        catch (Exception exception)
        {
            _logger.LogError(exception, "ABB Register gateway proxy failed for {TargetUrl}.", targetUrl);
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                errors = new[] { _sharedLocalizer["GatewayError"].Value }
            });
        }
    }

    private bool TryCreateGatewayRequest(
        HttpMethod method,
        string targetUrl,
        HttpContent? content,
        bool mutation,
        out HttpRequestMessage request)
    {
        request = new HttpRequestMessage(method, targetUrl) { Content = content };
        var token = Diten.Web.Services.Auth.AuthTokenCookies.GetAccessToken(Request);
        if (!string.IsNullOrWhiteSpace(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var tenants = User.Claims.Where(claim => claim.Type == "tenant_id").Select(claim => claim.Value).ToArray();
        var aliases = User.Claims.Where(claim => claim.Type == "tenantId" || claim.Type.EndsWith("/tenantId", StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrWhiteSpace(token) || tenants.Length != 1
            || !Guid.TryParseExact(tenants[0], "D", out var tenantId) || tenantId == Guid.Empty
            || aliases.Any(claim => claim.Value != tenants[0]))
        {
            request.Dispose();
            request = null!;
            return false;
        }

        request.Headers.Add("X-Tenant-Id", tenantId.ToString("D"));
        request.Headers.Add("X-Correlation-Id", HttpContext.TraceIdentifier);
        return true;
    }

    /// <summary>FIX1 S5 — a coded 403 answer for the page's own calls; never a redirect.</summary>
    private IActionResult Denied() => StatusCode(StatusCodes.Status403Forbidden, new
    {
        success = false,
        errors = new[] { _sharedLocalizer["AccessDenied"].Value },
        errorCode = "ABBREVIATION_PERMISSION_DENIED"
    });

    private bool Has(string permission) => User.Identity?.IsAuthenticated == true && User.Claims
        .Where(claim => claim.Type is "permission" or "permissions")
        .SelectMany(claim => claim.Value.Split(new[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        .Contains(permission, StringComparer.Ordinal);
}
