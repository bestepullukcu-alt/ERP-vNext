using Diten.BuildingBlocks.CentralAudit;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.WavePlanning;
using MediatR;
using static Diten.InventoryService.Application.Common.InventoryCentralAudit;

namespace Diten.InventoryService.Application.Features.Waves.Commands;

/// <summary>§4.1 <c>removeWaveOrders</c> — Draft memberships become Removed (never deleted; pack §8.2 rule 4).</summary>
public sealed record RemoveWaveOrdersCommand(Guid WaveId, IReadOnlyList<Guid> OutboundIds, string? Reason, string IdempotencyKey, int ExpectedVersion)
    : IRequest<Response<WaveOrdersResultDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => WaveCentralAudit.For("Update", "Wave");
    public Guid? CentralAuditEntityId(object? response) => WaveId;
}
