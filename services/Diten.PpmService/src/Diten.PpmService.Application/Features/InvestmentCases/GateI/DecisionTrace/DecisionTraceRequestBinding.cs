using System.Text;
using System.Text.Json;
using Diten.PpmService.Application.GateI;
using Diten.PpmService.Domain.Entities;
using Diten.PpmService.Domain.GateI.DecisionTrace;

namespace Diten.PpmService.Application.Features.InvestmentCases.GateI.DecisionTrace;


public static class DecisionTraceRequestBinding
{
    public static string Compute(DecisionTraceRequestBindingInput input, Guid tenantId, DecisionTraceValidationRequest request)
    {
        ArgumentNullException.ThrowIfNull(input); ArgumentNullException.ThrowIfNull(request); ArgumentNullException.ThrowIfNull(request.Reference);
        return S2SOutboundCanonicalRequestBinding.Compute(
            input.Method,
            input.Path,
            CanonicalBody(request),
            tenantId,
            DecisionTraceProducerProfile.Operation,
            [DecisionTraceProducerProfile.Permission]);
    }

    public static bool FixedTimeMatches(string? supplied, string expected) =>
        S2SOutboundCanonicalRequestBinding.FixedTimeMatches(supplied, expected);

    private static byte[] CanonicalBody(DecisionTraceValidationRequest request)
    {
        using var stream = new MemoryStream(); using var writer = new Utf8JsonWriter(stream);
        writer.WriteStartObject(); writer.WriteString("Mode", request.Mode.ToString()); writer.WritePropertyName("Reference"); writer.WriteRawValue(DecisionTraceReferenceCodec.Serialize(request.Reference)); writer.WriteEndObject(); writer.Flush();
        return stream.ToArray();
    }
}
