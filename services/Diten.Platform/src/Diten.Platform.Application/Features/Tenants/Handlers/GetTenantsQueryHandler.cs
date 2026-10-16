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
    private readonly Diten.Platform.Application.Contracts.IPlatformActorTenantScope _scope;

    public GetTenantsQueryHandler(ITenantRegistryRepository repository, IMapper mapper,
        Diten.Platform.Application.Contracts.IPlatformActorTenantScope scope)
    {
        _repository = repository;
        _mapper = mapper;
        _scope = scope;
    }

    public async Task<PagedResult<TenantListItemDto>> Handle(GetTenantsQuery request, CancellationToken cancellationToken)
    {
        var query = new TenantListQuery(
            request.Search,
            request.Status,
            request.Region,
            request.Page,
            request.PageSize,
            request.Sort,
            // BL-521 — a partner administrator lists only its tenants (and the total counts only them).
            (await _scope.GetAsync(cancellationToken)).QueryFilter);

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
