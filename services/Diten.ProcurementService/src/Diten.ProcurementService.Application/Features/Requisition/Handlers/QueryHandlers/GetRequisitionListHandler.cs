using Diten.ProcurementService.Application.Features.Requisition.Queries;
using Diten.ProcurementService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;
using RequisitionEntity = Diten.ProcurementService.Domain.Entities.Requisition;

namespace Diten.ProcurementService.Application.Features.Requisition.Handlers.QueryHandlers;

public sealed class GetRequisitionListHandler
    : IRequestHandler<GetRequisitionListQuery, Response<RequisitionListResultDto>>
{
    // Cursor sayfa boyutu (contract listRequisitions cursor). Sabit; policy netleşince config'e taşınır.
    private const int PageSize = 50;

    private readonly IRequisitionRepository _repository;

    public GetRequisitionListHandler(IRequisitionRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<RequisitionListResultDto>> Handle(
        GetRequisitionListQuery request,
        CancellationToken cancellationToken)
    {
        // Repository, Tenant + LegalEntity + IsDeleted=false ile filtreler (cross-LE/tenant sızıntısı yok).
        var entities = await _repository.GetAllAsync(request.Status, cancellationToken);

        // Cursor = son dönen RequisitionId (opaque). Kararlı sıralama RequisitionId.
        var ordered = entities
            .OrderBy(x => x.RequisitionId, StringComparer.Ordinal)
            .ToList();

        IEnumerable<RequisitionEntity> page = ordered;
        if (!string.IsNullOrWhiteSpace(request.Cursor))
        {
            page = ordered.Where(x => string.CompareOrdinal(x.RequisitionId, request.Cursor) > 0);
        }

        var pageList = page.Take(PageSize + 1).ToList();
        var hasMore = pageList.Count > PageSize;
        var items = pageList.Take(PageSize).Select(RequisitionMapping.ToDto).ToList();
        var nextCursor = hasMore ? pageList[PageSize - 1].RequisitionId : null;

        var result = new RequisitionListResultDto(items, nextCursor, RequisitionContract.Version);
        return Response<RequisitionListResultDto>.Success(result);
    }
}
