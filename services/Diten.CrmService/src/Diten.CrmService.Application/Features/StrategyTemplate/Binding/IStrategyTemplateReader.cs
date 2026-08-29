namespace Diten.CrmService.Application.Features.StrategyTemplate.Binding;

/// <summary>
/// MOD-0167 FU04 — the READ-ONLY consumption seam a future MOD-0155 (MicroTarget) reads. It reports and never writes:
/// no MicroTarget row, no <c>VisitFrequencyPolicy</c>, no <c>CampaignTarget</c>, no cycle line.
/// <para>A consumer goes through this seam so it never needs raw collection access, and so the guarantee "a template
/// produces nothing" lives in one place rather than in every consumer.</para>
/// </summary>
public interface IStrategyTemplateReader
{
    /// <summary>The bindings of an ACTIVE template that is effective at the instant. Returns <c>null</c> for a draft,
    /// an archived or an out-of-window template — a default play is never invented.</summary>
    Task<StrategyTemplateBindingSet?> GetActiveBindingsAsync(
        Guid templateId, DateTimeOffset effectiveAt, CancellationToken cancellationToken);

    /// <summary>The reverse question: which active plays bind this segment at the instant? Bounded by
    /// <c>StrategyTemplateLimits.MaxTemplatesPerSegment</c>.</summary>
    Task<IReadOnlyList<StrategyTemplateSummary>> ListBySegmentAsync(
        Guid segmentId, DateTimeOffset effectiveAt, CancellationToken cancellationToken);
}
