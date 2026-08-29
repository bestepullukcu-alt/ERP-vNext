using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.Knowledge.AudienceProfile.Queries;

/// <summary>Lists audience profiles for the tenant. Archived rows are included by default.</summary>
public sealed record ListAudienceProfilesQuery(
    string? Status = null,
    string? ProfileType = null,
    string? Search = null,
    bool IncludeArchived = true) : IRequest<Response<AudienceProfileListDto>>;

public sealed record GetAudienceProfileQuery(Guid AudienceProfileId) : IRequest<Response<AudienceProfileDto>>;
