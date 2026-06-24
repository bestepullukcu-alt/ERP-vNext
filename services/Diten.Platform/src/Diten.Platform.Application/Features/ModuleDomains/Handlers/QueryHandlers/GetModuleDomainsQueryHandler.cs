using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.ModuleDomains.Queries;
using Diten.Platform.Application.Features.Tenants;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.ModuleDomains.Handlers.QueryHandlers;

public sealed class GetModuleDomainsQueryHandler
    : IRequestHandler<GetModuleDomainsQuery, Response<PagedResult<ModuleDomainDto>>>
{
    private readonly IModuleDomainRepository _repository;

    public GetModuleDomainsQueryHandler(IModuleDomainRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<PagedResult<ModuleDomainDto>>> Handle(GetModuleDomainsQuery request, CancellationToken ct)
    {
        var query = new ModuleDomainQuery(
            request.Filter.Search,
            request.Filter.IsActive,
            request.Filter.Page,
            request.Filter.PageSize,
            request.Filter.Sort);

        var (items, totalCount) = await _repository.QueryAsync(query, ct);
        return Response<PagedResult<ModuleDomainDto>>.Success(
            ModuleDomainMapper.ToPagedResult(items, request.Filter.Page, request.Filter.PageSize, totalCount));
    }
}
