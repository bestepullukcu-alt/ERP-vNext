using System.Security.Cryptography;
using System.Text;
using Diten.Platform.API.Controllers.Common;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Quotas;
using Diten.Platform.Application.Features.Quotas.Commands;
using Diten.Platform.Application.Features.Quotas.Services;
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
    private readonly IQuotaService _quotaService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<InternalQuotasController> _logger;

    public InternalQuotasController(
        IMediator mediator,
        IQuotaService quotaService,
        IConfiguration configuration,
        ILogger<InternalQuotasController> logger)
    {
        _mediator = mediator;
        _quotaService = quotaService;
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

        // BL-459 — at the limit, say what the limit is: the caller (AuthService's user create) shows it to a person.
        // Read after the refused consume, same tenant + key; if the read fails the plain refusal goes out unchanged.
        if (response.StatusCode == StatusCodes.Status409Conflict
            && response.Errors.Contains(QuotaErrorCodes.LimitExceeded, StringComparer.Ordinal))
        {
            Response<QuotaStatusDto>? status = null;
            try
            {
                status = await _quotaService.GetStatusResponseAsync(normalized.TenantId, normalized.QuotaKey, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // The refusal must stay a refusal: a failed read never turns the 409 into a 500 the caller treats as "open".
                _logger.LogWarning(ex, "Quota status read after a limit refusal failed. TenantId={TenantId}", normalized.TenantId);
            }

            if (status is { IsSuccessful: true, Data: { } usage })
            {
                return Conflict(new QuotaLimitExceededEnvelope(
                    null,
                    response.StatusCode,
                    false,
                    response.Errors,
                    new QuotaLimitSnapshot(usage.QuotaKey, usage.LimitValue, usage.CurrentValue)));
            }
        }

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
