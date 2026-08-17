using AutoMapper;
using Diten.Platform.Application.Features.Tenants;
using Diten.Platform.Application.Features.Tenants.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Handlers;

public sealed class GetTenantsQueryHandler : IRequestHandler<GetTenantsQuery, PagedResult<TenantListItemDto>>
{
    private readonly ITenantRegistryRepository _repository;
    private readonly IMapper _mapper;

    public GetTenantsQueryHandler(ITenantRegistryRepository repository, IMapper mapper)
    {
        _repository = repository;
        _mapper = mapper;
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
            _mapper.Map<IReadOnlyList<TenantListItemDto>>(items),
            page,
            pageSize,
            totalCount,
            totalPages);
    }
}
