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

/// <summary><c>listWaveEvents</c>: the wave's own timeline in <c>seq</c> order (the setting stream is not answered here).</summary>
public sealed class GetWaveEventListHandler(IWaveRepository repository) : IRequestHandler<GetWaveEventListQuery, Response<WaveEventPageDto>>
{
    public async Task<Response<WaveEventPageDto>> Handle(GetWaveEventListQuery request, CancellationToken ct)
    {
        if (WavePaging.Parse(request.Page, request.PageSize) is not { } paging) return WaveSupport.Invalid<WaveEventPageDto>("page must be ≥ 1 and pageSize 1-200.");
        if (await repository.FindAsync(request.WaveId, ct) is null) return WaveSupport.NotFound<WaveEventPageDto>();
        var page = await repository.EventsAsync(request.WaveId, WaveEventStream.Wave, paging.Page, paging.PageSize, ct);
        return Response<WaveEventPageDto>.Success(new WaveEventPageDto(page.Items.Select(WaveReads.Event).ToList(), page.Total, paging.Page, paging.PageSize));
    }
}
