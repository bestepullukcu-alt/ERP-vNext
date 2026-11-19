using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.CycleCounting.Services;
using Diten.InventoryService.Application.Features.CycleCounts.Queries;
using Diten.InventoryService.Domain.CycleCounting.Repositories;
using MediatR;

using Diten.InventoryService.Domain.CycleCounting.Enums;

namespace Diten.InventoryService.Application.Features.CycleCounts.Handlers.QueryHandlers;

/// <summary><c>listCycleCounts</c> (draft §4.1): the caller's company only, soft-deleted counts absent, newest first; a bad parameter is 400.</summary>
public sealed class GetCycleCountListHandler(ICycleCountRepository repository) : IRequestHandler<GetCycleCountListQuery, Response<CycleCountPageDto>>
{
    public async Task<Response<CycleCountPageDto>> Handle(GetCycleCountListQuery request, CancellationToken ct)
    {
        if (CycleCountPaging.Parse(request.Page, request.PageSize) is not { } paging)
            return CycleCountSupport.Invalid<CycleCountPageDto>("page must be ≥ 1 and pageSize 1-200.");
        var statuses = new List<CycleCountStatus>();
        foreach (var raw in request.Status)
        {
            if (!Enum.TryParse<CycleCountStatus>(raw, false, out var s) || !Enum.IsDefined(s) || raw != s.ToString())
                return CycleCountSupport.Invalid<CycleCountPageDto>("status must be one of " + string.Join(", ", Enum.GetNames<CycleCountStatus>()) + ".");
            statuses.Add(s);
        }

        Guid? warehouse = null;
        if (request.WarehouseId is not null)
        {
            if (!CycleCountSupport.TryParseId(request.WarehouseId, out var w)) return CycleCountSupport.Invalid<CycleCountPageDto>("warehouseId must be a canonical UUID.");
            warehouse = w;
        }

        var page = await repository.ListAsync(new CycleCountQuery(statuses, warehouse, CycleCountSupport.Trimmed(request.Q), paging.Page, paging.PageSize), ct);
        return Response<CycleCountPageDto>.Success(new CycleCountPageDto(page.Items.Select(CycleCountReads.Dto).ToList(), page.Total, paging.Page, paging.PageSize));
    }
}
