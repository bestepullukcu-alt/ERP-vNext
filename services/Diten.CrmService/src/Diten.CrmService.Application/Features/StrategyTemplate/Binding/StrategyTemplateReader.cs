using Diten.CrmService.Application.Common;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using TemplateEntity = Diten.CrmService.Domain.Entities.StrategyTemplate;

namespace Diten.CrmService.Application.Features.StrategyTemplate.Binding;

/// <summary>
/// MOD-0167 FU04 — the read-only consumption seam. Every method here is a pure read: it opens no transaction, writes to
/// no collection and calls no other aggregate's write path.
/// <para>An inactive or out-of-window template answers <c>null</c> rather than "its bindings anyway": a consumer asking
/// for the play in force at an instant must not silently receive a draft.</para>
/// </summary>
public sealed class StrategyTemplateReader : IStrategyTemplateReader
{
    private readonly ITenantContext _tenant;
    private readonly IStrategyTemplateRepository _templates;

    public StrategyTemplateReader(ITenantContext tenant, IStrategyTemplateRepository templates)
    {
        _tenant = tenant;
        _templates = templates;
    }

    public async Task<StrategyTemplateBindingSet?> GetActiveBindingsAsync(
        Guid templateId, DateTimeOffset effectiveAt, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return null;
        }

        var template = await _templates.GetByIdAsync(tenantId, templateId, cancellationToken);
        if (template is null || !template.IsActive() || !template.IsEffectiveAt(effectiveAt))
        {
            return null;
        }

        return ToBindingSet(template);
    }

    public async Task<IReadOnlyList<StrategyTemplateSummary>> ListBySegmentAsync(
        Guid segmentId, DateTimeOffset effectiveAt, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Array.Empty<StrategyTemplateSummary>();
        }

        var rows = await _templates.ListAsync(tenantId, cancellationToken);
        var byId = rows.GroupBy(t => t.Id).ToDictionary(g => g.Key, g => g.First());
        var candidates = rows
            .Where(t => t.IsActive()
                        && t.IsEffectiveAt(effectiveAt)
                        && !IsReplacedAt(t, byId, effectiveAt)
                        && t.SegmentBindings.Any(b => b.SegmentId == segmentId))
            .Select(t => new StrategyTemplateSummary(
                t.Id, t.TemplateCode, t.TemplateName, t.TemplateStatus, t.TemplateVersion,
                t.EffectiveFrom, t.EffectiveTo));
        return InPreferenceOrder(candidates)
            .Take(StrategyTemplateLimits.MaxTemplatesPerSegment)
            .ToList();
    }

    /// <summary>
    /// WP-E2E-FIX-3 (E5-B2) — THE rule for "which play is in force" when a play is picked from a segment (never from an
    /// explicit template id): the first of this order wins. Highest <c>TemplateVersion</c> first, then the code and the id
    /// (ordinal) so ties are deterministic. Every consumer that picks a play from segments uses THIS order rather than its
    /// own sort — an ascending sort here was how v1 kept beating v2 on the same segment.
    /// </summary>
    public static IEnumerable<StrategyTemplateSummary> InPreferenceOrder(IEnumerable<StrategyTemplateSummary> plays)
        => plays
            .OrderByDescending(p => p.TemplateVersion)
            .ThenBy(p => p.TemplateCode, StringComparer.Ordinal)
            .ThenBy(p => p.TemplateId);

    /// <summary>
    /// A template is out of force once it is superseded (<see cref="TemplateEntity.IsSuperseded"/>) AND its successor is
    /// itself in force at the instant (active, not archived, effective). A successor that is a draft, archived or not yet
    /// effective takes nothing over: the predecessor stays in force until the successor goes live (activation is what
    /// stamps the mark, so this only matters when the successor is later archived or its window has not started). The
    /// status set is untouched — "superseded" stays a mark, not a status.
    /// </summary>
    public static bool IsReplacedAt(
        TemplateEntity template, IReadOnlyDictionary<Guid, TemplateEntity> byId, DateTimeOffset effectiveAt)
        => template.IsSuperseded()
           && byId.TryGetValue(template.SupersededByTemplateId!.Value, out var successor)
           && successor.IsActive()
           && successor.IsEffectiveAt(effectiveAt);

    /// <summary>Deterministic order everywhere: SortOrder then the child id, never a DateTimeOffset (they are stored as
    /// BSON arrays, and sorting two of them together is the parallel-array trap).</summary>
    public static StrategyTemplateBindingSet ToBindingSet(TemplateEntity template)
        => new(
            template.Id,
            template.TemplateCode,
            template.TemplateName,
            template.SubjectType,
            template.TemplateVersion,
            template.VersionLineageId,
            template.EffectiveFrom,
            template.EffectiveTo,
            template.SegmentBindings
                .OrderBy(b => b.SortOrder).ThenBy(b => b.BindingId)
                .Select(b => b.SegmentId)
                .ToList(),
            new StrategyTemplateFrequencyIntentSnapshot(
                template.FrequencyIntent.Mode,
                template.FrequencyIntent.VisitFrequencyPolicyId,
                template.FrequencyIntent.FrequencyType,
                template.FrequencyIntent.RequiredVisitCount,
                template.FrequencyIntent.PeriodType,
                // Only a policy reference is binding. A declared intent is the author's stated rhythm and MOD-0165
                // neither reads nor honours it — saying otherwise here would be the whole SoR breach this FU avoids.
                Binding: template.FrequencyIntent.IsPolicyReference()),
            template.ProductLines
                .OrderBy(l => l.SortOrder).ThenBy(l => l.LineId)
                .Select(l => new StrategyTemplateProductMixLine(
                    l.LineId,
                    l.GlobalProductId,
                    l.LineWeightPercentage,
                    l.SkuAllocationMode,
                    l.SkuAllocations
                        .OrderBy(a => a.SortOrder).ThenBy(a => a.AllocationId)
                        .Select(a => new StrategyTemplateSkuShare(a.GskuId, a.Percentage, a.SortOrder))
                        .ToList(),
                    StrategyTemplateAllocationRules.TotalOf(l),
                    ContainmentVerified: false,
                    Role: l.EffectiveRole(),
                    JourneyId: l.JourneyId,
                    SortOrder: l.SortOrder,
                    GlobalProductCodeDisplay: l.GlobalProductCodeDisplay))
                .ToList(),
            template.ContentBindings
                .OrderBy(c => c.SortOrder).ThenBy(c => c.BindingId)
                .Select(c => new StrategyTemplateContentReference(c.ContentRefType, c.ContentRefId, c.SortOrder))
                .ToList());
}
