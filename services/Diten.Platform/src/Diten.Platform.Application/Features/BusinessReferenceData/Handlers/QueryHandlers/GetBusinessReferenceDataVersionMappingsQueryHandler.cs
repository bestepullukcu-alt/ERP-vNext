using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.BusinessReferenceData.Models;
using Diten.Platform.Application.Features.BusinessReferenceData.Queries;
using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Handlers.QueryHandlers;

public sealed class GetBusinessReferenceDataVersionMappingsQueryHandler : IRequestHandler<GetBusinessReferenceDataVersionMappingsQuery, Response<BusinessReferenceDataVersionMappingsModel>>
{
    private readonly IBusinessReferenceDataStewardshipRepository _repository;

    public GetBusinessReferenceDataVersionMappingsQueryHandler(IBusinessReferenceDataStewardshipRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<BusinessReferenceDataVersionMappingsModel>> Handle(GetBusinessReferenceDataVersionMappingsQuery request, CancellationToken ct)
    {
        var version = await _repository.GetVersionByIdAsync(request.VersionId, ct);
        if (version is null)
        {
            return Response<BusinessReferenceDataVersionMappingsModel>.Fail("not_found", 404);
        }

        var items = version.Mappings
            .OrderBy(x => x.MappingKey, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.SourceValueCode, StringComparer.OrdinalIgnoreCase)
            .Select(x => new BusinessReferenceDataVersionMappingItemModel(
                x.MappingKey,
                x.SourceValueCode,
                x.TargetCode,
                x.TargetLabel))
            .ToList();

        return Response<BusinessReferenceDataVersionMappingsModel>.Success(new BusinessReferenceDataVersionMappingsModel(
            version.BusinessReferenceDataVersionId,
            version.Status.ToString(),
            version.IsEditable && version.Status == BusinessReferenceDataVersionStatus.Draft && !version.IsImmutable,
            version.ConcurrencyToken,
            items));
    }
}
