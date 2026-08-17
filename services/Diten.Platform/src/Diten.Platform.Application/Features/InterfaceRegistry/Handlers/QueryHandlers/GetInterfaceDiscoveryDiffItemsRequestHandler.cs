using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.InterfaceRegistry.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.InterfaceRegistry.Handlers.QueryHandlers;

public sealed class GetInterfaceDiscoveryDiffItemsRequestHandler
    : IRequestHandler<GetInterfaceDiscoveryDiffItemsRequest, Response<IReadOnlyList<InterfaceDiscoveryDiffItemDto>>>
{
    private readonly IInterfaceRegistryRepository _repository;

    public GetInterfaceDiscoveryDiffItemsRequestHandler(IInterfaceRegistryRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<IReadOnlyList<InterfaceDiscoveryDiffItemDto>>> Handle(GetInterfaceDiscoveryDiffItemsRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var batch = await _repository.GetBatchByIdAsync(request.BatchId, ct);
        if (batch is null)
        {
            return Response<IReadOnlyList<InterfaceDiscoveryDiffItemDto>>.Fail("Discovery batch not found.", 404);
        }

        var diffItems = await _repository.GetDiffItemsAsync(request.BatchId, ct);
        return Response<IReadOnlyList<InterfaceDiscoveryDiffItemDto>>.Success(diffItems.Select(InterfaceRegistryMapper.ToDto).ToList());
    }
}
