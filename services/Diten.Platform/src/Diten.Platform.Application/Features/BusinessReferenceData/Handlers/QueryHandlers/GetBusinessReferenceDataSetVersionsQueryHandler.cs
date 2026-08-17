using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.BusinessReferenceData.Models;
using Diten.Platform.Application.Features.BusinessReferenceData.Queries;
using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Handlers.QueryHandlers;

public sealed class GetBusinessReferenceDataSetVersionsQueryHandler : IRequestHandler<GetBusinessReferenceDataSetVersionsQuery, Response<BusinessReferenceDataVersionHistoryModel>>
{
    private readonly IBusinessReferenceDataStewardshipRepository _repository;

    public GetBusinessReferenceDataSetVersionsQueryHandler(IBusinessReferenceDataStewardshipRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<BusinessReferenceDataVersionHistoryModel>> Handle(GetBusinessReferenceDataSetVersionsQuery request, CancellationToken ct)
    {
        var set = await _repository.GetSetByIdAsync(request.SetId, ct);
        if (set is null)
        {
            return Response<BusinessReferenceDataVersionHistoryModel>.Fail("not_found", 404);
        }

        var items = await _repository.GetVersionsBySetIdAsync(request.SetId, ct);
        var history = items
            .OrderByDescending(x => x.VersionNumber)
            .Select(x => new BusinessReferenceDataVersionHistoryItemModel(
                x.BusinessReferenceDataVersionId,
                x.BusinessReferenceDataSetId,
                x.VersionNumber,
                x.Status.ToString(),
                x.BusinessReferenceDataGovernanceState.ToString(),
                x.BusinessReferenceDataApprovalState.ToString(),
                x.IsEditable && x.Status == BusinessReferenceDataVersionStatus.Draft && !x.IsImmutable,
                x.IsImmutable,
                x.CreatedAt,
                x.UpdatedAt,
                x.SubmittedAt,
                x.DecisionAt,
                x.PublishedAt,
                x.PublishedBy,
                x.LastEvidenceRef,
                x.SourceVersionId,
                x.SupersededByVersionId))
            .ToList();

        return Response<BusinessReferenceDataVersionHistoryModel>.Success(
            new BusinessReferenceDataVersionHistoryModel(request.SetId, history));
    }
}
