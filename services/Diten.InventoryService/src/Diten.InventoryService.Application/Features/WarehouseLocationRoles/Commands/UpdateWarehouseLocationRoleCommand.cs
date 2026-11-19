using Diten.BuildingBlocks.CentralAudit;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Warehouse;
using MediatR;

namespace Diten.InventoryService.Application.Features.WarehouseLocationRoles.Commands;

/// <summary>WMS draft §4 <c>updateLocationRole</c> (If-Match) — roles / accepted conditions / note; the node itself never changes.</summary>
public sealed record UpdateWarehouseLocationRoleCommand(Guid RoleId, UpdateLocationRoleInput Change, string IdempotencyKey, int ExpectedVersion)
    : IRequest<Response<WarehouseLocationRoleDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => WarehouseCentralAudit.For("Update", "WarehouseLocationRole");
    public Guid? CentralAuditEntityId(object? response) => RoleId;
}
