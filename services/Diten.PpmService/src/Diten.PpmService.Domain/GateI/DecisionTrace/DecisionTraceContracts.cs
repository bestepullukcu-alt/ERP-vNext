using System.Buffers;
using System.Text.Json;

namespace Diten.PpmService.Domain.GateI.DecisionTrace;

public static class DecisionTraceContractNames
{
    public const string Version = "1.0";
    public const string InvestmentCaseContext = "ppm.investment-case-context";
    public const string GoverningDecisionReference = "ppm.investment-case-governing-decision-reference";
    public const string SupportingDecisionReference = "ppm.investment-case-supporting-decision-reference";
    public const string DecisionRevisionReference = "management-governance.decision-reference";
}

public enum DecisionTraceValidationMode { HistoricalResolve, NewReferenceEligibility, CurrentSelectionEligibility }

public sealed record InvestmentCaseContextV1
{
    public InvestmentCaseContextV1(Guid investmentCaseId) => InvestmentCaseId = DecisionTraceGuard.Id(investmentCaseId, nameof(investmentCaseId));
    public string ContractName => DecisionTraceContractNames.InvestmentCaseContext;
    public string ContractVersion => DecisionTraceContractNames.Version;
    public Guid InvestmentCaseId { get; }
}

public sealed record DecisionRevisionReferenceV1
{
    public DecisionRevisionReferenceV1(Guid decisionId, Guid decisionRevisionId, int decisionRevisionNumber)
    {
        DecisionId = DecisionTraceGuard.Id(decisionId, nameof(decisionId));
        DecisionRevisionId = DecisionTraceGuard.Id(decisionRevisionId, nameof(decisionRevisionId));
        DecisionRevisionNumber = decisionRevisionNumber > 0 ? decisionRevisionNumber : throw new DecisionTraceContractException("DecisionRevisionNumber must be a positive integer.");
    }
    public string ContractName => DecisionTraceContractNames.DecisionRevisionReference;
    public string ContractVersion => DecisionTraceContractNames.Version;
    public Guid DecisionId { get; }
    public Guid DecisionRevisionId { get; }
    public int DecisionRevisionNumber { get; }
}

public interface IDecisionTraceReferenceV1
{
    string ContractName { get; }
    string ContractVersion { get; }
    InvestmentCaseContextV1 InvestmentCaseContext { get; }
    DecisionRevisionReferenceV1 DecisionRevisionReference { get; }
}

public sealed record GoverningDecisionReferenceV1 : IDecisionTraceReferenceV1
{
    public GoverningDecisionReferenceV1(InvestmentCaseContextV1 investmentCaseContext, DecisionRevisionReferenceV1 decisionRevisionReference)
    { InvestmentCaseContext = investmentCaseContext ?? throw new DecisionTraceContractException("InvestmentCaseContext is required."); DecisionRevisionReference = decisionRevisionReference ?? throw new DecisionTraceContractException("DecisionRevisionReference is required."); }
    public string ContractName => DecisionTraceContractNames.GoverningDecisionReference;
    public string ContractVersion => DecisionTraceContractNames.Version;
    public InvestmentCaseContextV1 InvestmentCaseContext { get; }
    public DecisionRevisionReferenceV1 DecisionRevisionReference { get; }
}

public sealed record SupportingDecisionReferenceV1 : IDecisionTraceReferenceV1
{
    public SupportingDecisionReferenceV1(InvestmentCaseContextV1 investmentCaseContext, DecisionRevisionReferenceV1 decisionRevisionReference)
    { InvestmentCaseContext = investmentCaseContext ?? throw new DecisionTraceContractException("InvestmentCaseContext is required."); DecisionRevisionReference = decisionRevisionReference ?? throw new DecisionTraceContractException("DecisionRevisionReference is required."); }
    public string ContractName => DecisionTraceContractNames.SupportingDecisionReference;
    public string ContractVersion => DecisionTraceContractNames.Version;
    public InvestmentCaseContextV1 InvestmentCaseContext { get; }
    public DecisionRevisionReferenceV1 DecisionRevisionReference { get; }
}

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

public sealed class DecisionTraceContractException(string message, Exception? innerException = null) : InvalidOperationException(message, innerException);
internal static class DecisionTraceGuard { public static Guid Id(Guid value, string name) => value == Guid.Empty ? throw new DecisionTraceContractException($"{name} cannot be empty.") : value; }
