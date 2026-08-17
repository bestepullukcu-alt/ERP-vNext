using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.TenantOrganization.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.TenantOrganization.Handlers.QueryHandlers;

public sealed class GetOrganizationUnitsQueryHandler : IRequestHandler<GetOrganizationUnitsQuery, Response<IReadOnlyList<OrganizationUnitDto>>>
{
    private readonly IOrganizationUnitRepository _repository;

    public GetOrganizationUnitsQueryHandler(IOrganizationUnitRepository repository) => _repository = repository;

    public async Task<Response<IReadOnlyList<OrganizationUnitDto>>> Handle(GetOrganizationUnitsQuery request, CancellationToken ct)
    {
        var items = await _repository.GetAllAsync(ct);
        return Response<IReadOnlyList<OrganizationUnitDto>>.Success(items.Select(TenantOrganizationMapper.ToDto).ToList());
    }
}
