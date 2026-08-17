using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.PersonReferenceDirectory.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.PersonReferenceDirectory.Handlers.QueryHandlers;

public sealed class GetPersonReferenceProjectionByIdHandler : IRequestHandler<GetPersonReferenceProjectionByIdQuery, Response<PersonReferenceProjectionDto>>
{
    private readonly IPersonReferenceDirectoryRepository _repository;

    public GetPersonReferenceProjectionByIdHandler(IPersonReferenceDirectoryRepository repository) => _repository = repository;

    public async Task<Response<PersonReferenceProjectionDto>> Handle(GetPersonReferenceProjectionByIdQuery request, CancellationToken ct)
    {
        var projection = await _repository.GetProjectionByIdAsync(request.Id, ct);
        return projection == null
            ? Response<PersonReferenceProjectionDto>.Fail("Person reference not found.", 404)
            : Response<PersonReferenceProjectionDto>.Success(PersonReferenceDirectoryMapper.ToDto(projection));
    }
}
