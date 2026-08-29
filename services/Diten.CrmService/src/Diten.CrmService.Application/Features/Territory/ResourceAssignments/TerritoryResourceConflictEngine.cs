using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Application.Features.Territory.ResourceAssignments;

public static class TerritoryResourceConflictKinds
{
    /// <summary>Two primary assignments for the same (node, role, business-scope set) in overlapping periods.</summary>
    public const string DuplicatePrimary = "duplicate-primary";

    /// <summary>One MR is primary in two DIFFERENT business scopes at the same time (pack §10 block).</summary>
    public const string CrossScopePrimaryResource = "cross-scope-primary-resource";

    /// <summary>One MR covers several nodes inside the SAME business scope — allowed, workload warning (pack §10).</summary>
    public const string MultiNodeCoverage = "multi-node-coverage";

    /// <summary>Role is assigned to a node level the pack does not recommend (advisory).</summary>
    public const string UnexpectedNodeLevel = "unexpected-node-level";

    public const string SeverityBlock = "block";
    public const string SeverityWarning = "warning";
}

/// <summary>
/// FU04 exclusivity guard (pack §10). Pure and side-effect free: it takes the existing assignments plus the candidate
/// and reports conflicts — it never writes.
///
/// <para><b>Scope of the guard.</b> Historical rows (<c>ended</c>/<c>rejected</c>) are ignored; both <c>proposed</c>
/// and <c>active</c> rows participate. Catching a clash while it is still a proposal is the whole point — otherwise a
/// planner could stack several conflicting proposals and only discover them at activation.</para>
///
/// <para>Non-primary (backup / shared) assignments are exempt from the exclusivity rules, exactly as pack §10 states.</para>
/// </summary>
public static class TerritoryResourceConflictEngine
{
    /// <summary>Statuses that still "hold" the responsibility and therefore contend for exclusivity.</summary>
    public static bool IsContending(TerritoryResourceAssignment a)
        => !a.IsDeleted
           && !string.Equals(a.Status, "ended", StringComparison.OrdinalIgnoreCase)
           && !string.Equals(a.Status, "rejected", StringComparison.OrdinalIgnoreCase);

    public static bool Overlaps(TerritoryResourceAssignment left, TerritoryResourceAssignment right)
    {
        var leftEnd = left.ValidTo ?? DateTimeOffset.MaxValue;
        var rightEnd = right.ValidTo ?? DateTimeOffset.MaxValue;
        return left.ValidFrom <= rightEnd && right.ValidFrom <= leftEnd;
    }

    public static string ScopeKey(TerritoryResourceAssignment a)
        => string.Join('|', a.BusinessScopes
            .Select(s => s.ScopeCode.Trim().ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(s => s, StringComparer.Ordinal));

    /// <summary>Blocking check for a single candidate against the model's existing assignments.
    /// Returns null when the candidate is acceptable.</summary>
    public static TerritoryResourceConflictDto? FindBlockingConflict(
        TerritoryResourceAssignment candidate,
        IReadOnlyList<TerritoryResourceAssignment> existing,
        IReadOnlyDictionary<Guid, TerritoryNode> nodes,
        bool allowOverride)
    {
        if (!candidate.IsPrimary)
        {
            return null;
        }

        var others = existing.Where(a => a.Id != candidate.Id && a.IsPrimary && IsContending(a) && Overlaps(a, candidate)).ToList();

        // Same node + position + business-scope set → only one primary may hold it.
        var duplicate = others.FirstOrDefault(a =>
            a.TerritoryId == candidate.TerritoryId
            && string.Equals(a.EffectivePositionCode, candidate.EffectivePositionCode, StringComparison.OrdinalIgnoreCase)
            && ScopeKey(a) == ScopeKey(candidate));

        if (duplicate is not null)
        {
            return new TerritoryResourceConflictDto(
                TerritoryResourceConflictKinds.DuplicatePrimary,
                TerritoryResourceConflictKinds.SeverityBlock,
                $"A primary '{candidate.EffectivePositionCode}' assignment already covers this scope in an overlapping period "
                + $"({duplicate.Resource.DisplayName}).",
                new[] { duplicate.Id },
                candidate.EffectivePositionCode,
                candidate.TerritoryId,
                candidate.TerritoryId is { } id && nodes.TryGetValue(id, out var n) ? n.TerritoryCode : null,
                candidate.BusinessScopes.Select(s => s.ScopeCode).ToList());
        }

        var crossBusinessUnit = others.FirstOrDefault(a =>
            string.Equals(a.Resource.ResourceId, candidate.Resource.ResourceId, StringComparison.OrdinalIgnoreCase)
            && string.Equals(a.EffectivePositionCode, candidate.EffectivePositionCode, StringComparison.OrdinalIgnoreCase)
            && ScopeKey(a) != ScopeKey(candidate));

        if (crossBusinessUnit is not null && !allowOverride)
        {
            return new TerritoryResourceConflictDto(
                TerritoryResourceConflictKinds.CrossScopePrimaryResource,
                TerritoryResourceConflictKinds.SeverityBlock,
                $"Resource '{candidate.Resource.DisplayName}' already has primary position "
                + $"'{candidate.EffectivePositionCode}' in another business-unit scope. Override source and reason are required.",
                new[] { crossBusinessUnit.Id },
                candidate.EffectivePositionCode,
                candidate.TerritoryId,
                candidate.TerritoryId is { } crossId && nodes.TryGetValue(crossId, out var crossNode) ? crossNode.TerritoryCode : null,
                candidate.BusinessScopes.Select(s => s.ScopeCode).ToList());
        }

        return null;
    }

    /// <summary>Full read-only report over a model: every blocking clash plus the advisory warnings.</summary>
    public static (List<TerritoryResourceConflictDto> Conflicts, List<TerritoryResourceConflictDto> Warnings) Report(
        IReadOnlyList<TerritoryResourceAssignment> assignments,
        IReadOnlyDictionary<Guid, TerritoryNode> nodes)
    {
        var conflicts = new List<TerritoryResourceConflictDto>();
        var warnings = new List<TerritoryResourceConflictDto>();
        var contending = assignments.Where(IsContending).ToList();
        var reportedPairs = new HashSet<string>();

        foreach (var a in contending.Where(x => x.IsPrimary))
        {
            var others = contending.Where(o => o.Id != a.Id && o.IsPrimary && Overlaps(o, a)).ToList();

            foreach (var other in others)
            {
                var pair = string.Join('|', new[] { a.Id, other.Id }.OrderBy(x => x));
                if (!reportedPairs.Add(pair))
                {
                    continue;
                }

                // Same node + position + business-scope set → two primaries clash.
                if (a.TerritoryId == other.TerritoryId
                    && string.Equals(a.EffectivePositionCode, other.EffectivePositionCode, StringComparison.OrdinalIgnoreCase)
                    && ScopeKey(a) == ScopeKey(other))
                {
                    conflicts.Add(new TerritoryResourceConflictDto(
                        TerritoryResourceConflictKinds.DuplicatePrimary,
                        TerritoryResourceConflictKinds.SeverityBlock,
                        $"Two primary '{a.EffectivePositionCode}' assignments overlap on the same scope: "
                        + $"{a.Resource.DisplayName} / {other.Resource.DisplayName}.",
                        new[] { a.Id, other.Id },
                        a.EffectivePositionCode,
                        a.TerritoryId,
                        a.TerritoryId is { } id && nodes.TryGetValue(id, out var n) ? n.TerritoryCode : null,
                        a.BusinessScopes.Select(s => s.ScopeCode).ToList()));
                }

                if (string.Equals(a.Resource.ResourceId, other.Resource.ResourceId, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(a.EffectivePositionCode, other.EffectivePositionCode, StringComparison.OrdinalIgnoreCase))
                {
                    if (ScopeKey(a) == ScopeKey(other) && a.TerritoryId != other.TerritoryId)
                    {
                        warnings.Add(new TerritoryResourceConflictDto(
                            TerritoryResourceConflictKinds.MultiNodeCoverage,
                            TerritoryResourceConflictKinds.SeverityWarning,
                            $"Resource '{a.Resource.DisplayName}' holds primary position '{a.EffectivePositionCode}' "
                            + "on multiple nodes in the same business-unit scope.",
                            new[] { a.Id, other.Id },
                            a.EffectivePositionCode,
                            a.TerritoryId,
                            a.TerritoryId is { } warningId && nodes.TryGetValue(warningId, out var warningNode)
                                ? warningNode.TerritoryCode : null,
                            a.BusinessScopes.Select(s => s.ScopeCode).ToList()));
                    }
                    else if (ScopeKey(a) != ScopeKey(other)
                             && !(IsApprovedOverride(a) || IsApprovedOverride(other)))
                    {
                        conflicts.Add(new TerritoryResourceConflictDto(
                            TerritoryResourceConflictKinds.CrossScopePrimaryResource,
                            TerritoryResourceConflictKinds.SeverityBlock,
                            $"Resource '{a.Resource.DisplayName}' holds primary position '{a.EffectivePositionCode}' "
                            + "in overlapping business-unit scopes without an explicit override.",
                            new[] { a.Id, other.Id },
                            a.EffectivePositionCode,
                            a.TerritoryId,
                            null,
                            a.BusinessScopes.Select(s => s.ScopeCode).ToList()));
                    }
                }
            }
        }

        return (conflicts, warnings);
    }

    private static bool IsApprovedOverride(TerritoryResourceAssignment assignment)
        => string.Equals(assignment.AssignmentSource, "override", StringComparison.OrdinalIgnoreCase)
           && !string.IsNullOrWhiteSpace(assignment.ChangeReason);
}
