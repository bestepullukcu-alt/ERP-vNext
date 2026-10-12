using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Application.Features.VisitContentSequence;

/// <summary>
/// WP-VP-3C (E7-B1, CT default F3-4) — a visit tells ONE branch of a path: its MAIN branch. The other branches are not
/// flattened into the visit.
/// <para><b>Main branch</b> = the first branch of the chain template the path is bound to, in the template's branch order
/// (<see cref="ConceptChainBranch.SortOrder"/>, then <see cref="ConceptChainBranch.BranchCode"/>) that the path actually
/// fills; when the template cannot be read, the branch of the path's first step (StepOrder). A step sits on a branch
/// through <see cref="KnowledgePathStep.Arrangement"/>.<see cref="KnowledgePathArrangement.BranchCode"/>; a path whose
/// steps carry no branch (a legacy, unbranched path) is told whole. Pure.</para>
/// <para>E7-B2 (F3-5): a step's minutes never enter the visit duration — the duration is the product-count model only.</para>
/// </summary>
public static class VisitContentMainBranch
{
    public static IReadOnlyList<KnowledgePathStep> Steps(KnowledgePath path, ConceptChainTemplate? template)
    {
        var steps = path.OrderedActiveSteps();
        var main = MainBranchCode(steps, template);
        return main is null
            ? steps
            : steps.Where(s => string.IsNullOrWhiteSpace(s.Arrangement?.BranchCode)
                               || string.Equals(s.Arrangement!.BranchCode.Trim(), main, StringComparison.OrdinalIgnoreCase))
                .ToList();
    }

    /// <summary>The main branch's code, or null for a path without branches.</summary>
    public static string? MainBranchCode(IReadOnlyList<KnowledgePathStep> steps, ConceptChainTemplate? template)
    {
        var filled = steps
            .Select(s => s.Arrangement?.BranchCode?.Trim())
            .Where(code => !string.IsNullOrEmpty(code))
            .Select(code => code!)
            .ToList();
        if (filled.Count == 0)
        {
            return null;
        }

        var fromTemplate = template?.Branches
            .OrderBy(b => b.SortOrder)
            .ThenBy(b => b.BranchCode, StringComparer.OrdinalIgnoreCase)
            .Select(b => b.BranchCode.Trim())
            .FirstOrDefault(code => filled.Contains(code, StringComparer.OrdinalIgnoreCase));
        return fromTemplate ?? filled[0];
    }
}
