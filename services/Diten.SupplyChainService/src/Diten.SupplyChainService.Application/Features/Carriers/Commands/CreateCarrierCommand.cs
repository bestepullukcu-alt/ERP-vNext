using MediatR;
using Diten.SupplyChainService.Application.Common;
namespace Diten.SupplyChainService.Application.Features.Carriers.Commands;
public sealed record CreateCarrierCommand(CreateCarrierRequest Body) : IRequest<Response<CarrierResponse>>;
