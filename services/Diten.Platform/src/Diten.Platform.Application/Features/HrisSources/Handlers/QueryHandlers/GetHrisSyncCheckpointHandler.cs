using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.HrisSources.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.HrisSources.Handlers.QueryHandlers;

public sealed class GetHrisSyncCheckpointHandler : IRequestHandler<GetHrisSyncCheckpointQuery, Response<HrisSyncCheckpointDto>>
{
    private readonly IHrisSourceRepository _repository;

    public GetHrisSyncCheckpointHandler(IHrisSourceRepository repository) => _repository = repository;

    public async Task<Response<HrisSyncCheckpointDto>> Handle(GetHrisSyncCheckpointQuery request, CancellationToken ct)
    {
        var source = await _repository.GetSourceProfileByIdAsync(request.SourceProfileId, ct);
        if (source == null)
        {
            return Response<HrisSyncCheckpointDto>.Fail("HRIS source not found.", 404);
        }

        var checkpoint = await _repository.GetLatestSyncCheckpointAsync(source.Id, ct);
        return checkpoint == null
            ? Response<HrisSyncCheckpointDto>.Fail("HRIS sync checkpoint not found.", 404)
            : Response<HrisSyncCheckpointDto>.Success(HrisSourceMapper.ToDto(checkpoint));
    }
}
