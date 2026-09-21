using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.VisitFrequencyPolicy.Resolve;
using MediatR;

namespace Diten.CrmService.Application.Features.VisitFrequencyPolicy.Queries;

/// <summary>List policies for the tenant, optionally narrowed by target / status. Archived rows are included by
/// default so history stays visible; pass <paramref name="status"/> to filter.</summary>
public sealed record ListVisitFrequencyPoliciesQuery(
    string? TargetType = null,
    Guid? TargetId = null,
    string? Status = null,
    string? Source = null) : IRequest<Response<VisitFrequencyPolicyListDto>>;

public sealed record GetVisitFrequencyPolicyQuery(Guid PolicyId) : IRequest<Response<VisitFrequencyPolicyDto>>;

/// <summary>WP-FREQ-DET-A read-only detail analysis for one policy: target impact (count + quarterly projection) plus
/// the conflict outcome (FU03 resolve engine REUSED for the policy's own target + context). Never writes.</summary>
public sealed record GetVisitFrequencyPolicyAnalysisQuery(Guid PolicyId)
    : IRequest<Response<VisitFrequencyPolicyAnalysisDto>>;

/// <summary>Read-only resolve query — "how often should this target be visited?". Never writes. Context ids are
/// supplied by the caller; membership/traversal is never computed here.</summary>
public sealed record ResolveVisitFrequencyPolicyQuery(
    string TargetType,
    Guid TargetId,
    DateTimeOffset? EffectiveAt = null,
    string? BusinessUnit = null,
    Guid? TerritoryNodeId = null,
    Guid? CampaignId = null,
    Guid? SegmentId = null,
    Guid? BrandId = null,
    Guid? ProductId = null,
    Guid? ConceptNodeId = null,
    Guid? AudienceProfileId = null,
    bool IncludeDiagnostics = true) : IRequest<Response<VisitFrequencyResolveResult>>;
