using Diten.MdmService.Domain.Enums;

namespace Diten.MdmService.Domain.Repositories;

public sealed record AuditIntentWorkItem(
    AuditIntentLocator Locator,
    AuditIntentDeliveryState DeliveryState,
    int AttemptCount,
    long ClaimGeneration,
    DateTimeOffset TimestampUtc,
    DateTimeOffset? NextRetryAt,
    DateTimeOffset? LeaseUntil,
    bool AggregateIsDeleted);
