using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.TenantOrganization.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.TenantOrganization.Handlers.QueryHandlers;

public sealed class GetPersonReferenceByIdQueryHandler : IRequestHandler<GetPersonReferenceByIdQuery, Response<PersonReferenceDto>>
{
    private readonly IPersonReferenceRepository _repository;

    public GetPersonReferenceByIdQueryHandler(IPersonReferenceRepository repository) => _repository = repository;

    public async Task<Response<PersonReferenceDto>> Handle(GetPersonReferenceByIdQuery request, CancellationToken ct)
    {
        try
        {
            var item = await _repository.GetByIdAsync(request.PersonId, ct);
            return item == null
                ? Response<PersonReferenceDto>.Fail("Person reference not found.", 404)
                : Response<PersonReferenceDto>.Success(TenantOrganizationMapper.ToDto(item));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return Response<PersonReferenceDto>.Fail("Person reference repository unavailable.", 503);
        }
    }
}
