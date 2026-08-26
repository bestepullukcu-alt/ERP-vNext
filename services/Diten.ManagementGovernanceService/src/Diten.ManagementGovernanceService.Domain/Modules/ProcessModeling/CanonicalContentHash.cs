using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Diten.ManagementGovernanceService.Domain.Modules.ProcessModeling;

public sealed record CanonicalProcessContent(
    string Title,
    string? Description,
    IReadOnlyCollection<ProcessActivity> Activities,
    IReadOnlyCollection<ProcessControlPoint> ControlPoints,
    IReadOnlyCollection<ProcessRelationship> Relationships);

public static class CanonicalContentHash
{
    public const string ContractName = "management-governance.process-modeling.content-hash";
    public const string ContractVersion = "1.0";

    public static byte[] Write(CanonicalProcessContent content)
    {
        ValidateText(content.Title); ValidateText(content.Description);
        var activities = content.Activities.OrderBy(x => x.SortOrder).ThenBy(x => x.ActivityCode, Utf8OrdinalComparer.Instance).ThenBy(x => x.LogicalActivityId.ToString("D"), StringComparer.Ordinal).ToArray();
        var controls = content.ControlPoints.OrderBy(x => x.SortOrder).ThenBy(x => x.ControlCode, Utf8OrdinalComparer.Instance).ThenBy(x => x.LogicalControlPointId.ToString("D"), StringComparer.Ordinal).ToArray();
        var relationships = content.Relationships.OrderBy(x => x.SortOrder).ThenBy(x => x.FromActivityId.ToString("D"), StringComparer.Ordinal).ThenBy(x => x.ToActivityId.ToString("D"), StringComparer.Ordinal).ThenBy(x => x.ConditionLabel, Utf8OrdinalComparer.Instance).ToArray();
        RejectDuplicateSortKeys(activities.Select(x => $"{x.SortOrder}|{x.ActivityCode}|{x.LogicalActivityId:D}"));
        RejectDuplicateSortKeys(controls.Select(x => $"{x.SortOrder}|{x.ControlCode}|{x.LogicalControlPointId:D}"));
        RejectDuplicateSortKeys(relationships.Select(x => $"{x.SortOrder}|{x.FromActivityId:D}|{x.ToActivityId:D}|{(x.ConditionLabel is null ? "0:" : "1:" + x.ConditionLabel)}"));
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            writer.WriteStartObject();
            writer.WriteString("contractName", ContractName); writer.WriteString("contractVersion", ContractVersion);
            writer.WriteString("title", content.Title); WriteNullable(writer, "description", content.Description);
            writer.WritePropertyName("activities"); writer.WriteStartArray();
            foreach (var item in activities)
            {
                ValidateId(item.LogicalActivityId); ValidateText(item.ActivityCode); ValidateText(item.Name); ValidateText(item.Description); ValidateNumber(item.SortOrder);
                writer.WriteStartObject(); writer.WriteString("logicalActivityId", item.LogicalActivityId.ToString("D")); writer.WriteString("activityCode", item.ActivityCode); writer.WriteString("name", item.Name); WriteNullable(writer, "description", item.Description); writer.WriteNumber("sortOrder", item.SortOrder); writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WritePropertyName("controlPoints"); writer.WriteStartArray();
            foreach (var item in controls)
            {
                ValidateId(item.LogicalControlPointId); ValidateText(item.ControlCode); ValidateText(item.Name); ValidateText(item.Description); ValidateNumber(item.SortOrder); if (item.LogicalActivityId.HasValue) ValidateId(item.LogicalActivityId.Value);
                writer.WriteStartObject(); writer.WriteString("logicalControlPointId", item.LogicalControlPointId.ToString("D")); writer.WriteString("controlCode", item.ControlCode); writer.WriteString("name", item.Name); WriteNullable(writer, "description", item.Description);
                if (item.LogicalActivityId.HasValue) writer.WriteString("logicalActivityId", item.LogicalActivityId.Value.ToString("D")); else writer.WriteNull("logicalActivityId");
                writer.WriteNumber("sortOrder", item.SortOrder); writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WritePropertyName("relationships"); writer.WriteStartArray();
            foreach (var item in relationships)
            {
                ValidateId(item.FromActivityId); ValidateId(item.ToActivityId); ValidateText(item.ConditionLabel); ValidateNumber(item.SortOrder);
                writer.WriteStartObject(); writer.WriteString("fromLogicalActivityId", item.FromActivityId.ToString("D")); writer.WriteString("toLogicalActivityId", item.ToActivityId.ToString("D")); WriteNullable(writer, "conditionLabel", item.ConditionLabel); writer.WriteNumber("sortOrder", item.SortOrder); writer.WriteEndObject();
            }
            writer.WriteEndArray(); writer.WriteEndObject(); writer.Flush();
        }
        return stream.ToArray();
    }

    public static string Compute(CanonicalProcessContent content) => "sha256:" + Convert.ToHexString(SHA256.HashData(Write(content))).ToLowerInvariant();

    private static void WriteNullable(Utf8JsonWriter writer, string name, string? value) { if (value is null) writer.WriteNull(name); else writer.WriteString(name, value); }
    private static void ValidateId(Guid id) { if (id == Guid.Empty) throw new ArgumentException("Nil UUID is forbidden."); }
    private static void ValidateNumber(int value) { if (value < 0) throw new ArgumentOutOfRangeException(nameof(value)); }
    private static void ValidateText(string? value) { if (value is not null && !value.IsNormalized(NormalizationForm.FormC)) throw new ArgumentException("Canonical strings must be NFC."); }
    private static void RejectDuplicateSortKeys(IEnumerable<string> keys) { if (keys.GroupBy(x => x, StringComparer.Ordinal).Any(x => x.Count() > 1)) throw new ArgumentException("Duplicate canonical sort key."); }

    private sealed class Utf8OrdinalComparer : IComparer<string?>
    {
        public static Utf8OrdinalComparer Instance { get; } = new();
        public int Compare(string? x, string? y)
        {
            if (ReferenceEquals(x, y)) return 0; if (x is null) return -1; if (y is null) return 1;
            return Encoding.UTF8.GetBytes(x).AsSpan().SequenceCompareTo(Encoding.UTF8.GetBytes(y));
        }
    }
}
