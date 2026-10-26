using Diten.BuildingBlocks.CentralAudit;
using Diten.InventoryService.Application.Common.Models;
using MediatR;
using static Diten.InventoryService.Application.Common.InventoryCentralAudit;

namespace Diten.InventoryService.Application.Features.Traceability.Commands;

/// <summary>POST /api/trace/lots/{lotId}/genealogy (v1.1 <c>linkGenealogy</c>; writer MOD-0194, API-only key).</summary>
public sealed record LinkGenealogyCommand : IRequest<Response<GenealogyLinkDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => ForLotSerial("Create", "GenealogyLink");
    public Guid? CentralAuditEntityId(object? response) => Id(Data<GenealogyLinkDto>(response)?.LinkId);

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
