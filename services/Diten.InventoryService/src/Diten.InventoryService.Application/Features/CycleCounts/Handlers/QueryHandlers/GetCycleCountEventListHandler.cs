using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.CycleCounting.Services;
using Diten.InventoryService.Application.Features.CycleCounts.Queries;
using Diten.InventoryService.Domain.CycleCounting.Repositories;
using MediatR;

namespace Diten.InventoryService.Application.Features.CycleCounts.Handlers.QueryHandlers;

/// <summary><c>listCycleCountEvents</c>: the count's insert-only timeline in <c>Seq</c> order (a soft-deleted Draft's timeline is gone with it: 404).</summary>
public sealed class GetCycleCountEventListHandler(ICycleCountRepository repository) : IRequestHandler<GetCycleCountEventListQuery, Response<CycleCountEventPageDto>>
{
    public async Task<Response<CycleCountEventPageDto>> Handle(GetCycleCountEventListQuery request, CancellationToken ct)
    {
        if (CycleCountPaging.Parse(request.Page, request.PageSize) is not { } paging)
            return CycleCountSupport.Invalid<CycleCountEventPageDto>("page must be ≥ 1 and pageSize 1-200.");
        if (await repository.FindAsync(request.CountId, ct) is null) return CycleCountSupport.NotFound<CycleCountEventPageDto>();
        var page = await repository.EventsAsync(request.CountId, paging.Page, paging.PageSize, ct);
        return Response<CycleCountEventPageDto>.Success(new CycleCountEventPageDto(page.Items.Select(CycleCountReads.Event).ToList(), page.Total,
            paging.Page, paging.PageSize));
    }
}
