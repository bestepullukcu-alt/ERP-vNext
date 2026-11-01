using Diten.BuildingBlocks.CentralAudit;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Warehouse;
using MediatR;
using static Diten.InventoryService.Application.Common.InventoryCentralAudit;

namespace Diten.InventoryService.Application.Features.WarehouseLocationRoles.Commands;

/// <summary>WMS draft §4 <c>createLocationRole</c> — one record per LOCATION node (pack §4.4); the create key is stored on the record.</summary>
public sealed record CreateWarehouseLocationRoleCommand(CreateLocationRoleInput Role, string IdempotencyKey)
    : IRequest<Response<WarehouseLocationRoleDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => WarehouseCentralAudit.For("Create", "WarehouseLocationRole");
    public Guid? CentralAuditEntityId(object? response) => Id(Data<WarehouseLocationRoleDto>(response)?.RoleId);
}
