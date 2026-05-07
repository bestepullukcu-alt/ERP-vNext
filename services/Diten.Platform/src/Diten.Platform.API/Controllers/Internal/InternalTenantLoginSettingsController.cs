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

[ApiController]
[AllowAnonymous]
[Route("api/internal/tenants")]
public sealed class InternalTenantLoginSettingsController : CustomBaseController
{
    private const string InternalApiKeyHeader = "X-Internal-Api-Key";
    private const string CorrelationIdHeader = "X-Correlation-Id";

    private readonly IMediator _mediator;
    private readonly IConfiguration _configuration;
    private readonly ILogger<InternalTenantLoginSettingsController> _logger;

    public InternalTenantLoginSettingsController(
        IMediator mediator,
        IConfiguration configuration,
        ILogger<InternalTenantLoginSettingsController> logger)
    {
        _mediator = mediator;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpGet("{tenantId:guid}/login-settings")]
    public async Task<IActionResult> GetLoginSettings(Guid tenantId, CancellationToken ct)
    {
        if (!IsInternalRequestAuthorized())
        {
            _logger.LogWarning(
                "Internal tenant login settings request rejected. TenantId={TenantId} CorrelationId={CorrelationId}",
                tenantId,
                Request.Headers[CorrelationIdHeader].FirstOrDefault() ?? HttpContext.TraceIdentifier);
            return CreateActionResultInstance(Response<TenantLoginSettingsDto>.Fail("Unauthorized.", 401));
        }

        var result = await _mediator.Send(new GetTenantLoginSettingsQuery(tenantId), ct);
        if (result == null)
        {
            return CreateActionResultInstance(Response<TenantLoginSettingsDto>.Fail("Tenant login settings not found.", 404));
        }

        return CreateActionResultInstance(Response<TenantLoginSettingsDto>.Success(result));
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
