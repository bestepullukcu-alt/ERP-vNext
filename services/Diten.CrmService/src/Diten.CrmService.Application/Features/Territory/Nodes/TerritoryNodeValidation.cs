using Diten.CrmService.Application.Common.ReferenceValidation;
using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Application.Features.Territory.Nodes;

/// <summary>
/// Shared MOD-0151 TerritoryNode validation (reference + hierarchy rank + date containment + microzone rule),
/// used by both create and update handlers. Every reference/metadata gap is fail-closed (controlled 400) — never a
/// default rank/level. Parent existence, uniqueness and cycle checks are done in the handlers (they need the repo).
/// </summary>
internal static class TerritoryNodeValidation
{
    internal sealed record Error(string Message, int StatusCode);

    /// <summary>Validates reference values, hierarchy rank ordering, date containment and the microzone rule.
    /// Returns null on success. <paramref name="parent"/> is the already-loaded parent node (null for a root node).</summary>
    internal static async Task<Error?> ValidateAsync(
        ITerritoryReferenceValidator references,
        TerritoryModel model,
        TerritoryNode? parent,
        string levelCode,
        string nodeStatus,
        DateTimeOffset effectiveFrom,
        DateTimeOffset? effectiveTo,
        bool hasMicroZoneProfile,
        CancellationToken cancellationToken)
    {
        // 1. Level reference + rank (fail-closed at every step; no default rank).
        var childRank = await references.ResolveLevelRankAsync(levelCode, cancellationToken);
        if (!childRank.Ok)
        {
            return LevelIssueError(levelCode, childRank.Issue);
        }

        // 2. Node status reference.
        var statusResult = await references.ValidateValueAsync(TerritoryReferenceSets.TerritoryNodeStatus, nodeStatus, cancellationToken);
        if (statusResult != ReferenceValidationStatus.Valid)
        {
            return new Error(ReferenceError(TerritoryReferenceSets.TerritoryNodeStatus, statusResult), 400);
        }

        // 3. Hierarchy rank: child rank must be strictly greater than parent rank (level skipping allowed, backward blocked).
        if (parent is not null)
        {
            var parentRank = await references.ResolveLevelRankAsync(parent.TerritoryLevel, cancellationToken);
            if (!parentRank.Ok)
            {
                return new Error($"Parent node level '{parent.TerritoryLevel}' rank could not be resolved from reference data.", 400);
            }

            if (childRank.Rank <= parentRank.Rank)
            {
                return new Error(
                    $"Level '{levelCode}' (rank {childRank.Rank}) cannot be a child of '{parent.TerritoryLevel}' (rank {parentRank.Rank}); child rank must be greater.",
                    400);
            }

            var parentDateError = CheckWithin(effectiveFrom, effectiveTo, parent.EffectiveFrom, parent.EffectiveTo, "parent node");
            if (parentDateError is not null)
            {
                return parentDateError;
            }
        }

        // 4. Date containment within the model range.
        var modelDateError = CheckWithin(effectiveFrom, effectiveTo, model.EffectiveFrom, model.EffectiveTo, "model");
        if (modelDateError is not null)
        {
            return modelDateError;
        }

        // 5. MicroZone profile is allowed only on a microzone node (pack §12 / §20).
        if (hasMicroZoneProfile && !string.Equals(levelCode, "microzone", StringComparison.OrdinalIgnoreCase))
        {
            return new Error("MicroZoneProfile is only allowed on a node whose level is 'microzone'.", 400);
        }

        return null;
    }

    private static Error? CheckWithin(
        DateTimeOffset childFrom, DateTimeOffset? childTo,
        DateTimeOffset parentFrom, DateTimeOffset? parentTo, string scope)
    {
        if (childFrom < parentFrom)
        {
            return new Error($"Node EffectiveFrom is before the {scope} EffectiveFrom.", 400);
        }

        if (parentTo is { } pTo)
        {
            if (childFrom > pTo)
            {
                return new Error($"Node EffectiveFrom is after the {scope} EffectiveTo.", 400);
            }

            if (childTo is { } cTo && cTo > pTo)
            {
                return new Error($"Node EffectiveTo is after the {scope} EffectiveTo.", 400);
            }
        }

        return null;
    }

    private static Error LevelIssueError(string levelCode, TerritoryReferenceIssue issue) => issue switch
    {
        TerritoryReferenceIssue.SetMissing => new Error(
            $"Required reference set '{TerritoryReferenceSets.TerritoryLevel}' is not published yet (MOD-0048 authoring pending).", 400),
        TerritoryReferenceIssue.InvalidValue => new Error(
            $"'{levelCode}' is not a valid published value of reference set '{TerritoryReferenceSets.TerritoryLevel}'.", 400),
        TerritoryReferenceIssue.MetadataMissing => new Error(
            $"Territory level '{levelCode}' has no 'rank' metadata; hierarchy ordering cannot be validated.", 400),
        TerritoryReferenceIssue.MetadataInvalid => new Error(
            $"Territory level '{levelCode}' 'rank' metadata is not a valid integer.", 400),
        _ => new Error($"Territory level '{levelCode}' could not be validated.", 400)
    };

    private static string ReferenceError(string setCode, ReferenceValidationStatus status) => status switch
    {
        ReferenceValidationStatus.SetMissing => $"Required reference set '{setCode}' is not published yet (MOD-0048 authoring pending).",
        _ => $"'{setCode}' does not contain the required published value."
    };
}
