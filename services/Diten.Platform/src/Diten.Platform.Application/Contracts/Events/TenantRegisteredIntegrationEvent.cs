namespace Diten.Platform.Application.Contracts.Events;

/// <summary>
/// Raised after a new tenant has been successfully registered and persisted.
/// Consumers: provisioning pipeline, audit trail, analytics.
/// </summary>
public sealed record TenantRegisteredIntegrationEvent(
    Guid EventId,
    Guid TenantId,
    string TenantCode,
    string Slug,
    string Name,
    string Domain,
    string TenantType,
    DateTimeOffset OccurredAt,
    string Actor);
