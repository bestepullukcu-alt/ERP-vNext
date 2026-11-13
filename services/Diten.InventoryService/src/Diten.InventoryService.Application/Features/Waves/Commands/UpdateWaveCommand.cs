using Diten.BuildingBlocks.CentralAudit;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.WavePlanning;
using MediatR;
using static Diten.InventoryService.Application.Common.InventoryCentralAudit;

namespace Diten.InventoryService.Application.Features.Waves.Commands;

/// <summary>§4.1 <c>updateWave</c> — the whole Draft form (pack §8.3: Draft only); the reason is optional (pack §8.13).</summary>
public sealed record UpdateWaveCommand(Guid WaveId, WaveFormInput Wave, string? Reason, string IdempotencyKey, int ExpectedVersion)
    : IRequest<Response<WaveDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => WaveCentralAudit.For("Update", "Wave");
    public Guid? CentralAuditEntityId(object? response) => WaveId;
}
