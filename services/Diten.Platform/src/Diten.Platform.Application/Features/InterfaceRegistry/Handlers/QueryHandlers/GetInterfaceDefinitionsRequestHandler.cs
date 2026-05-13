using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.InterfaceRegistry.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.InterfaceRegistry.Handlers.QueryHandlers;

public sealed class GetInterfaceDefinitionsRequestHandler
    : IRequestHandler<GetInterfaceDefinitionsRequest, Response<IReadOnlyList<InterfaceActiveSnapshotDto>>>
{
    private readonly IInterfaceRegistryRepository _repository;

    public GetInterfaceDefinitionsRequestHandler(IInterfaceRegistryRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<IReadOnlyList<InterfaceActiveSnapshotDto>>> Handle(GetInterfaceDefinitionsRequest request, CancellationToken ct)
    {
        var snapshots = await _repository.GetActiveSnapshotsAsync(ct);
        var result = snapshots
            .OrderBy(x => x.InterfaceCode, StringComparer.Ordinal)
            .ThenBy(x => x.InterfaceVersion, StringComparer.Ordinal)
            .Select(InterfaceRegistryMapper.ToDto)
            .ToList();

        return Response<IReadOnlyList<InterfaceActiveSnapshotDto>>.Success(result);
    }
}
