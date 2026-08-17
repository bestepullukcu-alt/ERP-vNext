using System.Security.Cryptography;
using System.Text;
using Diten.Platform.API.Controllers.Common;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Quotas;
using Diten.Platform.Application.Features.Quotas.Commands;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers.Internal;

[ApiController]
[AllowAnonymous]
[Route("api/internal/quotas")]
public sealed class InternalQuotasController : CustomBaseController
{
    private const string InternalApiKeyHeader = "X-Internal-Api-Key";
    private const string CorrelationIdHeader = "X-Correlation-Id";
    private readonly IMediator _mediator;
    private readonly IConfiguration _configuration;
    private readonly ILogger<InternalQuotasController> _logger;

    public InternalQuotasController(
        IMediator mediator,
        IConfiguration configuration,
        ILogger<InternalQuotasController> logger)
    {
        _mediator = mediator;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpPost("consume")]
    public async Task<IActionResult> Consume([FromBody] TryConsumeQuotaRequest request, CancellationToken ct)
    {
        if (!IsInternalRequestAuthorized(request.TenantId, request.Source))
        {
            return CreateActionResultInstance(Response<QuotaMutationDto>.Fail("Unauthorized.", 401));
        }

        var normalized = request with
        {
            CorrelationId = EnsureCorrelationId(request.CorrelationId),
            ActorId = string.IsNullOrWhiteSpace(request.ActorId) ? "InternalService" : request.ActorId
        };
        var response = await _mediator.Send(new TryConsumeQuotaCommand(normalized), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost("release")]
    public async Task<IActionResult> Release([FromBody] ReleaseQuotaRequest request, CancellationToken ct)
    {
        if (!IsInternalRequestAuthorized(request.TenantId, request.Source))
        {
            return CreateActionResultInstance(Response<QuotaMutationDto>.Fail("Unauthorized.", 401));
        }

        var normalized = request with
        {
            CorrelationId = EnsureCorrelationId(request.CorrelationId),
            ActorId = string.IsNullOrWhiteSpace(request.ActorId) ? "InternalService" : request.ActorId
        };
        var response = await _mediator.Send(new ReleaseQuotaCommand(normalized), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost("reset-period")]
    public async Task<IActionResult> ResetPeriod([FromBody] ResetQuotaPeriodRequest request, CancellationToken ct)
    {
        if (!IsInternalRequestAuthorized(request.TenantId, request.Source))
        {
            return CreateActionResultInstance(Response<QuotaStatusDto>.Fail("Unauthorized.", 401));
        }

        var normalized = request with
        {
            CorrelationId = EnsureCorrelationId(request.CorrelationId),
            ActorId = string.IsNullOrWhiteSpace(request.ActorId) ? "InternalService" : request.ActorId
        };
        var response = await _mediator.Send(new ResetQuotaPeriodCommand(normalized), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost("recalculate")]
    public async Task<IActionResult> Recalculate([FromBody] RecalculateQuotaUsageRequest request, CancellationToken ct)
    {
        if (!IsInternalRequestAuthorized(request.TenantId, request.Source))
        {
            return CreateActionResultInstance(Response<QuotaStatusDto>.Fail("Unauthorized.", 401));
        }

        var normalized = request with
        {
            CorrelationId = EnsureCorrelationId(request.CorrelationId),
            ActorId = string.IsNullOrWhiteSpace(request.ActorId) ? "InternalService" : request.ActorId
        };
        var response = await _mediator.Send(new RecalculateQuotaUsageCommand(normalized), ct);
        return CreateActionResultInstance(response);
    }

    private bool IsInternalRequestAuthorized(Guid tenantId, string source)
    {
        if (tenantId == Guid.Empty || string.IsNullOrWhiteSpace(source))
        {
            _logger.LogWarning(
                "Internal quota request rejected. TenantId={TenantId} SourcePresent={SourcePresent} CorrelationId={CorrelationId}",
                tenantId,
                !string.IsNullOrWhiteSpace(source),
                Request.Headers[CorrelationIdHeader].FirstOrDefault() ?? HttpContext.TraceIdentifier);
            return false;
        }

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

    private string EnsureCorrelationId(string? correlationId) =>
        string.IsNullOrWhiteSpace(correlationId)
            ? Request.Headers[CorrelationIdHeader].FirstOrDefault() ?? HttpContext.TraceIdentifier
            : correlationId;
}
