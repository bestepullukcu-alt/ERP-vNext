using Diten.InventoryService.Application.Features.Warehouse.Ports;
using Diten.InventoryService.Application.Features.WavePlanning.Ports;

namespace Diten.InventoryService.Infrastructure.WavePlanning;

/// <summary>
/// The production <see cref="IWaveResponsibleDirectory"/>: the same AUTH <c>lookup-validation</c> MOD-0178 asks (its
/// <see cref="IWarehouseAssigneeDirectory"/> HTTP adapter, called with the caller's token — not changed), mapped onto MOD-0180's own answer.
/// </summary>
public sealed class WaveResponsibleDirectory(IWarehouseAssigneeDirectory directory) : IWaveResponsibleDirectory
{
    public async Task<WaveResponsibleCheck> CheckAsync(Guid userId, CancellationToken cancellationToken = default)
        => await directory.CheckAsync(userId, cancellationToken) switch
        {
            AssigneeCheck.Valid => WaveResponsibleCheck.Valid,
            AssigneeCheck.Invalid => WaveResponsibleCheck.Invalid,
            _ => WaveResponsibleCheck.Unavailable
        };
}
