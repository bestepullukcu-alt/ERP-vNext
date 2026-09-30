using Diten.CrmService.Application.Features.Knowledge.Chain;
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
    /// <summary>WP-KP-1 — the rule itself lives in <see cref="ChainArrangementOrder"/> (shared with the knowledge path);
    /// this is only the content-set slot adapter, removed with the set in KP-4.</summary>
    public static IReadOnlyList<ContentSetComponent> Order(
        ConceptChainTemplate template, IReadOnlyList<ContentSetComponent> components)
        => ChainArrangementOrder.Order(template, components, c => new ChainArrangementOrder.Slot(
            c.Arrangement.BranchId, c.Arrangement.TemplateStepId, c.Arrangement.Position));
}
