namespace Diten.BuildingBlocks.Eventing;

/// <summary>
/// Marker for internal cross-service events that are not part of a public integration surface.
/// </summary>
public interface IInternalEvent : IIntegrationEvent;
