using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.HrisSources.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.HrisSources.Handlers.QueryHandlers;

public sealed class GetHrisSourceProfileByIdHandler : IRequestHandler<GetHrisSourceProfileByIdQuery, Response<HrisSourceProfileDto>>
{
    private readonly IHrisSourceRepository _repository;

    public GetHrisSourceProfileByIdHandler(IHrisSourceRepository repository) => _repository = repository;

    public async Task<Response<HrisSourceProfileDto>> Handle(GetHrisSourceProfileByIdQuery request, CancellationToken ct)
    {
        var profile = await _repository.GetSourceProfileByIdAsync(request.Id, ct);
        return profile == null
            ? Response<HrisSourceProfileDto>.Fail("HRIS source not found.", 404)
            : Response<HrisSourceProfileDto>.Success(HrisSourceMapper.ToDto(profile));
    }
}
