using Diten.CrmService.Application.Features.Account;
using Microsoft.Extensions.Logging;

namespace Diten.CrmService.Infrastructure.Audit;

/// <summary>
/// MOD-0021 audit seam for Account actions. CRM owns no audit store. This implementation emits a structured
/// audit log entry; wiring to the MOD-0021 audit append contract over the Gateway is a follow-up
/// (see MOD-0149 pack §23 / follow-ups). The seam keeps handlers decoupled from the transport.
/// </summary>
public sealed class LoggingAccountAuditPublisher : IAccountAuditPublisher
{
    private readonly ILogger<LoggingAccountAuditPublisher> _logger;

    public LoggingAccountAuditPublisher(ILogger<LoggingAccountAuditPublisher> logger)
    {
        _logger = logger;
    }

    public Task PublishAsync(string eventName, Guid tenantId, Guid accountId, string? detail, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "CRM audit event {AuditEvent} tenant={TenantId} account={AccountId} detail={Detail}",
            eventName, tenantId, accountId, detail);
        return Task.CompletedTask;
    }
}
