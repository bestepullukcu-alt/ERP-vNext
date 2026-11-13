using Diten.BuildingBlocks.CentralAudit;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.WavePlanning;
using MediatR;
using static Diten.InventoryService.Application.Common.InventoryCentralAudit;

namespace Diten.InventoryService.Application.Features.Waves.Commands;

/// <summary>§4.1 <c>updateWaveWarehouseSettings</c> — the whole warehouse setting, reason required (pack §4.5, §8.13; Q-6 = B).</summary>
public sealed record UpdateWaveWarehouseSettingsCommand(Guid WarehouseId, WaveSettingsInput Settings, string? Reason, string IdempotencyKey, int ExpectedVersion)
    : IRequest<Response<WaveWarehouseSettingsDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => WaveCentralAudit.For("Update", "WaveWarehouseSettings");
    public Guid? CentralAuditEntityId(object? response) => WarehouseId;
}
