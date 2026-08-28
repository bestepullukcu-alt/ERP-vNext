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
        return rows
            .Where(t => t.IsActive()
                        && t.IsEffectiveAt(effectiveAt)
                        && t.SegmentBindings.Any(b => b.SegmentId == segmentId))
            .OrderBy(t => t.TemplateCode, StringComparer.OrdinalIgnoreCase)
            .ThenBy(t => t.TemplateVersion)
            .Take(StrategyTemplateLimits.MaxTemplatesPerSegment)
            .Select(t => new StrategyTemplateSummary(
                t.Id, t.TemplateCode, t.TemplateName, t.TemplateStatus, t.TemplateVersion,
                t.EffectiveFrom, t.EffectiveTo))
            .ToList();
    }

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
                    ContainmentVerified: false))
                .ToList(),
            template.ContentBindings
                .OrderBy(c => c.SortOrder).ThenBy(c => c.BindingId)
                .Select(c => new StrategyTemplateContentReference(c.ContentRefType, c.ContentRefId, c.SortOrder))
                .ToList());
}
