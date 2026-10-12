using Diten.CrmService.Application.Features.Knowledge.Chain;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;

namespace Diten.CrmService.Application.Features.Knowledge.Path;

/// <summary>WP-VW-W3 (W3-BE-b, §2.3) — a step's presentation notes on the wire (add / update step). On UPDATE an absent
/// presentation (null) KEEPS the stored notes — an older client that does not know the field never wipes them; an object
/// replaces them, and an object with every field empty clears them.</summary>
public sealed record KnowledgePathStepPresentationInput(
    string? KeyMessage = null,
    string? TalkTrack = null,
    int? TargetSeconds = null,
    string? Transition = null,
    IReadOnlyList<Guid>? ObjectionResponseIds = null);

public sealed record KnowledgePathStepPresentationDto(
    string? KeyMessage, string? TalkTrack, int? TargetSeconds, string? Transition, IReadOnlyList<Guid> ObjectionResponseIds)
{
    public static KnowledgePathStepPresentationDto? From(KnowledgePathStepPresentation? p)
        => p is null ? null : new(p.KeyMessage, p.TalkTrack, p.TargetSeconds, p.Transition, p.ObjectionResponseIds.ToList());
}

/// <summary>
/// WP-VW-W3 (W3-BE-b) — the ONE check + mapping of a step's presentation notes:
/// key message ≤ 500, talk track ≤ 4000, target seconds 10–3600 or none, transition ≤ 500, at most 10 distinct objection
/// answers — else 400 <c>path_step_presentation_invalid</c>; every objection id must be one of THIS tenant's library
/// records (any status: a draft may be linked while the deck is written; only approved ones ever reach a rep) — else 400
/// <c>path_step_objection_not_found</c>. The frozen-path rule of the step write (published ⇒ 409) applies before this.
/// <para>WP-VW-W3 (W3-BE-c, K-A4) — a linked answer must ALSO be APPROVED and of the PATH's product (the product the chain
/// derives — <see cref="IKnowledgePathProductResolver"/>, the source the review snapshot uses); else 400
/// <c>path_step_objection_invalid</c>. A path with no derivable product links no answer.</para>
/// </summary>
public static class KnowledgePathPresentation
{
    public static async Task<string[]?> ValidateAsync(
        KnowledgePathStepPresentationInput? input, Guid tenantId, IObjectionResponseRepository? objections,
        CancellationToken cancellationToken, KnowledgePath? path = null, IKnowledgePathProductResolver? products = null)
    {
        if (input is null)
        {
            return null;
        }

        var ids = (input.ObjectionResponseIds ?? Array.Empty<Guid>()).Distinct().ToList();
        if (Length(input.KeyMessage) > KnowledgePathPresentationLimits.MaxKeyMessageLength
            || Length(input.TalkTrack) > KnowledgePathPresentationLimits.MaxTalkTrackLength
            || Length(input.Transition) > KnowledgePathPresentationLimits.MaxTransitionLength
            || input.TargetSeconds is < KnowledgePathPresentationLimits.MinTargetSeconds or > KnowledgePathPresentationLimits.MaxTargetSeconds
            || ids.Count > KnowledgePathPresentationLimits.MaxObjectionResponses
            || ids.Contains(Guid.Empty))
        {
            return new[]
            {
                KnowledgePathPresentationLimits.Invalid,
                $"Presentation notes: key message ≤ {KnowledgePathPresentationLimits.MaxKeyMessageLength}, talk track ≤ {KnowledgePathPresentationLimits.MaxTalkTrackLength}, target {KnowledgePathPresentationLimits.MinTargetSeconds}–{KnowledgePathPresentationLimits.MaxTargetSeconds} s, transition ≤ {KnowledgePathPresentationLimits.MaxTransitionLength}, at most {KnowledgePathPresentationLimits.MaxObjectionResponses} objection answers."
            };
        }

        if (ids.Count == 0)
        {
            return null;
        }

        var records = objections is null
            ? new Dictionary<Guid, ObjectionResponse>()
            : (await objections.ListByIdsAsync(tenantId, ids, cancellationToken))
                .Where(o => o.TenantId == tenantId && !o.IsDeleted)
                .ToDictionary(o => o.Id);
        var missing = ids.FirstOrDefault(id => !records.ContainsKey(id));
        if (missing != Guid.Empty)
        {
            return new[] { KnowledgePathPresentationLimits.ObjectionNotFound, $"Objection answer {missing} is not one of this tenant's records." };
        }

        var pathProduct = path is null || products is null ? null : await products.ProductOfAsync(tenantId, path, cancellationToken);
        if (pathProduct is null)
        {
            return new[]
            {
                KnowledgePathPresentationLimits.ObjectionInvalid,
                "This path has no product (its chain derives none), so no objection answer can be linked."
            };
        }

        var wrong = ids.Select(id => records[id]).FirstOrDefault(o => !o.IsApproved() || o.ProductId != pathProduct.Value);
        return wrong is null
            ? null
            : new[]
            {
                KnowledgePathPresentationLimits.ObjectionInvalid,
                wrong.IsApproved()
                    ? $"Objection answer {wrong.Id} is about another product than this path."
                    : $"Objection answer {wrong.Id} is not approved ({wrong.Status}); only approved answers are linked."
            };
    }

    /// <summary>The stored form: trimmed text, distinct ids; every field empty ⇒ null (no notes).</summary>
    public static KnowledgePathStepPresentation? Map(KnowledgePathStepPresentationInput? input)
    {
        if (input is null)
        {
            return null;
        }

        var notes = new KnowledgePathStepPresentation
        {
            KeyMessage = Trim(input.KeyMessage),
            TalkTrack = Trim(input.TalkTrack),
            TargetSeconds = input.TargetSeconds,
            Transition = Trim(input.Transition),
            ObjectionResponseIds = (input.ObjectionResponseIds ?? Array.Empty<Guid>()).Distinct().ToList()
        };
        return notes.KeyMessage is null && notes.TalkTrack is null && notes.TargetSeconds is null && notes.Transition is null
               && notes.ObjectionResponseIds.Count == 0
            ? null
            : notes;
    }

    private static int Length(string? value) => value?.Trim().Length ?? 0;

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>
/// WP-VW-W3 (W3-BE-c, K-A4) — the product of a knowledge path: the chain's derived product (the template subject's primary
/// MDM Global Product link — the SAME source the review snapshot freezes). A legacy, chain-less path has none.
/// </summary>
public interface IKnowledgePathProductResolver
{
    Task<Guid?> ProductOfAsync(Guid tenantId, KnowledgePath path, CancellationToken cancellationToken);
}

public sealed class ChainKnowledgePathProductResolver : IKnowledgePathProductResolver
{
    private readonly IChainContextResolver _chains;

    public ChainKnowledgePathProductResolver(IChainContextResolver chains) => _chains = chains;

    public async Task<Guid?> ProductOfAsync(Guid tenantId, KnowledgePath path, CancellationToken cancellationToken)
        => path.ChainTemplate is { } chain
            ? (await _chains.ResolveAsync(tenantId, chain.ConceptChainTemplateId, cancellationToken)).ProductId
            : null;
}
