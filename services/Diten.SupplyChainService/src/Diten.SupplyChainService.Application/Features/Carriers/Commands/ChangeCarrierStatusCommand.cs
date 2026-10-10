using MediatR;
using Diten.SupplyChainService.Application.Common;
namespace Diten.SupplyChainService.Application.Features.Carriers.Commands;
public sealed record ChangeCarrierStatusCommand(Guid CarrierId, ChangeCarrierStatusRequest Body) : IRequest<Response<CarrierResponse>>;
