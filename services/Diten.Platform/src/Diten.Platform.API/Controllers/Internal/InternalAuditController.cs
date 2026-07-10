using System.Security.Cryptography;
using System.Text;
using Diten.Platform.API.Controllers.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Common.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers.Internal;

/// <summary>
/// S2S sink for audit events raised by other services (e.g. MDM) into Platform's central audit store. Lives under
/// <c>/api/internal/*</c> (bypassed by TenantResolutionMiddleware, so a keyed call with no platform JWT is not 403'd)
/// and is X-Internal-Api-Key gated exactly like <see cref="InternalModuleRegistrationController"/> /
/// <see cref="InternalQuotasController"/>. Because the bypass leaves the tenant context unresolved, this controller
/// pins the tenant from the request body (TargetTenantId, or platform-global) so <see cref="IAuditService"/> can own
/// the event.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/internal/audit")]
public sealed class InternalAuditController : CustomBaseController
{
    private const string InternalApiKeyHeader = "X-Internal-Api-Key";

    private readonly IAuditService _auditService;
    private readonly IConfiguration _configuration;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<InternalAuditController> _logger;

    public InternalAuditController(
        IAuditService auditService,
        IConfiguration configuration,
        ITenantContext tenantContext,
        ILogger<InternalAuditController> logger)
    {
        _auditService = auditService;
        _configuration = configuration;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    [HttpPost("append")]
    public async Task<IActionResult> Append([FromBody] AuditAppendRequest request, CancellationToken ct)
    {
        if (!IsInternalRequestAuthorized())
        {
            return Unauthorized();
        }

        if (request is null || string.IsNullOrWhiteSpace(request.SourceService))
        {
            return BadRequest(new { error = "SourceService is required for cross-service audit append." });
        }

        // /api/internal bypasses tenant resolution, so the ambient context is unresolved here. Pin it from the
        // self-declared body: platform-global events use the platform-system tenant; tenant-scoped events require an
        // explicit TargetTenantId (the acting user's tenant, forwarded by the caller).
        if (request.IsPlatformGlobal)
        {
            _tenantContext.SetPlatformContext(Guid.Empty);
        }
        else if (request.TargetTenantId is { } targetTenantId && targetTenantId != Guid.Empty)
        {
            _tenantContext.SetTenant(targetTenantId);
        }
        else
        {
            return BadRequest(new { error = "TargetTenantId is required for a tenant-scoped audit append." });
        }

        var result = await _auditService.AppendAsync(request, ct);

        _logger.LogInformation(
            "Internal audit append. SourceService={SourceService} EntityType={EntityType} Operation={Operation} Status={Status}",
            request.SourceService,
            request.EntityType,
            request.Operation,
            result.Status);

        return result.Status switch
        {
            AuditAppendStatus.Queued or AuditAppendStatus.Duplicate =>
                Accepted(new { status = result.Status.ToString(), idempotencyKey = result.IdempotencyKey }),
            AuditAppendStatus.Rejected =>
                BadRequest(new { status = result.Status.ToString(), diagnostic = result.Diagnostic }),
            _ =>
                StatusCode(StatusCodes.Status502BadGateway, new { status = result.Status.ToString(), diagnostic = result.Diagnostic })
        };
    }

    private bool IsInternalRequestAuthorized()
    {
        // Same shared internal secret Platform already uses for its other internal S2S endpoints (see InternalQuotasController).
        var expected = _configuration["AuthService:InternalApiKey"];
        if (string.IsNullOrWhiteSpace(expected) || !Request.Headers.TryGetValue(InternalApiKeyHeader, out var providedValues))
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
