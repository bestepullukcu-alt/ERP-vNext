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
/// <c>listWaveCandidates</c> (pack §8.2 rule 2; the planner's left list): the warehouse's outbounds that are Allocated with deferred pick
/// issuance and in no active wave, oldest first — plus the Allocating ones, grey ("reservation pending"). Read only.
/// </summary>
public sealed class GetWaveCandidateListHandler(IWaveRepository repository, IWaveInventoryReader inventory)
    : IRequestHandler<GetWaveCandidateListQuery, Response<WaveCandidatePageDto>>
{
    public async Task<Response<WaveCandidatePageDto>> Handle(GetWaveCandidateListQuery request, CancellationToken ct)
    {
        if (WavePaging.Parse(request.Page, request.PageSize) is not { } paging) return WaveSupport.Invalid<WaveCandidatePageDto>("page must be ≥ 1 and pageSize 1-200.");
        if (!WaveSupport.TryParseId(request.WarehouseId, out var warehouse))
            return WaveSupport.Invalid<WaveCandidatePageDto>("warehouseId is required and must be a canonical UUID.");

        try
        {
            var taken = await repository.ActiveOutboundIdsAsync(warehouse, ct);
            var page = await inventory.CandidatesAsync(new WaveCandidateQuery(warehouse, WaveSupport.Trimmed(request.Q), WaveSupport.Trimmed(request.ShipTo),
                IncludeAllocating: true, taken, paging.Page, paging.PageSize), ct);
            return Response<WaveCandidatePageDto>.Success(new WaveCandidatePageDto(page.Items.Select(WaveReads.Candidate).ToList(), page.Total, paging.Page,
                paging.PageSize));
        }
        catch (WaveInventoryUnavailableException)
        {
            return WaveSupport.InventoryUnavailable<WaveCandidatePageDto>();
        }
    }
}
