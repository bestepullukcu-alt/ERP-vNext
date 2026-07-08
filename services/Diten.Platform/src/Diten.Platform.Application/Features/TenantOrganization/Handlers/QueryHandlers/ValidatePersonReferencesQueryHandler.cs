using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.TenantOrganization.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.TenantOrganization.Handlers.QueryHandlers;

public sealed class ValidatePersonReferencesQueryHandler : IRequestHandler<ValidatePersonReferencesQuery, Response<PersonReferenceLookupValidationResponseDto>>
{
    private readonly IPersonReferenceRepository _repository;

    public ValidatePersonReferencesQueryHandler(IPersonReferenceRepository repository) => _repository = repository;

    public async Task<Response<PersonReferenceLookupValidationResponseDto>> Handle(ValidatePersonReferencesQuery request, CancellationToken ct)
    {
        var requestedIds = request.Request.PersonIds.Distinct().ToArray();
        IReadOnlyList<Diten.Platform.Domain.Entities.Organization.PersonReference> references;
        try
        {
            references = await _repository.GetByIdsAsync(requestedIds, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return Response<PersonReferenceLookupValidationResponseDto>.Fail("Person reference repository unavailable.", 503);
        }

        var byId = references.ToDictionary(x => x.Id);

        var results = requestedIds
            .Select(id => byId.TryGetValue(id, out var person)
                ? TenantOrganizationMapper.ToLookupValidationDto(person)
                : new PersonReferenceLookupValidationResultDto(id, false, null, null, null, null))
            .ToList();

        return Response<PersonReferenceLookupValidationResponseDto>.Success(new PersonReferenceLookupValidationResponseDto(results));
    }
}
