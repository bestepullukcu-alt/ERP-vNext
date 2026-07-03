using System.Security.Cryptography;
using System.Text;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers.Internal;

// Vanity slug → tenant resolution for the Web login entry point (e.g. http://<host>/gmg).
// Consumed server-to-server by the Web app's slug fallback middleware, which holds no tenant
// context and only needs to translate a public slug into the tenant's id for the login link.
// Gated by the shared internal API key like the other internal controllers. Read-only.
[ApiController]
[AllowAnonymous]
[Route("api/internal/tenants")]
public sealed class InternalTenantResolveController : ControllerBase
{
    private const string InternalApiKeyHeader = "X-Internal-Api-Key";

    private readonly ITenantRegistryRepository _tenants;
    private readonly IConfiguration _configuration;
    private readonly ILogger<InternalTenantResolveController> _logger;

    public InternalTenantResolveController(
        ITenantRegistryRepository tenants,
        IConfiguration configuration,
        ILogger<InternalTenantResolveController> logger)
    {
        _tenants = tenants;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpGet("by-slug/{slug}")]
    public async Task<IActionResult> ResolveBySlug(string slug, CancellationToken ct)
    {
        if (!IsInternalRequestAuthorized())
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(slug))
        {
            return NotFound();
        }

        var tenant = await _tenants.GetBySlugAsync(slug.Trim().ToLowerInvariant(), ct);
        if (tenant is null)
        {
            return NotFound();
        }

        return Ok(new
        {
            tenantId = tenant.Id,
            slug = tenant.Slug,
            status = tenant.Status.ToString(),
            isActive = tenant.Status == TenantStatus.Active
        });
    }

    private bool IsInternalRequestAuthorized()
    {
        var expected = _configuration["AuthService:InternalApiKey"];
        if (string.IsNullOrWhiteSpace(expected))
        {
            return false;
        }

        if (!Request.Headers.TryGetValue(InternalApiKeyHeader, out var providedValues))
        {
            return false;
        }

        var provided = providedValues.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(provided))
        {
            return false;
        }

        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var providedBytes = Encoding.UTF8.GetBytes(provided);
        return expectedBytes.Length == providedBytes.Length
               && CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes);
    }
}
