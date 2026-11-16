using Diten.BuildingBlocks.CentralAudit;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Warehouse;
using MediatR;

namespace Diten.InventoryService.Application.Features.WarehouseLocationRoles.Commands;

/// <summary>WMS draft §4 <c>deleteLocationRole</c> (If-Match + Idempotency-Key header) — soft delete (<c>IsDeleted</c>, <c>DeletedAt</c>).</summary>
public sealed record DeleteWarehouseLocationRoleCommand(Guid RoleId, string IdempotencyKey, int ExpectedVersion)
    : IRequest<Response<WarehouseLocationRoleDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => WarehouseCentralAudit.For("Delete", "WarehouseLocationRole");
    public Guid? CentralAuditEntityId(object? response) => RoleId;
}
