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
/// <c>listWaves</c> (draft §4.1): the caller's company and the warehouse asked for — the warehouse is required (pack §8.1); earliest cutoff
/// first; a bad parameter is 400.
/// </summary>
public sealed class GetWaveListHandler(IWaveRepository repository) : IRequestHandler<GetWaveListQuery, Response<WavePageDto>>
{
    public async Task<Response<WavePageDto>> Handle(GetWaveListQuery request, CancellationToken ct)
    {
        if (WavePaging.Parse(request.Page, request.PageSize) is not { } paging) return WaveSupport.Invalid<WavePageDto>("page must be ≥ 1 and pageSize 1-200.");
        if (!WaveSupport.TryParseId(request.WarehouseId, out var warehouse)) return WaveSupport.Invalid<WavePageDto>("warehouseId is required and must be a canonical UUID.");
        var statuses = new List<WaveStatus>();
        foreach (var raw in request.Status)
        {
            if (!Enum.TryParse<WaveStatus>(raw, false, out var s) || !Enum.IsDefined(s) || raw != s.ToString())
                return WaveSupport.Invalid<WavePageDto>("status must be one of " + string.Join(", ", Enum.GetNames<WaveStatus>()) + ".");
            statuses.Add(s);
        }

        WaveType? type = null;
        if (request.WaveType is not null)
        {
            if (!Enum.TryParse<WaveType>(request.WaveType, false, out var t) || !Enum.IsDefined(t) || request.WaveType != t.ToString())
                return WaveSupport.Invalid<WavePageDto>("waveType must be one of " + string.Join(", ", Enum.GetNames<WaveType>()) + ".");
            type = t;
        }

        if (!Instant(request.CutoffFrom, out var from) || !Instant(request.CutoffTo, out var to))
            return WaveSupport.Invalid<WavePageDto>("cutoffFrom and cutoffTo must be UTC instants (yyyy-MM-ddTHH:mm:ss[.fff]Z).");

        var page = await repository.ListAsync(new WaveQuery(warehouse, statuses, type, WaveSupport.Trimmed(request.Q), from, to, paging.Page, paging.PageSize), ct);
        var totals = await repository.TotalsAsync(page.Items.Select(w => w.Id).ToList(), ct);
        return Response<WavePageDto>.Success(new WavePageDto(
            page.Items.Select(w => WaveReads.Dto(w, totals.GetValueOrDefault(w.Id, WaveTotals.Empty))).ToList(), page.Total, paging.Page, paging.PageSize));
    }

    private static bool Instant(string? raw, out DateTimeOffset? value)
    {
        value = null;
        if (raw is null) return true;
        if (!raw.EndsWith('Z') || !DateTimeOffset.TryParse(raw, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal, out var parsed))
            return false;
        value = parsed;
        return true;
    }
}
