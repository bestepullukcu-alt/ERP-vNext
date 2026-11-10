using Diten.BuildingBlocks.CentralAudit;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.CycleCounting;
using Diten.InventoryService.Application.Features.CycleCounts;
using MediatR;

namespace Diten.InventoryService.Application.Features.CountVariances.Commands;

/// <summary>
/// The sweeper's command (pack §8.6, §21; never an endpoint): one approved line's Pending correction — the ledger key first, then the same request.
/// Sent by <c>PendingCountAdjustmentReconcilerHostedService</c> in the line's tenant + company; its transactions are the repository's.
/// </summary>
public sealed record ReconcilePendingCountAdjustmentCommand(Guid LineId) : IRequest<Response<bool>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => CycleCountCentralAudit.For("Execute", "CycleCountLine");
    public Guid? CentralAuditEntityId(object? response) => LineId;
}
