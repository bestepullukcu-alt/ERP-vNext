using System.Text.Json;
using Diten.CrmService.Application.Features.Territory;
using Microsoft.Extensions.Logging;

namespace Diten.CrmService.Infrastructure.Audit;

public sealed class LoggingTerritoryLifecycleAuditPublisher : ITerritoryLifecycleAuditPublisher
{
    private readonly ILogger<LoggingTerritoryLifecycleAuditPublisher> _logger;

    public LoggingTerritoryLifecycleAuditPublisher(ILogger<LoggingTerritoryLifecycleAuditPublisher> logger)
        => _logger = logger;

    public Task PublishAsync(string eventName, TerritoryLifecycleAuditPayload payload, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "CRM audit event {AuditEvent} payload={AuditPayload}",
            eventName,
            JsonSerializer.Serialize(payload));
        return Task.CompletedTask;
    }
}
