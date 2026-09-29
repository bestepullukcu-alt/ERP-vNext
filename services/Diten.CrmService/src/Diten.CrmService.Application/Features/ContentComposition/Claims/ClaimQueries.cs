using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.ContentComposition.Claims;

/// <summary>Lists claims for the tenant. Archived rows included by default; <c>effectiveAt</c> filters to claims
/// effective at the instant (in-memory).</summary>
public sealed record ListClaimsQuery(
    string? Status = null,
    DateTimeOffset? EffectiveAt = null,
    string? Search = null,
    bool IncludeArchived = true,
    bool IncludeCounts = false) : IRequest<Response<ClaimListDto>>;

public sealed record GetClaimQuery(Guid ClaimId) : IRequest<Response<ClaimDto>>;

// ---------------------------------------------------------------- WP-CL-BE-1 (claims v2)

/// <summary>Every country version of the claim's ClaimCode (all core versions), optionally one country.</summary>
public sealed record ListClaimCountryVersionsQuery(
    Guid ClaimId,
    string? CountryCode = null,
    bool IncludeArchived = true) : IRequest<Response<IReadOnlyList<ClaimCountryVersionDto>>>;

public sealed record GetClaimCountryVersionQuery(Guid CountryVersionId) : IRequest<Response<ClaimCountryVersionDto>>;

/// <summary>The claim × country coverage matrix (rows = current record per ClaimCode, columns = COUNTRY_CODES).</summary>
public sealed record GetClaimCoverageQuery(
    Guid? ProductId = null,
    string? Kind = null,
    string? Status = null,
    string? Search = null) : IRequest<Response<ClaimCoverageDto>>;
