using System.Security.Cryptography;
using System.Text;
using Diten.Platform.API.Controllers.Common;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Notifications.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers.Internal;

// BL-454 — server-to-server lookup AuthService uses to send its own e-mails (the user invitation) under the
// tenant's name, in the tenant's language, with the tenant's reply address. Gated by the shared internal API key and
// checked BEFORE any lookup, so without the key the answer is 401 whether or not the tenant exists. It returns four
// presentation values only (display name, language, sender name, reply address) — no sender address, no SMTP
// setting, no credential reference. A deliberately separate endpoint from "branding", whose contract is the login
// page's logo and favicon.
[ApiController]
[AllowAnonymous]
[Route("api/internal/tenants")]
public sealed class InternalTenantEmailIdentityController : CustomBaseController
{
    private const string InternalApiKeyHeader = "X-Internal-Api-Key";
    private const string CorrelationIdHeader = "X-Correlation-Id";

    private readonly IMediator _mediator;
    private readonly IConfiguration _configuration;
    private readonly ILogger<InternalTenantEmailIdentityController> _logger;

    public InternalTenantEmailIdentityController(
        IMediator mediator,
        IConfiguration configuration,
        ILogger<InternalTenantEmailIdentityController> logger)
    {
        _mediator = mediator;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpGet("{tenantId:guid}/email-identity")]
    public async Task<IActionResult> GetEmailIdentity(Guid tenantId, CancellationToken ct)
    {
        if (!IsInternalRequestAuthorized())
        {
            _logger.LogWarning(
                "Internal tenant email identity request rejected. TenantId={TenantId} CorrelationId={CorrelationId}",
                tenantId,
                Request.Headers[CorrelationIdHeader].FirstOrDefault() ?? HttpContext.TraceIdentifier);
            return CreateActionResultInstance(Response<TenantEmailIdentityDto>.Fail("Unauthorized.", 401));
        }

        var result = await _mediator.Send(new GetTenantEmailIdentityQuery(tenantId), ct);
        return result is null
            // The message names nothing: not the id that was asked for, not why there is no answer.
            ? CreateActionResultInstance(Response<TenantEmailIdentityDto>.Fail("Not found.", 404))
            : CreateActionResultInstance(Response<TenantEmailIdentityDto>.Success(result));
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
