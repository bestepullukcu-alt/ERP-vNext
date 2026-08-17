using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.HrisSources.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.HrisSources.Handlers.QueryHandlers;

public sealed class GetHrisSourceProfileListHandler : IRequestHandler<GetHrisSourceProfileListQuery, Response<IReadOnlyList<HrisSourceProfileListItemDto>>>
{
    private readonly IHrisSourceRepository _repository;

    public GetHrisSourceProfileListHandler(IHrisSourceRepository repository) => _repository = repository;

    public async Task<Response<IReadOnlyList<HrisSourceProfileListItemDto>>> Handle(GetHrisSourceProfileListQuery request, CancellationToken ct)
    {
        var profiles = await _repository.GetSourceProfilesAsync(ct);
        return Response<IReadOnlyList<HrisSourceProfileListItemDto>>.Success(profiles.Select(HrisSourceMapper.ToListItemDto).ToList());
    }
}
