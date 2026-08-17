namespace Diten.BuildingBlocks.Eventing;

/// <summary>
/// Marker for broker-published integration/internal event payloads.
/// </summary>
public interface IIntegrationEvent
{
    string EventName { get; }

    int EventVersion { get; }
}
