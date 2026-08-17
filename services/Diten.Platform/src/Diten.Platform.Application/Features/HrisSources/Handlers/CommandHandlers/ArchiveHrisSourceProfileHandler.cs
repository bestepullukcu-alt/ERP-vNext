using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.HrisSources.Commands;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.HrisSources.Handlers.CommandHandlers;

public sealed class ArchiveHrisSourceProfileHandler : IRequestHandler<ArchiveHrisSourceProfileCommand, Response<NoContent>>
{
    private readonly IHrisSourceRepository _repository;

    public ArchiveHrisSourceProfileHandler(IHrisSourceRepository repository) => _repository = repository;

    public async Task<Response<NoContent>> Handle(ArchiveHrisSourceProfileCommand request, CancellationToken ct)
    {
        var entity = await _repository.GetSourceProfileByIdAsync(request.Id, ct);
        if (entity == null)
        {
            return Response<NoContent>.Fail("HRIS source not found.", 404);
        }

        if (await _repository.HasActiveIdentifierMapsAsync(request.Id, ct))
        {
            return Response<NoContent>.Fail("HRIS source has active identifier mappings.", 409);
        }

        var archived = await _repository.ArchiveSourceProfileAsync(request.Id, ct);
        return archived
            ? Response<NoContent>.Success(204)
            : Response<NoContent>.Fail("HRIS source not found.", 404);
    }
}
