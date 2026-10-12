using Diten.InventoryService.Domain.Traceability.Entities;
using Diten.InventoryService.Domain.Traceability.Enums;

namespace Diten.InventoryService.Application.Features.Traceability.Ports;

/// <summary>The MOD-0174 gate: every change that passed the lifecycle rules is allowed (no approval before MOD-0175).</summary>
public sealed class AlwaysAllowLotStatusChangeGate : ILotStatusChangeGate
{
    public Task<LotStatusGateDecision> EvaluateAsync(Lot lot, LotStatus target, string reason, CancellationToken cancellationToken = default)
        => Task.FromResult(LotStatusGateDecision.Allow);
}
