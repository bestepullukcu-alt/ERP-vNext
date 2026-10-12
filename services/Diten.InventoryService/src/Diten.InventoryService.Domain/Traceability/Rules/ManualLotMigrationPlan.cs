namespace Diten.InventoryService.Domain.Traceability.Rules;

/// <summary>TRACE <c>MigrationDecision.kind</c>.</summary>
public enum MigrationDecisionKind
{
    LinkExisting,
    Typo,
    NewLot
}

/// <summary>One decision of the commit, as sent (the new lot's fields are checked by the handler).</summary>
public sealed record MigrationDecisionInput(string GroupId, MigrationDecisionKind Kind, Guid? LotId, string? TargetGroupId);

/// <summary>
/// Where one group's transactions end up: an existing registered lot, or the new Draft lot that the group named
/// <see cref="NewLotOwnerGroupId"/> creates (its own NewLot decision, or the head of its typo chain).
/// </summary>
public sealed record MigrationTarget(string GroupId, Guid? ExistingLotId, string? NewLotOwnerGroupId);

public enum MigrationPlanError
{
    /// <summary>A decision names a group that is not in the current list (the list changed) → 409 <c>MIGRATION_STALE</c>.</summary>
    Stale,

    /// <summary>A group has no decision, or a decision misses its lot / target → 422 <c>MIGRATION_DECISIONS_INCOMPLETE</c>.</summary>
    Incomplete,

    /// <summary>A typo chain does not end at a lot / new lot of the same SKU within <see cref="ManualLotMatcher.MaxTypoChain"/> steps → 422 <c>MIGRATION_TYPO_UNRESOLVED</c>.</summary>
    TypoUnresolved,

    /// <summary>Two decisions for one group → 400 <c>INVALID_REQUEST</c>.</summary>
    Duplicate
}

public sealed record MigrationPlanResult(IReadOnlyList<MigrationTarget> Targets, MigrationPlanError? Error, IReadOnlyList<string> Groups)
{
    public bool IsValid => Error is null;

    public static MigrationPlanResult Refused(MigrationPlanError error, IEnumerable<string> groups) => new([], error, groups.ToList());
}

/// <summary>
/// Resolves the page's decisions into one target per group (pack §11.5, K-D, contract <c>commitManualLotMigration</c>),
/// pure. Error order: a decision for an unknown group (stale) → duplicate → a group without a complete decision →
/// typo chains. A <see cref="MigrationDecisionKind.Typo"/> follows <c>targetGroupId</c> through at most
/// <see cref="ManualLotMatcher.MaxTypoChain"/> groups of the SAME SKU until it reaches a <c>LinkExisting</c> or a
/// <c>NewLot</c>; a self link, a cycle, another SKU or a longer chain is unresolved.
/// </summary>
public static class ManualLotMigrationPlan
{
    public static MigrationPlanResult Resolve(IReadOnlyList<TypedLotGroup> groups, IReadOnlyList<MigrationDecisionInput> decisions)
    {
        var byId = groups.ToDictionary(g => g.GroupId, StringComparer.Ordinal);

        var unknown = decisions.Where(d => !byId.ContainsKey(d.GroupId)).Select(d => d.GroupId).Distinct(StringComparer.Ordinal).ToList();
        if (unknown.Count > 0) return MigrationPlanResult.Refused(MigrationPlanError.Stale, unknown);

        var repeated = decisions.GroupBy(d => d.GroupId, StringComparer.Ordinal).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (repeated.Count > 0) return MigrationPlanResult.Refused(MigrationPlanError.Duplicate, repeated);

        var decided = decisions.ToDictionary(d => d.GroupId, StringComparer.Ordinal);
        var incomplete = groups.Where(g => !decided.TryGetValue(g.GroupId, out var d)
                                           || (d.Kind == MigrationDecisionKind.LinkExisting && d.LotId is null)
                                           || (d.Kind == MigrationDecisionKind.Typo && string.IsNullOrWhiteSpace(d.TargetGroupId)))
            .Select(g => g.GroupId).ToList();
        if (incomplete.Count > 0) return MigrationPlanResult.Refused(MigrationPlanError.Incomplete, incomplete);

        var targets = new List<MigrationTarget>();
        var unresolved = new List<string>();
        foreach (var group in groups)
        {
            var current = decided[group.GroupId];
            var visited = new HashSet<string>(StringComparer.Ordinal) { group.GroupId };
            var hops = 0;
            while (current.Kind == MigrationDecisionKind.Typo)
            {
                hops++;
                var next = current.TargetGroupId!;
                if (hops > ManualLotMatcher.MaxTypoChain
                    || !byId.TryGetValue(next, out var target)
                    || target.SkuId != group.SkuId || target.SkuLevel != group.SkuLevel
                    || !visited.Add(next))
                {
                    current = null!;
                    break;
                }

                current = decided[next];
            }

            if (current is null)
            {
                unresolved.Add(group.GroupId);
                continue;
            }

            targets.Add(current.Kind == MigrationDecisionKind.LinkExisting
                ? new MigrationTarget(group.GroupId, current.LotId, null)
                : new MigrationTarget(group.GroupId, null, current.GroupId));
        }

        return unresolved.Count > 0
            ? MigrationPlanResult.Refused(MigrationPlanError.TypoUnresolved, unresolved)
            : new MigrationPlanResult(targets, null, []);
    }
}
