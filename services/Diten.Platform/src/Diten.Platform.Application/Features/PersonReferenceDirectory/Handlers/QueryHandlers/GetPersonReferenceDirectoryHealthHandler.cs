using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.PersonReferenceDirectory.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.PersonReferenceDirectory.Handlers.QueryHandlers;

public sealed class GetPersonReferenceDirectoryHealthHandler : IRequestHandler<GetPersonReferenceDirectoryHealthQuery, Response<PersonReferenceDirectoryHealthDto>>
{
    private readonly IPersonReferenceDirectoryRepository _repository;

    public GetPersonReferenceDirectoryHealthHandler(IPersonReferenceDirectoryRepository repository) => _repository = repository;

    public async Task<Response<PersonReferenceDirectoryHealthDto>> Handle(GetPersonReferenceDirectoryHealthQuery request, CancellationToken ct)
    {
        var health = await _repository.GetLatestHealthSnapshotAsync(ct);
        return health == null
            ? Response<PersonReferenceDirectoryHealthDto>.Fail("Person reference directory health not found.", 404)
            : Response<PersonReferenceDirectoryHealthDto>.Success(PersonReferenceDirectoryMapper.ToDto(health));
    }
}
