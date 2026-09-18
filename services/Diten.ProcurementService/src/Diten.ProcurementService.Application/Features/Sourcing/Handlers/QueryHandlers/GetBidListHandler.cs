using Diten.ProcurementService.Application.Features.Sourcing.Queries;
using Diten.ProcurementService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.ProcurementService.Application.Features.Sourcing.Handlers.QueryHandlers;

public sealed class GetBidListHandler
    : IRequestHandler<GetBidListQuery, Response<BidListResultDto>>
{
    private readonly IRfxRepository _repository;

    public GetBidListHandler(IRfxRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<BidListResultDto>> Handle(
        GetBidListQuery request,
        CancellationToken cancellationToken)
    {
        // Tenant+LE filtreli (repository). Başka tenant/LE'nin bid'i görünmez.
        var bids = await _repository.GetBidsByRfxIdAsync(request.RfxId, cancellationToken);
        var items = bids
            .OrderBy(b => b.BidId, StringComparer.Ordinal)
            .Select(SourcingMapping.ToBidDto)
            .ToList();

        var result = new BidListResultDto(items, SourcingContract.Version);
        return Response<BidListResultDto>.Success(result);
    }
}
