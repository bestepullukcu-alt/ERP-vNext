using Diten.BuildingBlocks.CentralAudit;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Warehouse;
using MediatR;

namespace Diten.InventoryService.Application.Features.WarehouseLocationRoles.Commands;

/// <summary>
/// WMS draft §4 <c>bulkDeleteLocationRoles</c> — each row soft-deleted at its own version; one central record for the command with no entity
/// (pack §21, the <c>QuarantineCommands.cs:116</c> bulk pattern).
/// </summary>
public sealed record BulkDeleteWarehouseLocationRoleCommand(IReadOnlyList<BulkDeleteLocationRoleItem> Items, string IdempotencyKey)
    : IRequest<Response<BulkDeleteLocationRolesResultDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => WarehouseCentralAudit.For("Delete", "WarehouseLocationRole");
    public Guid? CentralAuditEntityId(object? response) => null;
}
