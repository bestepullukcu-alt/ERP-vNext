using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
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

    [HttpGet("")]
    public IActionResult Index() => Has("mdm.product-abbreviations.read")
        ? View("~/Views/MDM/ProductAbbreviationRegister/Index.cshtml") : Forbid();

    [HttpGet("api/global-products/selector")]
    public Task<IActionResult> GlobalProductSelector(CancellationToken cancellationToken)
        => Has("mdm.global-products.read") ? ProxyAsync(
            HttpMethod.Get,
            $"{_gatewayUrl}/api/global-products/selector{Request.QueryString}",
            content: null,
            mutation: false,
            cancellationToken) : Task.FromResult<IActionResult>(Forbid());

    [HttpGet("api/by-global-product/{globalProductId:guid}")]
    public Task<IActionResult> GetByGlobalProduct(Guid globalProductId, CancellationToken cancellationToken)
        => Has("mdm.product-abbreviations.read") ? ProxyAsync(
            HttpMethod.Get,
            $"{_gatewayUrl}{ServicePath}/by-global-product/{globalProductId:D}",
            content: null,
            mutation: false,
            cancellationToken) : Task.FromResult<IActionResult>(Forbid());

    [HttpGet("api/{registerEntryId:guid}/evidence")]
    public Task<IActionResult> GetEvidence(Guid registerEntryId, CancellationToken cancellationToken)
        => Has("mdm.product-abbreviations.audit") ? ProxyAsync(
            HttpMethod.Get,
            $"{_gatewayUrl}{ServicePath}/{registerEntryId:D}/evidence",
            content: null,
            mutation: false,
            cancellationToken) : Task.FromResult<IActionResult>(Forbid());

    [HttpPost("api/requests")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> RequestAllocation(
        [FromForm] RequestProductAbbreviationViewModel model,
        CancellationToken cancellationToken)
    {
        if (!Has("mdm.product-abbreviations.request") || !Has("mdm.global-products.read"))
            return Task.FromResult<IActionResult>(Forbid());
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
        => Has("mdm.product-abbreviations.cancel") ? ProxyMutationAsync(HttpMethod.Patch, $"{ServicePath}/{registerEntryId:D}/cancel", model, cancellationToken) : Task.FromResult<IActionResult>(Forbid());

    [HttpPatch("api/{registerEntryId:guid}/approve")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Approve(
        Guid registerEntryId,
        [FromBody] ProductAbbreviationDecisionViewModel model,
        CancellationToken cancellationToken)
        => Has("mdm.product-abbreviations.approve") ? ProxyMutationAsync(HttpMethod.Patch, $"{ServicePath}/{registerEntryId:D}/approve", model, cancellationToken) : Task.FromResult<IActionResult>(Forbid());

    [HttpPatch("api/{registerEntryId:guid}/reject")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Reject(
        Guid registerEntryId,
        [FromBody] ProductAbbreviationDecisionViewModel model,
        CancellationToken cancellationToken)
        => Has("mdm.product-abbreviations.reject") ? ProxyMutationAsync(HttpMethod.Patch, $"{ServicePath}/{registerEntryId:D}/reject", model, cancellationToken) : Task.FromResult<IActionResult>(Forbid());

    [HttpPost("api/{registerEntryId:guid}/corrections")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> InitiateCorrection(
        Guid registerEntryId,
        [FromBody] ProductAbbreviationCorrectionViewModel model,
        CancellationToken cancellationToken)
        => Has("mdm.product-abbreviations.correct") ? ProxyMutationAsync(HttpMethod.Post, $"{ServicePath}/{registerEntryId:D}/corrections", model, cancellationToken) : Task.FromResult<IActionResult>(Forbid());

    [HttpPost("api/{registerEntryId:guid}/retirement-requests")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> RequestRetirement(
        Guid registerEntryId,
        [FromBody] ProductAbbreviationRetirementViewModel model,
        CancellationToken cancellationToken)
        => Has("mdm.product-abbreviations.retire") ? ProxyMutationAsync(HttpMethod.Post, $"{ServicePath}/{registerEntryId:D}/retirement-requests", model, cancellationToken) : Task.FromResult<IActionResult>(Forbid());

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

    private bool Has(string permission) => User.Identity?.IsAuthenticated == true && User.Claims
        .Where(claim => claim.Type is "permission" or "permissions")
        .SelectMany(claim => claim.Value.Split(new[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        .Contains(permission, StringComparer.Ordinal);
}
