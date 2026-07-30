using System.Security.Cryptography;
using System.Text;
using Diten.Platform.Common.Authorization;
using Diten.Platform.Contracts.Entitlements;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers.Internal;

[ApiController]
[AllowAnonymous]
[Route("api/internal/ppm/tenants")]
public sealed class InternalPpmEntitlementDecisionController : ControllerBase
{
    public const string ServiceCredentialHeader = "X-PPM-Service-Key";
    public const string CorrelationIdHeader = "X-Correlation-Id";
    private const string EnabledConfigurationKey = "PpmEntitlementDecision:Enabled";
    private const string ServiceCredentialConfigurationKey = "PpmEntitlementDecision:ServiceCredential";

    private readonly IEntitlementChecker _entitlementChecker;
    private readonly IConfiguration _configuration;
    private readonly ILogger<InternalPpmEntitlementDecisionController> _logger;

    public InternalPpmEntitlementDecisionController(
        IEntitlementChecker entitlementChecker,
        IConfiguration configuration,
        ILogger<InternalPpmEntitlementDecisionController> logger)
    {
        _entitlementChecker = entitlementChecker;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpGet("{tenantId:guid}/entitlement-decision")]
    [ProducesResponseType<PpmEntitlementDecisionV1>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetDecision(Guid tenantId, CancellationToken cancellationToken)
    {
        var correlationId = Request.Headers[CorrelationIdHeader].FirstOrDefault()
                            ?? HttpContext.TraceIdentifier;
        Response.Headers[CorrelationIdHeader] = correlationId;

        if (!_configuration.GetValue<bool>(EnabledConfigurationKey))
        {
            _logger.LogWarning(
                "ppm.entitlement.decision.disabled TenantId={TenantId} CorrelationId={CorrelationId}",
                tenantId,
                correlationId);
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        if (!IsCallerAuthorized())
        {
            _logger.LogWarning(
                "ppm.entitlement.decision.unauthorized TenantId={TenantId} CorrelationId={CorrelationId}",
                tenantId,
                correlationId);
            return Unauthorized();
        }

        if (tenantId == Guid.Empty)
        {
            _logger.LogWarning(
                "ppm.entitlement.decision.invalid_tenant CorrelationId={CorrelationId}",
                correlationId);
            return BadRequest();
        }

        EntitlementCheckResult decision;
        try
        {
            decision = await _entitlementChecker.IsModuleEntitledAsync(
                tenantId,
                PpmEntitlementDecisionContractV1.ModuleCode,
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(
                exception,
                "ppm.entitlement.decision.dependency_failure TenantId={TenantId} CorrelationId={CorrelationId}",
                tenantId,
                correlationId);
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        if (!decision.IsCacheable)
        {
            _logger.LogError(
                "ppm.entitlement.decision.indeterminate TenantId={TenantId} CorrelationId={CorrelationId}",
                tenantId,
                correlationId);
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        var response = new PpmEntitlementDecisionV1(
            tenantId,
            PpmEntitlementDecisionContractV1.ModuleCode,
            decision.IsAllowed,
            decision.ResolvedAtUtc.ToUniversalTime(),
            decision.ExpiresAtUtc?.ToUniversalTime());

        _logger.LogInformation(
            "ppm.entitlement.decision.resolved TenantId={TenantId} IsAllowed={IsAllowed} CorrelationId={CorrelationId}",
            tenantId,
            response.IsAllowed,
            correlationId);
        return Ok(response);
    }

    [HttpGet("{tenantId}/entitlement-decision", Order = 100)]
    [ApiExplorerSettings(IgnoreApi = true)]
    public IActionResult RejectMalformedTenant(string tenantId)
    {
        if (!_configuration.GetValue<bool>(EnabledConfigurationKey))
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        return IsCallerAuthorized() ? BadRequest() : Unauthorized();
    }

    private bool IsCallerAuthorized()
    {
        var expected = _configuration[ServiceCredentialConfigurationKey];
        if (string.IsNullOrWhiteSpace(expected)
            || !Request.Headers.TryGetValue(ServiceCredentialHeader, out var providedValues))
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
