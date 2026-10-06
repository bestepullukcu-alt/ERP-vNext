using System.Globalization;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.Knowledge.Chain;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;

namespace Diten.CrmService.Application.Features.Knowledge.Path;

/// <summary>A coded knowledge-path failure; <see cref="To{T}"/> renders the <c>[code, message]</c> error pair.</summary>
internal sealed record KnowledgePathFailure(string Code, string Message, int StatusCode)
{
    public Response<T> To<T>() => Response<T>.Fail(new[] { Code, Message }, StatusCode);
}

/// <summary>
/// WP-KP-1 (DESIGN-KP-STUDIO §2/§3.1) — the write rules of a CHAIN-BOUND knowledge path, in one place so the path, step
/// and claim handlers never diverge. A legacy (chain-less) path never reaches these rules: it keeps the FU04 behaviour.
/// <list type="bullet">
/// <item>the chain must be bindable (published, not archived, branched — <see cref="ChainSlots.IsBindable"/>);</item>
/// <item>a step / claim sits on an existing slot (400 <c>chain_slot_invalid</c>); a step never exceeds the slot's
/// MaxSelection (409 <c>chain_slot_full</c>) and never moves to another slot (409 <c>chain_slot_move_forbidden</c>);</item>
/// <item>StepOrder is computed branch-first (<see cref="ChainArrangementOrder"/>), the client value is ignored.</item>
/// </list>
/// </summary>
internal static class KnowledgePathStudio
{
    public const string CodePrefix = "KP-";

    /// <summary>Loads and checks a chain to bind: exists in the tenant, published, not archived, branched.</summary>
    public static async Task<(ConceptChainTemplate? Template, KnowledgePathFailure? Failure)> BindableChainAsync(
        IConceptChainTemplateRepository? templates, Guid tenantId, Guid? chainTemplateId, CancellationToken ct)
    {
        if (chainTemplateId is not { } id || id == Guid.Empty)
        {
            return (null, new KnowledgePathFailure(KnowledgePathStudioErrors.ChainTemplateInvalid,
                "ChainTemplateId is required together with CountryCode and LanguageCode.", 400));
        }

        var template = templates is null ? null : await templates.GetByIdAsync(tenantId, id, ct);
        if (!ChainSlots.IsBindable(template))
        {
            return (null, new KnowledgePathFailure(KnowledgePathStudioErrors.ChainTemplateInvalid,
                "The chain template must exist in this tenant, be published, not archived and declare branches.", 400));
        }

        return (template, null);
    }

    /// <summary>The pinned chain of a chain-bound path. A missing pinned chain is a 409 (the path cannot be edited
    /// against a skeleton it cannot read).</summary>
    public static async Task<(ConceptChainTemplate? Template, KnowledgePathFailure? Failure)> PinnedChainAsync(
        IConceptChainTemplateRepository? templates, Guid tenantId, KnowledgePath path, CancellationToken ct)
    {
        var template = templates is null || path.ChainTemplate is null
            ? null
            : await templates.GetByIdAsync(tenantId, path.ChainTemplate.ConceptChainTemplateId, ct);
        return template is null
            ? (null, new KnowledgePathFailure(KnowledgePathStudioErrors.ChainTemplateInvalid,
                "The pinned chain template of this path can no longer be read.", 409))
            : (template, null);
    }

    /// <summary>Validates an arrangement against the pinned chain (400 <c>chain_slot_invalid</c>).</summary>
    public static (KnowledgePathArrangement? Arrangement, KnowledgePathFailure? Failure) Arrangement(
        ConceptChainTemplate template, KnowledgePathArrangementInput? input)
    {
        if (input is null)
        {
            return (null, SlotInvalid("Arrangement (branchCode, chainStepId, position) is required on a chain-bound path."));
        }

        if (input.Position < 0)
        {
            return (null, SlotInvalid("Arrangement.Position must be zero or greater."));
        }

        if (ChainSlots.Find(template, input.BranchCode, input.ChainStepId) is not { } slot)
        {
            return (null, SlotInvalid(
                $"Branch '{input.BranchCode}' / chain step '{input.ChainStepId:D}' is not a slot of the pinned chain."));
        }

        return (new KnowledgePathArrangement
        {
            ChainStepId = slot.Step.ConceptTypeId,
            BranchCode = slot.Branch.BranchCode.Trim(),
            Position = input.Position
        }, null);
    }

    /// <summary>409 <c>chain_slot_full</c> when the slot already holds MaxSelection active steps (the step being edited
    /// excluded).</summary>
    public static KnowledgePathFailure? SlotFull(
        ConceptChainTemplate template, KnowledgePath path, KnowledgePathArrangement arrangement, Guid? editingStepId)
    {
        var max = ChainSlots.Find(template, arrangement.BranchCode, arrangement.ChainStepId)?.Step.MaxSelection;
        if (max is not { } limit)
        {
            return null;
        }

        var count = path.ActiveSteps().Count(s => s.StepId != editingStepId && ChainSlots.SameSlot(s.Arrangement, arrangement));
        return count >= limit
            ? new KnowledgePathFailure(KnowledgePathStudioErrors.ChainSlotFull,
                $"Chain step '{arrangement.ChainStepId:D}' of branch '{arrangement.BranchCode}' already holds its maximum of {limit}.",
                409)
            : null;
    }

    /// <summary>
    /// The branch-first StepOrder of the active steps. <paramref name="candidate"/> (a new step, or the edited step with
    /// its new arrangement) replaces / joins the stored steps. A step without an arrangement (it predates a bind-chain)
    /// goes last, in its stored order. Orders are 10, 20, 30… so they stay unique among active steps (V-S03).
    /// </summary>
    public static IReadOnlyDictionary<Guid, int> ComputeOrders(
        ConceptChainTemplate template, KnowledgePath path, KnowledgePathStep? candidate)
    {
        var active = path.ActiveSteps()
            .Where(s => candidate is null || s.StepId != candidate.StepId)
            .OrderBy(s => s.StepOrder)
            .ThenBy(s => s.StepCode, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (candidate is not null)
        {
            // An edited step keeps its place among equal-position peers; a new one joins the end of its slot's ties.
            var index = path.Steps.FindIndex(s => s.StepId == candidate.StepId && !s.IsArchived());
            if (index < 0)
            {
                active.Add(candidate);
            }
            else
            {
                var at = active.FindIndex(s => s.StepOrder > candidate.StepOrder);
                active.Insert(at < 0 ? active.Count : at, candidate);
            }
        }

        var ordered = ChainArrangementOrder.Order(template, active, s => ChainSlots.SlotOf(s.Arrangement));
        return ordered.Select((s, i) => (s.StepId, Order: (i + 1) * 10)).ToDictionary(x => x.StepId, x => x.Order);
    }

    /// <summary>Every active step's prerequisite must stay BEFORE it in the computed order (a re-position must not turn
    /// a dependency forward). Returns the 400 message or null.</summary>
    public static string? PrerequisitesStayBackward(
        KnowledgePath path, KnowledgePathStep? candidate, IReadOnlyDictionary<Guid, int> orders)
    {
        var steps = path.ActiveSteps()
            .Where(s => candidate is null || s.StepId != candidate.StepId)
            .Concat(candidate is null ? Enumerable.Empty<KnowledgePathStep>() : new[] { candidate });
        foreach (var step in steps)
        {
            if (step.PrerequisiteStepId is { } prereq && prereq != Guid.Empty
                && orders.TryGetValue(prereq, out var prereqOrder) && orders.TryGetValue(step.StepId, out var own)
                && prereqOrder >= own)
            {
                return $"Step '{step.StepCode}' would be placed before its prerequisite; a prerequisite must come earlier "
                       + "in the chain order.";
            }
        }

        return null;
    }

    public static void ApplyOrders(KnowledgePath path, IReadOnlyDictionary<Guid, int> orders)
    {
        foreach (var step in path.ActiveSteps())
        {
            if (orders.TryGetValue(step.StepId, out var order))
            {
                step.StepOrder = order;
            }
        }
    }

    /// <summary>A server-issued path code: <c>KP-{yyyy}-{6 hex}</c> (never an ISO-country-like prefix such as <c>BY-</c>;
    /// mockup correction #5), free among non-archived rows.</summary>
    public static async Task<string> NewPathCodeAsync(IKnowledgePathRepository paths, Guid tenantId, CancellationToken ct)
    {
        var year = DateTimeOffset.UtcNow.Year.ToString(CultureInfo.InvariantCulture);
        while (true)
        {
            var code = $"{CodePrefix}{year}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
            if (!(await paths.ListByCodeAsync(tenantId, code, ct)).Any(p => !p.IsArchived()))
            {
                return code;
            }
        }
    }

    /// <summary>A step's concept node, when given, must be of the slot's concept type (the chain step IS a concept type;
    /// a node of another type would contradict the slot) — 400 <c>chain_slot_invalid</c>. Decision 2026-09-30.</summary>
    public static async Task<KnowledgePathFailure?> NodeOutsideSlotAsync(
        IConceptNodeRepository nodes, Guid tenantId, Guid? conceptNodeId, KnowledgePathArrangement arrangement,
        CancellationToken ct)
    {
        if (conceptNodeId is not { } id || id == Guid.Empty)
        {
            return null;
        }

        var node = await nodes.GetByIdAsync(tenantId, id, ct);
        return node is not null && node.ConceptTypeId != arrangement.ChainStepId
            ? SlotInvalid("ConceptNodeId must be a node of the slot's concept type (the chain step).")
            : null;
    }

    public static KnowledgePathFailure SlotInvalid(string message)
        => new(KnowledgePathStudioErrors.ChainSlotInvalid, message, 400);

    public static KnowledgePathFailure LanguageMismatch(string? contentLanguage, string? pathLanguage)
        => new(ChainContextErrors.ComponentLanguageMismatch,
            $"The content language '{contentLanguage}' is not the path language '{pathLanguage}'.", 409);

    public static KnowledgePathFailure ChainRequired(string what)
        => new(KnowledgePathStudioErrors.ChainTemplateRequired,
            $"{what} needs a chain-bound path; bind the path to a chain first.", 409);

    public static KnowledgePathArrangement Copy(KnowledgePathArrangement a)
        => new() { ChainStepId = a.ChainStepId, BranchCode = a.BranchCode, Position = a.Position };
}
