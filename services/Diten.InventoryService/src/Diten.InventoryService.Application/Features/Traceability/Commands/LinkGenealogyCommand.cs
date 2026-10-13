using Diten.InventoryService.Application.Common.Models;
using MediatR;

namespace Diten.InventoryService.Application.Features.Traceability.Commands;

/// <summary>POST /api/trace/lots/{lotId}/genealogy (v1.1 <c>linkGenealogy</c>; writer MOD-0194, API-only key).</summary>
public sealed record LinkGenealogyCommand : IRequest<Response<GenealogyLinkDto>>
{
    public string? LotId { get; init; }
    public string? HeaderIdempotencyKey { get; init; }
    public string? IdempotencyKey { get; init; }
    public string? ParentLotId { get; init; }
    public string? ChildLotId { get; init; }
    public string? TransformationReference { get; init; }
    public string? SourceModule { get; init; }
    public string? QuantityConsumed { get; init; }
    public string? UomId { get; init; }
}
