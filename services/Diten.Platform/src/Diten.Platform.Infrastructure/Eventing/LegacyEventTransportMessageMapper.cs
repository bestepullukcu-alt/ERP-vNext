using System.Text;
using Diten.BuildingBlocks.Eventing;
using LegacyEventTransportMessage = Diten.Platform.Application.Contracts.Eventing.EventTransportMessage;

namespace Diten.Platform.Infrastructure.Eventing;

internal static class LegacyEventTransportMessageMapper
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public static EventTransportMessage Map(
        LegacyEventTransportMessage legacy,
        IEnumerable<KeyValuePair<string, string>>? brokerTransportHeaders = null)
    {
        ArgumentNullException.ThrowIfNull(legacy);
        var headers = legacy.TransportHeaders is { Count: > 0 }
            ? legacy.TransportHeaders
            : brokerTransportHeaders ?? [];

        return new EventTransportMessage(
            legacy.EventId,
            legacy.EventName,
            legacy.EventVersion,
            legacy.CorrelationId,
            legacy.CausationId,
            legacy.TenantId,
            legacy.Producer,
            legacy.OccurredAtUtc,
            StrictUtf8.GetBytes(legacy.PayloadJson),
            new TrustedTransportMetadata(headers));
    }
}
