using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Application.Features.ContentComposition.ContentSetRevisions;

/// <summary>
/// WP-SB-2 — the order of the knowledge-path steps a release produces. It is exactly the Content Studio workspace order
/// (<c>ContentSets/workspace.js</c> <c>slots()</c> + the per-slot Position sort), per the user decision of 2026-09-29
/// ("dal-öncelikli"):
/// <list type="number">
/// <item>branched template — branch <see cref="ConceptChainBranch.SortOrder"/> (ties keep the template's branch list
/// order), then the branch's <see cref="ConceptChainBranch.Steps"/> list order, then <see cref="ContentArrangement.Position"/>;</item>
/// <item>legacy template without branches — <see cref="ConceptChainTemplate.OrderedConceptTypes"/> order, then Position.</item>
/// </list>
/// Equal Position inside one slot keeps the frozen selection order (the workspace sort is stable too). A component whose
/// slot is not in the pinned template (it cannot be placed there — the arrange rules forbid it) goes last, in selection
/// order, rather than being dropped.
/// </summary>
public static class ContentSetPathOrder
{
    public static IReadOnlyList<ContentSetComponent> Order(
        ConceptChainTemplate template, IReadOnlyList<ContentSetComponent> components)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(components);

        var slotRank = new Dictionary<(string Branch, Guid Step), int>();
        if (template.Branches is { Count: > 0 })
        {
            var rank = 0;
            foreach (var branch in template.Branches
                         .Select((b, i) => (Branch: b, Index: i))
                         .OrderBy(x => x.Branch.SortOrder)
                         .ThenBy(x => x.Index)
                         .Select(x => x.Branch))
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

        return components
            .Select((c, i) => (Component: c, Index: i,
                Rank: slotRank.TryGetValue((Key(c.Arrangement.BranchId), c.Arrangement.TemplateStepId), out var r)
                    ? r
                    : int.MaxValue))
            .OrderBy(x => x.Rank)
            .ThenBy(x => x.Rank == int.MaxValue ? 0 : x.Component.Arrangement.Position)
            .ThenBy(x => x.Index)
            .Select(x => x.Component)
            .ToList();
    }

    private static string Key(string? branchCode) => branchCode?.Trim().ToUpperInvariant() ?? string.Empty;
}
