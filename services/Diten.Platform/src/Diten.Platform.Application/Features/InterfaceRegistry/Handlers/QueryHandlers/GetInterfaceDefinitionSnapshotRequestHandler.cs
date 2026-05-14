using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.InterfaceRegistry.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.InterfaceRegistry.Handlers.QueryHandlers;

public sealed class GetInterfaceDefinitionSnapshotRequestHandler
    : IRequestHandler<GetInterfaceDefinitionSnapshotRequest, Response<InterfaceActiveSnapshotDto>>
{
    private readonly IInterfaceRegistryRepository _repository;

    public GetInterfaceDefinitionSnapshotRequestHandler(IInterfaceRegistryRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<InterfaceActiveSnapshotDto>> Handle(GetInterfaceDefinitionSnapshotRequest request, CancellationToken ct)
    {
        var interfaceCode = InterfaceCodeNormalizer.Normalize(request.InterfaceCode);
        var version = request.Version.Trim().ToLowerInvariant();
        var snapshot = await _repository.GetActiveSnapshotAsync(interfaceCode, version, ct);
        return snapshot is null
            ? Response<InterfaceActiveSnapshotDto>.Fail("Active interface snapshot not found.", 404)
            : Response<InterfaceActiveSnapshotDto>.Success(InterfaceRegistryMapper.ToDto(snapshot));
    }
}
