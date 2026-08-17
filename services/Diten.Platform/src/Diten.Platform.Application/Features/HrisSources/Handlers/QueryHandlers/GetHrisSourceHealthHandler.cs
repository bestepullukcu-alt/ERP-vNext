using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.HrisSources.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.HrisSources.Handlers.QueryHandlers;

public sealed class GetHrisSourceHealthHandler : IRequestHandler<GetHrisSourceHealthQuery, Response<HrisSourceHealthDto>>
{
    private readonly IHrisSourceRepository _repository;

    public GetHrisSourceHealthHandler(IHrisSourceRepository repository) => _repository = repository;

    public async Task<Response<HrisSourceHealthDto>> Handle(GetHrisSourceHealthQuery request, CancellationToken ct)
    {
        var source = await _repository.GetSourceProfileByIdAsync(request.SourceProfileId, ct);
        if (source == null)
        {
            return Response<HrisSourceHealthDto>.Fail("HRIS source not found.", 404);
        }

        var health = await _repository.GetLatestHealthSnapshotAsync(source.Id, ct);
        return health == null
            ? Response<HrisSourceHealthDto>.Fail("HRIS source health not found.", 404)
            : Response<HrisSourceHealthDto>.Success(HrisSourceMapper.ToDto(health));
    }
}
