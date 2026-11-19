using Diten.InventoryService.Application.Features.CycleCounting.Ports;
using Diten.InventoryService.Application.Features.Warehouse.Ports;

namespace Diten.InventoryService.Infrastructure.CycleCounting;

/// <summary>
/// The production <see cref="ICycleCountAssigneeDirectory"/>: the same AUTH <c>lookup-validation</c> MOD-0178 asks (its
/// <see cref="IWarehouseAssigneeDirectory"/> HTTP adapter, called with the caller's token — not changed), mapped onto MOD-0181's own answer.
/// </summary>
public sealed class CycleCountAssigneeDirectory(IWarehouseAssigneeDirectory directory) : ICycleCountAssigneeDirectory
{
    public async Task<CycleCountAssigneeCheck> CheckAsync(Guid userId, CancellationToken cancellationToken = default)
        => await directory.CheckAsync(userId, cancellationToken) switch
        {
            AssigneeCheck.Valid => CycleCountAssigneeCheck.Valid,
            AssigneeCheck.Invalid => CycleCountAssigneeCheck.Invalid,
            _ => CycleCountAssigneeCheck.Unavailable
        };
}
