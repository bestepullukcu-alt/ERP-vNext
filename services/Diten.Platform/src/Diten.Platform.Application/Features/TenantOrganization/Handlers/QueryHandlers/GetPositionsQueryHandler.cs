using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.TenantOrganization.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.TenantOrganization.Handlers.QueryHandlers;

public sealed class GetPositionsQueryHandler : IRequestHandler<GetPositionsQuery, Response<IReadOnlyList<PositionDto>>>
{
    private readonly IPositionRepository _repository;

    public GetPositionsQueryHandler(IPositionRepository repository) => _repository = repository;

    public async Task<Response<IReadOnlyList<PositionDto>>> Handle(GetPositionsQuery request, CancellationToken ct)
    {
        var items = await _repository.GetAllAsync(ct);
        return Response<IReadOnlyList<PositionDto>>.Success(items.Select(TenantOrganizationMapper.ToDto).ToList());
    }
}
