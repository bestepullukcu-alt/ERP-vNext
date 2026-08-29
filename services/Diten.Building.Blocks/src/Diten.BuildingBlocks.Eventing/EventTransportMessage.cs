using System.Text;
using System.Text.Json.Serialization;

namespace Diten.BuildingBlocks.Eventing;

public sealed class EventTransportMessage
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private readonly byte[] _canonicalPayloadUtf8;

    public EventTransportMessage(
        Guid eventId,
        string eventName,
        int eventVersion,
        Guid correlationId,
        Guid? causationId,
        Guid? tenantId,
        string producer,
        DateTimeOffset occurredAtUtc,
        string payloadJson,
        TrustedTransportMetadata? transportMetadata = null)
        : this(
            eventId,
            eventName,
            eventVersion,
            correlationId,
            causationId,
            tenantId,
            producer,
            occurredAtUtc,
            StrictUtf8.GetBytes(payloadJson ?? throw new ArgumentNullException(nameof(payloadJson))),
            transportMetadata)
    {
    }

    [JsonConstructor]
    public EventTransportMessage(
        Guid eventId,
        string eventName,
        int eventVersion,
        Guid correlationId,
        Guid? causationId,
        Guid? tenantId,
        string producer,
        DateTimeOffset occurredAtUtc,
        ReadOnlyMemory<byte> canonicalPayloadUtf8,
        TrustedTransportMetadata? transportMetadata = null)
    {
        if (eventId == Guid.Empty || correlationId == Guid.Empty)
        {
            throw new EventValidationException("EventId and CorrelationId must be non-empty.");
        }

        if (string.IsNullOrWhiteSpace(eventName)
            || eventVersion < 1
            || !eventName.EndsWith($".v{eventVersion}", StringComparison.Ordinal))
        {
            throw new EventValidationException("EventName and EventVersion are inconsistent.");
        }
        if (string.IsNullOrWhiteSpace(producer))
        {
            throw new EventValidationException("Producer is required.");
        }

        if (occurredAtUtc.Offset != TimeSpan.Zero)
        {
            throw new EventValidationException("OccurredAtUtc must use the UTC offset.");
        }

        if (canonicalPayloadUtf8.IsEmpty)
        {
            throw new EventValidationException("Canonical payload must not be empty.");
        }

        // Validate before taking the immutable snapshot.
        _ = StrictUtf8.GetString(canonicalPayloadUtf8.Span);
        _canonicalPayloadUtf8 = canonicalPayloadUtf8.ToArray();

        EventId = eventId;
        EventName = eventName;
        EventVersion = eventVersion;
        CorrelationId = correlationId;
        CausationId = causationId;
        TenantId = tenantId;
        Producer = producer;
        OccurredAtUtc = occurredAtUtc;
        TransportMetadata = transportMetadata ?? TrustedTransportMetadata.Empty;
    }

    public Guid EventId { get; }
    public string EventName { get; }
    public int EventVersion { get; }
    public Guid CorrelationId { get; }
    public Guid? CausationId { get; }
    public Guid? TenantId { get; }
    public string Producer { get; }
    public DateTimeOffset OccurredAtUtc { get; }
    public ReadOnlyMemory<byte> CanonicalPayloadUtf8 => _canonicalPayloadUtf8;
    [JsonIgnore]
    public string PayloadJson => StrictUtf8.GetString(_canonicalPayloadUtf8);
    public TrustedTransportMetadata TransportMetadata { get; }
}
