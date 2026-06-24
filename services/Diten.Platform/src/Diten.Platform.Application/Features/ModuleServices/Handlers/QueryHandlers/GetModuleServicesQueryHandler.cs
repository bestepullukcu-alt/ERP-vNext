using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.ModuleServices.Queries;
using Diten.Platform.Application.Features.Tenants;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.ModuleServices.Handlers.QueryHandlers;

public sealed class GetModuleServicesQueryHandler
    : IRequestHandler<GetModuleServicesQuery, Response<PagedResult<ModuleServiceDto>>>
{
    private readonly IModuleServiceRepository _repository;

    public GetModuleServicesQueryHandler(IModuleServiceRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<PagedResult<ModuleServiceDto>>> Handle(GetModuleServicesQuery request, CancellationToken ct)
    {
        var query = new ModuleServiceQuery(
            request.Filter.Search,
            request.Filter.IsActive,
            request.Filter.Page,
            request.Filter.PageSize,
            request.Filter.Sort);

        var (items, totalCount) = await _repository.QueryAsync(query, ct);
        return Response<PagedResult<ModuleServiceDto>>.Success(
            ModuleServiceMapper.ToPagedResult(items, request.Filter.Page, request.Filter.PageSize, totalCount));
    }
}
