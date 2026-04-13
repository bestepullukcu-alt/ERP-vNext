namespace Diten.Application.EnterpriseStrategy.Events;

public abstract class EnterpriseStrategyEvent
{
    public string EventId { get; init; } = Guid.NewGuid().ToString("N");
    public DateTime OccurredAtUtc { get; init; } = DateTime.UtcNow;
    public string CorrelationId { get; init; } = string.Empty;
}

public sealed class GoalMutatedEvent : EnterpriseStrategyEvent
{
    public string GoalId { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty;
}

public sealed class ObjectiveMutatedEvent : EnterpriseStrategyEvent
{
    public string ObjectiveId { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty;
}

public sealed class ConnectionMutatedEvent : EnterpriseStrategyEvent
{
    public string ConnectionId { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty;
}
