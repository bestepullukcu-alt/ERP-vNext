using Diten.BuildingBlocks.CentralAudit;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.CycleCounting;
using MediatR;
using static Diten.InventoryService.Application.Common.InventoryCentralAudit;

namespace Diten.InventoryService.Application.Features.CycleCounts.Commands;

/// <summary>§4.1 <c>updateCycleCount</c> — the whole Draft form (POST command endpoint, F-1 = A), If-Match.</summary>
public sealed record UpdateCycleCountCommand(Guid CountId, CycleCountFormInput Count, string IdempotencyKey, int ExpectedVersion)
    : IRequest<Response<CycleCountDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => CycleCountCentralAudit.For("Update", "CycleCount");
    public Guid? CentralAuditEntityId(object? response) => CountId;
}
