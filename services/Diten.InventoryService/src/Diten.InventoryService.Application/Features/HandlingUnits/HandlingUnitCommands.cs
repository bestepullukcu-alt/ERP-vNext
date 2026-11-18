using Diten.BuildingBlocks.CentralAudit;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.OutboundShipments;
using Diten.InventoryService.Application.Features.Warehouse;
using MediatR;
using static Diten.InventoryService.Application.Common.InventoryCentralAudit;

namespace Diten.InventoryService.Application.Features.HandlingUnits;

// MOD-0178 DL-5 — packing (pack §8.20; WMS draft §11 createPackage / updatePackage / closePackage / listPackages). Packing writes NO stock
// movement and calls INVENTORY not at all (the stock stays on Staging, AVAILABLE and reserved).

/// <summary><c>createPackage</c> — only on a <c>Picked</c> outbound; the content Σ (open + closed) per allocation ≤ its picked quantity.</summary>
public sealed record CreateHandlingUnitCommand(Guid OutboundId, PackageInput Package, string IdempotencyKey)
    : IRequest<Response<PackageDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => WarehouseCentralAudit.For("Create", "HandlingUnit");
    public Guid? CentralAuditEntityId(object? response) => Id(Data<PackageDto>(response)?.PackageId);
}

/// <summary><c>updatePackage</c> — an <c>Open</c> package's content / weight / dimensions, If-Match.</summary>
public sealed record UpdateHandlingUnitCommand(Guid PackageId, PackageInput Package, string IdempotencyKey, int ExpectedVersion)
    : IRequest<Response<PackageDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => WarehouseCentralAudit.For("Update", "HandlingUnit");
    public Guid? CentralAuditEntityId(object? response) => PackageId;
}

/// <summary><c>closePackage</c> — at least one content line; the last close that fills every allocation makes the outbound <c>Packed</c>.</summary>
public sealed record CloseHandlingUnitCommand(Guid PackageId, string IdempotencyKey, int ExpectedVersion)
    : IRequest<Response<PackageDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => WarehouseCentralAudit.For("Update", "HandlingUnit");
    public Guid? CentralAuditEntityId(object? response) => PackageId;
}

/// <summary><c>listPackages</c> — the Packing page's read (no ship-to, Q-10a).</summary>
public sealed record GetHandlingUnitListQuery(Guid OutboundId) : IRequest<Response<PackingViewDto>>;
