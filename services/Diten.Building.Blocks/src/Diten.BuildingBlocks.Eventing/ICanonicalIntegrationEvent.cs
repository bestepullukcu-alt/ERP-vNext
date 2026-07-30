namespace Diten.BuildingBlocks.Eventing;

/// <summary>
/// An integration event whose producer owns the exact canonical UTF-8 payload.
/// The event bus persists and transports these bytes without reserialization.
/// </summary>
public interface ICanonicalIntegrationEvent : IIntegrationEvent
{
    ReadOnlyMemory<byte> CanonicalPayloadUtf8 { get; }
}
