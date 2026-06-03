using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.TenantOrganization.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.TenantOrganization.Handlers.QueryHandlers;

public sealed class GetManagerChainQueryHandler : IRequestHandler<GetManagerChainQuery, Response<ManagerChainDto>>
{
    private readonly IPositionRepository _repository;

    public GetManagerChainQueryHandler(IPositionRepository repository) => _repository = repository;

    public async Task<Response<ManagerChainDto>> Handle(GetManagerChainQuery request, CancellationToken ct)
    {
        var origin = await _repository.GetByIdAsync(request.PositionId, ct);
        if (origin == null || origin.IsArchived)
        {
            return Response<ManagerChainDto>.Fail("Position not found.", 404);
        }

        var chain = new List<ManagerChainNodeDto>();
        var visited = new HashSet<Guid> { origin.Id };
        var cursor = origin.ReportsToPositionId;

        for (var depth = 1; cursor.HasValue && depth <= 32; depth++)
        {
            if (!visited.Add(cursor.Value))
            {
                return Response<ManagerChainDto>.Fail("Manager Chain cycle detected.", 409);
            }

            var manager = await _repository.GetByIdAsync(cursor.Value, ct);
            if (manager == null || manager.IsArchived)
            {
                return Response<ManagerChainDto>.Fail("Manager Chain traversal failed.", 404);
            }

            chain.Add(new ManagerChainNodeDto(manager.Id, manager.Code, manager.Name, manager.ReportsToPositionId, depth));
            cursor = manager.ReportsToPositionId;
        }

        if (cursor.HasValue)
        {
            return Response<ManagerChainDto>.Fail("Manager Chain exceeded maximum depth.", 409);
        }

        return Response<ManagerChainDto>.Success(new ManagerChainDto(origin.Id, chain));
    }
}
