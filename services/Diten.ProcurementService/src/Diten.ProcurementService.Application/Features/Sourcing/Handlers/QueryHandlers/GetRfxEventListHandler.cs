using Diten.ProcurementService.Application.Features.Sourcing.Queries;
using Diten.ProcurementService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.ProcurementService.Application.Features.Sourcing.Handlers.QueryHandlers;

public sealed class GetRfxEventListHandler
    : IRequestHandler<GetRfxEventListQuery, Response<RfxEventListResultDto>>
{
    // Cursor sayfa boyutu (contract listRfxEvents cursor). Sabit; policy netleşince config'e taşınır.
    private const int PageSize = 50;

    private readonly IRfxRepository _repository;

    public GetRfxEventListHandler(IRfxRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<RfxEventListResultDto>> Handle(
        GetRfxEventListQuery request,
        CancellationToken cancellationToken)
    {
        // Repository, Tenant + LegalEntity + IsDeleted=false ile filtreler (cross-LE/tenant sızıntısı yok).
        var entities = await _repository.GetAllAsync(request.Status, cancellationToken);

        // Cursor = son dönen RfxId (opaque). Kararlı sıralama RfxId.
        var ordered = entities
            .OrderBy(x => x.RfxId, StringComparer.Ordinal)
            .ToList();

        IEnumerable<Domain.Entities.RfxEvent> page = ordered;
        if (!string.IsNullOrWhiteSpace(request.Cursor))
        {
            page = ordered.Where(x => string.CompareOrdinal(x.RfxId, request.Cursor) > 0);
        }

        var pageList = page.Take(PageSize + 1).ToList();
        var hasMore = pageList.Count > PageSize;
        var items = pageList.Take(PageSize).Select(SourcingMapping.ToRfxDto).ToList();
        var nextCursor = hasMore ? pageList[PageSize - 1].RfxId : null;

        var result = new RfxEventListResultDto(items, nextCursor, SourcingContract.Version);
        return Response<RfxEventListResultDto>.Success(result);
    }
}
