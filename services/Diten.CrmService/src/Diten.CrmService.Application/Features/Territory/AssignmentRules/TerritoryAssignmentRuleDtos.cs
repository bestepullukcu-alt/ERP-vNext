using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Domain.Entities;
using MediatR;

namespace Diten.CrmService.Application.Features.Territory.AssignmentRules;

public sealed record GetTerritoryAssignmentRuleListQuery(Guid ModelId)
    : IRequest<Response<TerritoryAssignmentRuleListDto>>;

public sealed record GetTerritoryAssignmentRuleByIdQuery(Guid ModelId, Guid RuleId)
    : IRequest<Response<TerritoryAssignmentRuleDto>>;

public sealed record TerritoryAssignmentRuleListDto(
    Guid ModelId,
    string ModelStatus,
    bool IsEditable,
    int TotalCount,
    int EnabledCount,
    IReadOnlyList<TerritoryAssignmentRuleDto> Items);

public sealed record TerritoryAssignmentRuleDto(
    Guid Id,
    Guid ModelId,
    string RuleCode,
    string Name,
    Guid TerritoryId,
    string? TerritoryCode,
    string? TerritoryName,
    string? TerritoryLevel,
    string RuleType,
    string ConflictPolicy,
    int Priority,
    bool IsEnabled,
    TerritoryRuleCriteriaDto Criteria,
    string CriteriaSummary,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    string? CorrelationId);

public sealed record TerritoryRuleCriteriaDto(
    IReadOnlyList<string> CountryRefs,
    IReadOnlyList<string> CityRefs,
    IReadOnlyList<string> DistrictRefs,
    IReadOnlyList<string> AccountTypes,
    IReadOnlyList<string> AccountCategories,
    IReadOnlyList<string> AccountStatuses,
    IReadOnlyList<Guid> IncludeAccountIds,
    IReadOnlyList<Guid> ExcludeAccountIds);

// ---------------------------------------------------------------------------------------------------------------
// Preview
// ---------------------------------------------------------------------------------------------------------------

/// <summary>
/// FU03 preview result. <see cref="PersistedAssignments"/> is always <c>false</c> and <see cref="PreviewRunId"/> is a
/// transient correlation handle — nothing in this DTO is stored, and no AccountTerritoryAssignment exists yet.
/// </summary>
public sealed record TerritoryAssignmentPreviewDto(
    Guid ModelId,
    string ModelStatus,
    Guid PreviewRunId,
    DateTimeOffset GeneratedAt,

    /// <summary>The instant the rule effective-windows were evaluated against. Equals "now" for a model that is
    /// currently in force, and is clamped into the model window for a future- or past-dated model so a planner does
    /// not get an empty preview.</summary>
    DateTimeOffset EffectiveAt,

    string? CorrelationId,
    bool PersistedAssignments,
    int EvaluatedRuleCount,
    int SkippedRuleCount,
    long TotalTenantAccounts,
    int ScannedAccounts,
    int TotalCandidateAccounts,
    int UnmatchedAccountsCount,
    int ConflictCount,
    IReadOnlyList<TerritoryAssignmentPreviewMatchDto> MatchedAccounts,
    IReadOnlyList<TerritoryAssignmentPreviewConflictDto> Conflicts,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<TerritoryAssignmentPreviewRuleSummaryDto> CriteriaSummary);

public sealed record TerritoryAssignmentPreviewMatchDto(
    Guid AccountId,
    string AccountCode,
    string AccountName,
    Guid TargetTerritoryNodeId,
    string? TargetTerritoryCode,
    string? TargetTerritoryName,
    string? TargetTerritoryLevel,
    Guid RuleId,
    string RuleCode,
    string RuleType,
    int Priority,
    string MatchReason,
    string ConflictStatus);

public sealed record TerritoryAssignmentPreviewConflictDto(
    Guid AccountId,
    string AccountCode,
    string AccountName,
    IReadOnlyList<TerritoryAssignmentPreviewCandidateDto> CandidateTerritoryNodes,
    IReadOnlyList<Guid> ConflictingRuleIds,
    string ConflictPolicy,
    string ResolutionSuggestion);

public sealed record TerritoryAssignmentPreviewCandidateDto(
    Guid TerritoryNodeId,
    string? TerritoryCode,
    string? TerritoryName,
    Guid RuleId,
    string RuleCode,
    int Priority,
    bool IsWinner);

public sealed record TerritoryAssignmentPreviewRuleSummaryDto(
    Guid RuleId,
    string RuleCode,
    string RuleType,
    int Priority,
    bool IsEnabled,
    bool Evaluated,
    string? SkipReason,
    string CriteriaSummary,
    int MatchCount);

// ---------------------------------------------------------------------------------------------------------------
// Mapping
// ---------------------------------------------------------------------------------------------------------------

public static class TerritoryAssignmentRuleMapper
{
    public static TerritoryAssignmentRuleDto ToDto(TerritoryAssignmentRule rule, TerritoryNode? node)
        => new(
            rule.Id,
            rule.ModelId,
            rule.RuleCode,
            rule.Name,
            rule.TerritoryId,
            node?.TerritoryCode,
            node?.Name,
            node?.TerritoryLevel,
            rule.RuleType,
            rule.ConflictPolicy,
            rule.Priority,
            rule.IsEnabled,
            ToCriteriaDto(rule.Criteria),
            Summarize(rule.Criteria),
            rule.EffectiveFrom,
            rule.EffectiveTo,
            rule.CreatedAt,
            rule.UpdatedAt,
            rule.CorrelationId);

    public static TerritoryRuleCriteriaDto ToCriteriaDto(TerritoryRuleCriteria c)
        => new(c.CountryRefs, c.CityRefs, c.DistrictRefs, c.AccountTypes, c.AccountCategories, c.AccountStatuses,
            c.IncludeAccountIds, c.ExcludeAccountIds);

    /// <summary>Short human-readable rendering of the criteria (also used by the UI and the preview summary).</summary>
    public static string Summarize(TerritoryRuleCriteria c)
    {
        var parts = new List<string>();
        void Add(string label, IReadOnlyList<string> values)
        {
            if (values.Count > 0) { parts.Add($"{label}={string.Join('|', values)}"); }
        }

        Add("country", c.CountryRefs);
        Add("city", c.CityRefs);
        Add("district", c.DistrictRefs);
        Add("accountType", c.AccountTypes);
        Add("accountCategory", c.AccountCategories);
        Add("accountStatus", c.AccountStatuses);
        if (c.IncludeAccountIds.Count > 0) { parts.Add($"include={c.IncludeAccountIds.Count} account(s)"); }
        if (c.ExcludeAccountIds.Count > 0) { parts.Add($"exclude={c.ExcludeAccountIds.Count} account(s)"); }

        return parts.Count == 0 ? "(no criteria)" : string.Join(" AND ", parts);
    }
}
