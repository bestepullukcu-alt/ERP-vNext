using MediatR;
using Diten.SupplyChainService.Application.Common;
using Diten.SupplyChainService.Domain.Features.Returns;
namespace Diten.SupplyChainService.Application.Features.Returns.Queries;
public sealed record GetReturnListQuery(ReturnStatus? Status=null,Guid? ShipmentId=null):IRequest<Response<ReturnListResponse>>;
