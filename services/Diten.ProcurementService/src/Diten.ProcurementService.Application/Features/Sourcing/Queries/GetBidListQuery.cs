using Diten.Shared.Core;
using MediatR;

namespace Diten.ProcurementService.Application.Features.Sourcing.Queries;

/// <summary>listBids (contract GET /events/{rfxId}/bids). Bir RFx'in teklifleri; tenant+LE filtreli.</summary>
public sealed record GetBidListQuery(string RfxId) : IRequest<Response<BidListResultDto>>;
