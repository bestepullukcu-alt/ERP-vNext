using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Features.Contact;
using Microsoft.Extensions.Logging;

namespace Diten.CrmService.Infrastructure.Audit;

/// <summary>
/// MOD-0021 audit seam for Contact actions (mirrors LoggingAccountAuditPublisher). CRM owns no audit store; this emits
/// a structured audit log entry. Wiring to the MOD-0021 audit append contract over the Gateway is FU06.
/// MOD-0150 PII/KVKK hardening: <c>detail</c> is redacted through <see cref="PiiMasking"/> as defence-in-depth — call
/// sites already pass entityId + non-PII context (never raw name/phone/e-mail), and this masks anything that slips.
/// </summary>
public sealed class LoggingContactAuditPublisher : IContactAuditPublisher
{
    private readonly ILogger<LoggingContactAuditPublisher> _logger;

    public LoggingContactAuditPublisher(ILogger<LoggingContactAuditPublisher> logger)
    {
        _logger = logger;
    }

    public Task PublishAsync(string eventName, Guid tenantId, Guid contactId, string? detail, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "CRM audit event {AuditEvent} tenant={TenantId} contact={ContactId} detail={Detail}",
            eventName, tenantId, contactId, PiiMasking.Redact(detail));
        return Task.CompletedTask;
    }
}
