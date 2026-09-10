using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.ContentComposition.Eligibility;

/// <summary>Lists eligibility policies for the tenant. Archived rows included by default; <c>effectiveAt</c> filters to
/// policies effective at the instant (in-memory).</summary>
public sealed record ListEligibilityPoliciesQuery(
    string? Status = null,
    DateTimeOffset? EffectiveAt = null,
    string? Search = null,
    bool IncludeArchived = true) : IRequest<Response<EligibilityPolicyListDto>>;

public sealed record GetEligibilityPolicyQuery(Guid EligibilityPolicyId) : IRequest<Response<EligibilityPolicyDto>>;
