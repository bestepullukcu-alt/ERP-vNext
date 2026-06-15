using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.BusinessReferenceData.Models;
using Diten.Platform.Application.Features.BusinessReferenceData.Queries;
using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Handlers.QueryHandlers;

public sealed class GetBusinessReferenceDataVersionValuesQueryHandler : IRequestHandler<GetBusinessReferenceDataVersionValuesQuery, Response<BusinessReferenceDataVersionValuesModel>>
{
    private readonly IBusinessReferenceDataStewardshipRepository _repository;

    public GetBusinessReferenceDataVersionValuesQueryHandler(IBusinessReferenceDataStewardshipRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<BusinessReferenceDataVersionValuesModel>> Handle(GetBusinessReferenceDataVersionValuesQuery request, CancellationToken ct)
    {
        var version = await _repository.GetVersionByIdAsync(request.VersionId, ct);
        if (version is null)
        {
            return Response<BusinessReferenceDataVersionValuesModel>.Fail("not_found", 404);
        }

        var items = version.Values
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.ValueCode, StringComparer.OrdinalIgnoreCase)
            .Select(x => new BusinessReferenceDataVersionValueItemModel(
                x.ValueCode,
                x.DisplayName,
                x.Description,
                x.IsActive,
                x.SortOrder,
                x.ParentValueCode,
                x.Attributes))
            .ToList();

        return Response<BusinessReferenceDataVersionValuesModel>.Success(new BusinessReferenceDataVersionValuesModel(
            version.BusinessReferenceDataVersionId,
            version.Status.ToString(),
            version.IsEditable && version.Status == BusinessReferenceDataVersionStatus.Draft && !version.IsImmutable,
            version.ConcurrencyToken,
            items));
    }
}
