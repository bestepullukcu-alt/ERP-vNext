using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.PersonReferenceDirectory.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.PersonReferenceDirectory.Handlers.QueryHandlers;

public sealed class GetPersonReferenceProjectionListHandler : IRequestHandler<GetPersonReferenceProjectionListQuery, Response<IReadOnlyList<PersonReferenceProjectionListItemDto>>>
{
    private readonly IPersonReferenceDirectoryRepository _repository;

    public GetPersonReferenceProjectionListHandler(IPersonReferenceDirectoryRepository repository) => _repository = repository;

    public async Task<Response<IReadOnlyList<PersonReferenceProjectionListItemDto>>> Handle(GetPersonReferenceProjectionListQuery request, CancellationToken ct)
    {
        var projections = await _repository.GetProjectionsAsync(ct);
        return Response<IReadOnlyList<PersonReferenceProjectionListItemDto>>.Success(
            projections.Select(PersonReferenceDirectoryMapper.ToListItemDto).ToList());
    }
}
