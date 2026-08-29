using Diten.CrmService.Application.Common.ReferenceValidation;
using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Application.Features.Territory.AssignmentRules;

public sealed record TerritoryRuleValidationError(string Message, int StatusCode);

/// <summary>
/// Shared FU03 rule validation. Reference values are resolved fail-closed against MOD-0048 (no hardcoded fallback),
/// exactly like the FU01 node validator; criteria are normalized into the typed whitelist so nothing unvalidated is
/// ever persisted.
/// </summary>
public static class TerritoryAssignmentRuleValidation
{
    public const int MaxCriteriaValues = 200;

    /// <summary>Normalizes the input into the typed criteria value object: trims, drops blanks, de-duplicates
    /// case-insensitively while preserving order.</summary>
    public static TerritoryRuleCriteria Normalize(TerritoryRuleCriteriaInput? input)
    {
        if (input is null)
        {
            return new TerritoryRuleCriteria();
        }

        return new TerritoryRuleCriteria
        {
            CountryRefs = Clean(input.CountryRefs),
            CityRefs = Clean(input.CityRefs),
            DistrictRefs = Clean(input.DistrictRefs),
            AccountTypes = Clean(input.AccountTypes),
            AccountCategories = Clean(input.AccountCategories),
            AccountStatuses = Clean(input.AccountStatuses),
            IncludeAccountIds = CleanIds(input.IncludeAccountIds),
            ExcludeAccountIds = CleanIds(input.ExcludeAccountIds)
        };
    }

    private static List<string> Clean(IReadOnlyList<string>? values)
    {
        if (values is null)
        {
            return new List<string>();
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();
        foreach (var raw in values)
        {
            var value = raw?.Trim();
            if (string.IsNullOrWhiteSpace(value) || !seen.Add(value))
            {
                continue;
            }

            result.Add(value);
        }

        return result;
    }

    private static List<Guid> CleanIds(IReadOnlyList<Guid>? values)
    {
        if (values is null)
        {
            return new List<Guid>();
        }

        var seen = new HashSet<Guid>();
        var result = new List<Guid>();
        foreach (var id in values)
        {
            if (id != Guid.Empty && seen.Add(id))
            {
                result.Add(id);
            }
        }

        return result;
    }

    /// <summary>Validates everything that does not need repository access: reference values, criteria shape and dates.</summary>
    public static async Task<TerritoryRuleValidationError?> ValidateAsync(
        ITerritoryReferenceValidator references,
        TerritoryModel model,
        TerritoryNode? targetNode,
        string ruleType,
        string conflictPolicy,
        TerritoryRuleCriteria criteria,
        DateTimeOffset effectiveFrom,
        DateTimeOffset? effectiveTo,
        CancellationToken cancellationToken)
    {
        // --- reference values (fail-closed, no fallback) ---
        var ruleTypeStatus = await references.ValidateValueAsync(TerritoryReferenceSets.TerritoryRuleType, ruleType, cancellationToken);
        if (ruleTypeStatus != ReferenceValidationStatus.Valid)
        {
            return new TerritoryRuleValidationError(ReferenceMessage(TerritoryReferenceSets.TerritoryRuleType, ruleType, ruleTypeStatus), 400);
        }

        var policyStatus = await references.ValidateValueAsync(TerritoryReferenceSets.TerritoryConflictPolicy, conflictPolicy, cancellationToken);
        if (policyStatus != ReferenceValidationStatus.Valid)
        {
            return new TerritoryRuleValidationError(ReferenceMessage(TerritoryReferenceSets.TerritoryConflictPolicy, conflictPolicy, policyStatus), 400);
        }

        // --- rule type must be one FU03 actually evaluates ---
        if (!TerritoryRuleTypes.IsSupported(ruleType))
        {
            var reason = TerritoryRuleTypes.IsDeferred(ruleType)
                ? $"Rule type '{ruleType}' is published but not evaluated in FU03 (later FU)."
                : $"Rule type '{ruleType}' is not supported by the FU03 preview engine.";
            return new TerritoryRuleValidationError(
                $"{reason} Supported: {string.Join(", ", TerritoryRuleTypes.Fu03Supported)}.", 400);
        }

        // --- target node ---
        if (targetNode is null)
        {
            return new TerritoryRuleValidationError("Target territory node not found in this model.", 404);
        }

        // --- dates ---
        if (effectiveTo is { } end && end < effectiveFrom)
        {
            return new TerritoryRuleValidationError("EffectiveTo must be greater than or equal to EffectiveFrom.", 400);
        }

        if (effectiveFrom < model.EffectiveFrom)
        {
            return new TerritoryRuleValidationError("Rule EffectiveFrom is outside the territory model window.", 400);
        }

        if (model.EffectiveTo is { } modelEnd)
        {
            if (effectiveTo is null || effectiveTo > modelEnd)
            {
                return new TerritoryRuleValidationError("Rule EffectiveTo is outside the territory model window.", 400);
            }
        }

        // --- criteria shape ---
        return ValidateCriteria(ruleType, criteria);
    }

    /// <summary>Rule-type specific criteria requirements. An empty criteria set is always rejected — a rule that
    /// matches every account would silently claim the whole tenant.</summary>
    public static TerritoryRuleValidationError? ValidateCriteria(string ruleType, TerritoryRuleCriteria criteria)
    {
        foreach (var (label, values) in new (string, List<string>)[]
                 {
                     ("countryRefs", criteria.CountryRefs), ("cityRefs", criteria.CityRefs),
                     ("districtRefs", criteria.DistrictRefs), ("accountTypes", criteria.AccountTypes),
                     ("accountCategories", criteria.AccountCategories), ("accountStatuses", criteria.AccountStatuses)
                 })
        {
            if (values.Count > MaxCriteriaValues)
            {
                return new TerritoryRuleValidationError($"Criteria '{label}' exceeds {MaxCriteriaValues} values.", 400);
            }
        }

        if (criteria.IncludeAccountIds.Count > MaxCriteriaValues || criteria.ExcludeAccountIds.Count > MaxCriteriaValues)
        {
            return new TerritoryRuleValidationError($"Criteria account id lists exceed {MaxCriteriaValues} entries.", 400);
        }

        var overlap = criteria.IncludeAccountIds.Intersect(criteria.ExcludeAccountIds).ToList();
        if (overlap.Count > 0)
        {
            return new TerritoryRuleValidationError("An account id cannot be in both includeAccountIds and excludeAccountIds.", 400);
        }

        if (criteria.IsEmpty)
        {
            return new TerritoryRuleValidationError("Rule criteria cannot be empty; a rule must constrain at least one attribute.", 400);
        }

        var hasGeography = criteria.CountryRefs.Count > 0 || criteria.CityRefs.Count > 0 || criteria.DistrictRefs.Count > 0;
        var hasClassification = criteria.AccountTypes.Count > 0 || criteria.AccountCategories.Count > 0 || criteria.AccountStatuses.Count > 0;

        if (string.Equals(ruleType, TerritoryRuleTypes.Geography, StringComparison.OrdinalIgnoreCase) && !hasGeography)
        {
            return new TerritoryRuleValidationError(
                "A 'geography' rule requires at least one of countryRefs, cityRefs or districtRefs.", 400);
        }

        if (string.Equals(ruleType, TerritoryRuleTypes.AccountType, StringComparison.OrdinalIgnoreCase) && !hasClassification)
        {
            return new TerritoryRuleValidationError(
                "An 'account-type' rule requires at least one of accountTypes, accountCategories or accountStatuses.", 400);
        }

        if (string.Equals(ruleType, TerritoryRuleTypes.AccountList, StringComparison.OrdinalIgnoreCase)
            && criteria.IncludeAccountIds.Count == 0)
        {
            return new TerritoryRuleValidationError("An 'account-list' rule requires at least one includeAccountIds entry.", 400);
        }

        return null;
    }

    private static string ReferenceMessage(string setCode, string value, ReferenceValidationStatus status)
        => status switch
        {
            ReferenceValidationStatus.SetMissing =>
                $"Required reference set '{setCode}' is not published yet (MOD-0048 authoring pending).",
            _ => $"'{value}' is not a published value of reference set '{setCode}'."
        };
}
