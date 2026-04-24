using Diten.AuthService.Application.Common.Events;
using Diten.AuthService.Application.Common.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Diten.AuthService.Api.Controllers;

[ApiController]
[Route("internal/events")]
public sealed class InternalEventsController : ControllerBase
{
    private const string InternalApiKeyHeader = "X-Internal-Api-Key";
    private const string TenantActivatedEventName = "tenant.activated";

    private readonly IInternalEventAuthService _internalEventAuthService;
    private readonly IRoleProvisioningService _roleProvisioningService;
    private readonly IIntegrationEventInboxRepository _inboxRepository;
    private readonly ILogger<InternalEventsController> _logger;

    public InternalEventsController(
        IInternalEventAuthService internalEventAuthService,
        IRoleProvisioningService roleProvisioningService,
        IIntegrationEventInboxRepository inboxRepository,
        ILogger<InternalEventsController> logger)
    {
        _internalEventAuthService = internalEventAuthService;
        _roleProvisioningService = roleProvisioningService;
        _inboxRepository = inboxRepository;
        _logger = logger;
    }

    [HttpPost("tenant-activated")]
    public async Task<IActionResult> TenantActivated([FromBody] TenantActivatedIntegrationEvent integrationEvent, CancellationToken ct)
    {
        if (!_internalEventAuthService.IsAuthorized(Request.Headers[InternalApiKeyHeader].FirstOrDefault()))
        {
            return Unauthorized(new { message = "internal authentication failed" });
        }

        if (!string.Equals(integrationEvent.EventName, TenantActivatedEventName, StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "event_name must be tenant.activated" });
        }

        var inserted = await _inboxRepository.TryInsertAsync(
            integrationEvent.EventId,
            integrationEvent.EventName,
            integrationEvent.TenantId,
            ct);

        if (!inserted)
        {
            _logger.LogInformation(
                "Duplicate internal event ignored. EventId={EventId} TenantId={TenantId} EventName={EventName}",
                integrationEvent.EventId,
                integrationEvent.TenantId,
                integrationEvent.EventName);
            return Ok(new { status = "noop_duplicate" });
        }

        await _roleProvisioningService.EnsureDefaultRolesAsync(integrationEvent.TenantId, ct);

        return Ok(new { status = "processed" });
    }
}
