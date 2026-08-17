using Diten.Platform.Application.Features.Tenants;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Queries;

public sealed record GetTenantsQuery(
    string? Search = null,
    string? Status = null,
    string? Region = null,
    int Page = 1,
    int PageSize = 20,
    string Sort = "-createdAt") : IRequest<PagedResult<TenantListItemDto>>;
