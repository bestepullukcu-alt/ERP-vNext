using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.InterfaceRegistry.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.InterfaceRegistry.Handlers.QueryHandlers;

public sealed class GetInterfaceDiscoveryBatchByIdRequestHandler
    : IRequestHandler<GetInterfaceDiscoveryBatchByIdRequest, Response<InterfaceDiscoveryBatchDto>>
{
    private readonly IInterfaceRegistryRepository _repository;

    public GetInterfaceDiscoveryBatchByIdRequestHandler(IInterfaceRegistryRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<InterfaceDiscoveryBatchDto>> Handle(GetInterfaceDiscoveryBatchByIdRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var batch = await _repository.GetBatchByIdAsync(request.BatchId, ct);
        return batch is null
            ? Response<InterfaceDiscoveryBatchDto>.Fail("Discovery batch not found.", 404)
            : Response<InterfaceDiscoveryBatchDto>.Success(InterfaceRegistryMapper.ToDto(batch));
    }
}
