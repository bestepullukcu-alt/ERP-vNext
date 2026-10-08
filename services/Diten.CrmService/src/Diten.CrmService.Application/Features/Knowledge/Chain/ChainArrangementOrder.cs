using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Application.Features.Knowledge.Chain;

/// <summary>
/// WP-KP-1 (moved from WP-SB-2's <c>ContentSetPathOrder</c>) — the "dal-öncelikli" (branch-first) order of items placed
/// on a chain's slots, per the user decision of 2026-09-29. It is exactly the Content Studio workspace order
/// (<c>ContentSets/workspace.js</c> <c>slots()</c> + the per-slot Position sort):
/// <list type="number">
/// <item>branched template — branch <see cref="ConceptChainBranch.SortOrder"/> (ties keep the template's branch list
/// order), then the branch's <see cref="ConceptChainBranch.Steps"/> list order, then Position;</item>
/// <item>legacy template without branches — <see cref="ConceptChainTemplate.OrderedConceptTypes"/> order, then Position
/// (content sets only; a knowledge path binds a branched template only).</item>
/// </list>
/// Equal Position inside one slot keeps the input order (stable). An item whose slot is not in the template (or that
/// has no slot) goes last, in input order, rather than being dropped.
/// </summary>
public static class ChainArrangementOrder
{
    /// <summary>A slot address: branch code (null on a legacy spine) + chain step (concept type) id + position.</summary>
    public readonly record struct Slot(string? BranchCode, Guid ChainStepId, int Position);

    public static IReadOnlyList<T> Order<T>(
        ConceptChainTemplate template, IReadOnlyList<T> items, Func<T, Slot?> slotOf)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(slotOf);

        var slotRank = new Dictionary<(string Branch, Guid Step), int>();
        if (template.Branches is { Count: > 0 })
        {
            var rank = 0;
            foreach (var branch in OrderedBranches(template))
            {
                var branchKey = Key(branch.BranchCode);
                foreach (var step in branch.Steps)
                {
                    slotRank.TryAdd((branchKey, step.ConceptTypeId), rank++);
                }
            }
        }
        else
        {
            var rank = 0;
            foreach (var type in template.OrderedConceptTypes)
            {
                slotRank.TryAdd((string.Empty, type), rank++);
            }
        }

        return items
            .Select((item, i) =>
            {
                var slot = slotOf(item);
                var rank = slot is { } s && slotRank.TryGetValue((Key(s.BranchCode), s.ChainStepId), out var r)
                    ? r
                    : int.MaxValue;
                return (Item: item, Index: i, Rank: rank, Position: rank == int.MaxValue ? 0 : slot!.Value.Position);
            })
            .OrderBy(x => x.Rank)
            .ThenBy(x => x.Position)
            .ThenBy(x => x.Index)
            .Select(x => x.Item)
            .ToList();
    }

    /// <summary>Branches in display order: SortOrder, ties by the template's list order.</summary>
    public static IEnumerable<ConceptChainBranch> OrderedBranches(ConceptChainTemplate template)
        => template.Branches
            .Select((b, i) => (Branch: b, Index: i))
            .OrderBy(x => x.Branch.SortOrder)
            .ThenBy(x => x.Index)
            .Select(x => x.Branch);

    private static string Key(string? branchCode) => branchCode?.Trim().ToUpperInvariant() ?? string.Empty;
}

/// <summary>
/// WP-KP-1 — the slot rules of a knowledge path on its chain (D-KP-7: the path never changes the skeleton — no branch or
/// step is created, and nothing is placed outside it). Only BRANCHED templates are bindable (user decision 2026-09-30,
/// option B): the legacy flat spine had two diverging slot readings (read DTO "B1" 1..1 vs the set's branch-less
/// unbounded slot), so it is rejected with <c>chain_template_invalid</c> instead of guessed.
/// </summary>
public static class ChainSlots
{
    /// <summary>Bindable = published, not archived and branched (every branch with at least one step).</summary>
    public static bool IsBindable(ConceptChainTemplate? template)
        => template is not null
           && template.IsPublished()
           && !template.IsArchived()
           && template.Branches is { Count: > 0 }
           && template.Branches.All(b => !string.IsNullOrWhiteSpace(b.BranchCode) && b.Steps.Count > 0);

    /// <summary>The chain step of (<paramref name="branchCode"/>, <paramref name="chainStepId"/>), or null.</summary>
    public static (ConceptChainBranch Branch, ConceptChainStep Step)? Find(
        ConceptChainTemplate template, string? branchCode, Guid chainStepId)
    {
        if (string.IsNullOrWhiteSpace(branchCode) || chainStepId == Guid.Empty)
        {
            return null;
        }

        var branch = template.Branches.FirstOrDefault(b =>
            string.Equals(b.BranchCode?.Trim(), branchCode.Trim(), StringComparison.OrdinalIgnoreCase));
        var step = branch?.Steps.FirstOrDefault(s => s.ConceptTypeId == chainStepId);
        return branch is null || step is null ? null : (branch, step);
    }

    public static bool SameSlot(KnowledgePathArrangement? a, KnowledgePathArrangement? b)
        => a is not null && b is not null
           && a.ChainStepId == b.ChainStepId
           && string.Equals(a.BranchCode?.Trim(), b.BranchCode?.Trim(), StringComparison.OrdinalIgnoreCase);

    public static ChainArrangementOrder.Slot? SlotOf(KnowledgePathArrangement? arrangement)
        => arrangement is null
            ? null
            : new ChainArrangementOrder.Slot(arrangement.BranchCode, arrangement.ChainStepId, arrangement.Position);
}
