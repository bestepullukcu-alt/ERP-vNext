using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.BusinessReferenceData.Models;
using Diten.Platform.Application.Features.BusinessReferenceData.Queries;
using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Handlers.QueryHandlers;

public sealed class GetBusinessReferenceDataSetsQueryHandler : IRequestHandler<GetBusinessReferenceDataSetsQuery, Response<BusinessReferenceDataSetListModel>>
{
    private readonly IBusinessReferenceDataStewardshipRepository _repository;

    public GetBusinessReferenceDataSetsQueryHandler(IBusinessReferenceDataStewardshipRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<BusinessReferenceDataSetListModel>> Handle(GetBusinessReferenceDataSetsQuery request, CancellationToken ct)
    {
        var (items, totalCount) = await _repository.QuerySetsAsync(
            new BusinessReferenceDataSetListQuery(
                request.Search,
                request.Status,
                request.ScopeType,
                request.Page,
                request.PageSize,
                request.Sort,
                CatalogGovernedOnly: false),
            ct);

        // Resolve the governance/approval summary from the version the user is most likely to act on:
        // the published version when one exists, otherwise the active draft. Versions are fetched in a
        // single batched read to avoid an N+1 per-set query.
        var governanceVersionIds = items
            .Select(x => x.PublishedVersionId ?? x.ActiveDraftVersionId)
            .Where(id => id is not null && id.Value != Guid.Empty)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        var versionsById = governanceVersionIds.Count == 0
            ? new Dictionary<Guid, BusinessReferenceDataVersion>()
            : (await _repository.GetVersionsByIdsAsync(governanceVersionIds, ct))
                .ToDictionary(v => v.BusinessReferenceDataVersionId);

        var models = items
            .Select(x =>
            {
                var governanceVersionId = x.PublishedVersionId ?? x.ActiveDraftVersionId;
                var governanceStatus = "NotConfigured";
                var approvalStatus = "NotStarted";
                if (governanceVersionId is not null
                    && versionsById.TryGetValue(governanceVersionId.Value, out var version))
                {
                    var (governance, approval) = BusinessReferenceDataModelMapper.NormalizeGovernance(version);
                    governanceStatus = governance.ToString();
                    approvalStatus = approval.ToString();
                }

                return new BusinessReferenceDataSetListItemModel(
                    x.BusinessReferenceDataSetId,
                    x.SetCode,
                    x.Name,
                    x.ScopeType,
                    x.Status.ToString(),
                    x.CreatedAt,
                    x.UpdatedAt,
                    x.RowVersion,
                    x.ActiveDraftVersionId,
                    x.PublishedVersionId,
                    governanceStatus,
                    approvalStatus);
            })
            .ToList();

        var totalPages = (int)Math.Ceiling(totalCount / (double)Math.Max(request.PageSize, 1));
        return Response<BusinessReferenceDataSetListModel>.Success(
            new BusinessReferenceDataSetListModel(models, request.Page, request.PageSize, totalCount, totalPages));
    }
}
