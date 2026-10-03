using MediatR;
using Diten.SupplyChainService.Application.Common;
namespace Diten.SupplyChainService.Application.Features.Carriers.Queries;
public sealed record GetCarrierListQuery(string? Status) : IRequest<Response<CarrierListResponse>>;
