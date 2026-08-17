using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.HrisSources.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.HrisSources.Handlers.QueryHandlers;

public sealed class GetHrisExternalIdentifierMapHandler : IRequestHandler<GetHrisExternalIdentifierMapQuery, Response<IReadOnlyList<HrisExternalIdentifierMapDto>>>
{
    private readonly IHrisSourceRepository _repository;

    public GetHrisExternalIdentifierMapHandler(IHrisSourceRepository repository) => _repository = repository;

    public async Task<Response<IReadOnlyList<HrisExternalIdentifierMapDto>>> Handle(GetHrisExternalIdentifierMapQuery request, CancellationToken ct)
    {
        var source = await _repository.GetSourceProfileByIdAsync(request.SourceProfileId, ct);
        if (source == null)
        {
            return Response<IReadOnlyList<HrisExternalIdentifierMapDto>>.Fail("HRIS source not found.", 404);
        }

        var maps = await _repository.GetIdentifierMapsAsync(source.Id, ct);
        return Response<IReadOnlyList<HrisExternalIdentifierMapDto>>.Success(maps.Select(HrisSourceMapper.ToDto).ToList());
    }
}
