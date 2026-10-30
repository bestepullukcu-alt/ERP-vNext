using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Diten.SupplyChainService.Domain.Features.SupplierPerformance;
namespace Diten.SupplyChainService.Application.Features.SupplierPerformance;

public sealed record MetricResultResponse(string MetricCode, string Score, string Weight);
public sealed record EvaluationResponse(string EvaluationId, string SupplierId, string PeriodStart, string PeriodEnd, string Status,
    IReadOnlyList<MetricResultResponse> Metrics, string OverallScore, string RiskLevel, int Version, Guid CorrelationId, string ContractVersion = "v1")
{
    public static EvaluationResponse From(SupplierEvaluation e) => new(e.Id.ToString("D"), e.SupplierId, e.PeriodStart, e.PeriodEnd, e.Status.ToString(),
        e.Metrics.Select(m => new MetricResultResponse(m.MetricCode, m.Score, m.Weight)).ToList(), e.OverallScore, e.RiskLevel, e.Version, e.CorrelationId);
}
public sealed record EvaluationListResponse(IReadOnlyList<EvaluationResponse> Items, int Page, int PageSize, long Total, Guid CorrelationId, string ContractVersion = "v1");
public sealed record ScorecardResponse(string ScorecardId, string EvaluationId, string SupplierId, string PeriodStart, string PeriodEnd,
    IReadOnlyList<MetricResultResponse> Metrics, string OverallScore, string RiskLevel, string PublishedAt, Guid CorrelationId, string ContractVersion = "v1")
{
    public static ScorecardResponse From(SupplierScorecard s) => new(s.ScorecardId.ToString("D"), s.EvaluationId.ToString("D"), s.SupplierId, s.PeriodStart, s.PeriodEnd,
        s.Metrics.Select(m => new MetricResultResponse(m.MetricCode, m.Score, m.Weight)).ToList(), s.OverallScore, s.RiskLevel,
        s.PublishedAt.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'", CultureInfo.InvariantCulture), s.CorrelationId);
}
public sealed record ScorecardListResponse(IReadOnlyList<ScorecardResponse> Items, long Total, Guid CorrelationId, string ContractVersion = "v1");

/// <summary>A schema-valid CreateEvaluationRequest; Notes is null when absent or null (§0.5: absent = null, "" distinct).</summary>
public sealed record CreateEvaluationInput(string SupplierId, string PeriodStart, string PeriodEnd, IReadOnlyList<MetricInput> Metrics, string? Notes);

/// <summary>
/// The SUPPLIER-PERFORMANCE v1 wire for the DL-1 operations. Schema checks (stage 4, 400) only — the v1 policy rules
/// (stage 7, 422) live in <see cref="ScorePolicy"/> and run after the receipt lookup.
/// </summary>
public static class SupplierPerformanceWire
{
    public const int NotesMaxLength = 2000;
    public const int MaxPageSize = 200;
    public const int DefaultPageSize = 50;
    private static readonly Regex UuidText = new(@"\A[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\z", RegexOptions.CultureInvariant);
    private static readonly Regex DecimalText = new(@"\A-?[0-9]+(\.[0-9]+)?\z", RegexOptions.CultureInvariant);
    // SUPPLIER 1.3.0 names the canonical code (Supplier.SupplierId) without a pattern; Procurement mints "SUP-" + upper-case
    // characters and the contract's examples are SUP-2001. Anything else is a legacy code (open point, evidence note).
    private static readonly Regex CanonicalCode = new(@"\ASUP-[A-Z0-9]+\z", RegexOptions.CultureInvariant);
    private static readonly string[] CreateKeys = ["supplierId", "periodStart", "periodEnd", "metrics", "notes"];
    private static readonly string[] MetricKeys = ["metricCode", "measuredValue", "weight"];

    public static bool Uuid(string? text) => text is not null && UuidText.IsMatch(text);
    public static bool IsCanonicalCode(string text) => CanonicalCode.IsMatch(text);
    /// <summary>§0.0 D2 (Natig P1): v1 accepts only a canonical SUP- code or the tenant-wide master uuid.</summary>
    public static bool IsAcceptedSupplierIdentity(string text) => IsCanonicalCode(text) || Uuid(text);
    public static bool Date(string? text, out DateOnly date) =>
        DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    public static bool Date(string? text) => Date(text, out _);
    public static bool Status(string? text) => text is not null && Enum.GetNames<EvaluationStatus>().Contains(text, StringComparer.Ordinal);
    public static bool RiskLevel(string? text) => text is "LOW" or "MEDIUM" or "HIGH" or "CRITICAL";
    public static bool PositiveInteger(string? text, int max, out int value) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value) && value >= 1 && value <= max;

    private static bool Object(JsonElement e, string[] required, string[] allowed) =>
        e.ValueKind == JsonValueKind.Object && required.All(x => e.TryGetProperty(x, out _))
        && e.EnumerateObject().All(x => allowed.Contains(x.Name, StringComparer.Ordinal))
        && e.EnumerateObject().Select(x => x.Name).Distinct(StringComparer.Ordinal).Count() == e.EnumerateObject().Count();
    private static bool Text(JsonElement e) => e.ValueKind == JsonValueKind.String;
    private static bool Decimal(JsonElement e) => Text(e) && DecimalText.IsMatch(e.GetString()!);

    public static bool CreateValid(JsonElement b) =>
        Object(b, CreateKeys[..4], CreateKeys)
        && Text(b.GetProperty("supplierId")) && b.GetProperty("supplierId").GetString()!.Length > 0
        && Text(b.GetProperty("periodStart")) && Date(b.GetProperty("periodStart").GetString())
        && Text(b.GetProperty("periodEnd")) && Date(b.GetProperty("periodEnd").GetString())
        && b.GetProperty("metrics").ValueKind == JsonValueKind.Array && b.GetProperty("metrics").GetArrayLength() >= 1
        && b.GetProperty("metrics").EnumerateArray().All(m => Object(m, MetricKeys, MetricKeys) && Text(m.GetProperty("metricCode"))
            && Decimal(m.GetProperty("measuredValue")) && Decimal(m.GetProperty("weight")))
        && (!b.TryGetProperty("notes", out var notes) || notes.ValueKind == JsonValueKind.Null
            || (Text(notes) && notes.GetString()!.EnumerateRunes().Count() <= NotesMaxLength));

    public static CreateEvaluationInput Parse(JsonElement b) => new(
        b.GetProperty("supplierId").GetString()!, b.GetProperty("periodStart").GetString()!, b.GetProperty("periodEnd").GetString()!,
        b.GetProperty("metrics").EnumerateArray().Select(m => new MetricInput(m.GetProperty("metricCode").GetString()!,
            m.GetProperty("measuredValue").GetString()!, m.GetProperty("weight").GetString()!)).ToList(),
        b.TryGetProperty("notes", out var notes) && notes.ValueKind == JsonValueKind.String ? notes.GetString() : null);
}
