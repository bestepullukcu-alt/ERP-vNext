using Diten.BuildingBlocks.CentralAudit;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.WavePlanning;
using MediatR;
using static Diten.InventoryService.Application.Common.InventoryCentralAudit;

namespace Diten.InventoryService.Application.Features.Waves.Commands;

/// <summary>WMS-WAVE-BUNDLE §4.1 <c>createWave</c> (pack §4.1, §8.2 rule 1, §8.14) — the create key is stored on the wave, scoped to the creating user.</summary>
public sealed record CreateWaveCommand(WaveFormInput Wave, string IdempotencyKey)
    : IRequest<Response<WaveDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => WaveCentralAudit.For("Create", "Wave");
    public Guid? CentralAuditEntityId(object? response) => Id(Data<WaveDto>(response)?.WaveId);
}
