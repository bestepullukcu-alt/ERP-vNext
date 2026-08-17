using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.PersonReferenceDirectory.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.PersonReferenceDirectory.Handlers.QueryHandlers;

public sealed class GetPersonReferenceCorrelationsHandler : IRequestHandler<GetPersonReferenceCorrelationsQuery, Response<IReadOnlyList<PersonReferenceExternalCorrelationDto>>>
{
    private readonly IPersonReferenceDirectoryRepository _repository;

    public GetPersonReferenceCorrelationsHandler(IPersonReferenceDirectoryRepository repository) => _repository = repository;

    public async Task<Response<IReadOnlyList<PersonReferenceExternalCorrelationDto>>> Handle(GetPersonReferenceCorrelationsQuery request, CancellationToken ct)
    {
        var projection = await _repository.GetProjectionByIdAsync(request.ProjectionId, ct);
        if (projection == null)
        {
            return Response<IReadOnlyList<PersonReferenceExternalCorrelationDto>>.Fail("Person reference not found.", 404);
        }

        var correlations = await _repository.GetCorrelationsAsync(projection.Id, ct);
        return Response<IReadOnlyList<PersonReferenceExternalCorrelationDto>>.Success(
            correlations.Select(PersonReferenceDirectoryMapper.ToDto).ToList());
    }
}
