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
/// <c>listWaveSuggestions</c> (pack §8.2 rule 5, D-1 "Suggest"): the eligible orders of the wave's warehouse that match its
/// <c>ShipToFilter</c>, oldest outbound first, as many as still fit under the wave's <c>MaxOrders</c> / <c>MaxLines</c> (200 without an order
/// limit). It WRITES NOTHING — the user picks and adds. Draft only.
/// </summary>
public sealed class GetWaveSuggestionListHandler(IWaveRepository repository, IWaveInventoryReader inventory)
    : IRequestHandler<GetWaveSuggestionListQuery, Response<WaveSuggestionsDto>>
{
    public async Task<Response<WaveSuggestionsDto>> Handle(GetWaveSuggestionListQuery request, CancellationToken ct)
    {
        var wave = await repository.FindAsync(request.WaveId, ct);
        if (wave is null) return WaveSupport.NotFound<WaveSuggestionsDto>();
        if (!WaveRules.IsDraft(wave)) return WaveSupport.InvalidState<WaveSuggestionsDto>(wave.Status);

        var totals = (await repository.TotalsAsync([wave.Id], ct)).GetValueOrDefault(wave.Id, WaveTotals.Empty);
        var room = Math.Min(WaveRules.SuggestionOrderRoom(wave.MaxOrders, totals), WaveRules.TechnicalMaxOrders);
        var limitedBy = wave.MaxOrders is null ? WaveContract.LimitedByDefault : WaveContract.LimitedByMaxOrders;
        if (room == 0) return Response<WaveSuggestionsDto>.Success(new WaveSuggestionsDto([], limitedBy));

        IReadOnlyList<WaveOutbound> eligible;
        try
        {
            // pack §8.2 rule 2: an order in ANY active wave is not eligible
            var taken = await repository.ActiveOutboundIdsAsync(wave.WarehouseId, ct);
            eligible = (await inventory.CandidatesAsync(new WaveCandidateQuery(wave.WarehouseId, null, wave.ShipToFilter, IncludeAllocating: false, taken, 1, room),
                ct)).Items;
        }
        catch (WaveInventoryUnavailableException)
        {
            return WaveSupport.InventoryUnavailable<WaveSuggestionsDto>();
        }

        var items = new List<WaveOutbound>();
        var lines = totals.LineCount;
        foreach (var outbound in eligible)
        {
            if (!WaveRules.FitsLines(wave.MaxLines, lines, outbound.LineCount))
            {
                limitedBy = WaveContract.LimitedByMaxLines;
                break;
            }

            lines += outbound.LineCount;
            items.Add(outbound);
        }

        return Response<WaveSuggestionsDto>.Success(new WaveSuggestionsDto(items.Select(WaveReads.Candidate).ToList(), limitedBy));
    }
}
