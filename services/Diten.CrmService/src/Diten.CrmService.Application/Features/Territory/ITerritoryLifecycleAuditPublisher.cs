namespace Diten.CrmService.Application.Features.Territory;

public sealed record TerritoryLifecycleAuditPayload(
    Guid TenantId,
    Guid ModelId,
    Guid? NodeId,
    string PreviousStatus,
    string NewStatus,
    string? ComputedStatus,
    string Actor,
    string? Reason,
    string? CorrelationId,
    DateTimeOffset Timestamp);

public interface ITerritoryLifecycleAuditPublisher
{
    Task PublishAsync(string eventName, TerritoryLifecycleAuditPayload payload, CancellationToken cancellationToken);
}

public static class TerritoryLifecycleAuditEvents
{
    public const string ModelActivated = "territory.model.activated";
    public const string ModelDeactivated = "territory.model.deactivated";
    public const string ModelArchived = "territory.model.archived";
    public const string ModelSoftDeleted = "territory.model.soft_deleted";
    public const string NodeSoftDeleted = "territory.node.soft_deleted";
    public const string ModelActivationRejected = "territory.model.activation_rejected";
    public const string ModelDeleteRejected = "territory.model.delete_rejected";
    public const string NodeDeleteRejected = "territory.node.delete_rejected";
}
