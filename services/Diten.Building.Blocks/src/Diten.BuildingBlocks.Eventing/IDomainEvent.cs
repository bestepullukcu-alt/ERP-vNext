namespace Diten.BuildingBlocks.Eventing;

/// <summary>
/// Optional envelope-scoped marker for platform eventing only.
/// Do not use this to model aggregate-local domain events.
/// </summary>
public interface IDomainEvent : IInternalEvent;
