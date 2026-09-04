using System.Buffers;
using System.Text.Json;

namespace Diten.PpmService.Domain.GateI.DecisionTrace;


public static class DecisionTraceReferenceCodec
{
    private static readonly string[] WrapperFields = ["ContractName", "ContractVersion", "InvestmentCaseContext", "DecisionRevisionReference"];
    private static readonly string[] ContextFields = ["ContractName", "ContractVersion", "InvestmentCaseId"];
    private static readonly string[] DecisionFields = ["ContractName", "ContractVersion", "DecisionId", "DecisionRevisionId", "DecisionRevisionNumber"];

    public static byte[] Serialize(IDecisionTraceReferenceV1 value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var output = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(output);
        writer.WriteStartObject();
        writer.WriteString(WrapperFields[0], value.ContractName); writer.WriteString(WrapperFields[1], value.ContractVersion);
        writer.WritePropertyName(WrapperFields[2]); writer.WriteStartObject();
        writer.WriteString(ContextFields[0], value.InvestmentCaseContext.ContractName); writer.WriteString(ContextFields[1], value.InvestmentCaseContext.ContractVersion); writer.WriteString(ContextFields[2], value.InvestmentCaseContext.InvestmentCaseId); writer.WriteEndObject();
        writer.WritePropertyName(WrapperFields[3]); writer.WriteStartObject();
        writer.WriteString(DecisionFields[0], value.DecisionRevisionReference.ContractName); writer.WriteString(DecisionFields[1], value.DecisionRevisionReference.ContractVersion); writer.WriteString(DecisionFields[2], value.DecisionRevisionReference.DecisionId); writer.WriteString(DecisionFields[3], value.DecisionRevisionReference.DecisionRevisionId); writer.WriteNumber(DecisionFields[4], value.DecisionRevisionReference.DecisionRevisionNumber); writer.WriteEndObject();
        writer.WriteEndObject(); writer.Flush();
        return output.WrittenSpan.ToArray();
    }

    public static IDecisionTraceReferenceV1 Parse(ReadOnlySpan<byte> utf8)
    {
        try
        {
            using var document = JsonDocument.Parse(utf8.ToArray(), new JsonDocumentOptions { AllowTrailingCommas = false, CommentHandling = JsonCommentHandling.Disallow });
            var root = document.RootElement; RequireExactObject(root, WrapperFields);
            var contractName = ExactString(root, WrapperFields[0]); Require(ExactString(root, WrapperFields[1]), DecisionTraceContractNames.Version, WrapperFields[1]);
            var contextElement = root.GetProperty(WrapperFields[2]); RequireExactObject(contextElement, ContextFields);
            Require(ExactString(contextElement, ContextFields[0]), DecisionTraceContractNames.InvestmentCaseContext, ContextFields[0]); Require(ExactString(contextElement, ContextFields[1]), DecisionTraceContractNames.Version, ContextFields[1]);
            var context = new InvestmentCaseContextV1(CanonicalGuid(contextElement, ContextFields[2]));
            var decisionElement = root.GetProperty(WrapperFields[3]); RequireExactObject(decisionElement, DecisionFields);
            Require(ExactString(decisionElement, DecisionFields[0]), DecisionTraceContractNames.DecisionRevisionReference, DecisionFields[0]); Require(ExactString(decisionElement, DecisionFields[1]), DecisionTraceContractNames.Version, DecisionFields[1]);
            var numberElement = decisionElement.GetProperty(DecisionFields[4]);
            if (numberElement.ValueKind != JsonValueKind.Number || !numberElement.TryGetInt32(out var number)) throw new DecisionTraceContractException("DecisionRevisionNumber must be a JSON integer.");
            var decision = new DecisionRevisionReferenceV1(CanonicalGuid(decisionElement, DecisionFields[2]), CanonicalGuid(decisionElement, DecisionFields[3]), number);
            return contractName switch
            {
                DecisionTraceContractNames.GoverningDecisionReference => new GoverningDecisionReferenceV1(context, decision),
                DecisionTraceContractNames.SupportingDecisionReference => new SupportingDecisionReferenceV1(context, decision),
                _ => throw new DecisionTraceContractException("Unsupported Decision Trace wrapper contract.")
            };
        }
        catch (DecisionTraceContractException) { throw; }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or FormatException) { throw new DecisionTraceContractException("Malformed Decision Trace contract.", exception); }
    }

    private static void RequireExactObject(JsonElement value, IReadOnlyList<string> expected)
    {
        if (value.ValueKind != JsonValueKind.Object) throw new DecisionTraceContractException("A contract object was required.");
        var actual = value.EnumerateObject().Select(property => property.Name).ToArray();
        if (actual.Length != expected.Count || actual.Distinct(StringComparer.Ordinal).Count() != actual.Length || !actual.SequenceEqual(expected, StringComparer.Ordinal)) throw new DecisionTraceContractException("Contract fields are missing, duplicated, extra, case-changed or out of order.");
    }
    private static string ExactString(JsonElement owner, string property) { var value = owner.GetProperty(property); return value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } text ? text : throw new DecisionTraceContractException($"{property} must be a non-empty JSON string."); }
    private static Guid CanonicalGuid(JsonElement owner, string property) { var text = ExactString(owner, property); return Guid.TryParseExact(text, "D", out var value) && value != Guid.Empty && string.Equals(value.ToString("D"), text, StringComparison.Ordinal) ? value : throw new DecisionTraceContractException($"{property} must be a canonical lowercase non-empty UUID."); }
    private static void Require(string actual, string expected, string property) { if (!string.Equals(actual, expected, StringComparison.Ordinal)) throw new DecisionTraceContractException($"Unsupported {property}."); }
}
