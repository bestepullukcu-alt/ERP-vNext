using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.ContentComposition.Claims;

/// <summary>Lists claims for the tenant. Archived rows included by default; <c>effectiveAt</c> filters to claims
/// effective at the instant (in-memory).</summary>
public sealed record ListClaimsQuery(
    string? Status = null,
    DateTimeOffset? EffectiveAt = null,
    string? Search = null,
    bool IncludeArchived = true) : IRequest<Response<ClaimListDto>>;

public sealed record GetClaimQuery(Guid ClaimId) : IRequest<Response<ClaimDto>>;
