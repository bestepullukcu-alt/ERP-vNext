using Diten.BuildingBlocks.CentralAudit;
using Diten.InventoryService.Application.Common.Models;
using MediatR;
using static Diten.InventoryService.Application.Common.InventoryCentralAudit;

namespace Diten.InventoryService.Application.Features.Traceability.Commands;

/// <summary>POST /api/trace/lots (v1 <c>createLot</c> + v1.1 members). Raw request values; the handler owns parsing.</summary>
public sealed record CreateLotCommand : IRequest<Response<LotRefDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => ForLotSerial("Create", "Lot");
    public Guid? CentralAuditEntityId(object? response) => Id(Data<LotRefDto>(response)?.LotId);

    public string? HeaderIdempotencyKey { get; init; }
    public string? IdempotencyKey { get; init; }
    public string? ItemId { get; init; }
    public string? LotNumber { get; init; }
    public string? ManufacturerLot { get; init; }
    public string? SupplierLot { get; init; }
    public string? ManufacturingDate { get; init; }
    public string? ExpiryDate { get; init; }
    public string? RetestDate { get; init; }
    public string? CountryOfOrigin { get; init; }
    public string? SkuId { get; init; }
    public string? SkuLevel { get; init; }
    public string? CoaReference { get; init; }

    /// <summary>TRACE 1.4.0 (MOD-0176 KF-D): the prepared override + signature of dates that differ from the suggestion.</summary>
    public Ports.LotDateOverrideRef? DateOverride { get; init; }
}
