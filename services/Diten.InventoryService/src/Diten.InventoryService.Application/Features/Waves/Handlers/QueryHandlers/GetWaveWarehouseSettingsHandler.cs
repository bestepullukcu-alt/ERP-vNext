using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.WavePlanning;
using Diten.InventoryService.Application.Features.WavePlanning.Ports;
using Diten.InventoryService.Application.Features.WavePlanning.Services;
using Diten.InventoryService.Application.Features.Waves.Queries;
using Diten.InventoryService.Domain.WavePlanning.Enums;
using Diten.InventoryService.Domain.WavePlanning.Repositories;
using Diten.InventoryService.Domain.WavePlanning.Rules;
using MediatR;

namespace Diten.InventoryService.Application.Features.Waves.Handlers.QueryHandlers;

/// <summary>
/// <c>getWaveWarehouseSettings</c> (pack §4.5; AC-15 c, d): the warehouse's setting — without a record the defaults (not managed, no time zone,
/// 90 / 70 / 30) with version 0 — plus the count of pending (deferred) orders. Another company's warehouse does not exist (404).
/// </summary>
public sealed class GetWaveWarehouseSettingsHandler(IWaveRepository repository, IWaveInventoryReader inventory, WaveChecks checks)
    : IRequestHandler<GetWaveWarehouseSettingsQuery, Response<WaveWarehouseSettingsDto>>
{
    public async Task<Response<WaveWarehouseSettingsDto>> Handle(GetWaveWarehouseSettingsQuery request, CancellationToken ct)
    {
        var (_, refusal) = await checks.WarehouseAsync<WaveWarehouseSettingsDto>(request.WarehouseId, 404, ct);
        if (refusal is not null) return refusal;

        try
        {
            var settings = await repository.FindSettingsAsync(request.WarehouseId, ct);
            return Response<WaveWarehouseSettingsDto>.Success(WaveReads.Settings(request.WarehouseId, settings,
                await inventory.DeferredCountAsync(request.WarehouseId, ct), await repository.ReleasingOrderCountAsync(request.WarehouseId, ct)));
        }
        catch (WaveInventoryUnavailableException)
        {
            return WaveSupport.InventoryUnavailable<WaveWarehouseSettingsDto>();
        }
    }
}
