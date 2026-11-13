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

/// <summary><c>listWaveOrders</c>: the wave's memberships (every state unless filtered), in the order they were added.</summary>
public sealed class GetWaveOrderListHandler(IWaveRepository repository) : IRequestHandler<GetWaveOrderListQuery, Response<WaveOrderPageDto>>
{
    public async Task<Response<WaveOrderPageDto>> Handle(GetWaveOrderListQuery request, CancellationToken ct)
    {
        if (WavePaging.Parse(request.Page, request.PageSize) is not { } paging) return WaveSupport.Invalid<WaveOrderPageDto>("page must be ≥ 1 and pageSize 1-200.");
        var states = new List<WaveOrderState>();
        foreach (var raw in request.State)
        {
            if (!Enum.TryParse<WaveOrderState>(raw, false, out var s) || !Enum.IsDefined(s) || raw != s.ToString())
                return WaveSupport.Invalid<WaveOrderPageDto>("state must be one of " + string.Join(", ", Enum.GetNames<WaveOrderState>()) + ".");
            states.Add(s);
        }

        if (await repository.FindAsync(request.WaveId, ct) is null) return WaveSupport.NotFound<WaveOrderPageDto>();
        var page = await repository.OrdersAsync(request.WaveId, states, paging.Page, paging.PageSize, ct);
        return Response<WaveOrderPageDto>.Success(new WaveOrderPageDto(page.Items.Select(WaveReads.Order).ToList(), page.Total, paging.Page, paging.PageSize));
    }
}
