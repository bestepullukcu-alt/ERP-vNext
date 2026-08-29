using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.StrategyTemplate.Binding;
using Diten.CrmService.Application.Features.StrategyTemplate.Queries;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.StrategyTemplate.Handlers.QueryHandlers;

/// <summary>
/// The read-only binding view. It reports what the play binds and adds DERIVED, never-persisted freshness hints: has a
/// bound segment been superseded or archived, is the referenced policy still active, is a bound content row still
/// published.
/// <para><b>Hints are warnings, not blocks.</b> An active play whose content was later archived does not become
/// invalid — the past must stay explainable, so the UI shows a badge and nothing is deleted or rewritten.</para>
/// <para>Every read here is a plain <c>GetByIdAsync</c> against a foreign aggregate. Nothing is mutated, nothing is
/// resolved, and no member, member count or subject id appears anywhere in the response: reading a play must never
/// imply the right to see the people inside its segments.</para>
/// </summary>
public sealed class GetStrategyTemplateBindingsHandler
    : IRequestHandler<GetStrategyTemplateBindingsQuery, Response<StrategyTemplateBindingsDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IStrategyTemplateRepository _templates;
    private readonly ISegmentRepository _segments;
    private readonly IVisitFrequencyPolicyRepository _policies;
    private readonly IKnowledgePathRepository _paths;
    private readonly IContentEngagementJourneyRepository _journeys;

    public GetStrategyTemplateBindingsHandler(
        ITenantContext tenant,
        IStrategyTemplateRepository templates,
        ISegmentRepository segments,
        IVisitFrequencyPolicyRepository policies,
        IKnowledgePathRepository paths,
        IContentEngagementJourneyRepository journeys)
    {
        _tenant = tenant;
        _templates = templates;
        _segments = segments;
        _policies = policies;
        _paths = paths;
        _journeys = journeys;
    }

    public async Task<Response<StrategyTemplateBindingsDto>> Handle(
        GetStrategyTemplateBindingsQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<StrategyTemplateBindingsDto>.Fail("Tenant context is required.", 400);
        }

        var template = await _templates.GetByIdAsync(tenantId, request.TemplateId, cancellationToken);
        if (template is null)
        {
            return Response<StrategyTemplateBindingsDto>.Fail("Strategy template not found.", 404);
        }

        var effectiveAt = request.EffectiveAt ?? DateTimeOffset.UtcNow;

        var segmentViews = new List<StrategyTemplateSegmentBindingViewDto>();
        foreach (var binding in StrategyTemplateMapper.OrderedSegmentBindings(template))
        {
            var segment = await _segments.GetByIdAsync(tenantId, binding.SegmentId, cancellationToken);
            segmentViews.Add(new StrategyTemplateSegmentBindingViewDto(
                binding.BindingId,
                binding.SegmentId,
                binding.SegmentLineageId,
                binding.SegmentVersionAtBinding,
                binding.SegmentCodeDisplay,
                binding.BindingRole,
                segment?.SegmentStatus,
                segment?.IsSuperseded() ?? false,
                segment?.IsArchived() ?? false,
                // "Resolvable" only says the population can still be asked for; it never asks.
                Resolvable: segment is not null && !segment.IsArchived(),
                binding.SortOrder));
        }

        var intent = template.FrequencyIntent;
        string? policyStatus = null;
        bool? targetMatches = null;
        if (intent.IsPolicyReference() && intent.VisitFrequencyPolicyId is { } policyId)
        {
            var policy = await _policies.GetByIdAsync(tenantId, policyId, cancellationToken);
            policyStatus = policy?.Status;
            if (policy is not null
                && string.Equals(policy.TargetType, FrequencyTargetType.Segment, StringComparison.Ordinal))
            {
                targetMatches = template.SegmentBindings.Any(b => b.SegmentId == policy.TargetId);
            }
        }

        var contentViews = new List<StrategyTemplateContentBindingViewDto>();
        foreach (var binding in StrategyTemplateMapper.OrderedContentBindings(template))
        {
            string? status = null;
            var archived = false;
            var published = false;

            if (binding.IsKnowledgePath())
            {
                var path = await _paths.GetByIdAsync(tenantId, binding.ContentRefId, cancellationToken);
                status = path?.PathStatus;
                archived = path?.IsArchived() ?? false;
                published = path?.IsPublished() ?? false;
            }
            else
            {
                var journey = await _journeys.GetByIdAsync(tenantId, binding.ContentRefId, cancellationToken);
                status = journey?.JourneyStatus;
                archived = journey?.IsArchived() ?? false;
                published = journey?.IsPublished() ?? false;
            }

            contentViews.Add(new StrategyTemplateContentBindingViewDto(
                binding.BindingId, binding.ContentRefType, binding.ContentRefId, binding.ContentCodeDisplay,
                binding.ContentVersionAtBinding, status, archived, published, binding.SortOrder));
        }

        var dto = new StrategyTemplateBindingsDto(
            template.Id,
            template.TemplateCode,
            template.TemplateStatus,
            template.TemplateVersion,
            template.IsEffectiveAt(effectiveAt),
            effectiveAt,
            segmentViews,
            new StrategyTemplateFrequencyIntentViewDto(
                intent.Mode,
                intent.VisitFrequencyPolicyId,
                intent.PolicyCodeDisplay,
                policyStatus,
                targetMatches,
                intent.FrequencyType,
                intent.RequiredVisitCount,
                intent.PeriodType,
                intent.IntentNote,
                // Only a policy reference binds. A declared rhythm is documentation: MOD-0165 does not read it.
                Binding: intent.IsPolicyReference()),
            StrategyTemplateMapper.OrderedProductLines(template)
                .Select(line => new StrategyTemplateProductLineViewDto(
                    line.LineId,
                    line.GlobalProductId,
                    line.GlobalProductCodeDisplay,
                    line.LineWeightPercentage,
                    line.SkuAllocationMode,
                    line.SkuAllocations
                        .OrderBy(a => a.SortOrder).ThenBy(a => a.AllocationId)
                        .Select(StrategyTemplateMapper.ToDto)
                        .ToList(),
                    StrategyTemplateAllocationRules.TotalOf(line),
                    // ALWAYS false: MDM's Gsku carries no GlobalProductId and this FU may not open a new read surface,
                    // so containment is the author's word. Saying "verified" here would be a lie with consequences.
                    ContainmentVerified: false,
                    line.SortOrder))
                .ToList(),
            contentViews);

        return Response<StrategyTemplateBindingsDto>.Success(dto);
    }
}
