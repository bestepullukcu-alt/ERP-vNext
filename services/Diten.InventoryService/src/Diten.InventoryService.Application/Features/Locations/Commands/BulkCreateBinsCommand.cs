using Diten.BuildingBlocks.CentralAudit;
using Diten.InventoryService.Application.Common.Models;
using MediatR;
using static Diten.InventoryService.Application.Common.InventoryCentralAudit;

namespace Diten.InventoryService.Application.Features.Locations.Commands;

/// <summary>POST /api/locations/{id}/bins/bulk (v1.1 <c>bulkCreateBins</c>, K-12).</summary>
public sealed record BulkCreateBinsCommand : IRequest<Response<BulkBinsResultDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => ForLedger("Create", "Location");
    public Guid? CentralAuditEntityId(object? response) => Id(LocationId);

    public string? LocationId { get; init; }
    public string? HeaderIdempotencyKey { get; init; }
    public string? IdempotencyKey { get; init; }
    public string? AisleFrom { get; init; }
    public string? AisleTo { get; init; }
    public int? RackFrom { get; init; }
    public int? RackTo { get; init; }
    public int? LevelFrom { get; init; }
    public int? LevelTo { get; init; }
    public int? PositionsPerLevel { get; init; }
    public bool? IsStockable { get; init; }
    public bool? DryRun { get; init; }
}
