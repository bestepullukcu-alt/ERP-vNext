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

/// <summary><c>getWave</c>: the wave of the caller's company, or 404 (another tenant's / company's wave does not exist here).</summary>
public sealed class GetWaveByIdHandler(IWaveRepository repository) : IRequestHandler<GetWaveByIdQuery, Response<WaveDto>>
{
    public async Task<Response<WaveDto>> Handle(GetWaveByIdQuery request, CancellationToken ct)
    {
        var wave = await repository.FindAsync(request.WaveId, ct);
        if (wave is null) return WaveSupport.NotFound<WaveDto>();
        var totals = (await repository.TotalsAsync([wave.Id], ct)).GetValueOrDefault(wave.Id, WaveTotals.Empty);
        return Response<WaveDto>.Success(WaveReads.Dto(wave, totals));
    }
}
