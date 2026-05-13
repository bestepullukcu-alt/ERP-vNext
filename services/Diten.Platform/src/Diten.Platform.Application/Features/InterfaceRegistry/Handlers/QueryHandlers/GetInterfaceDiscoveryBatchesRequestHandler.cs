using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.InterfaceRegistry.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.InterfaceRegistry.Handlers.QueryHandlers;

public sealed class GetInterfaceDiscoveryBatchesRequestHandler
    : IRequestHandler<GetInterfaceDiscoveryBatchesRequest, Response<IReadOnlyList<InterfaceDiscoveryBatchDto>>>
{
    private readonly IInterfaceRegistryRepository _repository;

    public GetInterfaceDiscoveryBatchesRequestHandler(IInterfaceRegistryRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<IReadOnlyList<InterfaceDiscoveryBatchDto>>> Handle(GetInterfaceDiscoveryBatchesRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var batches = await _repository.GetBatchesAsync(ct);
        return Response<IReadOnlyList<InterfaceDiscoveryBatchDto>>.Success(batches.Select(InterfaceRegistryMapper.ToDto).ToList());
    }
}
