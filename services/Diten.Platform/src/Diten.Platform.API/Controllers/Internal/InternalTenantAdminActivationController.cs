using System.Security.Cryptography;
using System.Text;
using Diten.Platform.API.Controllers.Common;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Tenants.Commands;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers.Internal;

// FIX-TENANT-ADMIN-INVITE-ACTIVATION (Part B) — S2S callback from AuthService: an invited tenant admin has completed
// its forced first-login password change, so flip the matching TenantAdminUser Invited → Active. Gated by the shared
// internal API key like the other internal controllers. Idempotent + fail-safe (unknown tenant / no matching admin /
// already-Active → 204), so the best-effort caller never sees an error.
[ApiController]
[AllowAnonymous]
[Route("api/internal/tenants")]
public sealed class InternalTenantAdminActivationController : CustomBaseController
{
    private const string InternalApiKeyHeader = "X-Internal-Api-Key";
    private const string CorrelationIdHeader = "X-Correlation-Id";

    private readonly IMediator _mediator;
    private readonly IConfiguration _configuration;
    private readonly ILogger<InternalTenantAdminActivationController> _logger;

    public InternalTenantAdminActivationController(
        IMediator mediator,
        IConfiguration configuration,
        ILogger<InternalTenantAdminActivationController> logger)
    {
        _mediator = mediator;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpPost("admin-activated")]
    public async Task<IActionResult> AdminActivated([FromBody] AdminActivatedRequest request, CancellationToken ct)
    {
        if (!IsInternalRequestAuthorized())
        {
            _logger.LogWarning(
                "Internal tenant admin activation request rejected. CorrelationId={CorrelationId}",
                Request.Headers[CorrelationIdHeader].FirstOrDefault() ?? HttpContext.TraceIdentifier);
            return CreateActionResultInstance(Response<NoContent>.Fail("Unauthorized.", 401));
        }

        if (request is null)
        {
            return CreateActionResultInstance(Response<NoContent>.Fail("email and tenantId are required.", 400));
        }

        var result = await _mediator.Send(new ActivateTenantAdminUserCommand(request.TenantId, request.Email), ct);
        return CreateActionResultInstance(result);
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

    public sealed record AdminActivatedRequest(Guid TenantId, string Email);
}
