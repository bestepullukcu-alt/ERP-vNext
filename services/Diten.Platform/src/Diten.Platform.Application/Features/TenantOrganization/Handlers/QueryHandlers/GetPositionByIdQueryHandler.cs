using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.TenantOrganization.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.TenantOrganization.Handlers.QueryHandlers;

public sealed class GetPositionByIdQueryHandler : IRequestHandler<GetPositionByIdQuery, Response<PositionDto>>
{
    private readonly IPositionRepository _repository;

    public GetPositionByIdQueryHandler(IPositionRepository repository) => _repository = repository;

    public async Task<Response<PositionDto>> Handle(GetPositionByIdQuery request, CancellationToken ct)
    {
        var item = await _repository.GetByIdAsync(request.Id, ct);
        return item == null
            ? Response<PositionDto>.Fail("Position not found.", 404)
            : Response<PositionDto>.Success(TenantOrganizationMapper.ToDto(item));
    }
}
