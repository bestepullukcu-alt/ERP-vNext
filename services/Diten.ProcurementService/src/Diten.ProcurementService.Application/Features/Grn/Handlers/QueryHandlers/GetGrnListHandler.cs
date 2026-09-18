using Diten.ProcurementService.Application.Features.Grn.Queries;
using Diten.ProcurementService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;
using GoodsReceiptEntity = Diten.ProcurementService.Domain.Entities.GoodsReceipt;

namespace Diten.ProcurementService.Application.Features.Grn.Handlers.QueryHandlers;

/// <summary>GetGrnList (pack §3). Repository Tenant + LegalEntity + IsDeleted=false ile filtreler (sızıntı yok).</summary>
public sealed class GetGrnListHandler : IRequestHandler<GetGrnListQuery, Response<GrnListResultDto>>
{
    private const int PageSize = 50;

    private readonly IGrnRepository _repository;

    public GetGrnListHandler(IGrnRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<GrnListResultDto>> Handle(GetGrnListQuery request, CancellationToken cancellationToken)
    {
        var entities = await _repository.GetAllAsync(request.PoId, request.Status, cancellationToken);

        var ordered = entities
            .OrderBy(x => x.GrnId, StringComparer.Ordinal)
            .ToList();

        IEnumerable<GoodsReceiptEntity> page = ordered;
        if (!string.IsNullOrWhiteSpace(request.Cursor))
        {
            page = ordered.Where(x => string.CompareOrdinal(x.GrnId, request.Cursor) > 0);
        }

        var pageList = page.Take(PageSize + 1).ToList();
        var hasMore = pageList.Count > PageSize;
        var items = pageList.Take(PageSize).Select(GrnMapping.ToDto).ToList();
        var nextCursor = hasMore ? pageList[PageSize - 1].GrnId : null;

        var result = new GrnListResultDto(items, nextCursor, GrnContract.Version);
        return Response<GrnListResultDto>.Success(result);
    }
}
