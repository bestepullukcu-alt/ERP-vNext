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
/// </summary>
public static class KnowledgePathPresentation
{
    public static async Task<string[]?> ValidateAsync(
        KnowledgePathStepPresentationInput? input, Guid tenantId, IObjectionResponseRepository? objections,
        CancellationToken cancellationToken)
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

        var found = objections is null
            ? new HashSet<Guid>()
            : (await objections.ListByIdsAsync(tenantId, ids, cancellationToken))
                .Where(o => o.TenantId == tenantId && !o.IsDeleted)
                .Select(o => o.Id)
                .ToHashSet();
        var missing = ids.FirstOrDefault(id => !found.Contains(id));
        return missing == Guid.Empty
            ? null
            : new[] { KnowledgePathPresentationLimits.ObjectionNotFound, $"Objection answer {missing} is not one of this tenant's records." };
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
