using Diten.BuildingBlocks.CentralAudit;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.WavePlanning;
using MediatR;
using static Diten.InventoryService.Application.Common.InventoryCentralAudit;

namespace Diten.InventoryService.Application.Features.Waves.Commands;

/// <summary>§4.1 <c>cancelWave</c> — Draft only, reason required; the wave is kept as Cancelled (pack §8.3, AC-8).</summary>
public sealed record CancelWaveCommand(Guid WaveId, string? Reason, string IdempotencyKey, int ExpectedVersion)
    : IRequest<Response<WaveDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => WaveCentralAudit.For("LifecycleTransition", "Wave");
    public Guid? CentralAuditEntityId(object? response) => WaveId;
}
