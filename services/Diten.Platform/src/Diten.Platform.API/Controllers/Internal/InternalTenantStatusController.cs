using System.Security.Cryptography;
using System.Text;
using Diten.Platform.API.Controllers.Common;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Tenants;
using Diten.Platform.Application.Features.Tenants.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers.Internal;

// FIX-4 — per-request tenant liveness surface for the Web shell session guard. The Web app holds a valid JWT
// but cannot tell whether the tenant was deleted/suspended after the token was issued; it polls this S2S
// endpoint (short-cached) and signs the user out on a definitive missing/inactive answer. Gated by the shared
// internal API key like the other internal controllers; returns ONLY existence + active flag + status string.
[ApiController]
[AllowAnonymous]
[Route("api/internal/tenants")]
public sealed class InternalTenantStatusController : CustomBaseController
{
    private const string InternalApiKeyHeader = "X-Internal-Api-Key";
    private const string CorrelationIdHeader = "X-Correlation-Id";

    private readonly IMediator _mediator;
    private readonly IConfiguration _configuration;
    private readonly ILogger<InternalTenantStatusController> _logger;

    public InternalTenantStatusController(
        IMediator mediator,
        IConfiguration configuration,
        ILogger<InternalTenantStatusController> logger)
    {
        _mediator = mediator;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpGet("{tenantId:guid}/status")]
    public async Task<IActionResult> GetStatus(Guid tenantId, CancellationToken ct)
    {
        if (!IsInternalRequestAuthorized())
        {
            _logger.LogWarning(
                "Internal tenant status request rejected. TenantId={TenantId} CorrelationId={CorrelationId}",
                tenantId,
                Request.Headers[CorrelationIdHeader].FirstOrDefault() ?? HttpContext.TraceIdentifier);
            return CreateActionResultInstance(Response<TenantStatusDto>.Fail("Unauthorized.", 401));
        }

        var result = await _mediator.Send(new GetTenantStatusQuery(tenantId), ct);
        return CreateActionResultInstance(Response<TenantStatusDto>.Success(result));
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
