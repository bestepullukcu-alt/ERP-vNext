using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.VisitFrequencyPolicy.Analysis;
using Diten.CrmService.Application.Features.VisitFrequencyPolicy.Queries;
using Diten.CrmService.Application.Features.VisitFrequencyPolicy.Resolve;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;
using Vfp = Diten.CrmService.Domain.Entities.VisitFrequencyPolicy;

namespace Diten.CrmService.Application.Features.VisitFrequencyPolicy.Handlers;

/// <summary>
/// WP-FREQ-DET-A — read-only DETAY ANALİZ handler. It loads the policy (tenant-scoped), computes its target impact via
/// the <see cref="IVisitFrequencyTargetImpactCounter"/> seam, normalises the required-visit count to one quarter, and
/// produces the conflict block by REUSING the FU03 resolve engine (<see cref="IVisitFrequencyPolicyResolver"/>) for the
/// policy's own target + context. It performs NO writes and changes no resolve/CRUD behaviour.
/// </summary>
public sealed class GetVisitFrequencyPolicyAnalysisHandler
    : IRequestHandler<GetVisitFrequencyPolicyAnalysisQuery, Response<VisitFrequencyPolicyAnalysisDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IVisitFrequencyPolicyRepository _repository;
    private readonly IVisitFrequencyPolicyResolver _resolver;
    private readonly IVisitFrequencyTargetImpactCounter _impactCounter;

    public GetVisitFrequencyPolicyAnalysisHandler(
        ITenantContext tenant,
        IVisitFrequencyPolicyRepository repository,
        IVisitFrequencyPolicyResolver resolver,
        IVisitFrequencyTargetImpactCounter impactCounter)
    {
        _tenant = tenant;
        _repository = repository;
        _resolver = resolver;
        _impactCounter = impactCounter;
    }

    public async Task<Response<VisitFrequencyPolicyAnalysisDto>> Handle(
        GetVisitFrequencyPolicyAnalysisQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<VisitFrequencyPolicyAnalysisDto>.Fail("Tenant context is required.", 400);
        }

        var policy = await _repository.GetByIdAsync(tenantId, request.PolicyId, cancellationToken);
        if (policy is null)
        {
            return Response<VisitFrequencyPolicyAnalysisDto>.Fail("Visit frequency policy not found.", 404);
        }

        var now = DateTimeOffset.UtcNow;
        var impact = await BuildImpactAsync(tenantId, policy, now, cancellationToken);
        var conflicts = await BuildConflictsAsync(policy, cancellationToken);

        return Response<VisitFrequencyPolicyAnalysisDto>.Success(
            new VisitFrequencyPolicyAnalysisDto(policy.Id, impact, conflicts));
    }

    private async Task<VisitFrequencyPolicyImpactDto> BuildImpactAsync(
        Guid tenantId, Vfp policy, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var impact = await _impactCounter.CountAsync(tenantId, policy, now, cancellationToken);

        var notes = new List<string>();
        if (!string.IsNullOrWhiteSpace(impact.Note))
        {
            notes.Add(impact.Note!);
        }

        int? plannedVisitsPerQuarter = null;
        if (impact.Computable && impact.Count is { } count)
        {
            var perTargetPerQuarter = QuarterMultiplier(policy.PeriodType);
            if (perTargetPerQuarter is { } multiplier)
            {
                plannedVisitsPerQuarter = count * policy.RequiredVisitCount * multiplier;
            }
            else
            {
                // cycle / campaign-period / custom have no fixed quarter length: report the raw product and say so.
                plannedVisitsPerQuarter = count * policy.RequiredVisitCount;
                notes.Add("dönem bazlı (cycle/campaign-period/custom); çeyreğe normalize edilemedi");
            }
        }

        var note = notes.Count == 0 ? null : string.Join("; ", notes);
        return new VisitFrequencyPolicyImpactDto(
            impact.Count, impact.Computable, plannedVisitsPerQuarter, note);
    }

    /// <summary>Visits-per-quarter multiplier for a normalisable period, or null when the period is not normalisable
    /// (cycle / campaign-period / custom). day ≈ 91 (13 weeks × 7), week = 13, month = 3, quarter = 1.</summary>
    private static int? QuarterMultiplier(string periodType) => FrequencyPeriodType.Normalize(periodType) switch
    {
        FrequencyPeriodType.Quarter => 1,
        FrequencyPeriodType.Month => 3,
        FrequencyPeriodType.Week => 13,
        FrequencyPeriodType.Day => 91,
        _ => null
    };

    private async Task<VisitFrequencyPolicyConflictsDto> BuildConflictsAsync(Vfp policy, CancellationToken cancellationToken)
    {
        // Resolve the policy's OWN target + stored context. EffectiveAt is left null (now) so the block answers "which
        // active policy governs this target right now?". This REUSES the FU03 resolver — no new resolution logic.
        var query = new ResolveVisitFrequencyPolicyQuery(
            policy.TargetType,
            policy.TargetId,
            EffectiveAt: null,
            BusinessUnit: policy.BusinessUnit,
            TerritoryNodeId: policy.TerritoryNodeId,
            CampaignId: policy.CampaignId,
            SegmentId: policy.SegmentId,
            BrandId: policy.BrandId,
            ProductId: policy.ProductId,
            ConceptNodeId: null,
            AudienceProfileId: null,
            IncludeDiagnostics: true);

        var result = await _resolver.ResolveAsync(query, cancellationToken);

        var candidates = result.CandidatePolicies
            .Select(c => new VisitFrequencyPolicyConflictCandidateDto(
                c.PolicyId,
                c.PolicyCode,
                c.PolicyName,
                $"{c.RequiredVisitCount}×/{c.PeriodType}",
                c.Priority,
                c.Selected,
                c.Reason))
            .ToList();

        return new VisitFrequencyPolicyConflictsDto(
            result.SelectedFrequencyPolicyId, result.FrequencyStatus, candidates);
    }
}
