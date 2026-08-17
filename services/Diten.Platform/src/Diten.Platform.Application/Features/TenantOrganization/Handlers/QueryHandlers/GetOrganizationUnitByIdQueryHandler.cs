using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.TenantOrganization.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.TenantOrganization.Handlers.QueryHandlers;

public sealed class GetOrganizationUnitByIdQueryHandler : IRequestHandler<GetOrganizationUnitByIdQuery, Response<OrganizationUnitDto>>
{
    private readonly IOrganizationUnitRepository _repository;

    public GetOrganizationUnitByIdQueryHandler(IOrganizationUnitRepository repository) => _repository = repository;

    public async Task<Response<OrganizationUnitDto>> Handle(GetOrganizationUnitByIdQuery request, CancellationToken ct)
    {
        var item = await _repository.GetByIdAsync(request.Id, ct);
        return item == null
            ? Response<OrganizationUnitDto>.Fail("Organization Unit not found.", 404)
            : Response<OrganizationUnitDto>.Success(TenantOrganizationMapper.ToDto(item));
    }
}
