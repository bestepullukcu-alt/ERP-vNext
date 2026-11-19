using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.CycleCounting.Services;
using Diten.InventoryService.Application.Features.CycleCounts.Queries;
using Diten.InventoryService.Domain.CycleCounting.Repositories;
using MediatR;

namespace Diten.InventoryService.Application.Features.CycleCounts.Handlers.QueryHandlers;

/// <summary>
/// <c>listCycleCountLines</c>: the count's lines (bin, SKU, lot order). Pack §8.2 rule 5: a blind count's expected quantity is not written before
/// submit, whatever the caller's keys (<see cref="CycleCountReads.ShowsExpected"/>).
/// </summary>
public sealed class GetCycleCountLineListHandler(ICycleCountRepository repository) : IRequestHandler<GetCycleCountLineListQuery, Response<CycleCountLinePageDto>>
{
    public async Task<Response<CycleCountLinePageDto>> Handle(GetCycleCountLineListQuery request, CancellationToken ct)
    {
        if (CycleCountPaging.Parse(request.Page, request.PageSize) is not { } paging)
            return CycleCountSupport.Invalid<CycleCountLinePageDto>("page must be ≥ 1 and pageSize 1-200.");
        var count = await repository.FindAsync(request.CountId, ct);
        if (count is null) return CycleCountSupport.NotFound<CycleCountLinePageDto>();
        var show = CycleCountReads.ShowsExpected(count, request.ReadsVariances);
        var page = await repository.LinesAsync(count.Id, paging.Page, paging.PageSize, ct);
        return Response<CycleCountLinePageDto>.Success(new CycleCountLinePageDto(page.Items.Select(l => CycleCountReads.Line(l, show)).ToList(), page.Total,
            paging.Page, paging.PageSize));
    }
}
