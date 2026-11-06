using Diten.BuildingBlocks.CentralAudit;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.CycleCounting;
using MediatR;
using static Diten.InventoryService.Application.Common.InventoryCentralAudit;

namespace Diten.InventoryService.Application.Features.CycleCounts.Commands;

/// <summary>§4.1 <c>releaseCycleCount</c> — the expected-quantity snapshot and the bin claims (pack §8.2), Draft → Released.</summary>
public sealed record ReleaseCycleCountCommand(Guid CountId, string IdempotencyKey, int ExpectedVersion)
    : IRequest<Response<CycleCountDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => CycleCountCentralAudit.For("LifecycleTransition", "CycleCount");
    public Guid? CentralAuditEntityId(object? response) => CountId;
}
