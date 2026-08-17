using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.PersonReferenceDirectory.Commands;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.PersonReferenceDirectory.Handlers.CommandHandlers;

public sealed class ArchivePersonReferenceProjectionHandler : IRequestHandler<ArchivePersonReferenceProjectionCommand, Response<NoContent>>
{
    private readonly IPersonReferenceDirectoryRepository _repository;

    public ArchivePersonReferenceProjectionHandler(IPersonReferenceDirectoryRepository repository) => _repository = repository;

    public async Task<Response<NoContent>> Handle(ArchivePersonReferenceProjectionCommand request, CancellationToken ct)
    {
        var archived = await _repository.ArchiveProjectionAsync(request.Id, ct);
        return archived
            ? Response<NoContent>.Success(204)
            : Response<NoContent>.Fail("Person reference not found.", 404);
    }
}
