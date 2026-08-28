using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.Territory.PlanVsCurrent;

/// <summary>
/// MOD-0151 FU04B diff vocabulary (pack §22.4). Declared once so the engine, the API and the UI speak the same words.
/// The order of <see cref="Precedence"/> IS the normative precedence: when a row satisfies more than one condition,
/// the first match wins and every other difference is reported in <c>SecondaryDifferences</c>.
/// </summary>
public static class TerritoryPlanVsCurrentDiffTypes
{
    public const string Replaced = "Replaced";
    public const string TransferredOut = "TransferredOut";
    public const string TransferredIn = "TransferredIn";
    public const string AddedAfterActivation = "AddedAfterActivation";
    public const string EndedAfterActivation = "EndedAfterActivation";
    public const string MissingCurrent = "MissingCurrent";
    public const string DateChanged = "DateChanged";
    public const string ScopeChanged = "ScopeChanged";
    public const string PositionChanged = "PositionChanged";
    public const string Unchanged = "Unchanged";

    /// <summary>Pack §22.4: Replaced &gt; TransferredOut/In &gt; Added/Ended &gt; MissingCurrent &gt; DateChanged &gt;
    /// ScopeChanged &gt; PositionChanged &gt; Unchanged.</summary>
    public static readonly IReadOnlyList<string> Precedence =
    [
        Replaced, TransferredOut, TransferredIn, AddedAfterActivation, EndedAfterActivation,
        MissingCurrent, DateChanged, ScopeChanged, PositionChanged, Unchanged
    ];

    public static int Rank(string diffType)
    {
        var index = Precedence.ToList().FindIndex(d => string.Equals(d, diffType, StringComparison.OrdinalIgnoreCase));
        return index < 0 ? Precedence.Count : index;
    }
}

/// <summary>Comparison availability, so the UI never has to guess why a table is empty.</summary>
public static class TerritoryPlanVsCurrentStates
{
    /// <summary>Model was never activated — only a planning preview exists (pack §22.4 D-FU04B-5).</summary>
    public const string NotYetActivated = "not-yet-activated";

    /// <summary>Model is/was active but carries no baseline (activated before FU04B shipped).</summary>
    public const string NotCaptured = "not-captured";

    /// <summary>Baseline present; the comparison is real.</summary>
    public const string Available = "available";
}

// ---------------------------------------------------------------------------------------------------------------
// Queries — every one is READ-ONLY (pack §22.4 D-FU04B-4).
// ---------------------------------------------------------------------------------------------------------------

public sealed record GetTerritoryResourceAssignmentPlanSnapshotQuery(Guid ModelId)
    : IRequest<Response<TerritoryPlanSnapshotDto>>;

public sealed record GetTerritoryPlanVsCurrentQuery(
    Guid ModelId,
    DateTimeOffset? EffectiveAt,
    Guid? TerritoryNodeId,
    string? BusinessUnit,
    string? PositionCode,
    string? ResourceId,
    string? DiffType) : IRequest<Response<TerritoryPlanVsCurrentDto>>;

public sealed record GetResourcePlanVsCurrentQuery(
    string ResourceId,
    DateTimeOffset? EffectiveAt,
    Guid? TerritoryNodeId,
    string? BusinessUnit,
    string? PositionCode,
    string? DiffType) : IRequest<Response<ResourcePlanVsCurrentDto>>;

// ---------------------------------------------------------------------------------------------------------------
// DTOs
// ---------------------------------------------------------------------------------------------------------------

public sealed record TerritoryPlanSnapshotDto(
    Guid ModelId,
    string ModelCode,
    string ModelName,
    string ModelStatus,
    string State,
    Guid? PlanSnapshotId,
    int? SnapshotVersion,
    DateTimeOffset? CapturedAt,
    string? CapturedBy,
    string? ActivationCorrelationId,
    int LineCount,
    IReadOnlyList<int> AvailableVersions,
    IReadOnlyList<TerritoryPlanSnapshotLineDto> Lines);

public sealed record TerritoryPlanSnapshotLineDto(
    Guid? TerritoryNodeId,
    string TerritoryNodeCode,
    string TerritoryNodeName,
    IReadOnlyList<string> BusinessScopes,
    string PositionCode,
    string PositionTitle,
    string PositionType,
    string ResourceId,
    string ResourceType,
    string ResourceDisplayName,
    DateTimeOffset PlannedEffectiveFrom,
    DateTimeOffset? PlannedEffectiveTo,
    bool IsPrimary,
    Guid SourceAssignmentId);

public sealed record TerritoryPlanVsCurrentDto(
    Guid ModelId,
    string ModelCode,
    string ModelName,
    string ModelStatus,
    string State,
    bool IsHistorical,
    Guid? PlanSnapshotId,
    int? SnapshotVersion,
    DateTimeOffset? CapturedAt,
    string? CapturedBy,
    string? ActivationCorrelationId,
    DateTimeOffset EffectiveAt,
    TerritoryPlanVsCurrentSummaryDto Summary,
    IReadOnlyList<TerritoryPlanVsCurrentRowDto> Rows);

public sealed record ResourcePlanVsCurrentDto(
    string ResourceId,
    string ResourceDisplayName,
    DateTimeOffset EffectiveAt,
    int ModelCount,
    TerritoryPlanVsCurrentSummaryDto Summary,
    IReadOnlyList<TerritoryPlanVsCurrentRowDto> Rows);

public sealed record TerritoryPlanVsCurrentSummaryDto(
    int PlannedCount,
    int CurrentCount,
    int RowCount,
    int ChangedCount,
    IReadOnlyDictionary<string, int> CountsByDiffType);

/// <summary>
/// One comparison row. <c>Planned*</c> comes from the immutable baseline, <c>Current*</c> from the FU04A current
/// responsibility policy, and the provenance fields are read (never written) off the live assignment chain.
/// </summary>
public sealed record TerritoryPlanVsCurrentRowDto(
    string DiffType,
    Guid ModelId,
    string ModelCode,
    Guid? TerritoryNodeId,
    string TerritoryNodeCode,
    string TerritoryNodeName,
    IReadOnlyList<string> BusinessUnitScopes,
    string PositionCode,
    string PositionTitle,
    string PositionType,

    string? PlannedResourceId,
    string? PlannedResourceDisplayName,
    DateTimeOffset? PlannedEffectiveFrom,
    DateTimeOffset? PlannedEffectiveTo,
    bool? PlannedIsPrimary,
    Guid? PlannedAssignmentId,

    string? CurrentResourceId,
    string? CurrentResourceDisplayName,
    string? CurrentPositionCode,
    string? CurrentPositionTitle,
    IReadOnlyList<string> CurrentBusinessUnitScopes,
    DateTimeOffset? CurrentEffectiveFrom,
    DateTimeOffset? CurrentEffectiveTo,
    bool? CurrentIsPrimary,
    Guid? CurrentAssignmentId,
    Guid? CurrentTerritoryNodeId,
    string? CurrentTerritoryNodeCode,
    string? CurrentStatus,

    string? ChangeReason,
    string? ReplacementReason,
    string? TransferReason,
    Guid? ReplacedAssignmentId,
    Guid? ReplacementAssignmentId,
    Guid? TransferFromAssignmentId,
    Guid? TransferToAssignmentId,
    DateTimeOffset? ChangedAt,
    string? ChangedBy,
    string? CorrelationId,

    IReadOnlyList<string> SecondaryDifferences,

    /// <summary>Display-only legacy value (pack §22.4 position rule). NEVER a match/diff key.</summary>
    string? LegacyRoleCode);
