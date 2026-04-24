using Diten.Platform.Application.Features.Tenants;
using Diten.Platform.Application.Features.Tenants.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Handlers;

public sealed class GetTenantsQueryHandler : IRequestHandler<GetTenantsQuery, PagedResult<TenantListItemDto>>
{
    private readonly ITenantRegistryRepository _repository;

    public GetTenantsQueryHandler(ITenantRegistryRepository repository)
    {
        _repository = repository;
    }

    public async Task<PagedResult<TenantListItemDto>> Handle(GetTenantsQuery request, CancellationToken cancellationToken)
    {
        var query = new TenantListQuery(
            request.Search,
            request.Status,
            request.Region,
            request.Page,
            request.PageSize,
            request.Sort);

        var (items, totalCount) = await _repository.QueryAsync(query, cancellationToken);
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 200);
        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

        return new PagedResult<TenantListItemDto>(
            items.Select(MapListItem).ToList(),
            page,
            pageSize,
            totalCount,
            totalPages);
    }

    private static TenantListItemDto MapListItem(Domain.Entities.Tenant tenant)
    {
        return new TenantListItemDto(
            tenant.Id,
            tenant.Code,
            tenant.Name,
            string.IsNullOrWhiteSpace(tenant.DisplayName) ? tenant.Name : tenant.DisplayName,
            tenant.Domain,
            string.IsNullOrWhiteSpace(tenant.Region) ? "US" : tenant.Region,
            string.IsNullOrWhiteSpace(tenant.Environment) ? "Production" : tenant.Environment,
            tenant.Status.ToString(),
            string.IsNullOrWhiteSpace(tenant.ProvisioningStatus) ? "Queued" : tenant.ProvisioningStatus,
            tenant.CreatedAt,
            tenant.UpdatedAt,
            string.IsNullOrWhiteSpace(tenant.CreatedBy) ? "system" : tenant.CreatedBy);
    }
}
