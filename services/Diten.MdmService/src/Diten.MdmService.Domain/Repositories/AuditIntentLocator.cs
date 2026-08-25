using Diten.MdmService.Domain.Enums;

namespace Diten.MdmService.Domain.Repositories;

public sealed record AuditIntentLocator(
    Guid TenantId,
    AuditAggregateType AggregateType,
    Guid AggregateId,
    Guid IntentId);
