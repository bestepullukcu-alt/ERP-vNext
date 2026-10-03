using MediatR;
using Diten.SupplyChainService.Application.Common;
namespace Diten.SupplyChainService.Application.Features.Claims.Queries;
public sealed record GetClaimListQuery(string? Status,string? ShipmentId):IRequest<Response<ClaimListResponse>>;
