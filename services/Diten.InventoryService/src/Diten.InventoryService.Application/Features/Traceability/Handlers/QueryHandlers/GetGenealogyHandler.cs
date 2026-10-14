using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Traceability.Queries;
using Diten.InventoryService.Application.Features.Traceability.Services;
using Diten.InventoryService.Domain.Traceability.Repositories;
using MediatR;

namespace Diten.InventoryService.Application.Features.Traceability.Handlers.QueryHandlers;

/// <summary>
/// getGenealogy (TRACE v1 + v1.1 <c>depth</c> 1–2): the links into the lot (parents) and out of it (children); with
/// depth 2 also the links of those parents' parents and children's children. Every item names both ends of its link, so a
/// depth-2 item is unambiguous (pack §11.3: two levels each way; MOD-0177 reads it for recall).
/// </summary>
public sealed class GetGenealogyHandler : IRequestHandler<GetGenealogyQuery, Response<GenealogyDto>>
{
    private readonly ILotRepository _lots;

    public GetGenealogyHandler(ILotRepository lots) => _lots = lots;

    public async Task<Response<GenealogyDto>> Handle(GetGenealogyQuery request, CancellationToken cancellationToken)
    {
        var depth = request.Depth switch
        {
            null or "1" => 1,
            "2" => 2,
            _ => 0
        };
        if (depth == 0) return TraceWriteSupport.Invalid<GenealogyDto>("depth must be 1 or 2.");
        if (!TraceWriteSupport.TryParseId(request.LotId, out var lotId) || await _lots.FindAsync(lotId, cancellationToken) is null)
            return TraceWriteSupport.UnknownLot<GenealogyDto>();

        var parents = (await _lots.ParentLinksAsync([lotId], cancellationToken)).ToList();
        var children = (await _lots.ChildLinksAsync([lotId], cancellationToken)).ToList();
        if (depth == 2)
        {
            parents.AddRange(await _lots.ParentLinksAsync(parents.Select(l => l.ParentLotId).Distinct().ToList(), cancellationToken));
            children.AddRange(await _lots.ChildLinksAsync(children.Select(l => l.ChildLotId).Distinct().ToList(), cancellationToken));
        }

        return Response<GenealogyDto>.Success(new GenealogyDto
        {
            LotId = TraceMapping.Id(lotId),
            Parents = parents.DistinctBy(l => l.Id).Select(TraceMapping.ToItem).ToList(),
            Children = children.DistinctBy(l => l.Id).Select(TraceMapping.ToItem).ToList()
        });
    }
}
