using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.BusinessReferenceData.Models;
using Diten.Platform.Application.Features.BusinessReferenceData.Queries;
using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Handlers.QueryHandlers;

public sealed class GetBusinessReferenceDataVersionAttributeDefinitionsQueryHandler : IRequestHandler<GetBusinessReferenceDataVersionAttributeDefinitionsQuery, Response<BusinessReferenceDataVersionAttributeDefinitionsModel>>
{
    private readonly IBusinessReferenceDataStewardshipRepository _repository;

    public GetBusinessReferenceDataVersionAttributeDefinitionsQueryHandler(IBusinessReferenceDataStewardshipRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<BusinessReferenceDataVersionAttributeDefinitionsModel>> Handle(GetBusinessReferenceDataVersionAttributeDefinitionsQuery request, CancellationToken ct)
    {
        var version = await _repository.GetVersionByIdAsync(request.VersionId, ct);
        if (version is null)
        {
            return Response<BusinessReferenceDataVersionAttributeDefinitionsModel>.Fail("not_found", 404);
        }

        var items = version.AttributeDefinitions
            .OrderBy(x => x.AttributeCode, StringComparer.OrdinalIgnoreCase)
            .Select(x => new BusinessReferenceDataVersionAttributeDefinitionItemModel(
                x.AttributeCode,
                x.DisplayName,
                x.DataType,
                x.IsRequired))
            .ToList();

        return Response<BusinessReferenceDataVersionAttributeDefinitionsModel>.Success(new BusinessReferenceDataVersionAttributeDefinitionsModel(
            version.BusinessReferenceDataVersionId,
            version.Status.ToString(),
            version.IsEditable && version.Status == BusinessReferenceDataVersionStatus.Draft && !version.IsImmutable,
            version.ConcurrencyToken,
            items));
    }
}
