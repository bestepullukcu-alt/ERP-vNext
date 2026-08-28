using Diten.CrmService.Application.Common.ReferenceValidation;

namespace Diten.CrmService.Application.Features.Territory;

/// <summary>Fail-closed outcome of resolving a required territory reference value (or its metadata).</summary>
public enum TerritoryReferenceIssue
{
    None,

    /// <summary>The required set is not published yet (operator authoring pending) — controlled dependency.</summary>
    SetMissing,

    /// <summary>The value is absent or deprecated in the published set.</summary>
    InvalidValue,

    /// <summary>The value is valid but a required metadata key is absent (e.g. territory-level has no <c>rank</c>).</summary>
    MetadataMissing,

    /// <summary>A required metadata value is present but cannot be parsed (e.g. <c>rank="abc"</c>).</summary>
    MetadataInvalid
}

/// <summary>Result of resolving a territory-level's rank metadata. <see cref="Rank"/> is meaningful only when Ok.</summary>
public sealed record LevelRankResult(TerritoryReferenceIssue Issue, int Rank)
{
    public bool Ok => Issue == TerritoryReferenceIssue.None;

    public static LevelRankResult Success(int rank) => new(TerritoryReferenceIssue.None, rank);
    public static LevelRankResult Fail(TerritoryReferenceIssue issue) => new(issue, 0);
}

/// <summary>Readiness of one required MOD-0048 set, for the MOD-0151 contract endpoint.</summary>
public sealed record TerritoryReferenceSetReadiness(
    string SetCode,
    bool Required,
    bool Ready,
    int ExpectedValueCount,
    int ActualValueCount,
    bool MetadataReady,
    IReadOnlyList<string> MissingMetadata);

/// <summary>
/// MOD-0151 reference validator. Composes the existing MOD-0048 consumer seams (single-value validate, per-value
/// attributes, whole-set catalog) — it NEVER seeds or hardcodes reference values and never falls back to a local list.
/// Every "missing set / missing value / missing or unparseable metadata" case is a controlled, testable failure.
/// </summary>
public interface ITerritoryReferenceValidator
{
    /// <summary>Validates a single value against a required set (existence + active). Delegates to the MOD-0048 seam.</summary>
    Task<ReferenceValidationStatus> ValidateValueAsync(string setCode, string value, CancellationToken cancellationToken);

    /// <summary>Resolves a <c>territory-level</c> value's rank metadata, fail-closed at every step.</summary>
    Task<LevelRankResult> ResolveLevelRankAsync(string levelCode, CancellationToken cancellationToken);

    /// <summary>Raw per-value metadata of a published value, or null when the set/value/metadata is absent.
    /// FU04 drives its role and coverage-scope rules from this (<c>requiresTerritoryId</c>, <c>canBePrimary</c>, …)
    /// instead of hardcoding them, so the published vocabulary stays the single source of truth.</summary>
    Task<IReadOnlyDictionary<string, string>?> GetValueMetadataAsync(string setCode, string value, CancellationToken cancellationToken);

    /// <summary>Reports readiness of every required MOD-0151 set (existence + count + required-metadata coverage).</summary>
    Task<IReadOnlyList<TerritoryReferenceSetReadiness>> GetReadinessAsync(CancellationToken cancellationToken);
}
