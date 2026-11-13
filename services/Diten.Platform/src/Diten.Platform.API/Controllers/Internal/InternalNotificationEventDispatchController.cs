using System.Security.Cryptography;
using System.Text;
using Diten.Platform.API.Controllers.Common;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Notifications.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers.Internal;

/// <summary>
/// WP-VW-W5 PLT-NOTIFY (K-N1) — the server-to-server door a service (CRM) and its scheduler use to start a catalogued
/// notification with no user behind the call. Lives under <c>/api/internal/*</c> (not routed by the Gateway, bypassed by
/// tenant resolution) and is gated by the shared internal API key, checked BEFORE anything is read: without the key the
/// answer is 401 whether or not the tenant or the event exists. The tenant is pinned from the body inside
/// <see cref="IInternalEventDispatchService"/>, which also refuses an event the asking module does not own (403) and an
/// unknown or inactive tenant (404). The answer carries dispatch ids, languages and statuses — never an address.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/internal/notifications/events")]
public sealed class InternalNotificationEventDispatchController : CustomBaseController
{
    private const string InternalApiKeyHeader = "X-Internal-Api-Key";
    private const string CorrelationIdHeader = "X-Correlation-Id";

    private readonly IInternalEventDispatchService _dispatch;
    private readonly IConfiguration _configuration;
    private readonly ILogger<InternalNotificationEventDispatchController> _logger;

    public InternalNotificationEventDispatchController(
        IInternalEventDispatchService dispatch,
        IConfiguration configuration,
        ILogger<InternalNotificationEventDispatchController> logger)
    {
        _dispatch = dispatch;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpPost("dispatch")]
    public async Task<IActionResult> Dispatch(
        [FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] InternalEventDispatchRequest? request,
        CancellationToken ct)
    {
        if (!IsInternalRequestAuthorized())
        {
            _logger.LogWarning(
                "Internal notification event dispatch rejected. CorrelationId={CorrelationId}",
                Request.Headers[CorrelationIdHeader].FirstOrDefault() ?? HttpContext.TraceIdentifier);
            return CreateActionResultInstance(Response<InternalEventDispatchResultDto>.Fail("Unauthorized.", 401));
        }

        if (request is null)
        {
            return CreateActionResultInstance(Response<InternalEventDispatchResultDto>.Fail(
                "A body is required.", 400, InternalEventDispatchService.ReasonBadRequest));
        }

        // The caller's correlation id travels with the mail when the body does not carry its own.
        var correlationId = string.IsNullOrWhiteSpace(request.CorrelationId)
            ? Request.Headers[CorrelationIdHeader].FirstOrDefault()
            : request.CorrelationId;

        var response = await _dispatch.DispatchAsync(request with { CorrelationId = correlationId }, ct);
        return CreateActionResultInstance(response);
    }

    private bool IsInternalRequestAuthorized()
    {
        // The same shared internal secret Platform's other internal endpoints use (InternalTenantEmailIdentityController).
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
