using MediatR;
using Diten.SupplyChainService.Application.Common;
namespace Diten.SupplyChainService.Application.Features.Returns.Queries;
/// <summary>SHIPMENT-BUNDLE 3.5.0 getReturn (MOD-0186 r10 §35.6): the first by-ID return read, tenant and company scoped.</summary>
public sealed record GetReturnByIdQuery(Guid ReturnId):IRequest<Response<ReturnDetail>>;
