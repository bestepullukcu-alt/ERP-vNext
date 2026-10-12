using Diten.InventoryService.Domain.Traceability.Entities;
using Diten.InventoryService.Domain.Traceability.Enums;

namespace Diten.InventoryService.Application.Features.Traceability.Ports;

/// <summary>
/// K-E extension point (pack §8): asked before every manual lot status change, after the lifecycle rules passed.
/// MOD-0174 ships only <see cref="AlwaysAllowLotStatusChangeGate"/>. MOD-0175 may replace it to wrap Draft → Released /
/// Blocked → Released with approval + e-signature (DEC-INV-08) and answer <see cref="LotStatusGateDecision.PendingApproval"/>
/// (the contract reserves 202). No e-signature is built here.
/// </summary>
public interface ILotStatusChangeGate
{
    Task<LotStatusGateDecision> EvaluateAsync(Lot lot, LotStatus target, string reason, CancellationToken cancellationToken = default);
}

public enum LotStatusGateDecision
{
    Allow,
    PendingApproval
}
