using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.PlatformAdministrators.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.PlatformAdministrators.Handlers.QueryHandlers;

public sealed class GetPlatformAdministratorByIdHandler
    : IRequestHandler<GetPlatformAdministratorByIdQuery, Response<PlatformAdministratorDetailDto>>
{
    private readonly IPlatformAdministratorRepository _repository;

    public GetPlatformAdministratorByIdHandler(IPlatformAdministratorRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<PlatformAdministratorDetailDto>> Handle(
        GetPlatformAdministratorByIdQuery request,
        CancellationToken ct)
    {
        var administrator = await _repository.GetByIdAsync(request.Id, ct);
        return administrator is null
            ? Response<PlatformAdministratorDetailDto>.Fail("Platform administrator not found.", 404)
            : Response<PlatformAdministratorDetailDto>.Success(PlatformAdministratorMapper.ToDetailDto(administrator));
    }
}
