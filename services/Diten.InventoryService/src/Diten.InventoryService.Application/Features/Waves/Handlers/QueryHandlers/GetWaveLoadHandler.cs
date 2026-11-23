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
/// <c>getWaveLoad</c> (draft §3.7): how the wave's active orders spread over zones and aisles — from the outbounds' persisted pick plan
/// (planned bins) and the LOCATION path of each bin (pack §8.7 inputs). <c>pickers</c> is filled by DL-2 / DL-3 (issued wave tasks); a Draft has
/// none. Read only.
/// </summary>
public sealed class GetWaveLoadHandler(IWaveRepository repository, IWaveInventoryReader inventory) : IRequestHandler<GetWaveLoadQuery, Response<WaveLoadDto>>
{
    public async Task<Response<WaveLoadDto>> Handle(GetWaveLoadQuery request, CancellationToken ct)
    {
        if (await repository.FindAsync(request.WaveId, ct) is null) return WaveSupport.NotFound<WaveLoadDto>();
        var members = await repository.ActiveOrdersAsync(request.WaveId, ct);
        if (members.Count == 0) return Response<WaveLoadDto>.Success(new WaveLoadDto([], []));

        try
        {
            var outbounds = await inventory.OutboundsAsync(members.Select(m => m.OutboundId).ToList(), ct);
            var bins = (await inventory.BinsAsync(outbounds.SelectMany(o => o.PlannedBinIds).Distinct().ToList(), ct)).ToDictionary(b => b.Id);
            var zones = outbounds
                .SelectMany(o => o.PlannedBinIds.Where(bins.ContainsKey).Select(id => (Outbound: o.Id, Bin: bins[id])))
                .GroupBy(x => (x.Bin.ZoneKey, Aisle: WaveLocationRules.Aisle(x.Bin.Code)))
                .OrderBy(g => g.Key.ZoneKey, StringComparer.Ordinal).ThenBy(g => g.Key.Aisle ?? string.Empty, StringComparer.Ordinal)
                .Select(g => new WaveLoadZoneDto
                {
                    ZoneKey = g.Key.ZoneKey, Aisle = g.Key.Aisle, PlannedTaskCount = g.Count(), OrderCount = g.Select(x => x.Outbound).Distinct().Count()
                }).ToList();
            return Response<WaveLoadDto>.Success(new WaveLoadDto(zones, []));
        }
        catch (WaveInventoryUnavailableException)
        {
            return WaveSupport.InventoryUnavailable<WaveLoadDto>();
        }
    }
}
