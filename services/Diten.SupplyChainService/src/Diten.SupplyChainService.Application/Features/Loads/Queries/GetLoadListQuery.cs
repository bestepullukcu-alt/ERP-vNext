using MediatR;
using Diten.SupplyChainService.Application.Common;
namespace Diten.SupplyChainService.Application.Features.Loads.Queries;
public sealed record GetLoadListQuery(string? Status,string? CarrierId):IRequest<Response<LoadListResponse>>;
