using System.Buffers;
using System.Text;
using System.Text.Json;

namespace Diten.PpmService.Domain.GateI.BenefitRealization;


public static class BenefitCommitmentOutcomeReferenceV1Codec
{
    private static readonly string[] WrapperFields =
        ["ContractName", "ContractVersion", "BenefitCommitmentId", "OutcomeReference"];
    private static readonly string[] OutcomeFields =
        ["contractName", "contractVersion", "outcomeId", "outcomeVersionId", "outcomeVersionNumber"];

    public static byte[] Serialize(BenefitCommitmentOutcomeReferenceV1 value)
    {
        ArgumentNullException.ThrowIfNull(value);
        value.ValidateIdentity();
        var buffer = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = false });
        writer.WriteStartObject();
        writer.WriteString("ContractName", value.ContractName);
        writer.WriteString("ContractVersion", value.ContractVersion);
        writer.WriteString("BenefitCommitmentId", value.BenefitCommitmentId);
        writer.WritePropertyName("OutcomeReference");
        writer.WriteStartObject();
        writer.WriteString("contractName", value.OutcomeReference.ContractName);
        writer.WriteString("contractVersion", value.OutcomeReference.ContractVersion);
        writer.WriteString("outcomeId", value.OutcomeReference.OutcomeId);
        writer.WriteString("outcomeVersionId", value.OutcomeReference.OutcomeVersionId);
        writer.WriteNumber("outcomeVersionNumber", value.OutcomeReference.OutcomeVersionNumber);
        writer.WriteEndObject();
        writer.WriteEndObject();
        writer.Flush();
        return buffer.WrittenSpan.ToArray();
    }

    public static BenefitCommitmentOutcomeReferenceV1 ParseStrict(ReadOnlySpan<byte> utf8)
    {
        try
        {
            using var document = JsonDocument.Parse(utf8.ToArray(), new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow
            });
            var root = document.RootElement;
            RequireExactFields(root, WrapperFields);
            var nested = root.GetProperty("OutcomeReference");
            RequireExactFields(nested, OutcomeFields);

            var wrapperVersion = RequiredString(root, "ContractVersion");
            var outcomeVersion = RequiredString(nested, "contractVersion");
            if (!string.Equals(wrapperVersion, BenefitCommitmentOutcomeReferenceV1.ExactContractVersion, StringComparison.Ordinal) ||
                !string.Equals(outcomeVersion, OutcomeReferenceV1.ExactContractVersion, StringComparison.Ordinal))
            {
                throw new OutcomeReferenceContractException(OutcomeReferenceContractError.UnsupportedVersion);
            }

            var value = new BenefitCommitmentOutcomeReferenceV1(
                RequiredString(root, "ContractName"),
                wrapperVersion,
                RequiredGuid(root, "BenefitCommitmentId"),
                new OutcomeReferenceV1(
                    RequiredString(nested, "contractName"),
                    outcomeVersion,
                    RequiredGuid(nested, "outcomeId"),
                    RequiredGuid(nested, "outcomeVersionId"),
                    RequiredPositiveInt(nested, "outcomeVersionNumber")));
            value.ValidateIdentity();
            return value;
        }
        catch (OutcomeReferenceContractException)
        {
            throw;
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or FormatException)
        {
            throw new OutcomeReferenceContractException(OutcomeReferenceContractError.Malformed);
        }
    }

    private static void RequireExactFields(JsonElement element, IReadOnlyList<string> expected)
    {
        if (element.ValueKind != JsonValueKind.Object)
            throw new OutcomeReferenceContractException(OutcomeReferenceContractError.Malformed);

        var actual = element.EnumerateObject().Select(property => property.Name).ToArray();
        if (actual.Length != expected.Count || actual.Distinct(StringComparer.Ordinal).Count() != actual.Length ||
            !actual.SequenceEqual(expected, StringComparer.Ordinal))
        {
            throw new OutcomeReferenceContractException(OutcomeReferenceContractError.Malformed);
        }
    }

    private static string RequiredString(JsonElement element, string name)
    {
        var value = element.GetProperty(name);
        return value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } text
            ? text
            : throw new OutcomeReferenceContractException(OutcomeReferenceContractError.Malformed);
    }

    private static Guid RequiredGuid(JsonElement element, string name) =>
        Guid.TryParseExact(RequiredString(element, name), "D", out var value) && value != Guid.Empty
            ? value
            : throw new OutcomeReferenceContractException(OutcomeReferenceContractError.Malformed);

    private static int RequiredPositiveInt(JsonElement element, string name) =>
        element.GetProperty(name).TryGetInt32(out var value) && value > 0
            ? value
            : throw new OutcomeReferenceContractException(OutcomeReferenceContractError.Malformed);
}
