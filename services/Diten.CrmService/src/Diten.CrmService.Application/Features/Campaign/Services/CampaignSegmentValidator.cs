using Diten.CrmService.Application.Features.Campaign.Read;
using Diten.CrmService.Application.Features.Campaign.Rules;
using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Application.Features.Campaign.Services;

/// <summary>
/// MOD-0165 FU10 — the campaign write path's targeting gate, in ONE place so create and update cannot drift.
///
/// <para>It decides three things, in this order, and stops at the first refusal:</para>
/// <list type="number">
/// <item><description>the MODE is one of the two known values (pure);</description></item>
/// <item><description>the structural rules of the set — no duplicate, within the published ceiling, and at least one
/// segment while the mode is <c>segment</c> (pure, no I/O);</description></item>
/// <item><description>the segments being ADDED exist and are ACTIVE (one batch read).</description></item>
/// </list>
///
/// <para><b>The "at least one" rule fires on EVERY write, the existence rule only on CHANGE, and that asymmetry is
/// the design.</b> If the minimum were only checked when the set changed, an author could select <c>segment</c> mode,
/// save, then empty the list in a later write and keep a segment-targeted campaign with no segments. If existence
/// were checked on every write, a campaign whose segment was archived months later would become uneditable — nobody
/// could even fix its description. So the shape of the set is always checked; the world outside it is checked only
/// where the author actually touched it.</para>
///
/// <para><b>Read-only and in-process.</b> It holds the narrow <see cref="ICampaignSegmentCatalog"/> and nothing else:
/// no repository, no <c>HttpClient</c>. It never writes a segment.</para>
/// </summary>
public sealed class CampaignSegmentValidator
{
    private readonly ICampaignSegmentCatalog _segments;

    public CampaignSegmentValidator(ICampaignSegmentCatalog segments)
    {
        _segments = segments;
    }

    /// <summary>The accepted mode and segment set, or the failure the handler answers with.</summary>
    public sealed record Result(
        string? TargetingMode,
        IReadOnlyList<Guid>? SegmentIds,
        CampaignScopeRules.Failure? Failure);

    /// <param name="requestedMode">The mode the caller wants. Absent means the pre-FU10 shape and derives to manual.</param>
    /// <param name="requestedSegmentIds">The full replacement set. Null is read as "no segments".</param>
    /// <param name="current">The stored campaign on update, or null on create — used ONLY to tell which segments are
    /// newly added, never to widen what is accepted.</param>
    public async Task<Result> ValidateAsync(
        string? requestedMode,
        IReadOnlyList<Guid>? requestedSegmentIds,
        Domain.Entities.Campaign? current,
        CancellationToken cancellationToken)
    {
        // 1 — the mode itself.
        var mode = CampaignTargetingModes.Normalize(requestedMode);
        if (mode.Length == 0)
        {
            // Pre-FU10 shape: no mode supplied. Keep whatever the row effectively has, which for a legacy row is
            // manual — never silently promote a campaign into segment targeting.
            mode = current?.EffectiveTargetingMode() ?? CampaignTargetingModes.Manual;
        }

        if (!CampaignTargetingModes.IsKnown(mode))
        {
            return Failure(
                $"Unknown TargetingMode '{requestedMode}'. Known values: "
                + $"{string.Join(", ", CampaignTargetingModes.All)}.",
                CampaignReasonCodes.CampaignTargetingModeUnknown);
        }

        var ids = (requestedSegmentIds ?? Array.Empty<Guid>())
            .Where(id => id != Guid.Empty)
            .ToList();

        // 2 — structural rules. These need no I/O, so they run before anything is read.
        if (ids.Count != ids.Distinct().Count())
        {
            return Failure(
                "The same segment was supplied more than once. Duplicates are refused rather than merged, so the "
                + "targeted set always says exactly what the author chose.",
                CampaignReasonCodes.CampaignSegmentDuplicate);
        }

        if (ids.Count > CampaignLimits.MaxTargetedSegments)
        {
            return Failure(
                $"A campaign may target at most {CampaignLimits.MaxTargetedSegments} segments; {ids.Count} were "
                + "supplied.",
                CampaignReasonCodes.CampaignSegmentLimitExceeded);
        }

        var isSegmentMode = string.Equals(mode, CampaignTargetingModes.Segment, StringComparison.Ordinal);
        if (isSegmentMode && ids.Count == 0)
        {
            return Failure(
                "A campaign targeted by segment must name at least one segment. Switch it to manual targeting if the "
                + "audience is hand-authored instead.",
                CampaignReasonCodes.CampaignSegmentRequired);
        }

        // 3 — existence and status, for the ADDED ids only.
        var added = AddedIds(current, ids);
        if (added.Count > 0)
        {
            var found = await _segments.GetByIdsAsync(added, cancellationToken);
            var byId = found.ToDictionary(s => s.SegmentId);

            foreach (var id in added)
            {
                if (!byId.TryGetValue(id, out var segment))
                {
                    return Failure(
                        $"Segment '{id}' was not found in this tenant. The campaign was not saved.",
                        CampaignReasonCodes.CampaignSegmentNotFound);
                }

                if (!string.Equals(segment.SegmentStatus, SegmentStatuses.Active, StringComparison.Ordinal))
                {
                    return Failure(
                        $"Segment '{segment.SegmentCode}' is {segment.SegmentStatus}; only an active segment can be "
                        + "added to a campaign. A segment archived after it was linked keeps its link.",
                        CampaignReasonCodes.CampaignSegmentNotActive);
                }
            }
        }

        return new Result(mode, ids, null);
    }

    /// <summary>
    /// Which ids the author is ADDING. On create every id is new. On update only the ones that were not already
    /// linked — which is what lets a campaign carrying a since-archived segment stay editable.
    /// <para>The passive mode's stored set is NOT consulted here: switching to manual leaves the segments dormant,
    /// and switching back must not suddenly re-validate a set the author never touched.</para>
    /// </summary>
    private static IReadOnlyList<Guid> AddedIds(Domain.Entities.Campaign? current, IReadOnlyList<Guid> requested)
    {
        if (current is null)
        {
            return requested;
        }

        var existing = current.TargetedSegments.Select(s => s.SegmentId).ToHashSet();
        return requested.Where(id => !existing.Contains(id)).ToList();
    }

    private static Result Failure(string error, string reasonCode)
        => new(null, null, new CampaignScopeRules.Failure(error, reasonCode));
}
