using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Diten.Web.Models.ProductAbbreviationRegister;
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
    private readonly ILogger<ProductAbbreviationRegisterController> _logger;

    public ProductAbbreviationRegisterController(
        HttpClient httpClient,
        IConfiguration configuration,
        IStringLocalizer<SharedResource> sharedLocalizer,
        ILogger<ProductAbbreviationRegisterController> logger)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"]?.TrimEnd('/')
            ?? throw new InvalidOperationException("GatewayUrl configuration is required.");
        _sharedLocalizer = sharedLocalizer;
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
    /// orderDir and the applied product filter in; <c>{ data: { items, total, filteredTotal } }</c> out. A list shows ONE
    /// product's entries (MDM answers them in one bounded read, at most 500); this proxy searches (abbreviation),
    /// sorts (whitelist below; anything else is 400, never a silent default) and pages them. MDM is not changed.
    /// No product chosen = an empty page, without a gateway call.
    /// </summary>
    [HttpGet("api/list")]
    public async Task<IActionResult> List(
        [FromQuery] int start = 0,
        [FromQuery] int length = 10,
        [FromQuery] string? search = null,
        [FromQuery] string? orderBy = null,
        [FromQuery] string? orderDir = null,
        [FromQuery] Guid? globalProductId = null,
        CancellationToken cancellationToken = default)
    {
        if (!Has("mdm.product-abbreviations.read")) return Denied();
        if (start < 0 || length is < 1 or > MaxListPageLength)
            return ListRefused("ABBREVIATION_LIST_PAGE_INVALID");
        if (orderBy is not null && !SortableListColumns.Contains(orderBy)
            || orderDir is not null && orderDir is not ("asc" or "desc"))
            return ListRefused("ABBREVIATION_LIST_ORDER_INVALID");
        if (globalProductId is null || globalProductId == Guid.Empty)
            return Ok(new { isSuccessful = true, data = new { items = Array.Empty<ProductAbbreviationListRow>(), total = 0, filteredTotal = 0 } });

        var targetUrl = $"{_gatewayUrl}{ServicePath}/by-global-product/{globalProductId.Value:D}/entries";
        if (!TryCreateGatewayRequest(HttpMethod.Get, targetUrl, null, mutation: false, out var request))
            return Unauthorized(new { errors = new[] { _sharedLocalizer["Unauthorized"].Value } });

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
            return StatusCode(StatusCodes.Status502BadGateway, new { errors = new[] { _sharedLocalizer["GatewayError"].Value } });
        }

        // MDM's own refusal (403, 404, 503 …) is passed on as it came: the page reads its code.
        if (status is < 200 or > 299)
            return new ContentResult { StatusCode = status, ContentType = "application/json", Content = body };

        if (!ProductAbbreviationListRow.TryReadEntries(body, out var rows))
            return StatusCode(StatusCodes.Status502BadGateway, new { errors = new[] { _sharedLocalizer["GatewayError"].Value } });

        var term = search?.Trim();
        var filtered = string.IsNullOrEmpty(term)
            ? rows
            : rows.Where(row => row.Abbreviation.Contains(term, StringComparison.OrdinalIgnoreCase)).ToList();
        var ordered = ProductAbbreviationListRow.Order(filtered, orderBy ?? "abbreviation", orderDir == "desc");
        return Ok(new
        {
            isSuccessful = true,
            data = new { items = ordered.Skip(start).Take(length).ToList(), total = rows.Count, filteredTotal = filtered.Count }
        });
    }

    private const int MaxListPageLength = 100;

    /// <summary>The register list's sortable columns (the page's column <c>data</c> names); the rest is 400.</summary>
    internal static readonly IReadOnlySet<string> SortableListColumns =
        new HashSet<string>(StringComparer.Ordinal) { "abbreviation", "lifecycleStatus", "version", "retirementPending" };

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
