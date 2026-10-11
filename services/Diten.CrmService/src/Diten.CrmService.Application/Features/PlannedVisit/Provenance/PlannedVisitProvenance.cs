using Diten.CrmService.Application.Common;
using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Application.Features.PlannedVisit.Provenance;

/// <summary>WP-VP-2 — the manual planned-visit create / update share of B-1 + B-3: the derived provenance of one visit
/// and the resource's display name, both decided on the server.</summary>
public static class PlannedVisitProvenance
{
    /// <summary>The derived play (doctor's segments → active play) + the campaign holding the doctor / account on the date.</summary>
    public sealed record Derived(Guid? StrategyTemplateId, Guid? SegmentId, Guid? CampaignId, string? ReasonCode);

    public static async Task<Derived> DeriveAsync(
        IVisitProvenanceDeriver deriver, Guid? contactId, Guid? accountId, DateOnly date, CancellationToken cancellationToken)
    {
        var at = new DateTimeOffset(date.ToDateTime(new TimeOnly(12, 0)), TimeSpan.Zero);
        var play = await deriver.DerivePlayAsync(contactId, at, cancellationToken);
        var campaign = await deriver.DeriveCampaignAsync(contactId, accountId, date, cancellationToken);
        return new Derived(play.StrategyTemplateId, play.SegmentId, campaign, play.ReasonCode);
    }

    public static PlannedVisitSelectionProvenance Selection(Derived derived, string? actor, DateTimeOffset now) => new()
    {
        SegmentId = derived.SegmentId,
        CampaignId = derived.CampaignId,
        StrategyTemplateId = derived.StrategyTemplateId,
        SelectionMode = PlannedVisitSelectionMode.Manual, // FU01 always manual (D11)
        DecidedAt = now,
        DecidedBy = actor
    };

    /// <summary>The resource's name from the user directory (one bulk call); when it cannot be resolved the
    /// <paramref name="fallback"/> (the record's existing name for the same resource) is kept — never a client value.</summary>
    public static async Task<string?> ResourceDisplayNameAsync(
        IUserDisplayNameResolver names, string resourceId, string? fallback, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(resourceId, out var userId))
        {
            return fallback;
        }

        var resolved = await names.ResolveAsync(new[] { userId }, cancellationToken);
        return resolved.TryGetValue(userId, out var name) && !string.IsNullOrWhiteSpace(name) ? name : fallback;
    }
}
